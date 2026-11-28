// FILE: src/GreatEmailApp.Core/Models/MailRule.cs
// Created: 2026-05-01 | Revised: 2026-11-27 | Rev: 4
// Changed by: Claude Opus 5.5 on behalf of James Reed
//
// SYNC COMPAT: rules travel to other PCs via Firestore (rules_json) and are
// read with JsonStringEnumConverter. An older client that sees an enum NAME it
// doesn't know fails the whole settings pull. So never add enum values here
// for new behavior — encode it in existing values + Value (see
// PermanentDeleteValue) so old clients degrade safely instead of breaking.

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
    /// "Vendors/Mobile Sentrix"). For Delete: <see cref="PermanentDeleteValue"/>
    /// means "delete permanently, skip Trash"; anything else moves to Trash.
    /// Ignored for other actions.
    /// </summary>
    public string Value { get; set; } = "";

    /// <summary>
    /// Delete + this Value = mark \Deleted and expunge in place — never lands in
    /// Trash. Older clients that don't know the marker fall back to plain Delete
    /// (move to Trash), which is the safe degrade.
    /// </summary>
    public const string PermanentDeleteValue = "permanent";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPermanentDelete =>
        Type == RuleAction.Delete
        && string.Equals(Value?.Trim(), PermanentDeleteValue, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Flag + this Value = "Mark priority": sets the Priority keyword (its own
    /// marker, separate from the user's \Flagged review flag) and raises a
    /// priority notification for new mail. Older clients that don't know the
    /// marker fall back to a plain Flag, which is the safe degrade.
    /// </summary>
    public const string PriorityValue = "priority";

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsPriority =>
        Type == RuleAction.Flag
        && string.Equals(Value?.Trim(), PriorityValue, StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// Only act on newly arrived inbox mail (UIDs the poller hasn't seen
    /// before). Skipped by "Run rules now" and by the regular re-scan of the
    /// latest inbox messages. Older clients ignore this unknown property and
    /// treat the rule as a normal one.
    /// </summary>
    public bool IncomingOnly { get; set; } = false;

    /// <summary>
    /// Which group the rule is listed under in Settings → Rules ("Spam",
    /// "Organizing", or any custom name the owner types). A plain string, NOT
    /// an enum — see the SYNC COMPAT note at the top. Empty (rules made before
    /// categories existed, or saved by an older client, which drops unknown
    /// properties) is shown under <see cref="EffectiveCategory"/>.
    /// </summary>
    public string Category { get; set; } = "";

    public const string SpamCategory = "Spam";
    public const string OrganizingCategory = "Organizing";

    /// <summary>
    /// <see cref="Category"/> if set; otherwise a guess: rules that delete
    /// mail are Spam rules, everything else is Organizing.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string EffectiveCategory =>
        !string.IsNullOrWhiteSpace(Category) ? Category.Trim()
        : Actions.Any(a => a.Type == RuleAction.Delete) ? SpamCategory
        : OrganizingCategory;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
