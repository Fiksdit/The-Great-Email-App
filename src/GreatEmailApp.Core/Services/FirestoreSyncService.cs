// FILE: src/GreatEmailApp.Core/Services/FirestoreSyncService.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed
//
// Uses the Firestore REST API (no additional NuGet needed).
// Document path: users/{uid}
// Fields:  settings (map)  +  accounts (array of maps)
//
// Firestore field type wrappers used here:
//   string  → { "stringValue": "..." }
//   integer → { "integerValue": "123" }   (note: value is a string in JSON)
//   double  → { "doubleValue": 1.5 }
//   bool    → { "booleanValue": true }
//   map     → { "mapValue": { "fields": { ... } } }
//   array   → { "arrayValue": { "values": [ ... ] } }

using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public sealed class FirestoreSyncService : IFirestoreSyncService
{
    private readonly IFirebaseAuthService _auth;
    private readonly HttpClient _http = new();

    private string DocUrl(string uid) =>
        $"https://firestore.googleapis.com/v1/projects/{FirebaseConfig.ProjectId}" +
        $"/databases/(default)/documents/users/{uid}";

    public FirestoreSyncService(IFirebaseAuthService auth) => _auth = auth;

    public async Task PushAsync(AppSettings settings, IReadOnlyList<Account> accounts, CancellationToken ct = default)
    {
        if (_auth.CurrentUser is null) return;
        if (!await _auth.RefreshIfNeededAsync(ct)) return;

        var doc = new
        {
            fields = new Dictionary<string, object>
            {
                ["settings"] = MapVal(SettingsToFields(settings)),
                ["accounts"] = ArrVal(accounts.Select(AccountToMap).ToArray()),
            }
        };

        var req = new HttpRequestMessage(HttpMethod.Patch, DocUrl(_auth.CurrentUser.Uid))
        {
            Content = JsonContent.Create(doc),
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.CurrentUser.IdToken);

        try { await _http.SendAsync(req, ct); }
        catch { /* silently ignore network errors on background push */ }
    }

    public async Task<(AppSettings? Settings, List<Account>? Accounts)> PullAsync(CancellationToken ct = default)
    {
        if (_auth.CurrentUser is null) return (null, null);
        if (!await _auth.RefreshIfNeededAsync(ct)) return (null, null);

        var req = new HttpRequestMessage(HttpMethod.Get, DocUrl(_auth.CurrentUser.Uid));
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.CurrentUser.IdToken);

        try
        {
            var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return (null, null);
            return ParseDocument(await resp.Content.ReadAsStringAsync(ct));
        }
        catch { return (null, null); }
    }

    // ── Serialisation helpers ──────────────────────────────────────────────────

    private static Dictionary<string, object> SettingsToFields(AppSettings s) => new()
    {
        ["theme"]                = StrVal(s.Theme.ToString()),
        ["accent"]               = StrVal(s.Accent),
        ["ribbon"]               = StrVal(s.Ribbon.ToString()),
        ["density"]              = StrVal(s.Density.ToString()),
        ["sidebarWidth"]         = DblVal(s.SidebarWidth),
        ["mailListWidth"]        = DblVal(s.MailListWidth),
        ["zoom"]                 = IntVal(s.Zoom),
        ["showHtml"]             = BoolVal(s.ShowHtml),
        ["markReadDelaySeconds"] = IntVal(s.MarkReadDelaySeconds),
        ["syncIntervalMinutes"]  = IntVal(s.SyncIntervalMinutes),
    };

    private static object AccountToMap(Account a) => MapVal(new Dictionary<string, object>
    {
        ["id"]            = StrVal(a.Id),
        ["displayName"]   = StrVal(a.DisplayName),
        ["emailAddress"]  = StrVal(a.EmailAddress),
        ["imapHost"]      = StrVal(a.ImapHost),
        ["imapPort"]      = IntVal(a.ImapPort),
        ["imapEncryption"]= StrVal(a.ImapEncryption.ToString()),
        ["smtpHost"]      = StrVal(a.SmtpHost),
        ["smtpPort"]      = IntVal(a.SmtpPort),
        ["smtpEncryption"]= StrVal(a.SmtpEncryption.ToString()),
        ["username"]      = StrVal(a.Username),
        ["syncSettings"]  = BoolVal(a.SyncSettings),
        ["color"]         = StrVal(a.Color),
    });

    private static object StrVal(string v)  => new { stringValue  = v };
    private static object IntVal(int v)     => new { integerValue = v.ToString() };
    private static object DblVal(double v)  => new { doubleValue  = v };
    private static object BoolVal(bool v)   => new { booleanValue = v };
    private static object MapVal(object fields) => new { mapValue  = new { fields } };
    private static object ArrVal(object[] values) => new { arrayValue = new { values } };

    // ── Deserialisation helpers ────────────────────────────────────────────────

    private static (AppSettings? Settings, List<Account>? Accounts) ParseDocument(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("fields", out var fields)) return (null, null);

            AppSettings?  settings = null;
            List<Account>? accounts = null;

            if (fields.TryGetProperty("settings", out var sf) &&
                sf.TryGetProperty("mapValue", out var mv) &&
                mv.TryGetProperty("fields", out var sFields))
            {
                settings = ParseSettings(sFields);
            }

            if (fields.TryGetProperty("accounts", out var af) &&
                af.TryGetProperty("arrayValue", out var av) &&
                av.TryGetProperty("values", out var vals))
            {
                accounts = new List<Account>();
                foreach (var v in vals.EnumerateArray())
                {
                    if (v.TryGetProperty("mapValue", out var mapV) &&
                        mapV.TryGetProperty("fields", out var aFields))
                    {
                        var acct = ParseAccount(aFields);
                        if (acct is not null) accounts.Add(acct);
                    }
                }
            }

            return (settings, accounts);
        }
        catch { return (null, null); }
    }

    private static AppSettings? ParseSettings(JsonElement f)
    {
        try
        {
            return new AppSettings
            {
                Theme                = ParseEnum(f, "theme",   AppTheme.Dark),
                Accent               = GetStr(f, "accent")                    ?? "#3A6FF8",
                Ribbon               = ParseEnum(f, "ribbon",  RibbonStyle.Simplified),
                Density              = ParseEnum(f, "density", DensityMode.Cozy),
                SidebarWidth         = GetDbl(f, "sidebarWidth")              ?? 264,
                MailListWidth        = GetDbl(f, "mailListWidth")             ?? 380,
                Zoom                 = (int)(GetDbl(f, "zoom")                ?? 100),
                ShowHtml             = GetBool(f, "showHtml")                 ?? true,
                MarkReadDelaySeconds = (int)(GetDbl(f, "markReadDelaySeconds") ?? 2),
                SyncIntervalMinutes  = (int)(GetDbl(f, "syncIntervalMinutes")  ?? 5),
            };
        }
        catch { return null; }
    }

    private static Account? ParseAccount(JsonElement f)
    {
        try
        {
            var id = GetStr(f, "id") ?? Guid.NewGuid().ToString();
            return new Account
            {
                Id             = id,
                DisplayName    = GetStr(f, "displayName")  ?? "",
                EmailAddress   = GetStr(f, "emailAddress") ?? "",
                ImapHost       = GetStr(f, "imapHost")     ?? "",
                ImapPort       = (int)(GetDbl(f, "imapPort")  ?? 993),
                ImapEncryption = ParseEnum(f, "imapEncryption", MailEncryption.SslTls),
                SmtpHost       = GetStr(f, "smtpHost")     ?? "",
                SmtpPort       = (int)(GetDbl(f, "smtpPort")  ?? 587),
                SmtpEncryption = ParseEnum(f, "smtpEncryption", MailEncryption.StartTls),
                Username       = GetStr(f, "username")     ?? "",
                SyncSettings   = GetBool(f, "syncSettings") ?? true,
                Color          = GetStr(f, "color")        ?? "#3A6FF8",
            };
        }
        catch { return null; }
    }

    private static string? GetStr(JsonElement f, string key) =>
        f.TryGetProperty(key, out var v) && v.TryGetProperty("stringValue", out var sv)
            ? sv.GetString() : null;

    private static double? GetDbl(JsonElement f, string key)
    {
        if (!f.TryGetProperty(key, out var v)) return null;
        if (v.TryGetProperty("doubleValue", out var dv))  return dv.GetDouble();
        if (v.TryGetProperty("integerValue", out var iv))
        {
            var raw = iv.ValueKind == JsonValueKind.String ? iv.GetString() : iv.GetRawText();
            return double.TryParse(raw, out var d) ? d : null;
        }
        return null;
    }

    private static bool? GetBool(JsonElement f, string key) =>
        f.TryGetProperty(key, out var v) && v.TryGetProperty("booleanValue", out var bv)
            ? bv.GetBoolean() : null;

    private static T ParseEnum<T>(JsonElement f, string key, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(GetStr(f, key), out var v) ? v : fallback;
}
