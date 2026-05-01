// FILE: src/GreatEmailApp.Core/Search/IMessageCache.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Search;

public sealed record SearchHit(
    string AccountId,
    string AccountEmail,
    string FolderPath,
    uint Uid,
    string Sender,
    string SenderEmail,
    string Subject,
    string Snippet,
    DateTimeOffset? SentAt);

public interface IMessageCache
{
    /// <summary>Create the schema if it doesn't exist. Idempotent. Call once at startup.</summary>
    Task InitAsync(CancellationToken ct = default);

    /// <summary>
    /// Insert or update message envelopes in bulk. Body fields are NOT touched
    /// here — UpsertBodyAsync writes those when a message is opened.
    /// </summary>
    Task<Result<bool>> UpsertEnvelopesAsync(string accountId, string accountEmail, string folderPath,
        IEnumerable<Message> messages, CancellationToken ct = default);

    /// <summary>Persist a message body so future searches can find on body text.</summary>
    Task<Result<bool>> UpsertBodyAsync(string accountId, string folderPath, uint uid,
        string bodyPlain, CancellationToken ct = default);

    /// <summary>Run a full-text search across cached messages. Returns ranked hits.</summary>
    Task<Result<List<SearchHit>>> SearchAsync(string query, int limit = 30, CancellationToken ct = default);
}
