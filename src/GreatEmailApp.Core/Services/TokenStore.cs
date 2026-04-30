// FILE: src/GreatEmailApp.Core/Services/TokenStore.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed
// Persists the Firebase refresh token + user metadata to token.json.
// The short-lived IdToken is never stored; it is always obtained via refresh.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class TokenStore
{
    private record TokenDto(string Uid, string Email, string DisplayName, string RefreshToken);

    public FirebaseUser? Load()
    {
        var path = AppPaths.TokenJson;
        if (!File.Exists(path)) return null;
        try
        {
            var dto = JsonSerializer.Deserialize<TokenDto>(File.ReadAllText(path));
            if (dto is null || string.IsNullOrEmpty(dto.RefreshToken)) return null;
            return new FirebaseUser
            {
                Uid          = dto.Uid,
                Email        = dto.Email,
                DisplayName  = dto.DisplayName,
                RefreshToken = dto.RefreshToken,
                // IdToken is blank — the caller must RefreshIfNeededAsync before use.
                ExpiresAt    = DateTimeOffset.MinValue,
            };
        }
        catch { return null; }
    }

    public void Save(FirebaseUser user)
    {
        var dto = new TokenDto(user.Uid, user.Email, user.DisplayName, user.RefreshToken);
        File.WriteAllText(AppPaths.TokenJson,
            JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true }));
    }

    public void Delete()
    {
        if (File.Exists(AppPaths.TokenJson))
            File.Delete(AppPaths.TokenJson);
    }
}
