// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace Wpf.Ui.Gallery.ViewModels.Pages;

public sealed partial class SettingsViewModel(INavigationService navigationService) : ViewModel
{
    public enum AppThemeMode
    {
        Light,
        Dark,
        FollowSystem,
    }

    public record ThemeItem(string Display, AppThemeMode AppTheme);

    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private AppThemeMode _currentApplicationTheme = AppThemeMode.Light;

    [ObservableProperty]
    private NavigationViewPaneDisplayMode _currentApplicationNavigationStyle =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    private ObservableCollection<ThemeItem> _themeList =
    [
        new("Light", AppThemeMode.Light),
        new("Dark", AppThemeMode.Dark),
        new("FollowSystem", AppThemeMode.FollowSystem)
    ];

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    partial void OnCurrentApplicationThemeChanged(AppThemeMode oldValue, AppThemeMode newValue)
    {
        switch (newValue)
        {
            case AppThemeMode.Light:
                ThemeManager.ThemeMode = UIThemeMode.Light;
                break;
            case AppThemeMode.Dark:
                ThemeManager.ThemeMode = UIThemeMode.Dark;
                break;
            case AppThemeMode.FollowSystem:
                ThemeManager.FollowSystemTheme = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(newValue), newValue, null);
        }
    }

    partial void OnCurrentApplicationNavigationStyleChanged(
        NavigationViewPaneDisplayMode oldValue,
        NavigationViewPaneDisplayMode newValue
    )
    {
        _ = navigationService.SetPaneDisplayMode(newValue);
    }

    private void InitializeViewModel()
    {
        if (ThemeManager.FollowSystemTheme)
        {
            CurrentApplicationTheme = AppThemeMode.FollowSystem;
        }
        else if (ThemeManager.ThemeMode == UIThemeMode.Dark)
        {
            CurrentApplicationTheme = AppThemeMode.Dark;
        }
        else
        {
            CurrentApplicationTheme = AppThemeMode.Light;
        }

        AppVersion = $"{GetAssemblyVersion()}";

        _isInitialized = true;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}