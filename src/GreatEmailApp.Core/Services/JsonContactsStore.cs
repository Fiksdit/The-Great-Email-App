// FILE: src/GreatEmailApp.Core/Services/JsonContactsStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Persists contacts to %LOCALAPPDATA%\GreatEmailApp\contacts.json.
// Same atomic-write + corrupt-file-sideline pattern as JsonAccountStore.
// Saved event lets SyncCoordinator pick up changes for cross-PC sync.

using System.Text.Json;
using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Storage;

namespace GreatEmailApp.Core.Services;

public sealed class JsonContactsStore : IContactsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
    };

    public event EventHandler? Saved;

    public IReadOnlyList<Contact> LoadAll()
    {
        try
        {
            if (!File.Exists(AppPaths.ContactsJson)) return Array.Empty<Contact>();
            var json = File.ReadAllText(AppPaths.ContactsJson);
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Contact>();
            return JsonSerializer.Deserialize<List<Contact>>(json, Options) ?? new List<Contact>();
        }
        catch (Exception ex)
        {
            try
            {
                if (File.Exists(AppPaths.ContactsJson))
                    File.Move(AppPaths.ContactsJson, AppPaths.ContactsJson + ".bad", overwrite: true);
            }
            catch { }
            Console.Error.WriteLine($"[JsonContactsStore.LoadAll] {ex.Message}");
            return Array.Empty<Contact>();
        }
    }

    public void Save(IEnumerable<Contact> contacts)
    {
        AppPaths.EnsureRoot();
        var list = contacts.ToList();
        var json = JsonSerializer.Serialize(list, Options);
        var tmp = AppPaths.ContactsJson + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, AppPaths.ContactsJson, overwrite: true);
        Saved?.Invoke(this, EventArgs.Empty);
    }

    public Contact AddOrGet(Contact candidate)
    {
        var current = LoadAll().ToList();
        var match = current.FirstOrDefault(c =>
            string.Equals(c.EmailAddress, candidate.EmailAddress, StringComparison.OrdinalIgnoreCase));
        if (match is not null) return match;

        var stored = new Contact
        {
            Id = string.IsNullOrEmpty(candidate.Id) ? Guid.NewGuid().ToString("N") : candidate.Id,
            EmailAddress = candidate.EmailAddress,
            DisplayName = candidate.DisplayName,
            Phone = candidate.Phone,
            Notes = candidate.Notes,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            AutoCollected = candidate.AutoCollected,
        };
        current.Add(stored);
        Save(current);
        return stored;
    }
}
