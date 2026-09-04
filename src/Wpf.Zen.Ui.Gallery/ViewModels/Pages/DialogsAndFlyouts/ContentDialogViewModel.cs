// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Extensions;
using Wpf.Zen.Ui.Gallery.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

public partial class ContentDialogViewModel(IContentDialogService contentDialogService) : ViewModel
{
    [ObservableProperty]
    public partial string DialogResultText { get; set; } = string.Empty;

    [RelayCommand]
    private async Task OnShowDialog(DataTemplate contentTemplate)
    {
        ContentDialogResult result = await contentDialogService.ShowAsync(
            new ContentDialog()
            {
                Title = "Save your work?",
                Content = new object(),
                ContentTemplate = contentTemplate,
                PrimaryAction = new ContentDialogAction { Content = "Save", Appearance = ControlAppearance.Accent },
                SecondaryAction = new ContentDialogAction { Content = "Don't Save" },
                TertiaryAction = new ContentDialogAction { Content = "Cancel" },
            },
            CancellationToken.None
        );

        DialogResultText = result switch
        {
            ContentDialogResult.Primary => "User saved their work",
            ContentDialogResult.Secondary => "User did not save their work",
            _ => "User cancelled the dialog",
        };
    }

    [RelayCommand]
    private async Task OnShowSignInContentDialog()
    {
        var termsOfUseContentDialog = new TermsOfUseContentDialog();
        _ = await contentDialogService.ShowAsync(termsOfUseContentDialog, CancellationToken.None);
    }
}
