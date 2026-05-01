// FILE: src/GreatEmailApp/Controls/Ribbon.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Linq;
using GreatEmailApp.ViewModels;
using GreatEmailApp.Views;
using GreatEmailApp.Views.Dialogs;

namespace GreatEmailApp.Controls;

public partial class Ribbon : UserControl
{
    public Ribbon()
    {
        InitializeComponent();
    }

    private void Tab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag && DataContext is MainViewModel vm)
        {
            vm.ActiveRibbonTab = tag;
            tb.IsChecked = true;
        }
    }

    private void FileTab_Click(object sender, RoutedEventArgs e)
    {
        // NOTE: full backstage (Account Info, Manage Rules, etc.) is Phase 3.5.
        // For now, File opens the Settings dialog which has the same surfaces.
        var dlg = new SettingsDialog { Owner = Window.GetWindow(this) };
        dlg.ShowDialog();
        if (dlg.AccountsChanged && DataContext is MainViewModel vm)
        {
            vm.ReloadAccounts();
        }
    }

    private void OnSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new GreatEmailApp.Views.Dialogs.SettingsDialog
        {
            Owner = Window.GetWindow(this),
        };
        dlg.ShowDialog();
        if (dlg.AccountsChanged && DataContext is MainViewModel vm)
        {
            vm.ReloadAccounts();
        }
    }

    /// <summary>
    /// Help → Check for Updates: just open Settings on the About tab.
    /// All update logic lives there; ribbon button is a shortcut, not a duplicate.
    /// </summary>
    private void OnCheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsDialog { Owner = Window.GetWindow(this) };
        dlg.OpenOnTab("About");
        dlg.ShowDialog();
        if (dlg.AccountsChanged && DataContext is MainViewModel vm)
        {
            vm.ReloadAccounts();
        }
    }

    // --- Compose -------------------------------------------------------- //

    private void OnNewEmail_Click(object sender, RoutedEventArgs e)
    {
        if (App.Accounts.LoadAll().Count == 0)
        {
            System.Windows.MessageBox.Show(Window.GetWindow(this),
                "Add an account before composing.", "No accounts",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var win = ComposeWindow.OpenNew(App.Accounts.LoadAll(), DefaultAccount());
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

    private void OnReply_Click(object sender, RoutedEventArgs e)    => OpenReplyWindow(replyAll: false);
    private void OnReplyAll_Click(object sender, RoutedEventArgs e) => OpenReplyWindow(replyAll: true);
    private void OnForward_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentMessage() is not GreatEmailApp.Core.Models.Message msg) return;
        var win = ComposeWindow.OpenForward(App.Accounts.LoadAll(), DefaultAccount(), msg);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

    private void OpenReplyWindow(bool replyAll)
    {
        if (CurrentMessage() is not GreatEmailApp.Core.Models.Message msg) return;
        var win = ComposeWindow.OpenReply(App.Accounts.LoadAll(), DefaultAccount(), msg, replyAll);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }

    private GreatEmailApp.Core.Models.Message? CurrentMessage() =>
        (DataContext as MainViewModel)?.SelectedMessage?.Model;

    private GreatEmailApp.Core.Models.Account? DefaultAccount()
    {
        var all = App.Accounts.LoadAll();
        var fromMsg = CurrentMessage()?.AccountId;
        if (!string.IsNullOrEmpty(fromMsg))
        {
            var match = all.FirstOrDefault(a => a.Id == fromMsg);
            if (match is not null) return match;
        }
        return all.FirstOrDefault(a => a.IsPrimary) ?? all.FirstOrDefault();
    }
}
