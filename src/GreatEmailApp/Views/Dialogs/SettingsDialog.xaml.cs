// FILE: src/GreatEmailApp/Views/Dialogs/SettingsDialog.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Views.Dialogs;

public partial class SettingsDialog : Window
{
    private readonly SettingsViewModel _vm;

    /// <summary>True if any account was added/removed — caller should refresh sidebar.</summary>
    public bool AccountsChanged { get; private set; }

    public SettingsDialog()
    {
        InitializeComponent();
        _vm = new SettingsViewModel(App.Settings, App.Accounts, App.Contacts, App.Rules, App.Credentials, App.SettingsStore, App.Auth, App.Sync, App.SyncCoordinator, App.Updates, App.UpdateInstaller);
        DataContext = _vm;
        Loaded += async (_, _) => await LoadSuggestionsAsync();
    }

    /// <summary>Programmatic tab switch — call before <c>ShowDialog</c>.</summary>
    public void OpenOnTab(string tag) => _vm.ActiveTab = tag;

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        // Persist on close. Theme/accent already applied live.
        App.PersistSettings();
        DialogResult = true;
        Close();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag)
        {
            _vm.ActiveTab = tag;
            tb.IsChecked = true;
        }
    }

    private void ThemeRadio_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag
            && System.Enum.TryParse<AppTheme>(tag, out var t))
        {
            _vm.Theme = t;
        }
    }

    private void Density_Click(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag
            && System.Enum.TryParse<DensityMode>(tag, out var d))
        {
            _vm.Density = d;
        }
    }

    private void Accent_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string hex)
        {
            _vm.Accent = hex;
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is Account a)
        {
            var confirm = MessageBox.Show(this,
                $"Remove {a.EmailAddress}?\n\nThe account configuration and stored password will be deleted from this PC. The mail on the server is not affected.",
                "Remove account",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.OK) return;
            _vm.RemoveAccount(a);
            AccountsChanged = true;
        }
    }

    // ----- Rules ----- //

    private void NewRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new GreatEmailApp.Core.Models.MailRule { Id = System.Guid.NewGuid().ToString("N"), Name = "" };
        OpenRuleEditor(rule);
    }

    private void EditRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is GreatEmailApp.Core.Models.MailRule r) OpenRuleEditor(r);
    }

    private void RemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is GreatEmailApp.Core.Models.MailRule r)
        {
            var ok = MessageBox.Show(this, $"Remove rule '{r.Name}'?", "Remove rule",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (ok == MessageBoxResult.OK) _vm.RemoveRule(r);
        }
    }

    private void RuleToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.Tag is GreatEmailApp.Core.Models.MailRule r) _vm.ToggleRule(r);
    }

    private async void RunRulesNow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        btn.IsEnabled = false;
        RulesStatus.Text = "Running rules across all inboxes…";
        try
        {
            int considered = 0, matched = 0, actions = 0;
            var errors = new System.Collections.Generic.List<string>();
            foreach (var account in App.Accounts.LoadAll())
            {
                var creds = App.Credentials.Read(account.Id);
                if (creds is null) continue;
                var listing = await App.Imap.ListMessagesAsync(account, creds.Value.Password, "INBOX", 200);
                if (listing is not GreatEmailApp.Core.Services.Result<System.Collections.Generic.List<GreatEmailApp.Core.Models.Message>>.Ok ok) continue;
                var res = await App.RulesEngine.ApplyAsync(account, "INBOX", ok.Value);
                if (res is GreatEmailApp.Core.Services.Result<GreatEmailApp.Core.Rules.RuleApplyResult>.Ok ro)
                {
                    considered += ro.Value.Considered;
                    matched    += ro.Value.Matched;
                    actions    += ro.Value.ActionsRun;
                    errors.AddRange(ro.Value.Errors);
                }
            }
            RulesStatus.Text = $"Considered {considered} message(s), matched {matched}, ran {actions} action(s)."
                             + (errors.Count == 0 ? "" : $" {errors.Count} error(s).");
        }
        finally { btn.IsEnabled = true; }
    }

    private async void RefreshSuggestions_Click(object sender, RoutedEventArgs e) => await LoadSuggestionsAsync();

    private async System.Threading.Tasks.Task LoadSuggestionsAsync()
    {
        SuggestionsList.Items.Clear();
        var result = await App.RuleSuggestions.ComputeAsync();
        if (result is not GreatEmailApp.Core.Services.Result<System.Collections.Generic.List<GreatEmailApp.Core.Rules.RuleSuggestion>>.Ok ok || ok.Value.Count == 0)
        {
            SuggestionsBanner.Visibility = Visibility.Collapsed;
            return;
        }

        SuggestionsBanner.Visibility = Visibility.Visible;
        SuggestionsHeader.Text = $"💡 {ok.Value.Count} frequent sender(s) without a rule";
        foreach (var s in ok.Value.Take(5)) // top 5 — keep the banner from getting absurd
            SuggestionsList.Items.Add(BuildSuggestionRow(s));
    }

    private FrameworkElement BuildSuggestionRow(GreatEmailApp.Core.Rules.RuleSuggestion s)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var label = new TextBlock
        {
            Text = $"{s.Source.Domain}  ·  {s.Source.Count} messages  ·  example: {s.Source.ExampleSenderName}",
            Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryTextBrush"],
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetColumn(label, 0); grid.Children.Add(label);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal };
        var create = new Button
        {
            Content = "Create rule",
            Style = (Style)Application.Current.Resources["AccentButton"],
            Padding = new Thickness(10, 4, 10, 4),
        };
        create.Click += (_, _) =>
        {
            var seed = new GreatEmailApp.Core.Models.MailRule
            {
                Id = System.Guid.NewGuid().ToString("N"),
                Name = s.SuggestedRuleName,
                IsEnabled = true,
                AccountId = string.IsNullOrEmpty(s.Source.ExampleAccountId) ? null : s.Source.ExampleAccountId,
                Match = GreatEmailApp.Core.Models.RuleMatch.All,
                Conditions = new System.Collections.Generic.List<GreatEmailApp.Core.Models.RuleCondition>
                {
                    new() { Field = GreatEmailApp.Core.Models.RuleField.From, Op = GreatEmailApp.Core.Models.RuleOp.Contains, Value = s.Source.Domain },
                },
                Actions = new System.Collections.Generic.List<GreatEmailApp.Core.Models.RuleActionItem>
                {
                    new() { Type = GreatEmailApp.Core.Models.RuleAction.MoveToFolder, Value = s.SuggestedFolderName },
                },
            };
            OpenRuleEditor(seed);
            _ = LoadSuggestionsAsync();
        };
        var dismiss = new Button
        {
            Content = "Dismiss",
            Style = (Style)Application.Current.Resources["SubtleButton"],
            Padding = new Thickness(10, 4, 10, 4),
            Margin = new Thickness(6, 0, 0, 0),
        };
        dismiss.Click += async (_, _) =>
        {
            App.RuleSuggestions.Dismiss(s.Source.Domain);
            await LoadSuggestionsAsync();
        };
        btnPanel.Children.Add(create);
        btnPanel.Children.Add(dismiss);
        Grid.SetColumn(btnPanel, 1); grid.Children.Add(btnPanel);

        return grid;
    }

    private void OpenRuleEditor(GreatEmailApp.Core.Models.MailRule rule)
    {
        // Pull the live folder tree from MainViewModel so the picker can show
        // folder paths for each account. Accounts whose folders haven't loaded
        // yet (user never expanded them) appear with empty lists; the user can
        // still type a custom path in those cases.
        var foldersByAccount = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyList<string>>();
        var mvm = (Application.Current.MainWindow?.DataContext as GreatEmailApp.ViewModels.MainViewModel);
        if (mvm is not null)
        {
            foreach (var avm in mvm.Accounts)
            {
                var paths = new System.Collections.Generic.List<string>();
                CollectFolderPaths(avm.Folders, paths);
                foldersByAccount[avm.Model.Id] = paths;
            }
        }
        var dlg = new RuleEditorDialog(rule, App.Accounts.LoadAll().ToList(), foldersByAccount) { Owner = this };
        if (dlg.ShowDialog() == true) _vm.AddOrUpdateRule(dlg.Result);
    }

    private static void CollectFolderPaths(
        System.Collections.ObjectModel.ObservableCollection<GreatEmailApp.ViewModels.FolderViewModel> folders,
        System.Collections.Generic.List<string> sink)
    {
        foreach (var f in folders)
        {
            if (!string.IsNullOrEmpty(f.Model.FullPath)) sink.Add(f.Model.FullPath);
            CollectFolderPaths(f.Children, sink);
        }
    }

    // ----- Contacts ----- //

    private void AddContact_Click(object sender, RoutedEventArgs e) => _vm.AddContact();

    private void RemoveContact_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is GreatEmailApp.Core.Models.Contact c)
            _vm.RemoveContact(c);
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddAccountDialog(App.Imap, App.Credentials, App.Accounts) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.Result is not null)
        {
            _vm.ManagedAccounts.Add(dlg.Result);
            AccountsChanged = true;
        }
    }

    private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            AppPaths.EnsureRoot();
            Process.Start(new ProcessStartInfo
            {
                FileName = AppPaths.Root,
                UseShellExecute = true,
            });
        }
        catch (System.Exception ex)
        {
            MessageBox.Show(this, $"Couldn't open folder: {ex.Message}", "Open data folder",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
