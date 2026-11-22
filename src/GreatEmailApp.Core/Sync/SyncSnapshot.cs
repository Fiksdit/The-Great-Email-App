// FILE: src/GreatEmailApp.Core/Sync/SyncSnapshot.cs
// Created: 2026-04-30 | Revised: 2026-11-21 | Rev: 3
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;
using GreatEmailApp.Core.Spam;

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
    IReadOnlyList<Contact>? Contacts = null,
    IReadOnlyList<MailRule>? Rules = null,
    SpamFilterConfig? SpamConfig = null,
    // Rule id → deletion time (rules_deleted_json). Null on docs from clients
    // that predate rule merging (FIX-2026-11-21-004).
    IReadOnlyDictionary<string, DateTimeOffset>? DeletedRules = null);
