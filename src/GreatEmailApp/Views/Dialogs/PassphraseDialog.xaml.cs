// FILE: src/GreatEmailApp/Views/Dialogs/PassphraseDialog.xaml.cs
// Created: 2026-05-07 | Revised: 2026-05-07 | Rev: 1
// Changed by: Claude Opus 4.7 on behalf of James Reed

using System.Windows;

namespace GreatEmailApp.Views.Dialogs;

public enum PassphraseDialogMode
{
    Setup,   // two fields, must match, min length enforced
    Unlock,  // one field
}

public partial class PassphraseDialog : Window
{
    private const int MinSetupLength = 8;

    public PassphraseDialogMode Mode { get; private set; } = PassphraseDialogMode.Setup;

    /// <summary>The passphrase the user typed. Null if the dialog was cancelled.</summary>
    public string? Passphrase { get; private set; }

    public PassphraseDialog(PassphraseDialogMode mode)
    {
        InitializeComponent();
        Mode = mode;
        ApplyMode();
        Loaded += (_, _) => P1.Focus();
    }

    private void ApplyMode()
    {
        if (Mode == PassphraseDialogMode.Setup)
        {
            TitleText.Text = "Set up password sync";
            SubText.Text =
                "Choose a master passphrase. Your IMAP passwords will be encrypted with it " +
                "before being uploaded. The cloud only ever sees ciphertext — losing the " +
                "passphrase means you'll need to re-enter passwords on each PC.";
            ConfirmRow.Visibility = Visibility.Visible;
            OkButton.Content = "Set passphrase";
        }
        else
        {
            TitleText.Text = "Unlock password sync";
            SubText.Text =
                "Enter the master passphrase you set on your other PC. Once unlocked, " +
                "your IMAP passwords will be restored to this PC's Credential Manager.";
            ConfirmRow.Visibility = Visibility.Collapsed;
            OkButton.Content = "Unlock";
        }
    }

    private void OnAnyChanged(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var p1 = P1.Password;
        if (string.IsNullOrEmpty(p1))
        {
            ErrorText.Text = "Passphrase is required.";
            return;
        }

        if (Mode == PassphraseDialogMode.Setup)
        {
            if (p1.Length < MinSetupLength)
            {
                ErrorText.Text = $"Passphrase must be at least {MinSetupLength} characters.";
                return;
            }
            if (p1 != P2.Password)
            {
                ErrorText.Text = "The two passphrases don't match.";
                return;
            }
        }

        Passphrase = p1;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Passphrase = null;
        DialogResult = false;
        Close();
    }
}
