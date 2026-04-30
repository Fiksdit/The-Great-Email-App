// FILE: src/GreatEmailApp/Views/Dialogs/AddAccountDialog.xaml.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Threading;
using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Views.Dialogs;

public partial class AddAccountDialog : Window
{
    private readonly AddAccountViewModel _vm;
    private readonly ICredentialStore _creds;
    private readonly IAccountStore _accountStore;
    private CancellationTokenSource? _cts;

    /// <summary>The created Account on success, null if cancelled.</summary>
    public Account? Result { get; private set; }

    public AddAccountDialog(IImapService imap, ICredentialStore creds, IAccountStore accountStore)
    {
        InitializeComponent();
        _creds = creds;
        _accountStore = accountStore;
        _vm = new AddAccountViewModel(imap);
        DataContext = _vm;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        DialogResult = false;
        Close();
    }

    private async void Test_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.ImapHost) || string.IsNullOrWhiteSpace(_vm.Username))
        {
            MessageBox.Show(this, "Fill in IMAP server and username first.", "Test Connection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pwd = PasswordBox.Password;
        if (string.IsNullOrEmpty(pwd))
        {
            MessageBox.Show(this, "Enter a password to test.", "Test Connection",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        TestBtn.IsEnabled = false;
        SaveBtn.IsEnabled = false;
        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            await _vm.TestAsync(pwd, _cts.Token);
        }
        finally
        {
            TestBtn.IsEnabled = true;
            SaveBtn.IsEnabled = true;
            // NOTE: pwd reference is local; nothing else holds it. PasswordBox
            // keeps its own SecureString-backed copy until the window closes.
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_vm.EmailAddress))
        {
            MessageBox.Show(this, "Email address is required.", "Add Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var pwd = PasswordBox.Password;
        if (string.IsNullOrEmpty(pwd))
        {
            MessageBox.Show(this, "Enter the account password.", "Add Account",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Always test before saving — surface auth/host errors up front rather than
        // saving a broken account and confusing the user later.
        SaveBtn.IsEnabled = false;
        TestBtn.IsEnabled = false;
        _cts?.Cancel();
        _cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            var test = await _vm.TestAsync(pwd, _cts.Token);
            if (test is not Result<bool>.Ok)
            {
                MessageBox.Show(this,
                    $"Couldn't sign in: {_vm.TestMessage}\n\nFix the settings or click Test Connection to retry.",
                    "Connection failed",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var account = _vm.ToAccount();

            // Persist account config (no password), then password to credential store.
            var existing = _accountStore.LoadAll().ToList();
            existing.Add(account);
            _accountStore.Save(existing);
            _creds.Save(account.Id, account.Username, pwd);

            Result = account;
            DialogResult = true;
            Close();
        }
        finally
        {
            SaveBtn.IsEnabled = true;
            TestBtn.IsEnabled = true;
        }
    }
}
