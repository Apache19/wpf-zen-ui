// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

/// <summary>
/// Represents the resolved application theme.
/// </summary>
public enum ApplicationTheme
{
    /// <summary>
    /// No theme dictionary is merged. The application uses the library default theme
    /// provided by <c>ThemeInfo</c> and <c>Generic.xaml</c>.
    /// </summary>
    Default,

    /// <summary>
    /// Light application theme.
    /// </summary>
    Light,

    /// <summary>
    /// Dark application theme.
    /// </summary>
    Dark,
}