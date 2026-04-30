// FILE: src/GreatEmailApp.Core/Services/JsonAccountStore.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed
// Persists Account[] to %LOCALAPPDATA%\GreatEmailApp\accounts.json. No passwords.
// Folders[] inside Account are NOT persisted (server is the source of truth) — see [JsonIgnore]
// pattern in Account model. We snapshot only the connection config.

using System.Text.Json;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class JsonAccountStore : IAccountStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public IReadOnlyList<Account> LoadAll()
    {
        try
        {
            if (!File.Exists(AppPaths.AccountsJson)) return Array.Empty<Account>();
            var json = File.ReadAllText(AppPaths.AccountsJson);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Account>();

            var dtos = JsonSerializer.Deserialize<List<AccountDto>>(json, Options);
            if (dtos is null) return Array.Empty<Account>();

            return dtos.Select(d => d.ToAccount()).ToList();
        }
        catch (Exception ex)
        {
            // NOTE: read failures are non-fatal — start fresh if the file is corrupt,
            // but preserve the bad file as accounts.json.bad for diagnostics.
            try
            {
                if (File.Exists(AppPaths.AccountsJson))
                    File.Move(AppPaths.AccountsJson, AppPaths.AccountsJson + ".bad", overwrite: true);
            }
            catch { }
            Console.Error.WriteLine($"[JsonAccountStore.LoadAll] {ex.Message}");
            return Array.Empty<Account>();
        }
    }

    public event EventHandler? Saved;

    public void Save(IEnumerable<Account> accounts)
    {
        AppPaths.EnsureRoot();
        var dtos = accounts.Select(AccountDto.From).ToList();
        var json = JsonSerializer.Serialize(dtos, Options);

        // Atomic write: tmp + rename, so a crash mid-write doesn't blow away the file.
        var tmp = AppPaths.AccountsJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.AccountsJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    // DTO keeps serialization decoupled from the model so we can evolve either independently
    // and so we can explicitly exclude folders/runtime state.
    private sealed record AccountDto(
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
        public static AccountDto From(Account a) => new(
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
