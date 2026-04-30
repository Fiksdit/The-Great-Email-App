// FILE: src/GreatEmailApp.Core/Models/FirebaseUser.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public sealed class FirebaseUser
{
    public string Uid          { get; set; } = "";
    public string Email        { get; set; } = "";
    public string DisplayName  { get; set; } = "";

    // Short-lived JWT (≈1 hour). Not stored on disk — refreshed from RefreshToken.
    public string IdToken      { get; set; } = "";

    // Long-lived token stored in token.json.
    public string RefreshToken { get; set; } = "";

    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.MinValue;

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt.AddMinutes(-5);
}
