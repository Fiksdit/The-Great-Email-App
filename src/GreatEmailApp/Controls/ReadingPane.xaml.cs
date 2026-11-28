// FILE: src/GreatEmailApp/Controls/ReadingPane.xaml.cs
// Created: 2026-04-29 | Revised: 2026-11-27 | Rev: 3
// Changed by: Claude Opus 5.5 on behalf of James Reed

using System.Linq;
using System.Windows;
using System.Windows.Controls;
using GreatEmailApp.Core.Models;
using GreatEmailApp.ViewModels;
using GreatEmailApp.Views;

namespace GreatEmailApp.Controls;

public partial class ReadingPane : UserControl
{
    public ReadingPane()
    {
        InitializeComponent();
    }

    private void Reply_Click(object sender, RoutedEventArgs e)    => OpenCompose(sender, replyAll: false, forward: false);
    private void ReplyAll_Click(object sender, RoutedEventArgs e) => OpenCompose(sender, replyAll: true,  forward: false);
    private void Forward_Click(object sender, RoutedEventArgs e)  => OpenCompose(sender, replyAll: false, forward: true);

    private void OpenCompose(object sender, bool replyAll, bool forward)
    {
        // The buttons sit inside the DockPanel bound to SelectedMessage; the
        // UserControl's own DataContext is MainViewModel, so read the
        // button's (FIX-2026-11-27-006 — these buttons never did anything).
        if ((sender as FrameworkElement)?.DataContext is not MessageViewModel mvm) return;
        var msg = mvm.Model;

        var accounts = App.Accounts.LoadAll();
        if (accounts.Count == 0) return;
        var defaultAccount = accounts.FirstOrDefault(a => a.Id == msg.AccountId)
                             ?? accounts.FirstOrDefault(a => a.IsPrimary)
                             ?? accounts.First();

        var win = forward
            ? ComposeWindow.OpenForward(accounts, defaultAccount, msg)
            : ComposeWindow.OpenReply(accounts, defaultAccount, msg, replyAll);
        win.Owner = Window.GetWindow(this);
        win.Show();
    }
}
