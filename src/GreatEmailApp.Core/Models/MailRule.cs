// FILE: src/GreatEmailApp.Core/Models/MailRule.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public enum RuleField   { From, To, Subject, Body }
public enum RuleOp      { Contains, Equals, StartsWith, EndsWith }
public enum RuleMatch   { All, Any }
public enum RuleAction  { MoveToFolder, MarkRead, Flag, Delete }

public sealed class RuleCondition
{
    public RuleField Field { get; set; } = RuleField.From;
    public RuleOp    Op    { get; set; } = RuleOp.Contains;
    public string    Value { get; set; } = "";
}

public sealed class RuleActionItem
{
    public RuleAction Type { get; set; } = RuleAction.MoveToFolder;

    /// <summary>
    /// For MoveToFolder: the destination folder's full path (e.g. "Receipts" or
    /// "Vendors/Mobile Sentrix"). Ignored for other actions.
    /// </summary>
    public string Value { get; set; } = "";
}

public sealed class MailRule
{
    public required string Id { get; init; }
    public string Name { get; set; } = "";
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Per-rule account scope. Empty means "applies to every account".
    /// Single account id keeps the rule from running on unrelated mailboxes
    /// (e.g. a "from boss@work.com → Work folder" rule shouldn't fire on
    /// the personal account that doesn't even have that folder).
    /// </summary>
    public string? AccountId { get; set; }

    public RuleMatch Match { get; set; } = RuleMatch.All;
    public List<RuleCondition> Conditions { get; set; } = new();
    public List<RuleActionItem> Actions { get; set; } = new();

    /// <summary>Stop processing further rules once this one matches a message.</summary>
    public bool StopOnMatch { get; set; } = false;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
