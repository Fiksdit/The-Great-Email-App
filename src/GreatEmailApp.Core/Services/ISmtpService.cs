// FILE: src/GreatEmailApp.Core/Services/ISmtpService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;
using MimeKit;

namespace GreatEmailApp.Core.Services;

public interface ISmtpService
{
    /// <summary>
    /// Send a fully-built MimeMessage via the account's SMTP server.
    /// Authentication uses the IMAP password from the credential store —
    /// most providers (incl. fiksdit.com / cPanel) use the same creds.
    /// </summary>
    Task<Result<bool>> SendAsync(Account account, string password, MimeMessage message,
        CancellationToken ct = default);
}
