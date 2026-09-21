// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using UiMessageBox = Wpf.Zen.Ui.Controls.MessageBox;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

/// <summary>
/// Demonstrates native-compatible modal calls and per-button text overrides.
/// </summary>
public partial class MessageBoxViewModel : ViewModel
{
    /// <summary>
    /// Gets the result returned after the modal window closes.
    /// </summary>
    [ObservableProperty]
    public partial string ResultText { get; set; } = string.Empty;

    /// <summary>
    /// Displays the native WPF message box for comparison with the Fluent message box.
    /// </summary>
    [RelayCommand]
    private void OnOpenStandardMessageBox(object sender)
    {
        ResultText = System.Windows.MessageBox.Show("Something about to happen", "I can feel it").ToString();
    }

    /// <summary>
    /// Replaces Yes and No labels while retaining the localized Cancel label and WPF results.
    /// </summary>
    [RelayCommand]
    private void OnOpenCustomMessageBox(object sender)
    {
        var owner = Window.GetWindow((DependencyObject)sender);

        ResultText = UiMessageBox.Show(
            owner,
            "Save changes before closing?",
            "Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Cancel,
            MessageBoxOptions.None,
            new MessageBoxButtonText
            {
                Yes = "_Save",
                No  = "_Don't save"
            }
        ).ToString();
    }
}