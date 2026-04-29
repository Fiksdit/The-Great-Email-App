// FILE: src/GreatEmailApp.Core/Models/Message.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public sealed class Attachment
{
    public required string Name { get; init; }
    public required string Size { get; init; }
    public string Extension { get; init; } = "";
    public string Color { get; init; } = "#3A6FF8";
}

public sealed class Message
{
    public required string Id { get; init; }
    public string AccountId { get; set; } = "";
    public string FolderId { get; set; } = "";
    public string Group { get; set; } = "";  // "Today", "Yesterday", "Last Week", etc.

    public required string Sender { get; init; }
    public string SenderEmail { get; set; } = "";
    public string Avatar { get; set; } = "";   // initials
    public string Color { get; set; } = "#3A6FF8";

    public required string Subject { get; init; }
    public string Preview { get; set; } = "";
    public string Time { get; set; } = "";     // short: "9:14 AM", "Yesterday", "Apr 21"
    public string FullTime { get; set; } = ""; // long: "Tue, Apr 28, 2026, 9:14 AM"

    public bool Unread { get; set; }
    public bool Flagged { get; set; }
    public bool Important { get; set; }

    public string To { get; set; } = "";
    public string Cc { get; set; } = "";

    public List<Attachment> Attachments { get; set; } = new();

    public string BodyHtml { get; set; } = "";
    public string BodyPlain { get; set; } = "";
}
