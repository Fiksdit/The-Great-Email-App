// FILE: src/GreatEmailApp.Core/Models/Account.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public enum AccountStatus
{
    Connected,
    Syncing,
    Error,
    Offline
}

public enum MailEncryption
{
    None,
    SslTls,
    StartTls
}

public sealed class Account
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string EmailAddress { get; init; }
    public string Initials { get; set; } = "";
    public string Color { get; set; } = "#3A6FF8";
    public AccountStatus Status { get; set; } = AccountStatus.Offline;
    public bool IsPrimary { get; set; }

    // IMAP
    public string ImapHost { get; set; } = "";
    public int ImapPort { get; set; } = 993;
    public MailEncryption ImapEncryption { get; set; } = MailEncryption.SslTls;

    // SMTP
    public string SmtpHost { get; set; } = "";
    public int SmtpPort { get; set; } = 587;
    public MailEncryption SmtpEncryption { get; set; } = MailEncryption.StartTls;

    // Auth
    public string Username { get; set; } = "";
    // NOTE: password never lives on this model — stored in Windows Credential Manager
    // keyed by Account.Id. See §7 Auth Flow.

    public List<Folder> Folders { get; set; } = new();

    // Sync
    public bool SyncSettings { get; set; } = true;
}
