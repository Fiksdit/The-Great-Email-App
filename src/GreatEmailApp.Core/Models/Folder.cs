// FILE: src/GreatEmailApp.Core/Models/Folder.cs
// Created: 2026-04-29 | Revised: 2026-04-29 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

namespace GreatEmailApp.Core.Models;

public enum SpecialFolder
{
    None,
    Inbox,
    Drafts,
    Sent,
    Deleted,
    Junk,
    Archive
}

public sealed class Folder
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string AccountId { get; set; } = "";
    public string FullPath { get; set; } = "";
    public SpecialFolder Special { get; set; } = SpecialFolder.None;
    public int UnreadCount { get; set; }
    public int TotalCount { get; set; }
    public bool IsNested { get; set; }
}
