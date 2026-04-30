// FILE: src/GreatEmailApp.Core/Auth/IAuthService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Auth;

/// <summary>
/// Firebase Auth surface for The Great Email App. Per rulebook §7A:
/// Google sign-in only (v1), refresh token DPAPI-encrypted in auth.dat.
/// All methods follow §9: never throw across the service boundary —
/// return <see cref="Result{T}"/> instead.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Currently active session, or null if signed out.
    /// Updated on sign-in, sign-out, and silent restore at app start.
    /// </summary>
    AuthSession? Current { get; }

    bool IsSignedIn { get; }

    /// <summary>Fired whenever <see cref="Current"/> changes (sign-in, sign-out, silent restore).</summary>
    event EventHandler? SessionChanged;

    /// <summary>
    /// On app start, attempt to restore a previous sign-in by reading the
    /// DPAPI-encrypted refresh token and trading it for a fresh id token.
    /// Returns Ok(null) if nothing to restore — that's a valid state, not an error.
    /// </summary>
    Task<Result<AuthSession?>> TryRestoreAsync(CancellationToken ct = default);

    /// <summary>
    /// Open the system browser, run the Google OAuth desktop loopback flow,
    /// exchange the Google id token for a Firebase id token, and persist the
    /// refresh token. On success, <see cref="Current"/> is set and
    /// <see cref="SessionChanged"/> fires.
    /// </summary>
    Task<Result<AuthSession>> SignInWithGoogleAsync(CancellationToken ct = default);

    /// <summary>
    /// Wipe the local auth.dat and clear <see cref="Current"/>. Does not
    /// revoke the refresh token server-side (could be added later — low value).
    /// </summary>
    Task<Result<bool>> SignOutAsync(CancellationToken ct = default);

    /// <summary>
    /// Returns a non-stale id token, refreshing transparently if needed.
    /// Fail when there's no session at all, or when the refresh request fails
    /// (e.g. token revoked at the Google account level).
    /// </summary>
    Task<Result<string>> GetValidIdTokenAsync(CancellationToken ct = default);
}
