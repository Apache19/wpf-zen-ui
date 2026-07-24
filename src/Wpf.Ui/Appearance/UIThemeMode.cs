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
    /// Indicates that no explicit Light or Dark mode is selected.
    ///
    /// When used as <see cref="Wpf.Ui.ThemeManager.ThemeMode"/>,
    /// the application uses the Light-compatible palette defined by Generic.xaml while retaining
    /// <see cref="ApplicationTheme.Default"/> as its reported global theme. The manager may still
    /// merge a Light-compatible global layer for accent, native-control, and additional resources.
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
