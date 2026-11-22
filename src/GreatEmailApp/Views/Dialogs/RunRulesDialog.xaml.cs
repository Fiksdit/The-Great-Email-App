// FILE: src/GreatEmailApp/Views/Dialogs/RunRulesDialog.xaml.cs
// Created: 2026-11-21 | Revised: 2026-11-21 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// "Run rules now" with a scope (roadmap P2-1a): all accounts or one, inbox /
// all folders / picked folders, all rules or one category. Each folder's
// newest N messages are listed and handed to RulesEngine.ApplyAsync — the
// same engine the poller uses, so behavior matches automatic runs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Rules;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Views.Dialogs;

public partial class RunRulesDialog : Window
{
    private const string AllCategories = "All rules";
    private static readonly int[] Counts = { 50, 200, 500, 1000 };

    private readonly List<Account> _accounts;
    private readonly Dictionary<string, List<Folder>> _foldersByAccount = new();
    private readonly string? _preselectFolder;
    private CancellationTokenSource? _cts;
    private bool _running;

    /// <param name="accountId">Preselect one account (null = all accounts).</param>
    /// <param name="folderPath">Preselect "Folders I pick" with this folder ticked.</param>
    public RunRulesDialog(string? accountId = null, string? folderPath = null)
    {
        InitializeComponent();
        _accounts = App.Accounts.LoadAll().ToList();
        _preselectFolder = folderPath;

        var picks = new List<Account> { new() { Id = "", DisplayName = "All accounts", EmailAddress = "" } };
        picks.AddRange(_accounts);
        AccountCombo.ItemsSource = picks;
        AccountCombo.SelectedValue = accountId ?? "";

        var categories = new List<string> { AllCategories };
        categories.AddRange(App.Rules.LoadAll().Where(r => r.IsEnabled)
            .Select(r => r.EffectiveCategory)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c == MailRule.SpamCategory ? 0 : c == MailRule.OrganizingCategory ? 1 : 2)
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .Select(c => $"{c} rules"));
        CategoryCombo.ItemsSource = categories;
        CategoryCombo.SelectedIndex = 0;

        CountCombo.ItemsSource = Counts;
        CountCombo.SelectedItem = 200;

        if (folderPath is not null) ChosenFoldersRadio.IsChecked = true;

        AccountCombo.SelectionChanged += async (_, _) => await RefreshFolderListAsync();
        InboxOnlyRadio.Checked += (_, _) => UpdateFolderListEnabled();
        AllFoldersRadio.Checked += (_, _) => UpdateFolderListEnabled();
        ChosenFoldersRadio.Checked += (_, _) => UpdateFolderListEnabled();
        UpdateFolderListEnabled();

        Loaded += async (_, _) => await RefreshFolderListAsync();
        Closing += (_, e) =>
        {
            // Closing mid-run stops after the current message instead of
            // abandoning a half-finished IMAP call.
            if (_running) { _cts?.Cancel(); StatusText.Text = "Stopping…"; }
        };
    }

    private string SelectedAccountId => AccountCombo.SelectedValue as string ?? "";

    private IEnumerable<Account> SelectedAccounts =>
        string.IsNullOrEmpty(SelectedAccountId) ? _accounts : _accounts.Where(a => a.Id == SelectedAccountId);

    // Dim rather than disable: the app theme draws a disabled ListBox as a blank white box.
    private void UpdateFolderListEnabled()
    {
        var on = ChosenFoldersRadio.IsChecked == true;
        FolderList.IsHitTestVisible = on;
        FolderList.Focusable = on;
        FolderList.Opacity = on ? 1.0 : 0.4;
    }

    // --------------------------------------------------------------------- //
    // Folder list
    // --------------------------------------------------------------------- //

    private async Task<List<Folder>> FoldersForAsync(Account account)
    {
        if (_foldersByAccount.TryGetValue(account.Id, out var cached)) return cached;
        var creds = App.Credentials.Read(account.Id);
        var flat = new List<Folder>();
        if (creds is not null
            && await App.Imap.ListFoldersAsync(account, creds.Value.Password) is Result<List<Folder>>.Ok ok)
            Flatten(ok.Value, flat);
        _foldersByAccount[account.Id] = flat;
        return flat;
    }

    private static void Flatten(IEnumerable<Folder> folders, List<Folder> sink)
    {
        foreach (var f in folders)
        {
            if (!string.IsNullOrEmpty(f.FullPath) && f.Special != SpecialFolder.Outbox) sink.Add(f);
            Flatten(f.Children, sink);
        }
    }

    private async Task RefreshFolderListAsync()
    {
        var ticked = FolderList.Items.OfType<CheckBox>()
            .Where(c => c.IsChecked == true).Select(c => c.Tag).OfType<FolderPick>().ToHashSet();
        FolderList.Items.Clear();
        FolderList.Items.Add(new TextBlock { Text = "Loading folders…", Margin = new Thickness(6),
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TertiaryTextBrush"] });

        var multi = string.IsNullOrEmpty(SelectedAccountId) && _accounts.Count > 1;
        var items = new List<CheckBox>();
        foreach (var account in SelectedAccounts)
        {
            foreach (var f in await FoldersForAsync(account))
            {
                var pick = new FolderPick(account.Id, f.FullPath);
                items.Add(new CheckBox
                {
                    Content = multi ? $"{account.EmailAddress}  ·  {f.FullPath}" : f.FullPath,
                    Tag = pick,
                    IsChecked = ticked.Contains(pick)
                                || string.Equals(f.FullPath, _preselectFolder, StringComparison.OrdinalIgnoreCase),
                    Margin = new Thickness(4, 2, 4, 2),
                    Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryTextBrush"],
                });
            }
        }

        FolderList.Items.Clear();
        foreach (var cb in items) FolderList.Items.Add(cb);
        if (items.Count == 0)
            FolderList.Items.Add(new TextBlock { Text = "No folders found (is the password saved?)", Margin = new Thickness(6),
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["TertiaryTextBrush"] });
    }

    private sealed record FolderPick(string AccountId, string Path);

    // --------------------------------------------------------------------- //
    // Run
    // --------------------------------------------------------------------- //

    private Func<MailRule, bool>? CategoryFilter()
    {
        var label = CategoryCombo.SelectedItem as string;
        if (label is null || label == AllCategories) return null;
        var cat = label.EndsWith(" rules") ? label[..^" rules".Length] : label;
        return r => string.Equals(r.EffectiveCategory, cat, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<(Account Account, string Path)>> TargetsAsync()
    {
        var targets = new List<(Account, string)>();
        if (ChosenFoldersRadio.IsChecked == true)
        {
            foreach (var pick in FolderList.Items.OfType<CheckBox>()
                         .Where(c => c.IsChecked == true).Select(c => c.Tag).OfType<FolderPick>())
            {
                var account = _accounts.FirstOrDefault(a => a.Id == pick.AccountId);
                if (account is not null) targets.Add((account, pick.Path));
            }
            return targets;
        }

        foreach (var account in SelectedAccounts)
        {
            if (InboxOnlyRadio.IsChecked == true) { targets.Add((account, "INBOX")); continue; }
            foreach (var f in await FoldersForAsync(account))
            {
                if (f.Special is SpecialFolder.Sent or SpecialFolder.Drafts or SpecialFolder.Deleted) continue;
                targets.Add((account, f.FullPath));
            }
        }
        return targets;
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_running) return;
        var filter = CategoryFilter();
        var rules = App.Rules.LoadAll()
            .Where(r => r.IsEnabled && !r.IncomingOnly && (filter is null || filter(r)))
            .ToList();
        if (rules.Count == 0)
        {
            StatusText.Text = "No active rules to run in that selection.";
            return;
        }

        var targets = await TargetsAsync();
        // Skip accounts no selected rule applies to — saves listing their folders.
        targets = targets.Where(t => rules.Any(r => string.IsNullOrEmpty(r.AccountId) || r.AccountId == t.Account.Id)).ToList();
        if (targets.Count == 0)
        {
            StatusText.Text = ChosenFoldersRadio.IsChecked == true
                ? "Tick at least one folder first."
                : "None of the selected rules apply to these accounts.";
            return;
        }

        if (rules.Any(r => r.Actions.Any(a => a.IsPermanentDelete))
            && MessageBox.Show(this,
                   $"Some of these rules permanently delete mail (it won't go to Trash).\n\n" +
                   $"They'll run on {targets.Count} folder(s). Continue?",
                   "Run rules now", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var count = CountCombo.SelectedItem is int n ? n : 200;
        _running = true;
        _cts = new CancellationTokenSource();
        RunButton.IsEnabled = false;
        int considered = 0, matched = 0, actions = 0, done = 0;
        var errors = new List<string>();
        try
        {
            foreach (var (account, path) in targets)
            {
                _cts.Token.ThrowIfCancellationRequested();
                done++;
                StatusText.Text = $"Folder {done} of {targets.Count}: {path} ({account.EmailAddress})…";
                var creds = App.Credentials.Read(account.Id);
                if (creds is null) { errors.Add($"No password saved for {account.EmailAddress}"); continue; }

                var listing = await App.Imap.ListMessagesAsync(account, creds.Value.Password, path, count, _cts.Token);
                if (listing is not Result<List<Message>>.Ok ok) { errors.Add($"{path}: {listing.AsError}"); continue; }

                var res = await App.RulesEngine.ApplyAsync(account, path, ok.Value, _cts.Token, ruleFilter: filter);
                if (res is Result<RuleApplyResult>.Ok ro)
                {
                    considered += ro.Value.Considered;
                    matched += ro.Value.Matched;
                    actions += ro.Value.ActionsRun;
                    errors.AddRange(ro.Value.Errors);
                }
                else errors.Add($"{path}: {res.AsError}");
            }
            StatusText.Text = $"Done. Checked {considered} message(s) in {targets.Count} folder(s): " +
                              $"{matched} matched a rule, {actions} action(s) run." +
                              (errors.Count == 0 ? "" : $"\n{errors.Count} problem(s): {string.Join("; ", errors.Take(3))}");
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = $"Stopped. {matched} matched, {actions} action(s) run before stopping.";
        }
        finally
        {
            _running = false;
            RunButton.IsEnabled = true;
        }
    }
}
