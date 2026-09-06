// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.Services.Contracts;

/// <summary>Application dialog operations expressed only through immutable requests and typed results.</summary>
/// <remarks>
/// The implementation owns presentation-scoped view models and views and shares the navigation
/// scope's dialog host. Callers therefore do not depend on binding state or concrete WPF elements.
/// </remarks>
public interface IGalleryDialogService
{
    /// <summary>Shows the fully custom local login simulation.</summary>
    /// <param name="ct">Cancels the presentation.</param>
    /// <returns>The demo account result, or null on dismissal. No real authentication is performed.</returns>
    Task<LoginDialogResult?> ShowLoginAsync(CancellationToken ct = default);

    /// <summary>Shows a save confirmation and returns its completed decision.</summary>
    /// <param name="options">Immutable content and initial values for this presentation.</param>
    /// <param name="ct">Cancels the presentation.</param>
    /// <returns>The completed decision, or null when the user dismisses the dialog.</returns>
    Task<SaveWorkDialogResult?> ShowSaveWorkAsync(SaveWorkDialogOptions options, CancellationToken ct = default);

    /// <summary>Presents the terms and returns whether the user accepted them.</summary>
    /// <param name="options">Immutable terms and labels for this presentation.</param>
    /// <param name="ct">Cancels the presentation.</param>
    Task<bool> ShowTermsOfUseAsync(TermsOfUseDialogOptions options, CancellationToken ct = default);

    /// <summary>Shows a reusable message box and returns the role used to complete it.</summary>
    /// <param name="options">Immutable message, icon and action configuration.</param>
    /// <param name="ct">Cancels the presentation.</param>
    Task<MessageBoxDialogResult> ShowMessageBoxAsync(MessageBoxDialogOptions options, CancellationToken ct = default);
}
