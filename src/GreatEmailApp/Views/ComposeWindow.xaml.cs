// FILE: src/GreatEmailApp/Views/ComposeWindow.xaml.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Views;

public partial class ComposeWindow : Window
{
    private readonly ComposeViewModel _vm;

    private ComposeWindow(ComposeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Sent += (_, _) => Dispatcher.BeginInvoke(new Action(Close));
    }

    /// <summary>Open a blank composer.</summary>
    public static ComposeWindow OpenNew(IEnumerable<Account> accounts, Account? defaultAccount = null)
        => Make(ComposeMode.New, accounts, defaultAccount, original: null);

    /// <summary>Open as a reply (or reply-all) to <paramref name="original"/>.</summary>
    public static ComposeWindow OpenReply(IEnumerable<Account> accounts, Account? defaultAccount,
        Message original, bool replyAll)
        => Make(replyAll ? ComposeMode.ReplyAll : ComposeMode.Reply, accounts, defaultAccount, original);

    /// <summary>Open as a forward of <paramref name="original"/>.</summary>
    public static ComposeWindow OpenForward(IEnumerable<Account> accounts, Account? defaultAccount,
        Message original)
        => Make(ComposeMode.Forward, accounts, defaultAccount, original);

    private static ComposeWindow Make(ComposeMode mode, IEnumerable<Account> accounts,
        Account? defaultAccount, Message? original)
    {
        var vm = new ComposeViewModel(App.Smtp, App.Imap, App.Credentials, accounts, defaultAccount);
        if (original is not null)
        {
            switch (mode)
            {
                case ComposeMode.Reply:    vm.PrepareReply(original, replyAll: false); break;
                case ComposeMode.ReplyAll: vm.PrepareReply(original, replyAll: true);  break;
                case ComposeMode.Forward:  vm.PrepareForward(original);                 break;
            }
        }
        var win = new ComposeWindow(vm)
        {
            Title = mode switch
            {
                ComposeMode.Reply    => "Reply",
                ComposeMode.ReplyAll => "Reply all",
                ComposeMode.Forward  => "Forward",
                _ => "New message",
            },
        };
        return win;
    }

    private void Discard_Click(object sender, RoutedEventArgs e)
    {
        // No persistence yet — Discard just closes. Drafts come in a follow-up
        // (would need an IDraftStore + IMAP Drafts append).
        if (HasUnsentContent())
        {
            var r = MessageBox.Show(this,
                "Discard this message? Anything you've typed will be lost.",
                "Discard message",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (r != MessageBoxResult.OK) return;
        }
        Close();
    }

    private bool HasUnsentContent() =>
        !string.IsNullOrWhiteSpace(_vm.ToAddresses) ||
        !string.IsNullOrWhiteSpace(_vm.Subject) ||
        !string.IsNullOrWhiteSpace(_vm.Body) ||
        _vm.Attachments.Count > 0;
}
