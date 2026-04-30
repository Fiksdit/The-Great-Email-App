// FILE: src/GreatEmailApp/Controls/MailList.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using GreatEmailApp.Core.Models;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Controls;

public partial class MailList : UserControl
{
    public MailList()
    {
        InitializeComponent();
    }

    private void Row_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is MessageViewModel msg
            && DataContext is MainViewModel vm)
        {
            vm.SelectMessageCommand.Execute(msg);
        }
    }

    private void Row_RightClick(object sender, MouseButtonEventArgs e)
    {
        // Select the message before the menu opens so the action targets it.
        if (sender is FrameworkElement fe && fe.Tag is MessageViewModel msg
            && DataContext is MainViewModel vm)
        {
            vm.SelectMessageCommand.Execute(msg);
        }
    }

    private void Pill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag && DataContext is MainViewModel vm)
        {
            vm.Filter = tag;
            tb.IsChecked = true;
        }
    }

    // ── Context-menu helpers ─────────────────────────────────────────

    private static MessageViewModel? TargetOf(object sender)
    {
        if (sender is MenuItem mi
            && FindContextMenu(mi) is { PlacementTarget: FrameworkElement target }
            && target.Tag is MessageViewModel msg)
            return msg;
        return null;
    }

    private static ContextMenu? FindContextMenu(DependencyObject d)
    {
        while (d is not null)
        {
            if (d is ContextMenu cm) return cm;
            d = LogicalTreeHelper.GetParent(d) ?? System.Windows.Media.VisualTreeHelper.GetParent(d);
        }
        return null;
    }

    private void MessageMenu_Opened(object sender, RoutedEventArgs e)
    {
        // Populate Move To submenu dynamically — every folder under every
        // account, indented by depth. Skips the synthetic Outbox (no IMAP path).
        if (sender is not ContextMenu cm || DataContext is not MainViewModel vm) return;
        if (cm.Items.OfType<MenuItem>().FirstOrDefault(i => i.Name == "MoveToMenu") is not MenuItem moveTo) return;

        moveTo.Items.Clear();
        var msg = (cm.PlacementTarget as FrameworkElement)?.Tag as MessageViewModel;

        foreach (var account in vm.Accounts)
        {
            // Only show folders for the message's own account (IMAP can't move
            // across accounts in a single command).
            if (msg is not null && account.Model.Id != msg.Model.AccountId) continue;

            var accountHeader = new MenuItem
            {
                Header = account.EmailAddress,
                IsEnabled = false,
                Tag = "header",
            };
            moveTo.Items.Add(accountHeader);

            foreach (var folder in account.Folders)
                AppendFolderMenuItem(moveTo, folder, vm, msg, depth: 0);
        }
    }

    private void AppendFolderMenuItem(MenuItem parent, FolderViewModel folder,
        MainViewModel vm, MessageViewModel? msg, int depth)
    {
        if (string.IsNullOrEmpty(folder.Model.FullPath)) return; // skip Outbox

        var indent = new string(' ', depth * 2);
        var item = new MenuItem { Header = indent + folder.Name };
        item.Click += (_, _) =>
        {
            if (msg is not null) vm.MoveToFolderCommand.Execute((msg, folder));
        };
        parent.Items.Add(item);

        foreach (var child in folder.Children)
            AppendFolderMenuItem(parent, child, vm, msg, depth + 1);
    }

    // ── Menu item handlers ───────────────────────────────────────────

    private void Reply_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Reply / compose lands in Phase 5.", "Reply",
            MessageBoxButton.OK, MessageBoxImage.Information);

    private void ReplyAll_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Reply All / compose lands in Phase 5.", "Reply All",
            MessageBoxButton.OK, MessageBoxImage.Information);

    private void Forward_Click(object sender, RoutedEventArgs e) =>
        MessageBox.Show("Forward / compose lands in Phase 5.", "Forward",
            MessageBoxButton.OK, MessageBoxImage.Information);

    private void ToggleRead_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.ToggleReadCommand.Execute(m);
    }

    private void ToggleFlag_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.ToggleFlagCommand.Execute(m);
    }

    private void Archive_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.ArchiveCommand.Execute(m);
    }

    private void Junk_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.JunkCommand.Execute(m);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.DeleteCommand.Execute(m);
    }

    private void CreateRule_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.CreateRuleFromMessageCommand.Execute(m);
    }
}
