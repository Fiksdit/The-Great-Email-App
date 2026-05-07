// FILE: src/GreatEmailApp.Core/Sync/FirestoreVaultSync.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// REST-based Firestore client for the password sync vault. Mirrors the pattern
// in FirestoreSyncService (settings/profile doc) but lives at a separate path
// so the regular settings sync never reads/writes vault fields.
//
// Document path:
//   projects/{projectId}/databases/(default)/documents/users/{uid}/vault/passwords
//
// Wire format (Firestore typed-value envelope):
//   {
//     "fields": {
//       "kdf_salt":          { "stringValue": "<base64>" },
//       "wrapped_data_key":  { "stringValue": "<base64>" },
//       "encrypted_passwords": { "mapValue": { "fields": {
//           "<accountId>": { "stringValue": "<base64>" },
//           ...
//       }}},
//       "updated_at":        { "timestampValue": "..." }
//     }
//   }
//
// All byte arrays travel as base64 strings — Firestore's REST API has a
// bytesValue type but base64-string is simpler, equally compact, and matches
// what we already do for settings_json in FirestoreSyncService.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Sync;

public sealed class FirestoreVaultSync : IVaultSync
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;
    private readonly FirebaseOptions _firebase;

    public FirestoreVaultSync(AppConfig config, IAuthService auth, HttpClient? http = null)
    {
        _firebase = config.Firebase;
        _auth = auth;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<Result<VaultSnapshot?>> PullAsync(CancellationToken ct = default)
    {
        var url = await BuildDocUrlAsync(ct).ConfigureAwait(false);
        if (url is Result<(string url, string token)>.Fail uf)
            return Result.Fail<VaultSnapshot?>(uf.Error);
        var (docUrl, token) = ((Result<(string url, string token)>.Ok)url).Value;

        using var req = new HttpRequestMessage(HttpMethod.Get, docUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return Result.Ok<VaultSnapshot?>(null);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Fail<VaultSnapshot?>($"Vault GET failed ({(int)resp.StatusCode}): {body}");
            }

            var doc = await resp.Content.ReadFromJsonAsync<FsDocument>(JsonOpts, ct).ConfigureAwait(false);
            if (doc?.Fields is null)
                return Result.Fail<VaultSnapshot?>("Vault document has no fields.");

            var saltB64 = doc.Fields.GetValueOrDefault("kdf_salt")?.StringValue ?? "";
            var wrappedB64 = doc.Fields.GetValueOrDefault("wrapped_data_key")?.StringValue ?? "";
            var updatedAtStr = doc.Fields.GetValueOrDefault("updated_at")?.TimestampValue;

            var passwords = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var pwField = doc.Fields.GetValueOrDefault("encrypted_passwords");
            if (pwField?.MapValue?.Fields is { } pwMap)
            {
                foreach (var kv in pwMap)
                {
                    var b64 = kv.Value?.StringValue;
                    if (string.IsNullOrEmpty(b64)) continue;
                    try { passwords[kv.Key] = Convert.FromBase64String(b64); }
                    catch (FormatException) { /* skip malformed entry — better than failing the pull */ }
                }
            }

            byte[] salt;
            byte[] wrapped;
            try
            {
                salt = string.IsNullOrEmpty(saltB64) ? Array.Empty<byte>() : Convert.FromBase64String(saltB64);
                wrapped = string.IsNullOrEmpty(wrappedB64) ? Array.Empty<byte>() : Convert.FromBase64String(wrappedB64);
            }
            catch (FormatException ex)
            {
                return Result.Fail<VaultSnapshot?>($"Vault doc has malformed base64: {ex.Message}");
            }

            if (salt.Length == 0 || wrapped.Length == 0)
                return Result.Fail<VaultSnapshot?>("Vault doc is incomplete (missing salt or wrapped key).");

            var updatedAt = DateTimeOffset.TryParse(updatedAtStr, out var dt) ? dt : DateTimeOffset.MinValue;
            return Result.Ok<VaultSnapshot?>(new VaultSnapshot(salt, wrapped, passwords, updatedAt));
        }
        catch (Exception ex)
        {
            return Result.Fail<VaultSnapshot?>($"Vault pull failed: {ex.Message}", ex);
        }
    }

    public async Task<Result<bool>> PushAsync(VaultSnapshot snapshot, CancellationToken ct = default)
    {
        var url = await BuildDocUrlAsync(ct).ConfigureAwait(false);
        if (url is Result<(string url, string token)>.Fail uf)
            return Result.Fail<bool>(uf.Error);
        var (docUrl, token) = ((Result<(string url, string token)>.Ok)url).Value;

        var pwFields = new Dictionary<string, FsValue>(StringComparer.Ordinal);
        foreach (var kv in snapshot.EncryptedPasswords)
            pwFields[kv.Key] = new FsValue { StringValue = Convert.ToBase64String(kv.Value) };

        var body = new FsDocument
        {
            Fields = new Dictionary<string, FsValue>
            {
                ["kdf_salt"]            = new() { StringValue = Convert.ToBase64String(snapshot.KdfSalt) },
                ["wrapped_data_key"]    = new() { StringValue = Convert.ToBase64String(snapshot.WrappedDataKey) },
                ["encrypted_passwords"] = new() { MapValue = new FsMap { Fields = pwFields } },
                ["updated_at"]          = new() { TimestampValue = snapshot.UpdatedAt.UtcDateTime.ToString("o") },
            },
        };

        // PATCH = upsert. updateMask explicitly lists every field to avoid clobbering
        // future fields older clients don't know about.
        var patchUrl = docUrl
            + "?updateMask.fieldPaths=kdf_salt"
            + "&updateMask.fieldPaths=wrapped_data_key"
            + "&updateMask.fieldPaths=encrypted_passwords"
            + "&updateMask.fieldPaths=updated_at";

        using var req = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
        {
            Content = JsonContent.Create(body, options: JsonOpts),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Fail<bool>($"Vault PATCH failed ({(int)resp.StatusCode}): {raw}");
            }
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"Vault push failed: {ex.Message}", ex);
        }
    }

    // --------------------------------------------------------------------- //
    // Helpers
    // --------------------------------------------------------------------- //

    private async Task<Result<(string url, string token)>> BuildDocUrlAsync(CancellationToken ct)
    {
        var session = _auth.Current;
        if (session is null)
            return Result.Fail<(string, string)>("Not signed in.");

        var tokenResult = await _auth.GetValidIdTokenAsync(ct).ConfigureAwait(false);
        if (tokenResult is Result<string>.Fail tf)
            return Result.Fail<(string, string)>(tf.Error);
        var token = ((Result<string>.Ok)tokenResult).Value;

        var url =
            $"https://firestore.googleapis.com/v1/projects/{_firebase.ProjectId}" +
            $"/databases/(default)/documents/users/{Uri.EscapeDataString(session.Uid)}/vault/passwords";
        return Result.Ok((url, token));
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // --------------------------------------------------------------------- //
    // Firestore typed-value envelope (subset — we use stringValue + mapValue + timestampValue)
    // --------------------------------------------------------------------- //

    private sealed class FsDocument
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FsValue>? Fields { get; set; }
    }

    private sealed class FsValue
    {
        [JsonPropertyName("stringValue")]    public string? StringValue { get; set; }
        [JsonPropertyName("timestampValue")] public string? TimestampValue { get; set; }
        [JsonPropertyName("mapValue")]       public FsMap? MapValue { get; set; }
    }

    private sealed class FsMap
    {
        [JsonPropertyName("fields")]
        public Dictionary<string, FsValue>? Fields { get; set; }
    }
}
