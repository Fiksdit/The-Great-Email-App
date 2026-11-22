// FILE: src/GreatEmailApp/Views/Dialogs/RuleEditorDialog.xaml.cs
// Created: 2026-05-01 | Revised: 2026-11-21 | Rev: 3
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// Single-rule editor — name + match radio + dynamic conditions/actions rows.
// Each row is built in code (no DataTemplates yet) so we can keep adding/
// removing them without an inner ObservableCollection of editor-only DTOs.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Views.Dialogs;

public partial class RuleEditorDialog : Window
{
    public MailRule Result { get; private set; }

    private const string OtherCategory = "Other…";

    private readonly List<ConditionRow> _conditionRows = new();
    private readonly List<ActionRow> _actionRows = new();

    /// <summary>Map of accountId → flat list of folder full paths, supplied by the host
    /// so the action editor can offer a folder picker. Empty Id ("") = union of all.</summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<string>> _foldersByAccount;

    public RuleEditorDialog(
        MailRule existing,
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<string, IReadOnlyList<string>> foldersByAccount)
    {
        InitializeComponent();
        _foldersByAccount = foldersByAccount;

        // Account picker: empty Id = "All accounts".
        var picks = new List<Account> { new() { Id = "", DisplayName = "All accounts", EmailAddress = "" } };
        picks.AddRange(accounts);
        AccountCombo.ItemsSource = picks;
        AccountCombo.SelectedValue = existing.AccountId ?? "";

        // Re-source every action row's folder list when the account scope changes,
        // since "Move to folder" should reflect the right account's hierarchy.
        AccountCombo.SelectionChanged += (_, _) =>
        {
            var folders = FoldersForCurrentAccount();
            foreach (var r in _actionRows) r.SetFolderChoices(folders);
        };

        // Category: the two built-ins plus any custom names already in use.
        var categories = new List<string> { MailRule.SpamCategory, MailRule.OrganizingCategory };
        foreach (var c in App.Rules.LoadAll().Select(r => r.EffectiveCategory))
            if (!categories.Contains(c, StringComparer.OrdinalIgnoreCase)) categories.Add(c);
        categories.Add(OtherCategory);
        CategoryCombo.ItemsSource = categories;
        CategoryCombo.SelectedItem = categories.FirstOrDefault(c =>
            string.Equals(c, existing.EffectiveCategory, StringComparison.OrdinalIgnoreCase));
        CategoryCombo.SelectionChanged += (_, _) =>
        {
            var other = Equals(CategoryCombo.SelectedItem, OtherCategory);
            CustomCategoryBox.Visibility = other ? Visibility.Visible : Visibility.Collapsed;
            if (other) CustomCategoryBox.Focus();
        };

        // Picking a category from the list sets the usual scope for it:
        // spam rules cover every account, organizing rules just one (folders
        // differ per account). The user can still change "Apply to" after.
        CategoryCombo.SelectionChanged += (_, _) =>
        {
            var picked = CategoryCombo.SelectedItem as string;
            if (string.Equals(picked, MailRule.SpamCategory, StringComparison.OrdinalIgnoreCase))
                AccountCombo.SelectedValue = "";
            else if (string.Equals(picked, MailRule.OrganizingCategory, StringComparison.OrdinalIgnoreCase)
                     && string.IsNullOrEmpty(AccountCombo.SelectedValue as string)
                     && accounts.Count > 0)
                AccountCombo.SelectedValue = (accounts.FirstOrDefault(a => a.IsPrimary) ?? accounts[0]).Id;
        };

        NameBox.Text = existing.Name;
        EnabledCheck.IsChecked = existing.IsEnabled;
        StopOnMatchCheck.IsChecked = existing.StopOnMatch;
        IncomingOnlyCheck.IsChecked = existing.IncomingOnly;
        MatchAll.IsChecked = existing.Match == RuleMatch.All;
        MatchAny.IsChecked = existing.Match == RuleMatch.Any;

        // Snapshot the existing rule's id so we update vs. insert correctly on save.
        Result = new MailRule
        {
            Id = string.IsNullOrEmpty(existing.Id) ? Guid.NewGuid().ToString("N") : existing.Id,
            Name = existing.Name,
            CreatedAt = existing.CreatedAt == default ? DateTimeOffset.UtcNow : existing.CreatedAt,
        };

        if (existing.Conditions.Count == 0) AddConditionRow(new RuleCondition());
        else foreach (var c in existing.Conditions) AddConditionRow(c);

        if (existing.Actions.Count == 0) AddActionRow(new RuleActionItem());
        else foreach (var a in existing.Actions) AddActionRow(a);
    }

    // --------------------------------------------------------------------- //
    // Condition / action rows
    // --------------------------------------------------------------------- //

    private void AddCondition_Click(object sender, RoutedEventArgs e) => AddConditionRow(new RuleCondition());
    private void AddAction_Click(object sender, RoutedEventArgs e)    => AddActionRow(new RuleActionItem());

    private void AddConditionRow(RuleCondition seed)
    {
        var row = new ConditionRow(seed);
        row.Removed += () => { _conditionRows.Remove(row); ConditionsList.Items.Remove(row.Root); };
        _conditionRows.Add(row);
        ConditionsList.Items.Add(row.Root);
    }

    private void AddActionRow(RuleActionItem seed)
    {
        var row = new ActionRow(seed, FoldersForCurrentAccount());
        row.Removed += () => { _actionRows.Remove(row); ActionsList.Items.Remove(row.Root); };
        _actionRows.Add(row);
        ActionsList.Items.Add(row.Root);
    }

    private IReadOnlyList<string> FoldersForCurrentAccount()
    {
        var pickedId = (AccountCombo.SelectedValue as string) ?? "";
        if (!string.IsNullOrEmpty(pickedId) && _foldersByAccount.TryGetValue(pickedId, out var list))
            return list;
        // "All accounts" → union, distinct, sorted.
        return _foldersByAccount.SelectMany(kv => kv.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // --------------------------------------------------------------------- //
    // Save
    // --------------------------------------------------------------------- //

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Result.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? "(unnamed rule)" : NameBox.Text.Trim();
        Result.IsEnabled = EnabledCheck.IsChecked == true;
        Result.StopOnMatch = StopOnMatchCheck.IsChecked == true;
        Result.IncomingOnly = IncomingOnlyCheck.IsChecked == true;
        Result.Match = MatchAny.IsChecked == true ? RuleMatch.Any : RuleMatch.All;
        Result.Category = Equals(CategoryCombo.SelectedItem, OtherCategory)
            ? (CustomCategoryBox.Text ?? "").Trim()
            : (CategoryCombo.SelectedItem as string ?? "").Trim();
        var pickedId = AccountCombo.SelectedValue as string;
        Result.AccountId = string.IsNullOrEmpty(pickedId) ? null : pickedId;
        Result.Conditions = _conditionRows.Select(r => r.ToCondition()).Where(c => !string.IsNullOrWhiteSpace(c.Value)).ToList();
        Result.Actions    = _actionRows.Select(r => r.ToAction()).Where(a => a.Type != RuleAction.MoveToFolder || !string.IsNullOrWhiteSpace(a.Value)).ToList();

        if (Result.Conditions.Count == 0)
        {
            MessageBox.Show(this, "Add at least one condition with a value.", "Rule incomplete",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (Result.Actions.Count == 0)
        {
            MessageBox.Show(this, "Add at least one action.", "Rule incomplete",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Permanent delete is irreversible — confirm once when the rule uses it.
        if (Result.Actions.Any(a => a.IsPermanentDelete)
            && MessageBox.Show(this,
                   "This rule permanently deletes matching mail. It won't go to Trash and can't be recovered.\n\n" +
                   "It also applies when you click \"Run rules now\". Save it?",
                   "Delete permanently", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        DialogResult = true;
        Close();
    }

    // --------------------------------------------------------------------- //
    // Inner row builders — code-built so we don't pay for DataTemplate ceremony.
    // --------------------------------------------------------------------- //

    private sealed class ConditionRow
    {
        public Border Root { get; }
        public ComboBox FieldCb { get; }
        public ComboBox OpCb { get; }
        public TextBox ValueBox { get; }
        public event Action? Removed;

        public ConditionRow(RuleCondition seed)
        {
            FieldCb = LabeledCombo(FieldChoices, seed.Field);
            OpCb    = LabeledCombo(OpChoices, seed.Op);
            ValueBox = new TextBox
            {
                Text = seed.Value,
                ToolTip = "For \"From ends with\", list several endings separated by commas to match any of them " +
                          "(e.g. .pro, .xyz, .top). Pasted lines are joined with commas automatically.",
                Background = (Brush)Application.Current.Resources["ElevatedBackgroundBrush"],
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
                BorderBrush = (Brush)Application.Current.Resources["DividerBrush"],
                BorderThickness = new Thickness(1),
                CaretBrush = (Brush)Application.Current.Resources["PrimaryTextBrush"],
                Padding = new Thickness(6, 5, 6, 5),
            };
            var remove = new Button
            {
                Content = "✕",
                Style = (Style)Application.Current.Resources["SubtleButton"],
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(8, 0, 0, 0),
            };
            remove.Click += (_, _) => Removed?.Invoke();

            // Single-line box: turn a pasted one-per-line list into "a, b, c"
            // instead of silently keeping only the first line.
            DataObject.AddPastingHandler(ValueBox, (_, e) =>
            {
                // Reading the clipboard throws CLIPBRD_E_CANT_OPEN when another
                // program has it open at that instant — that crashed the app
                // (FIX-2026-11-21-005). On failure, let the normal paste run.
                string? text;
                try { text = e.DataObject.GetData(DataFormats.UnicodeText) as string; }
                catch (System.Runtime.InteropServices.COMException) { return; }
                if (text is null || !text.Contains('\n')) return;
                var joined = string.Join(", ", text
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(s => s.Length > 0));
                var d = new DataObject();
                d.SetData(DataFormats.UnicodeText, joined);
                e.DataObject = d;
            });

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            FieldCb.Margin = new Thickness(0, 0, 6, 0); Grid.SetColumn(FieldCb, 0); grid.Children.Add(FieldCb);
            OpCb.Margin    = new Thickness(0, 0, 6, 0); Grid.SetColumn(OpCb, 1); grid.Children.Add(OpCb);
            Grid.SetColumn(ValueBox, 2); grid.Children.Add(ValueBox);
            Grid.SetColumn(remove, 3); grid.Children.Add(remove);

            Root = new Border { Padding = new Thickness(0, 4, 0, 4), Child = grid };
        }

        public RuleCondition ToCondition() => new()
        {
            Field = ((Choice<RuleField>)FieldCb.SelectedItem).Value,
            Op = ((Choice<RuleOp>)OpCb.SelectedItem).Value,
            Value = ValueBox.Text ?? "",
        };
    }

    private sealed class ActionRow
    {
        public Border Root { get; }
        public ComboBox TypeCb { get; }
        public ComboBox FolderCb { get; }
        public event Action? Removed;

        public ActionRow(RuleActionItem seed, IReadOnlyList<string> folderChoices)
        {
            TypeCb = new ComboBox
            {
                ItemsSource = ActionChoices,
                DisplayMemberPath = nameof(ActionChoice.Label),
                SelectedItem = ActionChoices.First(c => c.Type == seed.Type && c.Permanent == seed.IsPermanentDelete),
            };
            // Editable ComboBox: dropdown shows known folder paths for the rule's
            // account scope, but the user can also type a custom path (lets you
            // pre-create rules for folders that don't exist yet).
            FolderCb = new ComboBox
            {
                IsEditable = true,
                IsTextSearchEnabled = true,
                Background = (Brush)Application.Current.Resources["ElevatedBackgroundBrush"],
                Foreground = (Brush)Application.Current.Resources["PrimaryTextBrush"],
                BorderBrush = (Brush)Application.Current.Resources["DividerBrush"],
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6, 5, 6, 5),
                Text = seed.Type == RuleAction.MoveToFolder ? seed.Value ?? "" : "",
                ToolTip = "Pick or type a folder full path (e.g. \"Receipts\" or \"Vendors/Mobile Sentrix\"). Subfolders are nested with '/'.",
            };
            SetFolderChoicesInternal(folderChoices);

            FolderCb.IsEnabled = SelectedChoice.Type == RuleAction.MoveToFolder;
            TypeCb.SelectionChanged += (_, _) =>
                FolderCb.IsEnabled = SelectedChoice.Type == RuleAction.MoveToFolder;

            var remove = new Button
            {
                Content = "✕",
                Style = (Style)Application.Current.Resources["SubtleButton"],
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(8, 0, 0, 0),
            };
            remove.Click += (_, _) => Removed?.Invoke();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(160) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            TypeCb.Margin = new Thickness(0, 0, 6, 0); Grid.SetColumn(TypeCb, 0); grid.Children.Add(TypeCb);
            Grid.SetColumn(FolderCb, 1); grid.Children.Add(FolderCb);
            Grid.SetColumn(remove, 2); grid.Children.Add(remove);

            Root = new Border { Padding = new Thickness(0, 4, 0, 4), Child = grid };
        }

        public void SetFolderChoices(IReadOnlyList<string> folders) => SetFolderChoicesInternal(folders);

        private void SetFolderChoicesInternal(IReadOnlyList<string> folders)
        {
            // Preserve the user's typed/selected text across an itemsource swap.
            var current = FolderCb.Text;
            FolderCb.ItemsSource = folders;
            FolderCb.Text = current;
        }

        private ActionChoice SelectedChoice => (ActionChoice)TypeCb.SelectedItem;

        public RuleActionItem ToAction()
        {
            var c = SelectedChoice;
            return new RuleActionItem
            {
                Type = c.Type,
                Value = c.Permanent ? RuleActionItem.PermanentDeleteValue
                      : c.Type == RuleAction.MoveToFolder ? FolderCb.Text ?? ""
                      : "",
            };
        }
    }

    // --------------------------------------------------------------------- //
    // Dropdown choices — human labels over the stored enums. Labels are
    // display-only; rules.json still stores the enum names (sync-compatible).
    // --------------------------------------------------------------------- //

    private sealed record Choice<T>(T Value, string Label) where T : struct, Enum;

    private sealed record ActionChoice(RuleAction Type, bool Permanent, string Label);

    private static readonly Choice<RuleField>[] FieldChoices =
    {
        new(RuleField.From, "From"),
        new(RuleField.To, "To"),
        new(RuleField.Subject, "Subject"),
        new(RuleField.Body, "Body"),
    };

    private static readonly Choice<RuleOp>[] OpChoices =
    {
        new(RuleOp.Contains, "contains"),
        new(RuleOp.Equals, "is exactly"),
        new(RuleOp.StartsWith, "starts with"),
        new(RuleOp.EndsWith, "ends with"),
    };

    private static readonly ActionChoice[] ActionChoices =
    {
        new(RuleAction.MoveToFolder, false, "Move to folder"),
        new(RuleAction.MarkRead, false, "Mark as read"),
        new(RuleAction.Flag, false, "Flag"),
        new(RuleAction.Delete, false, "Move to Trash"),
        new(RuleAction.Delete, true, "Delete permanently (skip Trash)"),
    };

    private static ComboBox LabeledCombo<T>(Choice<T>[] choices, T initial) where T : struct, Enum => new()
    {
        ItemsSource = choices,
        DisplayMemberPath = nameof(Choice<T>.Label),
        SelectedItem = choices.FirstOrDefault(c => EqualityComparer<T>.Default.Equals(c.Value, initial)) ?? choices[0],
    };
}
