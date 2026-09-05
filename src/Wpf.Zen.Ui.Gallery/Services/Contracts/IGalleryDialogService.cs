// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.Services.Contracts;

/// <summary>Application dialog operations expressed only in data and business results.</summary>
/// <remarks>The implementation owns view creation and shares the navigation scope's presentation host.</remarks>
public interface IGalleryDialogService
{
    /// <summary>Edits save options and returns the chosen action, or null when dismissed.</summary>
    /// <param name="data">Options for this presentation; bindings update this instance.</param>
    /// <param name="cancellationToken">Cancels the presentation.</param>
    Task<SaveWorkChoice?> ShowSaveWorkAsync(SaveWorkDialogData data, CancellationToken cancellationToken = default);

    /// <summary>Presents the terms and returns whether the user accepted them.</summary>
    /// <param name="data">Agreement and validation state for this presentation.</param>
    /// <param name="cancellationToken">Cancels the presentation.</param>
    Task<bool> ShowTermsOfUseAsync(TermsOfUseDialogData data, CancellationToken cancellationToken = default);
}
