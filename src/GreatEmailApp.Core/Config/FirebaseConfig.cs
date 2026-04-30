// FILE: src/GreatEmailApp.Core/Config/FirebaseConfig.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed
//
// HOW TO CONFIGURE:
//   1. Go to https://console.firebase.google.com → create / open your project.
//   2. Project Settings → General → Your apps → add a Web app → copy the Web API Key.
//   3. Authentication → Sign-in method → enable Google.
//   4. Go to https://console.cloud.google.com → APIs & Services → Credentials.
//      Create an OAuth 2.0 Client ID of type "Desktop app".
//      No redirect URI configuration needed (loopback is allowed by default for desktop).
//   5. Replace the placeholder strings below with the real values.
//   6. In Firestore, create a database and add a security rule that allows reads/writes
//      only to users/{uid} where request.auth.uid == uid.

namespace GreatEmailApp.Core.Config;

public static class FirebaseConfig
{
    public const string ApiKey             = "YOUR_FIREBASE_WEB_API_KEY";
    public const string ProjectId          = "YOUR_FIREBASE_PROJECT_ID";
    public const string GoogleClientId     = "YOUR_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com";
    public const string GoogleClientSecret = "YOUR_GOOGLE_OAUTH_CLIENT_SECRET";

    public static bool IsConfigured =>
        ApiKey             != "YOUR_FIREBASE_WEB_API_KEY"                              &&
        ProjectId          != "YOUR_FIREBASE_PROJECT_ID"                               &&
        GoogleClientId     != "YOUR_GOOGLE_OAUTH_CLIENT_ID.apps.googleusercontent.com" &&
        GoogleClientSecret != "YOUR_GOOGLE_OAUTH_CLIENT_SECRET";
}
