// FILE: src/GreatEmailApp.Core/Sync/SyncSnapshot.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Sync;

/// <summary>
/// Single payload pushed to / pulled from Firestore at
/// <c>users/{uid}/settings/profile</c>. Wraps everything that should sync
/// across devices: AppSettings + the account roster (no passwords — those
/// stay in Windows Credential Manager per rulebook §7B).
/// <para><see cref="UpdatedAt"/> drives last-write-wins resolution per the
/// roadmap decision log (2026-04-29).</para>
/// </summary>
public sealed record SyncSnapshot(
    AppSettings Settings,
    IReadOnlyList<Account> Accounts,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<Contact>? Contacts = null);
