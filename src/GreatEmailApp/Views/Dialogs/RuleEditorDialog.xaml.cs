// FILE: src/GreatEmailApp/Views/Dialogs/RuleEditorDialog.xaml.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
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

        NameBox.Text = existing.Name;
        EnabledCheck.IsChecked = existing.IsEnabled;
        StopOnMatchCheck.IsChecked = existing.StopOnMatch;
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
        Result.Match = MatchAny.IsChecked == true ? RuleMatch.Any : RuleMatch.All;
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
            FieldCb = ComboFor<RuleField>(seed.Field);
            OpCb    = ComboFor<RuleOp>(seed.Op);
            ValueBox = new TextBox
            {
                Text = seed.Value,
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
            Field = (RuleField)FieldCb.SelectedItem,
            Op = (RuleOp)OpCb.SelectedItem,
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
            TypeCb = ComboFor<RuleAction>(seed.Type);
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
                Text = seed.Value ?? "",
                ToolTip = "Pick or type a folder full path (e.g. \"Receipts\" or \"Vendors/Mobile Sentrix\"). Subfolders are nested with '/'.",
            };
            SetFolderChoicesInternal(folderChoices);

            FolderCb.IsEnabled = (RuleAction)TypeCb.SelectedItem == RuleAction.MoveToFolder;
            TypeCb.SelectionChanged += (_, _) =>
                FolderCb.IsEnabled = (RuleAction)TypeCb.SelectedItem == RuleAction.MoveToFolder;

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

        public RuleActionItem ToAction() => new()
        {
            Type = (RuleAction)TypeCb.SelectedItem,
            Value = FolderCb.Text ?? "",
        };
    }

    private static ComboBox ComboFor<T>(T initial) where T : struct, Enum
    {
        var cb = new ComboBox
        {
            ItemsSource = Enum.GetValues<T>(),
            SelectedItem = initial,
        };
        return cb;
    }
}
