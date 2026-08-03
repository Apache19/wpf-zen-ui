// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Demo.Simple.Views.Pages;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage
{
    public SettingsPage()
    {
        InitializeComponent();

        AppVersionTextBlock.Text = $"WPF Zen UI - Simple Demo - {GetAssemblyVersion()}";

        switch (ThemeManager.ThemeMode)
        {
            case UiThemeMode.Light:
            case UiThemeMode.Default:
                DarkThemeRadioButton.IsChecked = true;
                break;

            case UiThemeMode.Dark:
                LightThemeRadioButton.IsChecked = true;
                break;

            case UiThemeMode.System:
                SysThemeRadioButton.IsChecked = true;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void OnLightThemeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeMode = UiThemeMode.Light;
    }

    private void OnDarkThemeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeMode = UiThemeMode.Dark;
    }

    private void OnSysThemeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        ThemeManager.ThemeMode = UiThemeMode.System;
    }

    private static string GetAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? string.Empty;
    }
}
