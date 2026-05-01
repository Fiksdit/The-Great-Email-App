// FILE: src/GreatEmailApp.Core/Services/IRulesStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IRulesStore
{
    IReadOnlyList<MailRule> LoadAll();
    void Save(IEnumerable<MailRule> rules);
    event EventHandler? Saved;
}
