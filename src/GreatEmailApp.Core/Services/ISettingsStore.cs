// FILE: src/GreatEmailApp.Core/Services/ISettingsStore.cs
// Created: 2026-04-29 | Revised: 2026-04-30 | Rev: 2
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface ISettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);

    /// <summary>Fires after a successful Save. Used by SyncCoordinator to debounced-push.</summary>
    event EventHandler? Saved;
}
