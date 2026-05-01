// FILE: src/GreatEmailApp.Core/Sync/FirestoreSyncService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// REST-based Firestore client for the single-document settings sync.
// We deliberately don't take a dependency on Google.Cloud.Firestore — the
// .NET SDK targets server scenarios and pulls in gRPC + a long transitive
// tree. For one document with one collection, REST is plenty.
//
// Document path:  projects/{projectId}/databases/(default)/documents/users/{uid}/settings/profile
//
// Wire format (Firestore typed-value envelope):
//   {
//     "fields": {
//       "settings_json": { "stringValue": "<serialized AppSettings>" },
//       "accounts_json": { "stringValue": "<serialized Account[] (no passwords)>" },
//       "updated_at":    { "timestampValue": "2026-04-30T12:34:56.000Z" }
//     }
//   }
//
// We hide the typed envelope inside this file — the rest of the app sees
// a clean SyncSnapshot record.

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Auth;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Sync;

public sealed class FirestoreSyncService : IFirestoreSyncService
{
    private readonly HttpClient _http;
    private readonly IAuthService _auth;
    private readonly FirebaseOptions _firebase;

    public FirestoreSyncService(AppConfig config, IAuthService auth, HttpClient? http = null)
    {
        _firebase = config.Firebase;
        _auth = auth;
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<Result<SyncSnapshot?>> PullAsync(CancellationToken ct = default)
    {
        var url = await BuildDocUrlAsync(ct).ConfigureAwait(false);
        if (url is Result<(string url, string token)>.Fail uf)
            return Result.Fail<SyncSnapshot?>(uf.Error);
        var (docUrl, token) = ((Result<(string url, string token)>.Ok)url).Value;

        using var req = new HttpRequestMessage(HttpMethod.Get, docUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        try
        {
            using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
            if (resp.StatusCode == HttpStatusCode.NotFound)
                return Result.Ok<SyncSnapshot?>(null);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Fail<SyncSnapshot?>($"Firestore GET failed ({(int)resp.StatusCode}): {body}");
            }

            var doc = await resp.Content.ReadFromJsonAsync<FsDocument>(JsonOpts, ct).ConfigureAwait(false);
            if (doc?.Fields is null)
                return Result.Fail<SyncSnapshot?>("Firestore document has no fields.");

            var settingsJson = doc.Fields.GetValueOrDefault("settings_json")?.StringValue ?? "";
            var accountsJson = doc.Fields.GetValueOrDefault("accounts_json")?.StringValue ?? "[]";
            var contactsJson = doc.Fields.GetValueOrDefault("contacts_json")?.StringValue ?? "[]";
            var rulesJson    = doc.Fields.GetValueOrDefault("rules_json")?.StringValue    ?? "[]";
            var updatedAtStr = doc.Fields.GetValueOrDefault("updated_at")?.TimestampValue;

            var settings = JsonSerializer.Deserialize<AppSettings>(settingsJson, PayloadOpts) ?? new AppSettings();
            var dtos = JsonSerializer.Deserialize<List<SyncAccountDto>>(accountsJson, PayloadOpts) ?? new();
            var contacts = JsonSerializer.Deserialize<List<Contact>>(contactsJson, PayloadOpts) ?? new();
            var rules = JsonSerializer.Deserialize<List<MailRule>>(rulesJson, PayloadOpts) ?? new();
            var updatedAt = DateTimeOffset.TryParse(updatedAtStr, out var dt) ? dt : DateTimeOffset.MinValue;

            var snapshot = new SyncSnapshot(
                settings,
                dtos.Select(d => d.ToAccount()).ToList(),
                updatedAt,
                contacts,
                rules);
            return Result.Ok<SyncSnapshot?>(snapshot);
        }
        catch (Exception ex)
        {
            return Result.Fail<SyncSnapshot?>($"Pull failed: {ex.Message}", ex);
        }
    }

    public async Task<Result<bool>> PushAsync(SyncSnapshot snapshot, CancellationToken ct = default)
    {
        var url = await BuildDocUrlAsync(ct).ConfigureAwait(false);
        if (url is Result<(string url, string token)>.Fail uf)
            return Result.Fail<bool>(uf.Error);
        var (docUrl, token) = ((Result<(string url, string token)>.Ok)url).Value;

        var settingsJson = JsonSerializer.Serialize(snapshot.Settings, PayloadOpts);
        var accountsJson = JsonSerializer.Serialize(
            snapshot.Accounts.Select(SyncAccountDto.From).ToList(),
            PayloadOpts);
        var contactsJson = JsonSerializer.Serialize(snapshot.Contacts ?? new List<Contact>(), PayloadOpts);
        var rulesJson    = JsonSerializer.Serialize(snapshot.Rules    ?? new List<MailRule>(), PayloadOpts);

        var body = new FsDocument
        {
            Fields = new Dictionary<string, FsValue>
            {
                ["settings_json"] = new() { StringValue = settingsJson },
                ["accounts_json"] = new() { StringValue = accountsJson },
                ["contacts_json"] = new() { StringValue = contactsJson },
                ["rules_json"]    = new() { StringValue = rulesJson },
                ["updated_at"]    = new() { TimestampValue = snapshot.UpdatedAt.UtcDateTime.ToString("o") },
            },
        };

        // PATCH = upsert. updateMask explicitly lists every field so other fields
        // (if a future version adds any) aren't blown away by an older client.
        var patchUrl = docUrl
            + "?updateMask.fieldPaths=settings_json"
            + "&updateMask.fieldPaths=accounts_json"
            + "&updateMask.fieldPaths=contacts_json"
            + "&updateMask.fieldPaths=rules_json"
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
                return Result.Fail<bool>($"Firestore PATCH failed ({(int)resp.StatusCode}): {raw}");
            }
            return Result.Ok(true);
        }
        catch (Exception ex)
        {
            return Result.Fail<bool>($"Push failed: {ex.Message}", ex);
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
            $"/databases/(default)/documents/users/{Uri.EscapeDataString(session.Uid)}/settings/profile";
        return Result.Ok((url, token));
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions PayloadOpts = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // --------------------------------------------------------------------- //
    // Firestore typed-value envelope
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
    }

    // --------------------------------------------------------------------- //
    // Account DTO — local to sync; keeps passwords + runtime state out.
    // --------------------------------------------------------------------- //

    private sealed record SyncAccountDto(
        string Id,
        string DisplayName,
        string EmailAddress,
        string Initials,
        string Color,
        bool IsPrimary,
        string ImapHost,
        int ImapPort,
        MailEncryption ImapEncryption,
        string SmtpHost,
        int SmtpPort,
        MailEncryption SmtpEncryption,
        string Username,
        bool SyncSettings)
    {
        public static SyncAccountDto From(Account a) => new(
            a.Id, a.DisplayName, a.EmailAddress, a.Initials, a.Color, a.IsPrimary,
            a.ImapHost, a.ImapPort, a.ImapEncryption,
            a.SmtpHost, a.SmtpPort, a.SmtpEncryption,
            a.Username, a.SyncSettings);

        public Account ToAccount() => new()
        {
            Id = Id,
            DisplayName = DisplayName,
            EmailAddress = EmailAddress,
            Initials = Initials,
            Color = Color,
            IsPrimary = IsPrimary,
            ImapHost = ImapHost,
            ImapPort = ImapPort,
            ImapEncryption = ImapEncryption,
            SmtpHost = SmtpHost,
            SmtpPort = SmtpPort,
            SmtpEncryption = SmtpEncryption,
            Username = Username,
            SyncSettings = SyncSettings,
            Status = AccountStatus.Offline,
        };
    }
}
