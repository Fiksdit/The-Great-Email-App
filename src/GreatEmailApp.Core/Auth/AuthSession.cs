// FILE: src/GreatEmailApp.Core/Auth/AuthSession.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Auth;

/// <summary>
/// Snapshot of a signed-in Firebase user. The id token is short-lived (~1h);
/// callers should always go through <see cref="IAuthService.GetValidIdTokenAsync"/>
/// rather than caching this directly.
/// </summary>
public sealed record AuthSession(
    string Uid,
    string Email,
    string? DisplayName,
    string? PhotoUrl,
    string IdToken,
    DateTimeOffset IdTokenExpiresAt,
    string RefreshToken)
{
    /// <summary>Treat the id token as expired 60s before its real expiry to avoid edge races.</summary>
    public bool IsIdTokenStale => DateTimeOffset.UtcNow >= IdTokenExpiresAt.AddSeconds(-60);
}
