// FILE: src/GreatEmailApp.Core/Services/IDraftStore.cs
// Created: 2026-05-01 | Revised: 2026-05-01 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Models;

namespace GreatEmailApp.Core.Services;

public interface IDraftStore
{
    IReadOnlyList<Draft> LoadAll();

    /// <summary>Insert or update by Id.</summary>
    void Save(Draft draft);

    void Delete(string id);

    /// <summary>Fired after Save/Delete so consumers can refresh.</summary>
    event EventHandler? Changed;
}
