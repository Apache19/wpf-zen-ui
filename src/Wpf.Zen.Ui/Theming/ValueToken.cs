// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Identifies non-color theme values such as font sizes, corner radii, thicknesses, and padding.
/// </summary>
public enum ValueToken
{
    None,
    /// <summary>
    /// The default control minimum width, expressed in device-independent units; defaults to 32.
    /// </summary>
    DefaultControlMinWidth,
    DefaultControlMinHeight,
    DefaultIconFontSize,
    DefaultThemeFontSize,
    ControlCornerRadius,
    OverlayCornerRadius,
    PopupCornerRadius,
    TextControlThemePadding,
    ButtonThemePadding,
}
