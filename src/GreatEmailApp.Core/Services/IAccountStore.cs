// FILE: src/GreatEmailApp.Core/Services/IAccountStore.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

/// <summary>
/// Persists account configuration (host/port/encryption/etc) but NOT passwords.
/// Passwords go to ICredentialStore. This split is per rulebook §7.
/// </summary>
public interface IAccountStore
{
    IReadOnlyList<Account> LoadAll();
    void Save(IEnumerable<Account> accounts);
}
