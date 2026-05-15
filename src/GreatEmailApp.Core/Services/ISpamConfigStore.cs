// FILE: src/GreatEmailApp.Core/Services/ISpamConfigStore.cs
// Created: 2026-05-15 | Revised: 2026-05-15 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Spam;

namespace GreatEmailApp.Core.Services;

public interface ISpamConfigStore
{
    SpamFilterConfig Load();
    void Save(SpamFilterConfig config);
    event EventHandler? Saved;
}
