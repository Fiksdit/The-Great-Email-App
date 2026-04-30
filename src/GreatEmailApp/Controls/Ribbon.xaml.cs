// FILE: src/GreatEmailApp/Controls/Ribbon.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GreatEmailApp.ViewModels;
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
}
