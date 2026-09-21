// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Services.Contracts;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

/// <summary>
/// Drives the dialog examples through immutable requests and typed results.
/// </summary>
/// <param name="dialogService">
/// Application dialog operations for this navigation scope.
/// </param>
public partial class ContentDialogViewModel(IGalleryDialogService dialogService) : ViewModel
{
    /// <summary>
    /// Gets the latest result of the custom login simulation.
    /// </summary>
    [ObservableProperty]
    public partial string LoginResultText { get; private set; } = string.Empty;

    [RelayCommand]
    private async Task ShowLoginAsync()
    {
        var result = await dialogService.ShowLoginAsync().ConfigureAwait(true);
        LoginResultText = result is null
            ? "Demo sign-in canceled."
            : $"Demo signed in as {result.UserName} (no connection).";
    }

    /// <summary>
    /// Gets the description of the most recent save decision.
    /// </summary>
    [ObservableProperty]
    public partial string DialogResultText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the save option returned with the most recent completed save decision.
    /// </summary>
    [ObservableProperty]
    public partial string SaveWorkDetailsText { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the description of the most recent ContentDialog prompt result.
    /// </summary>
    [ObservableProperty]
    public partial string PromptResultText { get; private set; } = string.Empty;

    [RelayCommand]
    private async Task ShowSaveWorkDialogAsync()
    {
        var options = new SaveWorkDialogOptions();
        var result  = await dialogService.ShowSaveWorkAsync(options).ConfigureAwait(true);

        DialogResultText = result?.Choice switch
        {
            SaveWorkChoice.Save    => "User saved their work",
            SaveWorkChoice.Discard => "User did not save their work",
            _                      => "User cancelled the dialog",
        };

        SaveWorkDetailsText = result?.Choice == SaveWorkChoice.Save
            ? $"Upload to cloud: {(result.UploadToCloud ? "Yes" : "No")}"
            : string.Empty;
    }

    [RelayCommand]
    private async Task ShowTermsOfUseDialogAsync()
    {
        var options = new TermsOfUseDialogOptions(
            "These sample terms are supplied as immutable dialog options. Accepting them updates "
            + "only the presentation-scoped view model owned by the dialog service."
        )
        {
            MaxWidth = 480,
        };

        _ = await dialogService.ShowTermsOfUseAsync(options).ConfigureAwait(true);
    }

    [RelayCommand]
    private async Task ShowInformationPromptAsync()
    {
        var options = new PromptDialogOptions(
            "Changes saved",
            "Your gallery preferences were saved successfully.",
            new PromptDialogButtonOptions("Got it")
            {
                Appearance = ControlAppearance.Accent,
                IsDefault  = true,
            }
        )
        {
            Icon = SymbolRegular.Info24,
        };

        PromptDialogResult result = await dialogService.ShowPromptAsync(options).ConfigureAwait(true);
        PromptResultText = DescribePromptResult("Information", result);
    }

    [RelayCommand]
    private async Task ShowConfirmationPromptAsync()
    {
        var options = new PromptDialogOptions(
            "Continue editing?",
            "You can continue now, postpone the change, or cancel this decision.",
            new PromptDialogButtonOptions("Continue")
            {
                Appearance = ControlAppearance.Accent,
                IsDefault  = true,
            }
        )
        {
            Icon            = SymbolRegular.Question24,
            MaxWidth        = 440,
            SecondaryButton = new PromptDialogButtonOptions("Not now"),
            CloseButton     = new PromptDialogButtonOptions("Cancel"),
        };

        PromptDialogResult result = await dialogService.ShowPromptAsync(options).ConfigureAwait(true);
        PromptResultText = DescribePromptResult("Confirmation", result);
    }

    [RelayCommand]
    private async Task ShowDestructivePromptAsync()
    {
        var options = new PromptDialogOptions(
            "Delete this item?",
            "This example demonstrates a destructive action that is intentionally not the default button.",
            new PromptDialogButtonOptions("Delete")
            {
                Appearance = ControlAppearance.Danger,
            }
        )
        {
            Icon        = SymbolRegular.Delete24,
            MaxWidth    = 400,
            CloseButton = new PromptDialogButtonOptions("Cancel"),
        };

        PromptDialogResult result = await dialogService.ShowPromptAsync(options).ConfigureAwait(true);
        PromptResultText = DescribePromptResult("Destructive", result);
    }

    private static string DescribePromptResult(string example, PromptDialogResult result)
    {
        return $"{example} dialog result: {result}";
    }
}
