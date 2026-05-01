// FILE: src/GreatEmailApp/Views/ComposeWindow.xaml.cs
// Created: 2026-04-30 | Revised: 2026-05-01 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System;
using System.Threading.Tasks;
using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Views;

public partial class ComposeWindow : Window
{
    private readonly ComposeViewModel _vm;
    private bool _editorBootstrapped;

    private ComposeWindow(ComposeViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Sent += (_, _) => Dispatcher.BeginInvoke(new Action(Close));

        // Mirror editor edits back to the VM so Send.CanExecute and the eventual
        // BodyText / BodyHtml capture work without polling. Bootstrap the editor
        // contents once it's loaded so we don't race the WebView2 init.
        BodyEditor.Loaded += (_, _) => BootstrapEditor();
        BodyEditor.ContentChanged += async (_, _) => await CaptureBodyAsync();
        Loaded += (_, _) => BootstrapEditor();

        // We need the latest BodyHtml / BodyText right before send fires. Listen
        // on the VM's IsSending edge, capture on the way up.
        vm.PropertyChanged += async (_, ev) =>
        {
            if (ev.PropertyName == nameof(ComposeViewModel.IsSending) && vm.IsSending)
                await CaptureBodyAsync();
        };
    }

    private void BootstrapEditor()
    {
        if (_editorBootstrapped) return;
        _editorBootstrapped = true;
        BodyEditor.SetHtml(_vm.BodyHtml);
    }

    private async Task CaptureBodyAsync()
    {
        try
        {
            _vm.BodyHtml = await BodyEditor.GetHtmlAsync();
            _vm.BodyText = await BodyEditor.GetPlainTextAsync();
        }
        catch { /* editor not ready, ignore */ }
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
        var vm = new ComposeViewModel(App.Smtp, App.Imap, App.Credentials, App.Contacts, accounts, defaultAccount);
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
        !string.IsNullOrWhiteSpace(_vm.BodyText) ||
        _vm.Attachments.Count > 0;
}
