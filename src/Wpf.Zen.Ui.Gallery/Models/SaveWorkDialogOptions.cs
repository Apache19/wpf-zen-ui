// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes the initial presentation and selection supplied to the save-work dialog.
/// </summary>
/// <remarks>
/// This immutable request contains no binding state. The dialog service copies it into a
/// presentation-scoped view model so callers do not own or observe the dialog's transient state.
/// </remarks>
public sealed record SaveWorkDialogOptions
{
    /// <summary>
    /// Gets the optional dialog width limit in device-independent units.
    /// The default is null, which preserves the view's default maximum width.
    /// </summary>
    /// <remarks>
    /// Limits long text while allowing shorter content to use less space. The view's
    /// minimum width still takes precedence, so choose a limit at least as large as that minimum.
    /// Positive infinity removes the width limit; negative values and NaN are rejected by WPF
    /// when the service creates the view.
    /// </remarks>
    public double? MaxWidth { get; init; }

    /// <summary>Gets the heading displayed by the dialog.</summary>
    public string Title { get; init; } = "Save your work?";

    /// <summary>Gets the explanatory message displayed above the save option.</summary>
    public string Message { get; init; } = "Choose whether to save your changes before continuing.";

    /// <summary>Gets the label displayed beside the cloud-upload option.</summary>
    public string UploadToCloudText { get; init; } = "Upload your content to the cloud.";

    /// <summary>
    /// Gets a value indicating whether the cloud-upload option is selected when the dialog opens.
    /// </summary>
    public bool UploadToCloud { get; init; }
}
