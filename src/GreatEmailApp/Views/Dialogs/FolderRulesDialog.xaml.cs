// FILE: src/GreatEmailApp/Views/Dialogs/FolderRulesDialog.xaml.cs
// Created: 2026-11-21 | Revised: 2026-11-21 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Folder rules (roadmap P2-1c). Instead of one rule per sender, a folder owns
// a list of senders/domains: "@walmart, @bambulab.com → Ads". Stored as an
// ordinary MailRule (Match Any, one "From contains" condition per entry, one
// Move-to-folder action) so it syncs, runs and degrades on older clients
// exactly like any other rule — no new rule format.
//
// The dialog shows every From-only rule that files into this folder as
// editable entries; rules with other conditions are listed read-only (edit
// them in Settings → Rules).

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Rules;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Views.Dialogs;

public partial class FolderRulesDialog : Window
{
    private readonly Folder _folder;
    private readonly Account? _account;
    private List<Message> _folderMessages = new();

    public FolderRulesDialog(Folder folder)
    {
        InitializeComponent();
        _folder = folder;
        _account = App.Accounts.LoadAll().FirstOrDefault(a => a.Id == folder.AccountId);

        TitleText.Text = $"Senders filed into “{folder.Name}”";
        SubtitleText.Text = $"{_account?.EmailAddress} · {folder.FullPath}. New mail from these senders " +
                            "is moved here automatically. Add an address, a domain (@walmart.com) or part of one (@walmart).";

        Refresh();
        Loaded += async (_, _) => await LoadFolderSendersAsync();
    }

    // --------------------------------------------------------------------- //
    // Which rules file into this folder
    // --------------------------------------------------------------------- //

    private bool TargetsThisFolder(MailRule r) =>
        (string.IsNullOrEmpty(r.AccountId) || r.AccountId == _folder.AccountId)
        && r.Actions.Any(a => a.Type == RuleAction.MoveToFolder
                              && string.Equals(a.Value?.Trim(), _folder.FullPath, StringComparison.OrdinalIgnoreCase));

    /// <summary>A rule whose senders can be listed and edited one by one:
    /// only From conditions, OR-ed together (or just one), and the only
    /// action is the move here.</summary>
    private static bool IsSenderList(MailRule r) =>
        r.Conditions.Count > 0
        && r.Conditions.All(c => c.Field == RuleField.From && c.Op is RuleOp.Contains or RuleOp.EndsWith or RuleOp.Equals)
        && (r.Match == RuleMatch.Any || r.Conditions.Count == 1)
        && r.Actions.Count == 1;

    private sealed record Entry(string RuleId, int ConditionIndex, string Text, string RuleName);

    private List<Entry> EntriesFrom(IEnumerable<MailRule> rules)
    {
        var list = new List<Entry>();
        foreach (var r in rules)
            for (int i = 0; i < r.Conditions.Count; i++)
                foreach (var text in SplitEntries(r.Conditions[i]))
                    list.Add(new Entry(r.Id, i, text, r.Name));
        return list;
    }

    /// <summary>"From ends with" holds a comma list (see RulesEngine); other
    /// operators hold one value.</summary>
    private static IEnumerable<string> SplitEntries(RuleCondition c) =>
        c.Op == RuleOp.EndsWith
            ? c.Value.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : new[] { c.Value.Trim() };

    private void Refresh()
    {
        var rules = App.Rules.LoadAll().Where(TargetsThisFolder).ToList();
        var editable = rules.Where(IsSenderList).ToList();
        var other = rules.Where(r => !IsSenderList(r)).ToList();

        EntriesList.Items.Clear();
        var entries = EntriesFrom(editable);
        foreach (var e in entries.OrderBy(e => e.Text, StringComparer.OrdinalIgnoreCase))
            EntriesList.Items.Add(EntryRow(e, editable.First(r => r.Id == e.RuleId)));
        if (entries.Count == 0)
            EntriesList.Items.Add(Note("Nothing yet — add a sender above or pick one from below."));

        OtherRulesList.Items.Clear();
        OtherRulesHeader.Visibility = other.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var r in other)
            OtherRulesList.Items.Add(Note($"{r.Name} — has other conditions; edit it in Settings → Rules."));

        RenderSuggestions();
    }

    // --------------------------------------------------------------------- //
    // Add / remove
    // --------------------------------------------------------------------- //

    private void AddBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Add(AddBox.Text); e.Handled = true; }
    }

    private void Add_Click(object sender, RoutedEventArgs e) => Add(AddBox.Text);

    private void Add(string raw)
    {
        var wanted = raw.Split(new[] { ',', ';', '\n', '\r', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 1).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (wanted.Count == 0) return;

        var all = App.Rules.LoadAll().ToList();
        var existing = EntriesFrom(all.Where(TargetsThisFolder).Where(IsSenderList))
            .Select(e => e.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        wanted = wanted.Where(w => !existing.Contains(w)).ToList();
        if (wanted.Count == 0) { AddBox.Clear(); return; }

        // Senders already routed to a DIFFERENT folder would now match two
        // rules — say so instead of silently creating a conflict.
        var elsewhere = all.Where(r => !TargetsThisFolder(r) && r.Actions.Any(a => a.Type == RuleAction.MoveToFolder))
            .SelectMany(r => r.Conditions.Where(c => c.Field == RuleField.From)
                .SelectMany(SplitEntries).Select(t => (Text: t, Rule: r)))
            .Where(x => wanted.Contains(x.Text, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (elsewhere.Count > 0
            && MessageBox.Show(this,
                   "Already filed somewhere else:\n\n" +
                   string.Join("\n", elsewhere.Select(x => $"{x.Text}  →  {x.Rule.Actions.First(a => a.Type == RuleAction.MoveToFolder).Value}  (rule “{x.Rule.Name}”)")) +
                   "\n\nAdd here too? The rule listed first in Settings → Rules wins.",
                   "Folder rules", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var rule = all.FirstOrDefault(r => TargetsThisFolder(r) && IsSenderList(r) && r.AccountId == _folder.AccountId
                                           && r.Match == RuleMatch.Any && r.Name.StartsWith("Folder:", StringComparison.Ordinal));
        if (rule is null)
        {
            rule = new MailRule
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = $"Folder: {_folder.Name}",
                Category = MailRule.OrganizingCategory,
                AccountId = _folder.AccountId,
                Match = RuleMatch.Any,
                Actions = new List<RuleActionItem> { new() { Type = RuleAction.MoveToFolder, Value = _folder.FullPath } },
            };
            all.Add(rule);
        }
        foreach (var w in wanted)
            rule.Conditions.Add(new RuleCondition { Field = RuleField.From, Op = RuleOp.Contains, Value = w });
        rule.UpdatedAt = DateTimeOffset.UtcNow;

        App.Rules.Save(all);
        AddBox.Clear();
        Refresh();
    }

    private void Remove(Entry entry)
    {
        var all = App.Rules.LoadAll().ToList();
        var rule = all.FirstOrDefault(r => r.Id == entry.RuleId);
        if (rule is null || entry.ConditionIndex >= rule.Conditions.Count) { Refresh(); return; }

        var cond = rule.Conditions[entry.ConditionIndex];
        var rest = SplitEntries(cond).Where(t => !string.Equals(t, entry.Text, StringComparison.OrdinalIgnoreCase)).ToList();
        if (cond.Op == RuleOp.EndsWith && rest.Count > 0) cond.Value = string.Join(", ", rest);
        else rule.Conditions.RemoveAt(entry.ConditionIndex);

        if (rule.Conditions.Count == 0) all.Remove(rule); // nothing left to match — drop the rule
        else rule.UpdatedAt = DateTimeOffset.UtcNow;

        App.Rules.Save(all);
        Refresh();
    }

    // --------------------------------------------------------------------- //
    // Senders already in the folder (suggestions)
    // --------------------------------------------------------------------- //

    private async System.Threading.Tasks.Task LoadFolderSendersAsync()
    {
        SuggestionsList.Items.Clear();
        SuggestionsList.Items.Add(Note("Looking at this folder…"));
        if (_account is not null && App.Credentials.Read(_account.Id) is { } creds
            && await App.Imap.ListMessagesAsync(_account, creds.Password, _folder.FullPath, 200) is Result<List<Message>>.Ok ok)
            _folderMessages = ok.Value;
        RenderSuggestions();
    }

    private void RenderSuggestions()
    {
        SuggestionsList.Items.Clear();
        var rules = App.Rules.LoadAll().Where(TargetsThisFolder).ToList();
        var uncovered = _folderMessages
            .Where(m => !string.IsNullOrWhiteSpace(m.SenderEmail) && !rules.Any(r => RuleMatchesSender(r, m)))
            .GroupBy(m => BaseDomain(DomainOf(m.SenderEmail)), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Key.Length > 0)
            .OrderByDescending(g => g.Count())
            .Take(12)
            .ToList();

        if (uncovered.Count == 0)
        {
            SuggestionsList.Items.Add(Note(_folderMessages.Count == 0
                ? "No messages found in this folder."
                : "Every sender here is already covered."));
            return;
        }

        foreach (var g in uncovered)
        {
            var addresses = g.Select(m => m.SenderEmail.Trim().ToLowerInvariant()).Distinct().ToList();
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = new TextBlock
            {
                Text = $"{g.Key}  ·  {g.Count()} message(s)  ·  {g.First().Sender}",
                Foreground = Brush("PrimaryTextBrush"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = string.Join("\n", addresses),
            };
            grid.Children.Add(label);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal };
            var domainBtn = SmallButton($"Add {g.Key}");
            domainBtn.ToolTip = $"Every address at {g.Key}, including subdomains (e.g. news.{g.Key})";
            domainBtn.Click += (_, _) => Add(g.Key);
            buttons.Children.Add(domainBtn);
            if (addresses.Count == 1)
            {
                var addrBtn = SmallButton("Just this address");
                addrBtn.Margin = new Thickness(6, 0, 0, 0);
                addrBtn.Click += (_, _) => Add(addresses[0]);
                buttons.Children.Add(addrBtn);
            }
            Grid.SetColumn(buttons, 1);
            grid.Children.Add(buttons);
            SuggestionsList.Items.Add(grid);
        }
    }

    private static string DomainOf(string email)
    {
        var at = email.LastIndexOf('@');
        return at < 0 ? "" : email[(at + 1)..].Trim().TrimEnd('>').ToLowerInvariant();
    }

    /// <summary>"send.store.creality.com" → "creality.com" so one entry covers
    /// every subdomain a company mails from. Keeps three labels for country
    /// second-levels like "shop.co.uk" → "shop.co.uk".</summary>
    private static readonly HashSet<string> CountrySecondLevels =
        new(StringComparer.OrdinalIgnoreCase) { "co", "com", "org", "net", "ac", "gov", "edu", "ne", "or" };

    private static string BaseDomain(string domain)
    {
        var parts = domain.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 2) return domain;
        var take = parts[^1].Length == 2 && CountrySecondLevels.Contains(parts[^2]) ? 3 : 2; // co.uk, com.au, …
        return string.Join('.', parts[^Math.Min(take, parts.Length)..]);
    }

    /// <summary>Same From matching as RulesEngine, for From conditions only.</summary>
    private static bool RuleMatchesSender(MailRule r, Message m)
    {
        var hay = $"{m.Sender} {m.SenderEmail}";
        var addr = (m.SenderEmail ?? "").Trim().TrimEnd('>');
        bool One(RuleCondition c) => c.Field == RuleField.From && c.Op switch
        {
            RuleOp.Contains => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries).All(t => hay.Contains(t.Trim('"'), StringComparison.OrdinalIgnoreCase)),
            RuleOp.Equals => hay.Equals(c.Value.Trim(), StringComparison.OrdinalIgnoreCase)
                             || addr.Equals(c.Value.Trim(), StringComparison.OrdinalIgnoreCase),
            RuleOp.StartsWith => hay.StartsWith(c.Value.Trim(), StringComparison.OrdinalIgnoreCase),
            RuleOp.EndsWith => SplitEntries(c).Any(e => addr.EndsWith(e, StringComparison.OrdinalIgnoreCase)),
            _ => false,
        };
        var fromConds = r.Conditions.Where(c => c.Field == RuleField.From).ToList();
        if (fromConds.Count == 0) return false;
        return r.Match == RuleMatch.Any ? fromConds.Any(One) : fromConds.All(One);
    }

    // --------------------------------------------------------------------- //
    // Move existing inbox mail here
    // --------------------------------------------------------------------- //

    private async void RunNow_Click(object sender, RoutedEventArgs e)
    {
        if (_account is null || sender is not Button btn) return;
        var dlg = new RunRulesDialog(_account.Id, "INBOX") { Owner = this };
        dlg.ShowDialog();
        await LoadFolderSendersAsync();
    }

    // --------------------------------------------------------------------- //
    // UI helpers
    // --------------------------------------------------------------------- //

    private FrameworkElement EntryRow(Entry e, MailRule rule)
    {
        var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = e.Text, FontSize = 13, Foreground = Brush("PrimaryTextBrush") });
        var note = rule.IsEnabled ? "" : " · paused";
        if (!rule.Name.StartsWith("Folder:", StringComparison.Ordinal)) note += $" · rule “{rule.Name}”";
        if (note.Length > 0)
            text.Children.Add(new TextBlock { Text = note, FontSize = 11, Margin = new Thickness(6, 1, 0, 0),
                Foreground = Brush("TertiaryTextBrush") });
        grid.Children.Add(text);
        var remove = SmallButton("Remove");
        remove.Click += (_, _) => Remove(e);
        Grid.SetColumn(remove, 1);
        grid.Children.Add(remove);
        return grid;
    }

    private static TextBlock Note(string s) => new()
    {
        Text = s, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 2),
        Foreground = Brush("TertiaryTextBrush"),
    };

    private static Button SmallButton(string content) => new()
    {
        Content = content,
        Style = (Style)Application.Current.Resources["SubtleButton"],
        Padding = new Thickness(10, 3, 10, 3),
    };

    private static Brush Brush(string key) => (Brush)Application.Current.Resources[key];
}
