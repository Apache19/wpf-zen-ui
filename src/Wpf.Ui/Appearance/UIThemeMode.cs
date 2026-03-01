// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

/// <summary>
/// Specifies the theme mode for the application or an individual element.
/// </summary>
public enum UiThemeMode
{
    /// <summary>
    /// Indicates that no explicit theme mode is specified.
    ///
    /// When used as <see cref="Wpf.Ui.ThemeManager.ThemeMode"/>,
    /// the application will not explicitly apply a Light or Dark theme
    /// and will fall back to the library's default theme defined in Generic.xaml.
    ///
    /// When used as the per-element <c>Theme.Requested</c> attached property,
    /// any previously applied theme resources on the target element are removed,
    /// allowing the element to return to the normal resource resolution behavior
    /// (for example, inheriting from a parent or the global theme).
    /// </summary>
    Default,

    /// <summary>
    /// Forces the Light theme mode.
    /// </summary>
    Light,

    /// <summary>
    /// Forces the Dark theme mode.
    /// </summary>
    Dark,

    /// <summary>
    /// Automatically resolves to Light or Dark based on the current system setting.
    /// </summary>
    System,
}
