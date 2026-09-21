// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Services.Contracts;
using Wpf.Zen.Ui.Gallery.ViewModels.Dialogs;
using Wpf.Zen.Ui.Gallery.Views.Dialogs;

namespace Wpf.Zen.Ui.Gallery.Services;

/// <summary>
/// Maps immutable Gallery dialog requests to presentation-scoped view models and XAML views.
/// </summary>
/// <param name="cds">Presenter attached to the current navigation scope.</param>
public sealed class GalleryDialogService(IContentDialogService cds) : IGalleryDialogService
{
    /// <inheritdoc />
    public async Task<LoginDialogResult?> ShowLoginAsync(CancellationToken ct = default)
    {
        var viewModel = new LoginDialogViewModel();
        var view      = new LoginDialog { DataContext = viewModel };

        // Adapt the view model's completion signal here so it never needs a reference to WPF controls.
        viewModel.SignInCompleted += Complete;
        try
        {
            return (LoginDialogResult?)await cds.ShowAsync(view, ct).ConfigureAwait(true);
        }
        finally
        {
            // Also clear bindings after cancellation or host teardown, not only successful sign-in.
            viewModel.SignInCompleted -= Complete;
            viewModel.Password = string.Empty;
        }

        void Complete(LoginDialogResult result)
        {
            view.Close(result);
        }
    }

    /// <inheritdoc />
    public async Task<SaveWorkDialogResult?> ShowSaveWorkAsync(
        SaveWorkDialogOptions options,
        CancellationToken ct = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var viewModel = new SaveWorkDialogViewModel(options);
        var view      = new SaveWorkDialog { DataContext = viewModel };

        if (options.MaxWidth is { } maxWidth)
        {
            view.MaxWidth = maxWidth;
        }

        // Both the view and its mutable binding state belong to one host session. Only immutable
        // input and a completed business result cross this service boundary.
        var result = await cds.ShowAsync(view, ct).ConfigureAwait(true);

        return result is SaveWorkChoice choice
            ? new SaveWorkDialogResult(choice, viewModel.UploadToCloud)
            : null;
    }

    /// <inheritdoc />
    public async Task<bool> ShowTermsOfUseAsync(
        TermsOfUseDialogOptions options,
        CancellationToken ct = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var viewModel = new TermsOfUseDialogViewModel(options);
        var view      = new TermsOfUseDialog { DataContext = viewModel };

        if (options.MaxWidth is { } maxWidth)
        {
            view.MaxWidth = maxWidth;
        }

        object? result = await cds.ShowAsync(view, ct).ConfigureAwait(true);
        return result is true;
    }

    /// <inheritdoc />
    public async Task<PromptDialogResult> ShowPromptAsync(
        PromptDialogOptions options,
        CancellationToken ct = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var viewModel = new PromptDialogViewModel(options);
        var view      = new PromptDialog { DataContext = viewModel };

        if (options.MaxWidth is { } maxWidth)
        {
            view.MaxWidth = maxWidth;
        }

        object? result = await cds.ShowAsync(view, ct).ConfigureAwait(true);

        // A null host result represents top-right dismissal or host teardown. Cancellation remains
        // distinct because ContentDialogHost propagates it by cancelling the returned task.
        return result is PromptDialogResult typedResult
            ? typedResult
            : PromptDialogResult.Dismissed;
    }
}
