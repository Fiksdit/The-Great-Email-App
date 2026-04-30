// FILE: src/GreatEmailApp.Core/Services/IFirebaseAuthService.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IFirebaseAuthService
{
    FirebaseUser? CurrentUser { get; }
    event EventHandler<FirebaseUser?> UserChanged;

    /// <summary>Runs the Google OAuth2 loopback flow, then exchanges for a Firebase token.</summary>
    Task<FirebaseUser?> SignInWithGoogleAsync(CancellationToken ct = default);

    /// <summary>Uses the stored refresh token to silently obtain a fresh IdToken.
    /// Returns false if the refresh fails (network error, revoked token, etc.).</summary>
    Task<bool> RefreshIfNeededAsync(CancellationToken ct = default);

    void SignOut();
}
