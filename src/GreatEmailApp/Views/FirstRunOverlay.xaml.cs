// FILE: src/GreatEmailApp/Views/FirstRunOverlay.xaml.cs
// Created: 2026-04-30 | Rev: 1
// Changed by: Claude Sonnet 4.6 on behalf of James Reed

using System.Windows.Controls;
using GreatEmailApp.ViewModels;

namespace GreatEmailApp.Views;

public partial class FirstRunOverlay : UserControl
{
    private readonly SignInViewModel _vm;

    public event EventHandler? Dismissed;

    public FirstRunOverlay()
    {
        InitializeComponent();
        _vm = new SignInViewModel(App.FirebaseAuth);
        _vm.Dismissed += (_, _) => Dismissed?.Invoke(this, EventArgs.Empty);
        DataContext = _vm;
    }
}
