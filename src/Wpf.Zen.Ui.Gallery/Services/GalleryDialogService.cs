// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Controls;
using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Services.Contracts;

namespace Wpf.Zen.Ui.Gallery.Services;

/// <summary>Maps Gallery dialog data to XAML views using the scoped content dialog presenter.</summary>
/// <param name="contentDialogService">Presenter attached to the current navigation scope.</param>
public sealed class GalleryDialogService(IContentDialogService contentDialogService) : IGalleryDialogService
{
    /// <inheritdoc />
    public async Task<SaveWorkChoice?> ShowSaveWorkAsync(SaveWorkDialogData data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        // Each request receives a fresh view: host ownership and focus state belong to one session.
        object? result = await contentDialogService.ShowAsync(new SaveWorkContentDialog { DataContext = data }, cancellationToken);
        return (SaveWorkChoice?)result;
    }

    /// <inheritdoc />
    public async Task<bool> ShowTermsOfUseAsync(TermsOfUseDialogData data, CancellationToken cancellationToken = default)
    {
        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        object? result = await contentDialogService.ShowAsync(new TermsOfUseContentDialog { DataContext = data }, cancellationToken);
        return result is true;
    }
}
