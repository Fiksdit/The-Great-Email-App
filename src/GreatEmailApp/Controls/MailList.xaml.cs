// FILE: src/GreatEmailApp/Controls/MailList.xaml.cs
// Created: 2026-04-29 | Revised: 2026-06-12 | Rev: 9
// Changed by: Claude Opus 4.8 on behalf of James Reed

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
    private MainViewModel? _boundVm;

    public MailList()
    {
        InitializeComponent();
        // Listen for folder-click events on whichever MainViewModel is bound
        // (re-binds correctly if DataContext ever swaps).
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_boundVm is not null) _boundVm.FolderLoaded -= OnFolderLoaded;
        _boundVm = e.NewValue as MainViewModel;
        if (_boundVm is not null) _boundVm.FolderLoaded += OnFolderLoaded;
    }

    private void OnFolderLoaded(object? sender, EventArgs e)
    {
        // Marshal to UI thread defensively — the event is raised from an
        // async method whose continuation runs on the captured context, but
        // belt-and-suspenders here in case that ever changes.
        Dispatcher.BeginInvoke(new Action(() => MessageScroll?.ScrollToTop()));
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

    // ListSearchBox_TextChanged removed — placeholder visibility is now
    // driven entirely by a XAML MultiDataTrigger on ListSearchPlaceholder
    // (empty text AND not focused). Removed the TextChanged handler hook
    // in MailList.xaml too.

    private void Pill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton tb || tb.Tag is not string tag) return;
        if (DataContext is MainViewModel vm) vm.Filter = tag;

        // Force exclusive selection: walk the parent StackPanel and set every
        // sibling pill's IsChecked = (its tag == ours). Relying on the OneWay
        // {Filter}-to-{IsChecked} binding alone wasn't enough — ToggleButton's
        // built-in click behavior locally sets IsChecked, and the previously-
        // selected pill could end up stuck checked alongside the new one.
        if (tb.Parent is System.Windows.Controls.Panel panel)
        {
            foreach (var child in panel.Children)
            {
                if (child is ToggleButton sibling && sibling.Tag is string siblingTag)
                    sibling.IsChecked = string.Equals(siblingTag, tag, System.StringComparison.Ordinal);
            }
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
        // Populate Move To submenu dynamically. Subfolders nest under their
        // parent as a real submenu (hover to open) so deeply-foldered accounts
        // don't blow the menu past screen height. Skips the synthetic Outbox
        // (no IMAP path).
        if (sender is not ContextMenu cm || DataContext is not MainViewModel vm) return;

        // "Not spam" only makes sense inside the Junk folder — hide it elsewhere.
        if (cm.Items.OfType<MenuItem>().FirstOrDefault(i => i.Name == "NotSpamMenu") is MenuItem notSpam)
        {
            notSpam.Visibility = vm.SelectedFolder?.Model.Special == SpecialFolder.Junk
                ? Visibility.Visible : Visibility.Collapsed;
        }

        if (cm.Items.OfType<MenuItem>().FirstOrDefault(i => i.Name == "MoveToMenu") is not MenuItem moveTo) return;

        moveTo.Items.Clear();
        var msg = (cm.PlacementTarget as FrameworkElement)?.Tag as MessageViewModel;

        // Shared with the ribbon Move button — see FolderMoveMenu.
        foreach (var item in FolderMoveMenu.BuildItems(vm, msg))
            moveTo.Items.Add(item);
    }

    // ── Menu item handlers ───────────────────────────────────────────

    private void Reply_Click(object sender, RoutedEventArgs e)    => OpenCompose(sender, replyAll: false, forward: false);
    private void ReplyAll_Click(object sender, RoutedEventArgs e) => OpenCompose(sender, replyAll: true,  forward: false);
    private void Forward_Click(object sender, RoutedEventArgs e)  => OpenCompose(sender, replyAll: false, forward: true);

    private void OpenCompose(object sender, bool replyAll, bool forward)
    {
        // Same flow as ReadingPane.OpenCompose, but the target message comes
        // from the context-menu's placement target (the right-clicked row's
        // Tag), not from DataContext.
        if (TargetOf(sender) is not MessageViewModel mvm) return;
        var msg = mvm.Model;

        var accounts = App.Accounts.LoadAll();
        if (accounts.Count == 0) return;
        var defaultAccount = accounts.FirstOrDefault(a => a.Id == msg.AccountId)
                             ?? accounts.FirstOrDefault(a => a.IsPrimary)
                             ?? accounts.First();

        var win = forward
            ? GreatEmailApp.Views.ComposeWindow.OpenForward(accounts, defaultAccount, msg)
            : GreatEmailApp.Views.ComposeWindow.OpenReply(accounts, defaultAccount, msg, replyAll);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

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

    private void MarkAsSpam_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.MarkAsSpamCommand.Execute(m);
    }

    private void NotSpam_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is MessageViewModel m)
            vm.NotSpamCommand.Execute(m);
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
