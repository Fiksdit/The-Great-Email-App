// FILE: src/GreatEmailApp/ViewModels/AddAccountViewModel.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.ViewModels;

public enum TestStatus { Idle, Testing, Ok, Failed }

public partial class AddAccountViewModel : ObservableObject
{
    private readonly IImapService _imap;

    [ObservableProperty] private string displayName = "";
    [ObservableProperty] private string emailAddress = "";

    [ObservableProperty] private string imapHost = "";
    [ObservableProperty] private int imapPort = 993;
    [ObservableProperty] private MailEncryption imapEncryption = MailEncryption.SslTls;

    [ObservableProperty] private string smtpHost = "";
    [ObservableProperty] private int smtpPort = 465;
    [ObservableProperty] private MailEncryption smtpEncryption = MailEncryption.SslTls;

    [ObservableProperty] private string username = "";
    // NOTE: password is NOT stored on this VM beyond the dialog's lifetime.
    // The dialog reads it from a PasswordBox at save-time and hands it to the
    // credential store, then clears it. See rulebook §7.

    [ObservableProperty] private bool syncSettings = true;
    [ObservableProperty] private TestStatus testStatus = TestStatus.Idle;
    [ObservableProperty] private string testMessage = "";

    // NOTE: track which fields the user has manually edited so we can keep
    // refreshing the auto-fill from the email address as they type, but stop
    // overwriting once they've taken control of a field.
    private bool _autoFilling;
    private bool _userEditedDisplayName;
    private bool _userEditedUsername;
    private bool _userEditedImapHost;
    private bool _userEditedSmtpHost;

    public AddAccountViewModel(IImapService imap)
    {
        _imap = imap;
    }

    /// <summary>Auto-populate IMAP/SMTP/username/display name from the email
    /// domain on every keystroke — until the user manually edits a field.</summary>
    partial void OnEmailAddressChanged(string value)
    {
        var at = value.IndexOf('@');
        if (at <= 0 || at == value.Length - 1) return;
        var domain = value[(at + 1)..].Trim();
        if (string.IsNullOrEmpty(domain)) return;

        _autoFilling = true;
        try
        {
            if (!_userEditedUsername)    Username = value;
            if (!_userEditedImapHost)    ImapHost = $"imap.{domain}";
            if (!_userEditedSmtpHost)    SmtpHost = $"smtp.{domain}";
            if (!_userEditedDisplayName) DisplayName = char.ToUpper(value[0]) + value[1..at];
        }
        finally { _autoFilling = false; }
    }

    partial void OnDisplayNameChanged(string value) { if (!_autoFilling) _userEditedDisplayName = true; }
    partial void OnUsernameChanged(string value)    { if (!_autoFilling) _userEditedUsername = true; }
    partial void OnImapHostChanged(string value)    { if (!_autoFilling) _userEditedImapHost = true; }
    partial void OnSmtpHostChanged(string value)    { if (!_autoFilling) _userEditedSmtpHost = true; }

    public async Task<Result<bool>> TestAsync(string password, CancellationToken ct = default)
    {
        TestStatus = TestStatus.Testing;
        TestMessage = "Connecting…";

        var account = ToAccount();
        var result = await _imap.TestConnectionAsync(account, password, ct);

        if (result is Result<bool>.Ok)
        {
            TestStatus = TestStatus.Ok;
            TestMessage = "Connection OK";
        }
        else if (result is Result<bool>.Fail f)
        {
            TestStatus = TestStatus.Failed;
            TestMessage = f.Error;
        }
        return result;
    }

    public Account ToAccount()
    {
        var initials = MakeInitials(string.IsNullOrWhiteSpace(DisplayName) ? EmailAddress : DisplayName);
        return new Account
        {
            Id = string.IsNullOrEmpty(EmailAddress) ? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? EmailAddress : DisplayName,
            EmailAddress = EmailAddress,
            Initials = initials,
            Color = "#3A6FF8",
            Status = AccountStatus.Offline,
            ImapHost = ImapHost,
            ImapPort = ImapPort,
            ImapEncryption = ImapEncryption,
            SmtpHost = SmtpHost,
            SmtpPort = SmtpPort,
            SmtpEncryption = SmtpEncryption,
            Username = string.IsNullOrWhiteSpace(Username) ? EmailAddress : Username,
            SyncSettings = SyncSettings,
        };
    }

    private static string MakeInitials(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "?";
        var parts = s.Split(new[] { ' ', '.', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();
        return (parts[0][0].ToString() + parts[1][0]).ToUpperInvariant();
    }
}
