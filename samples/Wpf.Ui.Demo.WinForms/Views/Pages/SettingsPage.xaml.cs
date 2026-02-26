// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using Wpf.Ui.Appearance;

namespace Wpf.Ui.Demo.Simple.Views.Pages;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage
{
    private bool _isSuppressed;

    public SettingsPage()
    {
#error 右键菜单弹出以后，进入设置界面会报错
        InitializeComponent();
        AppVersionTextBlock.Text = $"WPF UI - Simple Demo - {GetAssemblyVersion()}";

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isSuppressed = true;

        if (ThemeManager.ThemeMode == UIThemeMode.Dark)
        {
            DarkThemeRadioButton.IsChecked = true;
        }
        else
        {
            LightThemeRadioButton.IsChecked = true;
        }

        _isSuppressed = false;
    }
    
    private void OnLightThemeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (_isSuppressed)
        {
            return;
        }

        ThemeManager.ThemeMode = UIThemeMode.Light;
    }

    private void OnDarkThemeRadioButtonChecked(object sender, RoutedEventArgs e)
    {
        if (_isSuppressed)
        {
            return;
        }

        ThemeManager.ThemeMode = UIThemeMode.Dark;
    }

    private static string GetAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? string.Empty;
    }
}