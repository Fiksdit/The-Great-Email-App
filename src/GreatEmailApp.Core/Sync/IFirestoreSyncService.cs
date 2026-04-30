// FILE: src/GreatEmailApp.Core/Sync/IFirestoreSyncService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Sync;

/// <summary>
/// REST-based Firestore client scoped to <c>users/{uid}/settings/profile</c>.
/// Both methods require the caller to be signed in — they ask the
/// <see cref="Auth.IAuthService"/> for a fresh id token internally.
/// Per rulebook §9: never throw, return <see cref="Result{T}"/>.
/// </summary>
public interface IFirestoreSyncService
{
    /// <summary>
    /// Pull the remote snapshot. Returns <c>Ok(null)</c> when no document exists yet
    /// (first-time sign-in on a brand new account) — that's a valid state, not an error.
    /// </summary>
    Task<Result<SyncSnapshot?>> PullAsync(CancellationToken ct = default);

    /// <summary>
    /// Upsert the remote snapshot. The provided <see cref="SyncSnapshot.UpdatedAt"/>
    /// is what gets written; callers should set it to <c>DateTimeOffset.UtcNow</c>
    /// at push time so last-write-wins works correctly.
    /// </summary>
    Task<Result<bool>> PushAsync(SyncSnapshot snapshot, CancellationToken ct = default);
}
