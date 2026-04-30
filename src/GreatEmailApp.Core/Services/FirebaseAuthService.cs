// FILE: src/GreatEmailApp.Core/Services/FirebaseAuthService.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed
//
// Sign-in flow:
//   1. GoogleWebAuthorizationBroker opens the system browser (loopback redirect).
//   2. User grants permission; Google returns an authorization code.
//   3. The library exchanges it for a Google ID token (JWT).
//   4. We POST the Google ID token to Firebase Auth REST API to get a Firebase session.
//   5. Firebase returns an idToken (1 h), refreshToken (long-lived), uid, email.
//   6. We store only the refreshToken + metadata in token.json.
//
// Subsequent launches:
//   1. TokenStore.Load() restores uid/email/refreshToken (no IdToken).
//   2. RefreshIfNeededAsync() calls the token endpoint with the refreshToken.
//   3. We get a fresh IdToken and can call Firestore.

using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public sealed class FirebaseAuthService : IFirebaseAuthService
{
    private static readonly string[] GoogleScopes = { "openid", "email", "profile" };

    private readonly TokenStore _tokenStore;
    private readonly HttpClient _http = new();

    private FirebaseUser? _current;

    public FirebaseUser? CurrentUser => _current;
    public event EventHandler<FirebaseUser?> UserChanged = delegate { };

    public FirebaseAuthService(TokenStore tokenStore)
    {
        _tokenStore = tokenStore;
        _current = tokenStore.Load();
    }

    public async Task<FirebaseUser?> SignInWithGoogleAsync(CancellationToken ct = default)
    {
        if (!FirebaseConfig.IsConfigured)
            throw new InvalidOperationException(
                "Firebase is not configured. Fill in FirebaseConfig.cs with your project credentials.");

        var secrets = new ClientSecrets
        {
            ClientId     = FirebaseConfig.GoogleClientId,
            ClientSecret = FirebaseConfig.GoogleClientSecret,
        };

        // MemoryDataStore → no file caching; we manage the Firebase token ourselves.
        var googleCred = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets, GoogleScopes, "user", ct, new MemoryDataStore());

        // Force a token refresh to populate IdToken (it may be absent on first auth).
        await googleCred.RefreshTokenAsync(ct);

        var googleIdToken = googleCred.Token.IdToken
            ?? throw new InvalidOperationException("Google did not return an ID token.");

        var user = await ExchangeGoogleTokenAsync(googleIdToken, ct);
        Commit(user);
        return user;
    }

    public async Task<bool> RefreshIfNeededAsync(CancellationToken ct = default)
    {
        if (_current is null) return false;
        if (!_current.IsExpired) return true;

        if (string.IsNullOrEmpty(_current.RefreshToken)) return false;

        try
        {
            var payload = new
            {
                grant_type    = "refresh_token",
                refresh_token = _current.RefreshToken,
            };
            var url = $"https://securetoken.googleapis.com/v1/token?key={FirebaseConfig.ApiKey}";
            var resp = await _http.PostAsJsonAsync(url, payload, ct);
            if (!resp.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;

            _current.IdToken      = root.GetProperty("id_token").GetString()      ?? "";
            _current.RefreshToken = root.GetProperty("refresh_token").GetString() ?? _current.RefreshToken;
            _current.ExpiresAt    = DateTimeOffset.UtcNow.AddSeconds(
                long.TryParse(root.GetProperty("expires_in").GetString(), out var exp) ? exp : 3600);

            _tokenStore.Save(_current);
            return true;
        }
        catch { return false; }
    }

    public void SignOut()
    {
        _tokenStore.Delete();
        Commit(null);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<FirebaseUser> ExchangeGoogleTokenAsync(string googleIdToken, CancellationToken ct)
    {
        var payload = new
        {
            postBody             = $"id_token={googleIdToken}&providerId=google.com",
            requestUri           = "http://localhost",
            returnIdpCredential  = true,
            returnSecureToken    = true,
        };
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={FirebaseConfig.ApiKey}";
        var resp = await _http.PostAsJsonAsync(url, payload, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;

        return new FirebaseUser
        {
            Uid          = root.GetProperty("localId").GetString()          ?? "",
            Email        = root.GetProperty("email").GetString()            ?? "",
            DisplayName  = root.TryGetProperty("displayName", out var dn)
                               ? dn.GetString() ?? "" : "",
            IdToken      = root.GetProperty("idToken").GetString()          ?? "",
            RefreshToken = root.GetProperty("refreshToken").GetString()     ?? "",
            ExpiresAt    = DateTimeOffset.UtcNow.AddSeconds(
                int.TryParse(root.GetProperty("expiresIn").GetString(), out var exp) ? exp : 3600),
        };
    }

    private void Commit(FirebaseUser? user)
    {
        _current = user;
        if (user is not null) _tokenStore.Save(user);
        UserChanged.Invoke(this, user);
    }
}
