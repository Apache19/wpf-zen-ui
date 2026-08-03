// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Appearance;

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
    public ApplicationTheme AppliedTheme { get; }

    public ThemeChangeSource ChangeSource { get; }

    public ThemeChangedEventArgs(ApplicationTheme appliedTheme, ThemeChangeSource changeSource)
    {
        AppliedTheme = appliedTheme;
        ChangeSource = changeSource;
    }
}