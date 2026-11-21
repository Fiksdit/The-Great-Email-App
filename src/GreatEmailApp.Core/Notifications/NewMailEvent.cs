// FILE: src/GreatEmailApp.Core/Notifications/NewMailEvent.cs
// Created: 2026-04-30 | Revised: 2026-11-21 | Rev: 2
// Changed by: Claude Opus 5.5 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Notifications;

/// <summary>
/// One detected message we haven't shown a notification for yet.
/// Carries the originating <see cref="Account"/> so the UI can deep-link
/// "open this account's inbox" on click.
/// </summary>
public sealed record NewMailEvent(Account Account, Message Message);

/// <summary>
/// Raised once per poll cycle per account, after the inbox listing succeeds.
/// Carries the full fetched envelope set so a search indexer can upsert them
/// into the message cache without making its own IMAP round-trip.
/// <para><paramref name="NewUids"/>: UIDs that arrived since the previous poll
/// (not in the poller's seen-set). Null on an account's first-ever poll, when
/// there is no baseline — "incoming only" rules skip that cycle.</para>
/// </summary>
public sealed record MessagesPolledEvent(Account Account, string FolderPath, IReadOnlyList<Message> Messages,
    IReadOnlySet<uint>? NewUids = null);
