// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Demo.Mvvm.ViewModels;

public partial class SettingsViewModel : ViewModel
{
    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private bool _isLightTheme;

    partial void OnIsLightThemeChanged(bool value)
    {
        if (value)
        {
            ThemeManager.ThemeMode = UiThemeMode.Light;
        }
    }

    [ObservableProperty]
    private bool _isDarkTheme;

    partial void OnIsDarkThemeChanged(bool value)
    {
        if (value)
        {
            ThemeManager.ThemeMode = UiThemeMode.Dark;
        }
    }

    [ObservableProperty]
    private bool _isSystemTheme;

    partial void OnIsSystemThemeChanged(bool value)
    {
        if (value)
        {
            ThemeManager.ThemeMode = UiThemeMode.System;
        }
    }

    public override void OnNavigationEntered()
    {
        InitializeViewModel();
    }

    public void UpdateCurrentTheme()
    {
        switch (ThemeManager.ThemeMode)
        {
            case UiThemeMode.Default:
            case UiThemeMode.Light:
                IsLightTheme = true;
                break;

            case UiThemeMode.Dark:
                IsDarkTheme = true;
                break;

            case UiThemeMode.System:
                IsSystemTheme = true;
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private void InitializeViewModel()
    {
        AppVersion = $"Wpf.Zen.Ui.Demo.Mvvm - {GetAssemblyVersion()}";
        UpdateCurrentTheme();
    }

    private static string GetAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? string.Empty;
    }
}
