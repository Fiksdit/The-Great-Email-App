// FILE: src/GreatEmailApp.Core/Services/SmtpService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace GreatEmailApp.Core.Services;

public sealed class SmtpService : ISmtpService
{
    public async Task<Result<bool>> SendAsync(
        Account account, string password, MimeMessage message,
        CancellationToken ct = default)
    {
        try
        {
            using var smtp = new SmtpClient { Timeout = 30_000 };
            var sec = MapEncryption(account.SmtpEncryption);
            await smtp.ConnectAsync(account.SmtpHost, account.SmtpPort, sec, ct).ConfigureAwait(false);

            // Most servers want the bare mailbox name (the IMAP convention) — try
            // explicit Username first, fall back to the email address.
            var user = string.IsNullOrWhiteSpace(account.Username) ? account.EmailAddress : account.Username;
            await smtp.AuthenticateAsync(user, password, ct).ConfigureAwait(false);

            await smtp.SendAsync(message, ct).ConfigureAwait(false);
            await smtp.DisconnectAsync(true, ct).ConfigureAwait(false);
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>(Sanitize(ex.Message), ex);
        }
    }

    private static SecureSocketOptions MapEncryption(MailEncryption e) => e switch
    {
        MailEncryption.SslTls   => SecureSocketOptions.SslOnConnect,
        MailEncryption.StartTls => SecureSocketOptions.StartTls,
        _                       => SecureSocketOptions.None,
    };

    /// <summary>Strip auth headers / passwords that some servers reflect back in error text.</summary>
    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        var lower = s.ToLowerInvariant();
        if (lower.Contains("password") || lower.Contains("auth"))
        {
            // Coarse filter — keep the first sentence, drop anything after a colon
            // that might echo credentials.
            var colon = s.IndexOf(':');
            if (colon > 0 && colon < 80) return s[..colon].Trim();
        }
        return s;
    }
}
