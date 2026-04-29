// FILE: src/GreatEmailApp/Controls/Ribbon.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using GreatEmailApp.ViewModels;

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
        // Phase 3: opens the File Backstage view. For now, ack with a message.
        MessageBox.Show(
            "The File backstage view (Account Settings, Rules, Options, Sign In/Out) arrives in Phase 3.",
            "File",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OnSettings_Click(object sender, RoutedEventArgs e)
    {
        MessageBox.Show(
            "Settings dialog arrives in Phase 3.",
            "Settings",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
