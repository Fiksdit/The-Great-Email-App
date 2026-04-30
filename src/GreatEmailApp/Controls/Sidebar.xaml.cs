// FILE: src/GreatEmailApp/Controls/Sidebar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Controls;

public partial class Sidebar : UserControl
{
    public Sidebar()
    {
        InitializeComponent();
    }

    private void AccountHeader_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is AccountViewModel acc)
        {
            acc.IsExpanded = !acc.IsExpanded;
            e.Handled = true;
        }
    }

    private void FolderRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is FolderViewModel folder
            && DataContext is MainViewModel vm)
        {
            vm.SelectFolderCommand.Execute(folder);
            e.Handled = true;
        }
    }

    private void FolderRow_RightClick(object sender, MouseButtonEventArgs e)
    {
        // Select the folder before opening the menu so any actions act on it.
        if (sender is FrameworkElement fe && fe.Tag is FolderViewModel folder
            && DataContext is MainViewModel vm)
        {
            vm.SelectFolderCommand.Execute(folder);
        }
    }

    private void FolderCaret_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is FolderViewModel folder && folder.HasChildren)
        {
            folder.IsExpanded = !folder.IsExpanded;
            e.Handled = true;
        }
    }

    private void AddAccount_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new GreatEmailApp.Views.Dialogs.AddAccountDialog(App.Imap, App.Credentials, App.Accounts)
        {
            Owner = Window.GetWindow(this),
        };
        if (dlg.ShowDialog() == true && dlg.Result is not null
            && DataContext is ViewModels.MainViewModel vm)
        {
            vm.OnAccountAdded(dlg.Result);
        }
    }

    // ── Folder context-menu helpers ──────────────────────────────────

    private static FolderViewModel? TargetOf(object sender)
    {
        if (sender is MenuItem mi
            && FindContextMenu(mi) is { PlacementTarget: FrameworkElement target }
            && target.Tag is FolderViewModel folder)
            return folder;
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

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.SelectFolderCommand.Execute(f);
    }

    private void MarkFolderRead_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.MarkFolderReadCommand.Execute(f);
    }

    private void NewSubfolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.NewSubfolderCommand.Execute(f);
    }

    private void RenameFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.RenameFolderCommand.Execute(f);
    }

    private void DeleteFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.DeleteFolderCommand.Execute(f);
    }

    private void EmptyFolder_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm && TargetOf(sender) is FolderViewModel f)
            vm.EmptyFolderCommand.Execute(f);
    }
}
