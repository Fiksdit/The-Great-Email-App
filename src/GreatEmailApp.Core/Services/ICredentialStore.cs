// FILE: src/GreatEmailApp.Core/Services/ICredentialStore.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Services;

/// <summary>
/// Abstraction over the OS credential store. On Windows this maps to
/// the Credential Manager (advapi32 CredRead / CredWrite / CredDelete).
/// Per rulebook §7: passwords NEVER live in JSON or memory beyond what's
/// needed to hand them to MailKit. This is the only place credentials live.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Save or replace a credential keyed by accountId.</summary>
    void Save(string accountId, string username, string password);

    /// <summary>Returns null if no credential is stored for this accountId.</summary>
    (string Username, string Password)? Read(string accountId);

    /// <summary>No-op if not present.</summary>
    void Delete(string accountId);
}
