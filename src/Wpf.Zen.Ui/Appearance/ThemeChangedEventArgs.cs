// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Appearance;

/// <summary>
/// Identifies the origin of a theme refresh.
/// </summary>
public enum ThemeChangeSource
{
    /// <summary>
    /// The refresh was caused by application configuration.
    /// </summary>
    Manual,

    /// <summary>
    /// The refresh was caused by an operating-system setting change.
    /// </summary>
    System,
}

/// <summary>
/// Provides data for the theme changed event.
/// </summary>
public sealed class ThemeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the concrete Light or Dark variant effective after the refresh.
    /// </summary>
    public ThemeVariant EffectiveTheme { get; }

    /// <summary>
    /// Gets the origin of the refresh.
    /// </summary>
    public ThemeChangeSource ChangeSource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="effectiveTheme">The resolved Light or Dark variant.</param>
    /// <param name="changeSource">The origin of the refresh.</param>
    public ThemeChangedEventArgs(ThemeVariant effectiveTheme, ThemeChangeSource changeSource)
    {
        EffectiveTheme = effectiveTheme;
        ChangeSource = changeSource;
    }
}
