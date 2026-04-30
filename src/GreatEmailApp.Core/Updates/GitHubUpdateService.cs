// FILE: src/GreatEmailApp.Core/Updates/GitHubUpdateService.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed
//
// Hits the GitHub releases API for the configured repo and decides whether the
// running build is older than the latest published release. No auth required —
// public repo releases are anonymous-readable.

using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json.Serialization;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.Core.Updates;

public sealed class GitHubUpdateService : IUpdateService
{
    private const string Owner = "Fiksdit";
    private const string Repo  = "The-Great-Email-App";

    // Look for the first asset whose name matches this. Keep the publish script
    // in lockstep — see scripts/publish.ps1.
    private const string AssetNamePrefix = "GreatEmailApp-";
    private const string AssetNameSuffix = ".zip";

    private readonly HttpClient _http;

    public GitHubUpdateService(HttpClient? http = null)
    {
        _http = http ?? new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        // GitHub requires a User-Agent on all API requests.
        if (!_http.DefaultRequestHeaders.UserAgent.Any())
            _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("TGEA-Updater", CurrentVersion().ToString()));
    }

    public async Task<Result<UpdateInfo?>> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var url = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
                return Result.Ok<UpdateInfo?>(null); // no releases yet — that's OK
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                return Result.Fail<UpdateInfo?>($"GitHub releases API returned {(int)resp.StatusCode}: {body}");
            }

            var release = await resp.Content.ReadFromJsonAsync<GhRelease>(ct).ConfigureAwait(false);
            if (release is null) return Result.Fail<UpdateInfo?>("Empty release response.");
            if (release.Draft || release.Prerelease) return Result.Ok<UpdateInfo?>(null);

            if (!TryParseTag(release.TagName, out var releaseVersion))
                return Result.Fail<UpdateInfo?>($"Unparseable release tag: '{release.TagName}'.");

            var current = CurrentVersion();
            if (releaseVersion <= current)
                return Result.Ok<UpdateInfo?>(null);

            var asset = release.Assets?.FirstOrDefault(a =>
                a.Name.StartsWith(AssetNamePrefix, StringComparison.OrdinalIgnoreCase)
                && a.Name.EndsWith(AssetNameSuffix, StringComparison.OrdinalIgnoreCase));
            if (asset is null)
                return Result.Fail<UpdateInfo?>(
                    $"Release {release.TagName} has no matching '{AssetNamePrefix}*{AssetNameSuffix}' asset.");

            return Result.Ok<UpdateInfo?>(new UpdateInfo(
                releaseVersion,
                release.TagName,
                release.Body ?? "",
                asset.BrowserDownloadUrl,
                asset.Size,
                release.PublishedAt));
        }
        catch (Exception ex)
        {
            return Result.Fail<UpdateInfo?>($"Update check failed: {ex.Message}", ex);
        }
    }

    public static Version CurrentVersion() =>
        Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

    private static bool TryParseTag(string tag, out Version version)
    {
        // Accept both "v1.2.3" and "1.2.3".
        var stripped = tag.StartsWith('v') ? tag[1..] : tag;
        return Version.TryParse(stripped, out version!);
    }

    // --- Wire DTO -------------------------------------------------------- //
    private sealed record GhRelease(
        [property: JsonPropertyName("tag_name")]      string TagName,
        [property: JsonPropertyName("name")]          string? Name,
        [property: JsonPropertyName("draft")]         bool Draft,
        [property: JsonPropertyName("prerelease")]    bool Prerelease,
        [property: JsonPropertyName("body")]          string? Body,
        [property: JsonPropertyName("published_at")]  DateTimeOffset PublishedAt,
        [property: JsonPropertyName("assets")]        List<GhAsset>? Assets);

    private sealed record GhAsset(
        [property: JsonPropertyName("name")]                 string Name,
        [property: JsonPropertyName("size")]                 long Size,
        [property: JsonPropertyName("browser_download_url")] string BrowserDownloadUrl);
}
