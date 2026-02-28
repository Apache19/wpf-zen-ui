// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Appearance;

namespace Wpf.Ui.Markup;

/// <summary>
/// Provides a dictionary implementation that contains <c>WPF UI</c> theme resources used by components and other elements of a WPF application.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;Application
///     xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"&gt;
///     &lt;Application.Resources&gt;
///         &lt;ResourceDictionary&gt;
///             &lt;ResourceDictionary.MergedDictionaries&gt;
///                 &lt;ui:ThemesDictionary Theme = "Dark" /&gt;
///             &lt;/ResourceDictionary.MergedDictionaries&gt;
///         &lt;/ResourceDictionary&gt;
///     &lt;/Application.Resources&gt;
/// &lt;/Application&gt;
/// </code>
/// </example>
[Localizability(LocalizationCategory.Ignore)]
[Ambient]
[UsableDuringInitialization(true)]
public class ThemeDictionary : ResourceDictionary
{
    /// <summary>
    /// Sets the default application theme.
    /// </summary>
    public UiThemeMode Theme
    {
        set => SetSourceBasedOnSelectedTheme(value);
    }

    public ThemeDictionary()
    {
        SetSourceBasedOnSelectedTheme(UiThemeMode.Light);
    }

    private void SetSourceBasedOnSelectedTheme(UiThemeMode? themeMode)
    {
        Source = new Uri(
            $"{ThemeManager.ThemeBaseUri}{(themeMode == UiThemeMode.Dark ? "Dark" : "Light")}.xaml",
            UriKind.Absolute
        );
    }
}