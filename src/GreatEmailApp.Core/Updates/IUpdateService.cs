// FILE: src/GreatEmailApp.Core/Updates/IUpdateService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Updates;

public sealed record UpdateInfo(
    Version Version,
    string TagName,
    string ReleaseNotes,
    string ZipDownloadUrl,
    long? ZipSizeBytes,
    DateTimeOffset PublishedAt);

/// <summary>
/// Checks GitHub Releases for a newer build of TGEA. Returns
/// <c>Ok(UpdateInfo)</c> if newer, <c>Ok(null)</c> if up to date.
/// Per rulebook §9, never throws; failures come back as <see cref="Result{T}"/>.Fail.
/// </summary>
public interface IUpdateService
{
    Task<Result<UpdateInfo?>> CheckAsync(CancellationToken ct = default);
}
