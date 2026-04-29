// FILE: src/GreatEmailApp.Core/Sample/SampleData.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
// Mirrors docs/design/data.jsx so Phase 1 visuals match the design mockup exactly.

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Sample;

public static class SampleData
{
    public static List<Account> GetAccounts() => new()
    {
        new Account
        {
            Id = "primary",
            DisplayName = "Personal",
            EmailAddress = "me@somemail.io",
            Initials = "M",
            Color = "#3A6FF8",
            Status = AccountStatus.Connected,
            IsPrimary = true,
            Folders = new()
            {
                new Folder { Id = "inbox", Name = "Inbox", Special = SpecialFolder.Inbox, UnreadCount = 12 },
                new Folder { Id = "drafts", Name = "Drafts", Special = SpecialFolder.Drafts, UnreadCount = 3 },
                new Folder { Id = "sent", Name = "Sent Items", Special = SpecialFolder.Sent },
                new Folder { Id = "deleted", Name = "Deleted Items", Special = SpecialFolder.Deleted, UnreadCount = 87 },
                new Folder { Id = "junk", Name = "Junk", Special = SpecialFolder.Junk, UnreadCount = 4 },
                new Folder { Id = "archive", Name = "Archive", Special = SpecialFolder.Archive },
                new Folder { Id = "newsletters", Name = "Newsletters", IsNested = true, UnreadCount = 24 },
                new Folder { Id = "receipts", Name = "Receipts", IsNested = true },
                new Folder { Id = "travel", Name = "Travel", IsNested = true, UnreadCount = 2 },
            }
        },
        new Account
        {
            Id = "work",
            DisplayName = "Work",
            EmailAddress = "j.dixon@workmail.co",
            Initials = "JD",
            Color = "#14a37f",
            Status = AccountStatus.Connected,
            Folders = new()
            {
                new Folder { Id = "inbox-work", Name = "Inbox", Special = SpecialFolder.Inbox, UnreadCount = 38 },
                new Folder { Id = "drafts-work", Name = "Drafts", Special = SpecialFolder.Drafts },
                new Folder { Id = "sent-work", Name = "Sent Items", Special = SpecialFolder.Sent },
                new Folder { Id = "deleted-work", Name = "Deleted Items", Special = SpecialFolder.Deleted },
                new Folder { Id = "junk-work", Name = "Junk", Special = SpecialFolder.Junk },
                new Folder { Id = "archive-work", Name = "Archive", Special = SpecialFolder.Archive },
                new Folder { Id = "clients", Name = "Clients", IsNested = true, UnreadCount = 7 },
                new Folder { Id = "internal", Name = "Internal", IsNested = true },
            }
        },
        new Account
        {
            Id = "side",
            DisplayName = "Studio",
            EmailAddress = "hello@brightside.studio",
            Initials = "BS",
            Color = "#8a5cf5",
            Status = AccountStatus.Syncing,
            Folders = new()
            {
                new Folder { Id = "inbox-side", Name = "Inbox", Special = SpecialFolder.Inbox, UnreadCount = 5 },
                new Folder { Id = "drafts-side", Name = "Drafts", Special = SpecialFolder.Drafts },
                new Folder { Id = "sent-side", Name = "Sent Items", Special = SpecialFolder.Sent },
                new Folder { Id = "deleted-side", Name = "Deleted Items", Special = SpecialFolder.Deleted },
            }
        },
        new Account
        {
            Id = "alumni",
            DisplayName = "Alumni",
            EmailAddress = "j.dixon@alum.edu",
            Initials = "AL",
            Color = "#d29014",
            Status = AccountStatus.Connected,
            Folders = new()
            {
                new Folder { Id = "inbox-alum", Name = "Inbox", Special = SpecialFolder.Inbox, UnreadCount = 1 },
                new Folder { Id = "sent-alum", Name = "Sent Items", Special = SpecialFolder.Sent },
                new Folder { Id = "archive-alum", Name = "Archive", Special = SpecialFolder.Archive },
            }
        },
        new Account
        {
            Id = "legacy",
            DisplayName = "Legacy IMAP",
            EmailAddress = "old.address@isp.net",
            Initials = "L",
            Color = "#d4406b",
            Status = AccountStatus.Error,
            Folders = new()
            {
                new Folder { Id = "inbox-legacy", Name = "Inbox", Special = SpecialFolder.Inbox },
                new Folder { Id = "sent-legacy", Name = "Sent", Special = SpecialFolder.Sent },
            }
        },
    };

    public static List<Message> GetMessages() => new()
    {
        new Message
        {
            Id = "e1",
            Group = "Today",
            Sender = "Field Notes Weekly",
            SenderEmail = "issue@fieldnotes.email",
            Avatar = "FN",
            Color = "#14a37f",
            Subject = "Issue 142 — On craft, calm software, and the slow web",
            Preview = "This week: the case for slower interfaces, three small tools that get out of your way, and a long read on workshop discipline by Aiko Tanaka.",
            Time = "9:14 AM",
            FullTime = "Tue, Apr 28, 2026, 9:14 AM",
            Unread = true,
            Flagged = true,
            To = "you <me@somemail.io>",
            Cc = "subscribers <subs@fieldnotes.email>",
            Attachments = new()
            {
                new Attachment { Name = "issue-142.pdf", Size = "1.2 MB", Extension = "PDF", Color = "#d4406b" },
                new Attachment { Name = "cover-art.jpg", Size = "640 KB", Extension = "JPG", Color = "#3A6FF8" },
            },
            BodyHtml = SampleNewsletterHtml,
        },
        new Message
        {
            Id = "e2", Group = "Today", Sender = "Priya Anand", SenderEmail = "priya@workmail.co",
            Avatar = "PA", Color = "#8a5cf5",
            Subject = "Re: Q2 roadmap review — quick thoughts before Friday",
            Preview = "Hey — looked through the deck last night. Two things stood out. First, the timeline for the auth migration feels aggressive given we're still scoping…",
            Time = "8:52 AM", Unread = true, Important = true,
        },
        new Message
        {
            Id = "e3", Group = "Today", Sender = "GitHub", SenderEmail = "noreply@github.com",
            Avatar = "GH", Color = "#1a1a1a",
            Subject = "[tgea/desktop] PR #284: Fluent ribbon polish (review requested)",
            Preview = "@you, Marco Reyes requested your review on this pull request. 14 files changed (+412 −188) — most edits in src/ribbon/, src/theme/, and src/icons/.",
            Time = "8:04 AM",
        },
        new Message
        {
            Id = "e4", Group = "Today", Sender = "Calendar", SenderEmail = "calendar@somemail.io",
            Avatar = "CA", Color = "#3A6FF8",
            Subject = "Reminder: Design crit — Thursday 2pm",
            Preview = "Heads up: you have Design crit (Thu, May 1, 2:00 PM – 3:00 PM) starting in 2 days. Location: Room 4 / Zoom link in description.",
            Time = "7:30 AM",
        },
        new Message
        {
            Id = "e5", Group = "Yesterday", Sender = "Mira Okonkwo", SenderEmail = "mira@brightside.studio",
            Avatar = "MO", Color = "#0ea5e9",
            Subject = "Hand-off: brand guidelines + final asset bundle",
            Preview = "Final files are in the shared drive. I've attached the lighter version of the brand guidelines as a quick reference. Let me know if you want me to walk through anything before…",
            Time = "Yesterday", Flagged = true,
            Attachments = new() { new Attachment { Name = "brand-guidelines.pdf", Size = "8.4 MB", Extension = "PDF", Color = "#d4406b" } }
        },
        new Message
        {
            Id = "e6", Group = "Yesterday", Sender = "Stripe", SenderEmail = "receipts@stripe.com",
            Avatar = "S", Color = "#635bff",
            Subject = "Your receipt from Brightside Studio LLC",
            Preview = "Thanks for your business. Your payment of $2,400.00 USD to Brightside Studio LLC has been completed. View your invoice or manage your subscription…",
            Time = "Yesterday",
        },
        new Message
        {
            Id = "e7", Group = "Yesterday", Sender = "Jules Park", SenderEmail = "jules@workmail.co",
            Avatar = "JP", Color = "#f43f5e",
            Subject = "Lunch this week?",
            Preview = "Free Thursday or Friday? I'm in the office both days and would love to catch up properly — feels like ages. The new place on 4th finally opened btw…",
            Time = "Yesterday",
        },
        new Message
        {
            Id = "e8", Group = "Last Week", Sender = "1Password", SenderEmail = "noreply@1password.com",
            Avatar = "1P", Color = "#1d4ed8",
            Subject = "Your monthly Watchtower report — 2 items need attention",
            Preview = "We checked your vaults and found 2 items that may need a closer look: 1 reused password, 1 site that supports 2FA you haven't enabled yet.",
            Time = "Apr 23",
        },
        new Message
        {
            Id = "e9", Group = "Last Week", Sender = "Ben Carver", SenderEmail = "ben@oldfriend.dev",
            Avatar = "BC", Color = "#22c55e",
            Subject = "weird thing in the new build",
            Preview = "Hey — was poking around the latest nightly and noticed the sync status indicator gets stuck on amber after a manual reconnect. Repro: disable wifi for ~30s, re-enable…",
            Time = "Apr 22",
            Attachments = new() { new Attachment { Name = "screen-recording.mov", Size = "12 MB", Extension = "MOV", Color = "#8a5cf5" } }
        },
        new Message
        {
            Id = "e10", Group = "Last Week", Sender = "Aiko Tanaka", SenderEmail = "aiko@fieldnotes.email",
            Avatar = "AT", Color = "#14a37f",
            Subject = "Workshop visit — would you be up for a feature?",
            Preview = "Big fan of what you've been making. We're putting together a series on independent software workshops and would love to feature TGEA in an upcoming issue…",
            Time = "Apr 21", Important = true,
        },
    };

    private const string SampleNewsletterHtml =
        "Hi friend,\n\n" +
        "Welcome to Issue 142 of Field Notes Weekly. This week we explore why slower interfaces are quietly winning, three calm tools we've been using daily, and a long read from Aiko Tanaka on workshop discipline.\n\n" +
        "— On the case for slower interfaces —\n" +
        "Software that respects attention. Less reactive, more deliberate. The shift is subtle but unmistakable in the apps people are choosing in 2026.\n\n" +
        "— Three small tools —\n" +
        "1. Reflect — a journal that does one thing well.\n" +
        "2. Maple — a focus timer with no notifications.\n" +
        "3. Workshop — a writing app shaped like an actual desk.\n\n" +
        "— Long read: workshop discipline —\n" +
        "Aiko Tanaka spent six months in a small studio in Kyoto. Her notes on rhythm, repetition, and routine are below.\n\n" +
        "Thanks for reading. Reply if you want to chat about any of this.\n" +
        "— Field Notes";
}
