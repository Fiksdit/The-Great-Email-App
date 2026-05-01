// FILE: src/GreatEmailApp.Core/Services/IContactsStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IContactsStore
{
    IReadOnlyList<Contact> LoadAll();

    /// <summary>Replace the entire contact roster with this list. Atomic write.</summary>
    void Save(IEnumerable<Contact> contacts);

    /// <summary>
    /// Add the contact if no contact with that email already exists. Returns the
    /// stored Contact (existing or newly added). Used by the auto-collect path.
    /// </summary>
    Contact AddOrGet(Contact candidate);

    /// <summary>Fired after a successful Save. Used by SyncCoordinator to debounce-push.</summary>
    event EventHandler? Saved;
}
