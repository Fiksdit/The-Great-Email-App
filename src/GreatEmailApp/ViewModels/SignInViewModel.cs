// FILE: src/GreatEmailApp/ViewModels/SignInViewModel.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed
// Backs the first-run sign-in overlay (P0-13).

using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GreatEmailApp.Core.Config;
using GreatEmailApp.Core.Services;

namespace GreatEmailApp.ViewModels;

public partial class SignInViewModel : ObservableObject
{
    private readonly IFirebaseAuthService _auth;

    [ObservableProperty] private bool   isBusy;
    [ObservableProperty] private string statusMessage = "";

    /// <summary>Raised when the overlay should be hidden (sign-in succeeded or skipped).</summary>
    public event EventHandler? Dismissed;

    public SignInViewModel(IFirebaseAuthService auth) => _auth = auth;

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (!FirebaseConfig.IsConfigured)
        {
            StatusMessage = "Firebase is not configured. Fill in FirebaseConfig.cs.";
            return;
        }

        IsBusy        = true;
        StatusMessage = "Opening browser — sign in with your Google account…";
        try
        {
            var user = await _auth.SignInWithGoogleAsync();
            if (user is not null)
            {
                App.Settings.SyncEnabled   = true;
                App.Settings.SignedInEmail = user.Email;
                // Dismiss() calls PersistSettings which fires the Firestore push.
                Dismiss();
            }
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Sign-in cancelled.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Sign-in failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Skip() => Dismiss();

    private void Dismiss()
    {
        App.Settings.HasShownFirstRun = true;
        App.PersistSettings();
        Dismissed?.Invoke(this, EventArgs.Empty);
    }
}
