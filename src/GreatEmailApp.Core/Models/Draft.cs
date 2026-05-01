// FILE: src/GreatEmailApp.Core/Models/Draft.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public sealed class Draft
{
    public required string Id { get; init; }
    public string AccountId { get; set; } = "";
    public string To  { get; set; } = "";
    public string Cc  { get; set; } = "";
    public string Bcc { get; set; } = "";
    public string Subject { get; set; } = "";
    public string BodyHtml { get; set; } = "";
    public string BodyText { get; set; } = "";
    public string? InReplyToMessageId { get; set; }
    /// <summary>Absolute file paths the user attached. Restored as-is — if a
    /// file moved/deleted between save and resume, the user gets a missing
    /// attachment indicator and can re-pick.</summary>
    public List<string> AttachmentPaths { get; set; } = new();
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
