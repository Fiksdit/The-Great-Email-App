// FILE: src/GreatEmailApp.Core/Auth/FirebaseAuthService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Hand-rolled OAuth desktop loopback flow + Firebase token exchange.
// Avoids dragging Google.Apis.Auth + dependencies into the desktop bundle.
//
// Flow:
//   1. Start an HttpListener on a free 127.0.0.1 port.
//   2. Open the system browser to Google's auth endpoint with PKCE.
//   3. Wait for the redirect with ?code=...&state=...
//   4. Exchange the code for a Google id token (oauth2.googleapis.com/token).
//   5. Exchange the Google id token for a Firebase id+refresh token
//      (identitytoolkit.googleapis.com/v1/accounts:signInWithIdp).
//   6. Persist the refresh token via DPAPI; cache the id token in memory.
//   7. Refresh on demand via securetoken.googleapis.com/v1/token.

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Auth;

public sealed class FirebaseAuthService : IAuthService
{
    private readonly HttpClient _http;
    private readonly ITokenVault _vault;
    private readonly FirebaseOptions _firebase;
    private readonly GoogleOAuthOptions _oauth;
    private readonly object _gate = new();

    private AuthSession? _current;

    public FirebaseAuthService(AppConfig config, ITokenVault vault, HttpClient? http = null)
    {
        _firebase = config.Firebase;
        _oauth = config.GoogleOAuth;
        _vault = vault;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public AuthSession? Current { get { lock (_gate) return _current; } }
    public bool IsSignedIn => Current is not null;
    public event EventHandler? SessionChanged;

    private void SetSession(AuthSession? s)
    {
        lock (_gate) _current = s;
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }

    // --------------------------------------------------------------------- //
    // Public surface
    // --------------------------------------------------------------------- //

    public async Task<Result<AuthSession?>> TryRestoreAsync(CancellationToken ct = default)
    {
        var refresh = _vault.Read();
        if (string.IsNullOrEmpty(refresh)) return Result.Ok<AuthSession?>(null);

        var refreshed = await ExchangeRefreshTokenAsync(refresh, ct).ConfigureAwait(false);
        if (refreshed is Result<RefreshResponse>.Fail f)
        {
            _vault.Clear();
            return Result.Fail<AuthSession?>($"Restore failed: {f.Error}");
        }
        var ok = (Result<RefreshResponse>.Ok)refreshed;

        // We don't have email/uid from the refresh endpoint alone — call accounts:lookup.
        var lookup = await LookupAccountAsync(ok.Value.IdToken, ct).ConfigureAwait(false);
        if (lookup is Result<AccountInfo>.Fail lf)
        {
            _vault.Clear();
            return Result.Fail<AuthSession?>($"Account lookup failed: {lf.Error}");
        }
        var info = ((Result<AccountInfo>.Ok)lookup).Value;

        var session = new AuthSession(
            info.LocalId, info.Email, info.DisplayName, info.PhotoUrl,
            ok.Value.IdToken,
            DateTimeOffset.UtcNow.AddSeconds(ok.Value.ExpiresIn),
            ok.Value.RefreshToken);

        _vault.Write(session.RefreshToken);
        SetSession(session);
        return Result.Ok<AuthSession?>(session);
    }

    public async Task<Result<AuthSession>> SignInWithGoogleAsync(CancellationToken ct = default)
    {
        try
        {
            // 1. PKCE
            var (verifier, challenge) = GeneratePkce();
            var state = RandomToken(16);

            // 2. Loopback listener
            int port = GetFreeLoopbackPort();
            var redirectUri = $"http://127.0.0.1:{port}/";
            using var listener = new HttpListener();
            listener.Prefixes.Add(redirectUri);
            listener.Start();

            // 3. Open browser
            var authUrl =
                "https://accounts.google.com/o/oauth2/v2/auth" +
                $"?client_id={Uri.EscapeDataString(_oauth.DesktopClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                "&response_type=code" +
                "&scope=" + Uri.EscapeDataString("openid email profile") +
                $"&code_challenge={Uri.EscapeDataString(challenge)}" +
                "&code_challenge_method=S256" +
                $"&state={Uri.EscapeDataString(state)}" +
                "&access_type=offline" +
                "&prompt=consent";
            OpenBrowser(authUrl);

            // 4. Wait for redirect (cancellable, with a hard cap)
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var contextTask = listener.GetContextAsync();
            var done = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, linked.Token))
                .ConfigureAwait(false);
            if (done != contextTask)
            {
                listener.Stop();
                return Result.Fail<AuthSession>("Sign-in cancelled or timed out.");
            }
            var context = await contextTask.ConfigureAwait(false);

            var query = context.Request.QueryString;
            var code = query["code"];
            var returnedState = query["state"];
            var error = query["error"];

            // 4a. Validate redirect params first; bail before holding the browser open
            // for a token exchange we'd never make.
            if (error is not null)
            {
                await WriteBrowserPageAsync(context, success: false, $"OAuth error: {error}", ct).ConfigureAwait(false);
                listener.Stop();
                return Result.Fail<AuthSession>($"OAuth error: {error}");
            }
            if (code is null)
            {
                await WriteBrowserPageAsync(context, success: false, "Missing code in OAuth response.", ct).ConfigureAwait(false);
                listener.Stop();
                return Result.Fail<AuthSession>("OAuth response missing code.");
            }
            if (returnedState != state)
            {
                await WriteBrowserPageAsync(context, success: false, "State mismatch (possible CSRF).", ct).ConfigureAwait(false);
                listener.Stop();
                return Result.Fail<AuthSession>("OAuth state mismatch (possible CSRF).");
            }

            // 5. Exchange code → Google id token. Do this BEFORE writing the browser
            // response so the success page only appears when sign-in actually worked.
            var googleResult = await ExchangeAuthCodeAsync(code, verifier, redirectUri, ct).ConfigureAwait(false);
            if (googleResult is Result<GoogleTokenResponse>.Fail gf)
            {
                await WriteBrowserPageAsync(context, success: false, gf.Error, ct).ConfigureAwait(false);
                listener.Stop();
                return Result.Fail<AuthSession>(gf.Error);
            }
            var googleToken = ((Result<GoogleTokenResponse>.Ok)googleResult).Value.IdToken;

            // 6. Exchange Google id token → Firebase id+refresh
            var fbResult = await SignInWithIdpAsync(googleToken, ct).ConfigureAwait(false);
            if (fbResult is Result<FirebaseSignInResponse>.Fail ff)
            {
                await WriteBrowserPageAsync(context, success: false, ff.Error, ct).ConfigureAwait(false);
                listener.Stop();
                return Result.Fail<AuthSession>(ff.Error);
            }
            var fb = ((Result<FirebaseSignInResponse>.Ok)fbResult).Value;

            // Both exchanges succeeded — show the success page and close the listener.
            await WriteBrowserPageAsync(context, success: true, null, ct).ConfigureAwait(false);
            listener.Stop();

            var session = new AuthSession(
                fb.LocalId, fb.Email, fb.DisplayName, fb.PhotoUrl,
                fb.IdToken,
                DateTimeOffset.UtcNow.AddSeconds(int.TryParse(fb.ExpiresIn, out var s) ? s : 3600),
                fb.RefreshToken);

            _vault.Write(session.RefreshToken);
            SetSession(session);
            return Result.Ok(session);
        }
        catch (Exception ex)
        {
            return Result.Fail<AuthSession>($"Sign-in failed: {ex.Message}", ex);
        }
    }

    public Task<Result<bool>> SignOutAsync(CancellationToken ct = default)
    {
        _vault.Clear();
        SetSession(null);
        return Task.FromResult<Result<bool>>(Result.Ok(true));
    }

    public async Task<Result<string>> GetValidIdTokenAsync(CancellationToken ct = default)
    {
        var current = Current;
        if (current is null) return Result.Fail<string>("Not signed in.");
        if (!current.IsIdTokenStale) return Result.Ok(current.IdToken);

        var refreshed = await ExchangeRefreshTokenAsync(current.RefreshToken, ct).ConfigureAwait(false);
        if (refreshed is Result<RefreshResponse>.Fail f)
        {
            // Refresh failure = token revoked or network. Sign the user out so the
            // UI prompts re-auth instead of silently looping.
            await SignOutAsync(ct).ConfigureAwait(false);
            return Result.Fail<string>(f.Error);
        }
        var r = ((Result<RefreshResponse>.Ok)refreshed).Value;
        var updated = current with
        {
            IdToken = r.IdToken,
            IdTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(r.ExpiresIn),
            RefreshToken = r.RefreshToken,
        };
        _vault.Write(updated.RefreshToken);
        SetSession(updated);
        return Result.Ok(updated.IdToken);
    }

    // --------------------------------------------------------------------- //
    // HTTP helpers
    // --------------------------------------------------------------------- //

    private async Task<Result<GoogleTokenResponse>> ExchangeAuthCodeAsync(
        string code, string verifier, string redirectUri, CancellationToken ct)
    {
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("client_id", _oauth.DesktopClientId),
            new KeyValuePair<string, string>("client_secret", _oauth.DesktopClientSecret),
            new KeyValuePair<string, string>("code", code),
            new KeyValuePair<string, string>("code_verifier", verifier),
            new KeyValuePair<string, string>("grant_type", "authorization_code"),
            new KeyValuePair<string, string>("redirect_uri", redirectUri),
        });
        var resp = await _http.PostAsync("https://oauth2.googleapis.com/token", form, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Result.Fail<GoogleTokenResponse>($"Google token exchange failed ({(int)resp.StatusCode}): {body}");
        }
        var parsed = await resp.Content.ReadFromJsonAsync<GoogleTokenResponse>(JsonOpts, ct).ConfigureAwait(false);
        if (parsed is null || string.IsNullOrEmpty(parsed.IdToken))
            return Result.Fail<GoogleTokenResponse>("Google token response missing id_token.");
        return Result.Ok(parsed);
    }

    private async Task<Result<FirebaseSignInResponse>> SignInWithIdpAsync(string googleIdToken, CancellationToken ct)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={_firebase.ApiKey}";
        var payload = new
        {
            postBody = $"id_token={Uri.EscapeDataString(googleIdToken)}&providerId=google.com",
            requestUri = "http://localhost",
            returnIdpCredential = true,
            returnSecureToken = true,
        };
        var resp = await _http.PostAsJsonAsync(url, payload, JsonOpts, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Result.Fail<FirebaseSignInResponse>($"signInWithIdp failed ({(int)resp.StatusCode}): {body}");
        }
        var parsed = await resp.Content.ReadFromJsonAsync<FirebaseSignInResponse>(JsonOpts, ct).ConfigureAwait(false);
        if (parsed is null || string.IsNullOrEmpty(parsed.IdToken))
            return Result.Fail<FirebaseSignInResponse>("signInWithIdp response malformed.");
        return Result.Ok(parsed);
    }

    private async Task<Result<RefreshResponse>> ExchangeRefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        var url = $"https://securetoken.googleapis.com/v1/token?key={_firebase.ApiKey}";
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("grant_type", "refresh_token"),
            new KeyValuePair<string, string>("refresh_token", refreshToken),
        });
        var resp = await _http.PostAsync(url, form, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Result.Fail<RefreshResponse>($"Token refresh failed ({(int)resp.StatusCode}): {body}");
        }
        var parsed = await resp.Content.ReadFromJsonAsync<RefreshResponse>(JsonOpts, ct).ConfigureAwait(false);
        if (parsed is null || string.IsNullOrEmpty(parsed.IdToken))
            return Result.Fail<RefreshResponse>("Refresh response malformed.");
        return Result.Ok(parsed);
    }

    private async Task<Result<AccountInfo>> LookupAccountAsync(string idToken, CancellationToken ct)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:lookup?key={_firebase.ApiKey}";
        var resp = await _http.PostAsJsonAsync(url, new { idToken }, JsonOpts, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            return Result.Fail<AccountInfo>($"accounts:lookup failed ({(int)resp.StatusCode}): {body}");
        }
        var parsed = await resp.Content.ReadFromJsonAsync<AccountLookupResponse>(JsonOpts, ct).ConfigureAwait(false);
        var first = parsed?.Users is { Count: > 0 } ? parsed.Users[0] : null;
        if (first is null) return Result.Fail<AccountInfo>("accounts:lookup returned no users.");
        return Result.Ok(first);
    }

    // --------------------------------------------------------------------- //
    // Utilities
    // --------------------------------------------------------------------- //

    private static int GetFreeLoopbackPort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        try { return ((IPEndPoint)l.LocalEndpoint).Port; }
        finally { l.Stop(); }
    }

    private static (string verifier, string challenge) GeneratePkce()
    {
        var verifier = RandomToken(64);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.ASCII.GetBytes(verifier));
        var challenge = Base64Url(hash);
        return (verifier, challenge);
    }

    private static string RandomToken(int bytes)
    {
        var buf = new byte[bytes];
        RandomNumberGenerator.Fill(buf);
        return Base64Url(buf);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static async Task WriteBrowserPageAsync(
        HttpListenerContext context, bool success, string? errorDetail, CancellationToken ct)
    {
        // Inline, no external assets. Matches the app's dark theme + accent.
        // window.close() is best-effort — most browsers allow it for tabs the app opened.
        var (title, headline, subline) = success
            ? ("Signed in", "You're signed in",
               "All set. You can close this tab and return to The Great Email App.")
            : ("Sign-in failed", "Sign-in failed",
               WebUtility.HtmlEncode(errorDetail ?? "Unknown error"));

        var html = $@"<!doctype html>
<html lang=""en""><head><meta charset=""utf-8"">
<title>{title} · The Great Email App</title>
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<style>
  :root {{ color-scheme: dark; }}
  html, body {{ height: 100%; margin: 0; }}
  body {{
    background: #1f1f1f;
    color: #f0f0f0;
    font: 14px/1.5 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
    display: grid; place-items: center; padding: 32px;
  }}
  .card {{
    width: 100%; max-width: 420px;
    background: #2a2a2a; border: 1px solid #3a3a3a; border-radius: 12px;
    padding: 32px 28px; text-align: center;
    box-shadow: 0 8px 28px rgba(0,0,0,.35);
  }}
  .badge {{
    width: 56px; height: 56px; margin: 0 auto 18px;
    border-radius: 14px; display: grid; place-items: center;
    background: {(success ? "#1F3A6FF8" : "#3A1F26")};
    color: {(success ? "#5A8BFF" : "#F36A8E")};
    font-size: 28px; font-weight: 600;
  }}
  h1 {{ margin: 0 0 8px; font-size: 20px; font-weight: 600; }}
  p  {{ margin: 0; color: #b6b6b6; }}
  .small {{ margin-top: 18px; font-size: 12px; color: #8a8a8a; }}
</style>
</head>
<body>
  <main class=""card"" role=""status"">
    <div class=""badge"" aria-hidden=""true"">{(success ? "✓" : "!")}</div>
    <h1>{headline}</h1>
    <p>{subline}</p>
    <p class=""small"">This window will close automatically.</p>
  </main>
  <script>setTimeout(function(){{ try{{window.close();}}catch(e){{}} }}, 1500);</script>
</body></html>";

        var bytes = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = success ? 200 : 400;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, ct).ConfigureAwait(false);
        context.Response.OutputStream.Close();
    }

    private static void OpenBrowser(string url)
    {
        // ProcessStartInfo with UseShellExecute=true → default browser handles https://
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true,
        });
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        // securetoken.googleapis.com returns expires_in as a quoted string; oauth2.googleapis.com
        // returns it as a number. AllowReadingFromString lets a single int field handle both.
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };

    // --------------------------------------------------------------------- //
    // Response DTOs (snake_case from Google, mixed at Firebase)
    // --------------------------------------------------------------------- //

    private sealed record GoogleTokenResponse(
        [property: JsonPropertyName("id_token")]     string IdToken,
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("expires_in")]   int ExpiresIn,
        [property: JsonPropertyName("scope")]        string? Scope,
        [property: JsonPropertyName("token_type")]   string? TokenType);

    private sealed record FirebaseSignInResponse(
        [property: JsonPropertyName("localId")]     string LocalId,
        [property: JsonPropertyName("email")]       string Email,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("photoUrl")]    string? PhotoUrl,
        [property: JsonPropertyName("idToken")]     string IdToken,
        [property: JsonPropertyName("refreshToken")] string RefreshToken,
        [property: JsonPropertyName("expiresIn")]   string ExpiresIn);

    private sealed record RefreshResponse(
        [property: JsonPropertyName("id_token")]      string IdToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")]    int ExpiresIn,
        [property: JsonPropertyName("user_id")]       string? UserId,
        [property: JsonPropertyName("token_type")]    string? TokenType);

    private sealed record AccountInfo(
        [property: JsonPropertyName("localId")]     string LocalId,
        [property: JsonPropertyName("email")]       string Email,
        [property: JsonPropertyName("displayName")] string? DisplayName,
        [property: JsonPropertyName("photoUrl")]    string? PhotoUrl);

    private sealed record AccountLookupResponse(
        [property: JsonPropertyName("users")] List<AccountInfo>? Users);
}
