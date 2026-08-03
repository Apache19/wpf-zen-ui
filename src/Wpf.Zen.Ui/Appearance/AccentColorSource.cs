// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Appearance;

/// <summary>
/// Specifies where <see cref="ThemeManager"/> obtains the base accent color from which
/// theme-specific accent resources are generated.
/// </summary>
public enum AccentColorSource
{
    /// <summary>
    /// Uses the accent color reported by Windows through <c>UISettings.GetColorValue(Accent)</c>.
    /// If that Windows Runtime API is unavailable, as it is on Windows 7 and Windows 8.1,
    /// the built-in default accent color is used.
    /// </summary>
    System,

    /// <summary>
    /// Uses <see cref="ThemeManager.CustomAccentColor"/> as the base accent color.
    /// The color's alpha channel is ignored because accent palette colors are always opaque.
    /// </summary>
    Custom,

    /// <summary>
    /// Uses the library's built-in blue accent color, independently of the Windows setting.
    /// </summary>
    Default,
}
