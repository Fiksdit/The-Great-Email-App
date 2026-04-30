// FILE: src/GreatEmailApp.Core/Services/IFirestoreSyncService.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IFirestoreSyncService
{
    /// <summary>Write settings + account configs (no passwords) to Firestore.</summary>
    Task PushAsync(AppSettings settings, IReadOnlyList<Account> accounts, CancellationToken ct = default);

    /// <summary>Read settings + account configs from Firestore.
    /// Returns (null, null) when the document doesn't exist yet or the call fails.</summary>
    Task<(AppSettings? Settings, List<Account>? Accounts)> PullAsync(CancellationToken ct = default);
}
