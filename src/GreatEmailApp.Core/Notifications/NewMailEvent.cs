// FILE: src/GreatEmailApp.Core/Notifications/NewMailEvent.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

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
/// </summary>
public sealed record MessagesPolledEvent(Account Account, string FolderPath, IReadOnlyList<Message> Messages);
