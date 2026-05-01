// FILE: src/GreatEmailApp.Core/Models/Contact.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public sealed class Contact
{
    public required string Id { get; init; }
    public string DisplayName { get; set; } = "";
    public required string EmailAddress { get; init; }
    public string? Phone { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>"Sent mail" auto-collected entries flag here so the user can
    /// see which contacts they actually typed vs. which the app accreted.</summary>
    public bool AutoCollected { get; set; }
}
