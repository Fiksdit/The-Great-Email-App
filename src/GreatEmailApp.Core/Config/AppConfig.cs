// FILE: src/GreatEmailApp.Core/Config/AppConfig.cs
// Created: 2026-04-30 | Revised: 2026-04-30 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Text.Json;
using System.Text.Json.Serialization;

namespace GreatEmailApp.Core.Config;

/// <summary>
/// Strongly-typed view of appsettings.json. Loaded once at startup.
/// Per rulebook §12 the Firebase API key and Desktop OAuth client ID
/// are public-by-design — they identify the project, they don't authorize
/// access. Auth comes from Google sign-in + Firestore security rules.
/// </summary>
public sealed record AppConfig(
    [property: JsonPropertyName("Firebase")]    FirebaseOptions Firebase,
    [property: JsonPropertyName("GoogleOAuth")] GoogleOAuthOptions GoogleOAuth)
{
    public static AppConfig Load(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"appsettings.json not found at '{path}'. " +
                "Build should copy it to the output directory.", path);

        using var stream = File.OpenRead(path);
        var cfg = JsonSerializer.Deserialize<AppConfig>(stream, JsonOpts)
            ?? throw new InvalidDataException("appsettings.json deserialized to null.");

        cfg.Validate();
        return cfg;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Firebase.ProjectId))
            throw new InvalidDataException("Firebase.ProjectId is required.");
        if (string.IsNullOrWhiteSpace(Firebase.ApiKey))
            throw new InvalidDataException("Firebase.ApiKey is required.");
        if (string.IsNullOrWhiteSpace(GoogleOAuth.DesktopClientId))
            throw new InvalidDataException("GoogleOAuth.DesktopClientId is required.");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

public sealed record FirebaseOptions(
    string ProjectId,
    string ApiKey,
    string AuthDomain,
    string StorageBucket,
    string MessagingSenderId,
    string AppId);

public sealed record GoogleOAuthOptions(string DesktopClientId);
