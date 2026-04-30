// FILE: src/GreatEmailApp/Controls/Sidebar.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
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
        }
    }

    private void FolderRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is FolderViewModel folder
            && DataContext is MainViewModel vm)
        {
            vm.SelectFolderCommand.Execute(folder);
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
}
