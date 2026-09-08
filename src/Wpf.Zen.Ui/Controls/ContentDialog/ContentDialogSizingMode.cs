// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies which dimensions of a <see cref="ContentDialog"/> should consume the space available
/// inside its <see cref="ContentDialogHost"/>.
/// </summary>
/// <remarks>
/// The host's dialog-surface padding remains outside the stretched dimensions, so every mode keeps
/// the theme-defined distance from the host edges. An explicit <see cref="FrameworkElement.Width"/>,
/// <see cref="FrameworkElement.Height"/>, <see cref="FrameworkElement.MaxWidth"/>, or
/// <see cref="FrameworkElement.MaxHeight"/> on the dialog continues to constrain the result.
/// </remarks>
public enum ContentDialogSizingMode
{
    /// <summary>
    /// Uses the dialog's desired or explicitly configured size and centers it in the host, subject
    /// to the theme's default maximum width and height. This is the default mode.
    /// </summary>
    Auto,

    /// <summary>
    /// Stretches the dialog horizontally and vertically to the host's available dialog area.
    /// This mode is appropriate for large forms and page-like dialog content.
    /// </summary>
    Stretch,

    /// <summary>
    /// Stretches the dialog horizontally while retaining automatic, centered height and the theme's
    /// default maximum height.
    /// </summary>
    StretchWidth,

    /// <summary>
    /// Stretches the dialog vertically while retaining automatic, centered width and the theme's
    /// default maximum width.
    /// </summary>
    StretchHeight,
}
