// FILE: src/GreatEmailApp/Controls/MailList.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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

    private void Pill_Click(object sender, RoutedEventArgs e)
    {
        if (sender is ToggleButton tb && tb.Tag is string tag && DataContext is MainViewModel vm)
        {
            vm.Filter = tag;
            tb.IsChecked = true;
        }
    }
}
