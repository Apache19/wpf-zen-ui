// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

public enum ThemeChangeSource
{
    Manual, // 用户手动设置
    System, // 系统触发
}

/// <summary>
/// Provides data for the theme changed event.
/// </summary>
public class ThemeChangedEventArgs : EventArgs
{
    public ThemeMode AppliedTheme { get; }

    public Color SystemAccentColor { get; }

    public ThemeChangeSource ChangeSource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThemeChangedEventArgs"/> class.
    /// </summary>
    /// <param name="currentApplicationTheme">The current application theme.</param>
    /// <param name="systemAccentColor">The system accent color.</param>
    public ThemeChangedEventArgs(ThemeMode appliedTheme, Color systemAccentColor)
    {
        CurrentApplicationTheme = currentApplicationTheme;
        SystemAccentColor       = systemAccentColor;
    }
}