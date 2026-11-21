// FILE: src/GreatEmailApp/Views/Dialogs/EnterPasswordDialog.xaml.cs
// Created: 2026-11-21 | Revised: 2026-11-21 | Rev: 1
// Changed by: Claude Opus 5.5 on behalf of James Reed

using System;
using System.Threading;
using System.Windows;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Views.Dialogs;

/// <summary>
/// Prompts for an existing account's IMAP password, verifies it with a test
/// login, and saves it to Credential Manager (rulebook §7 — the only place
/// passwords live). DialogResult is true only after a successful save.
/// </summary>
public partial class EnterPasswordDialog : Window
{
    private readonly Account _account;
    private readonly IImapService _imap;
    private readonly ICredentialStore _creds;
    private CancellationTokenSource? _cts;

    public EnterPasswordDialog(Account account, IImapService imap, ICredentialStore creds)
    {
        InitializeComponent();
        _account = account;
        _imap = imap;
        _creds = creds;
        SubText.Text =
            $"This PC doesn't have the password for {account.EmailAddress} yet. " +
            "If you use password sync, cancel and unlock it in Settings → Sync instead — " +
            "that restores every account at once. Otherwise, enter it here.";
        Loaded += (_, _) => Pwd.Focus();
        Closed += (_, _) => _cts?.Cancel();
    }

    private void Pwd_Changed(object sender, RoutedEventArgs e) => SetStatus("", isError: false);

    private async void Ok_Click(object sender, RoutedEventArgs e)
    {
        var password = Pwd.Password;
        if (string.IsNullOrEmpty(password))
        {
            SetStatus("Password is required.", isError: true);
            return;
        }

        SetBusy(true);
        SetStatus($"Connecting to {_account.ImapHost}…", isError: false);
        _cts = new CancellationTokenSource();
        try
        {
            var result = await _imap.TestConnectionAsync(_account, password, _cts.Token);
            if (result is Result<bool>.Fail f)
            {
                SetStatus($"Couldn't sign in: {f.Error}", isError: true);
                return;
            }

            var username = string.IsNullOrWhiteSpace(_account.Username) ? _account.EmailAddress : _account.Username;
            _creds.Save(_account.Id, username, password);
            DialogResult = true;
            Close();
        }
        catch (OperationCanceledException) { /* dialog closed mid-test */ }
        catch (Exception ex)
        {
            SetStatus($"Couldn't sign in: {ex.Message}", isError: true);
        }
        finally
        {
            if (IsLoaded) SetBusy(false);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void SetBusy(bool busy)
    {
        OkButton.IsEnabled = !busy;
        Pwd.IsEnabled = !busy;
    }

    private void SetStatus(string text, bool isError)
    {
        StatusText.Text = text;
        StatusText.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty,
            isError ? "RoseBrush" : "SecondaryTextBrush");
    }
}
