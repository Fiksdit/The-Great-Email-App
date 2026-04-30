// FILE: src/GreatEmailApp.Core/Services/IImapService.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

/// <summary>Live IMAP/SMTP operations. All async, all returning Result&lt;T&gt;.</summary>
public interface IImapService
{
    /// <summary>Open a connection and authenticate. Used by Test Connection in Add Account dialog.</summary>
    Task<Result<bool>> TestConnectionAsync(Account account, string password, CancellationToken ct = default);

    /// <summary>Top-level folder list for the account, with unread/total counts.</summary>
    Task<Result<List<Folder>>> ListFoldersAsync(Account account, string password, CancellationToken ct = default);

    /// <summary>
    /// Fetch the most recent <paramref name="limit"/> messages in <paramref name="folderFullPath"/>.
    /// Headers + preview only — body comes via <see cref="FetchBodyAsync"/>.
    /// </summary>
    Task<Result<List<Message>>> ListMessagesAsync(
        Account account, string password, string folderFullPath,
        int limit = 200, CancellationToken ct = default);

    /// <summary>Fetch the full body for a single message UID.</summary>
    Task<Result<(string PlainText, string Html)>> FetchBodyAsync(
        Account account, string password, string folderFullPath, uint uid,
        CancellationToken ct = default);
}
