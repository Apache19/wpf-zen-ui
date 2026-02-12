// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

/// <summary>
/// Provides data for the theme changed event.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current application theme.
    /// </summary>
    public ApplicationTheme CurrentApplicationTheme { get; }

    /// <summary>
    /// Gets the system accent color.
    /// </summary>
    public Color SystemAccentColor { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="currentApplicationTheme">The current application theme.</param>
    /// <param name="systemAccentColor">The system accent color.</param>
    public ThemeChangedEventArgs(ApplicationTheme currentApplicationTheme, Color systemAccentColor)
    {
        CurrentApplicationTheme = currentApplicationTheme;
        SystemAccentColor       = systemAccentColor;
    }
}