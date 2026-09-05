// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Services.Contracts;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

/// <summary>Drives the dialog examples through application data and typed results.</summary>
/// <param name="dialogService">Application dialog operations for this navigation scope.</param>
public partial class ContentDialogViewModel(IGalleryDialogService dialogService) : ViewModel
{
    /// <summary>Gets or sets the description of the most recent save decision.</summary>
    [ObservableProperty]
    public partial string DialogResultText { get; private set; } = string.Empty;

    [RelayCommand]
    private async Task ShowDialogAsync()
    {
        var data   = new SaveWorkDialogData();
        var result = await dialogService.ShowSaveWorkAsync(data).ConfigureAwait(true);

        DialogResultText = result switch
        {
            SaveWorkChoice.Save    => "User saved their work",
            SaveWorkChoice.Discard => "User did not save their work",
            _                      => "User cancelled the dialog",
        };
    }

    [RelayCommand]
    private async Task ShowSignInContentDialogAsync()
    {
        _ = await dialogService.ShowTermsOfUseAsync(new TermsOfUseDialogData()).ConfigureAwait(true);
    }
}