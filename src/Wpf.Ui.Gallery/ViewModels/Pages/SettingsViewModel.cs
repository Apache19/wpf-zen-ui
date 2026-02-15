// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;
using ThemeMode = Wpf.Ui.Appearance.ThemeMode;

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
                ThemeManager.ThemeMode = ThemeMode.Light;
                break;
            case AppThemeMode.Dark:
                ThemeManager.ThemeMode = ThemeMode.Dark;
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
        // todo 根据主题管理器状态设置不同的值，但不触发主题更改
        if (ThemeManager.FollowSystemTheme)
        {
            CurrentApplicationTheme = AppThemeMode.FollowSystem;
        }
        else if (ThemeManager.ThemeMode == ThemeMode.Dark)
        {
            CurrentApplicationTheme = AppThemeMode.Dark;
        }
        else
        {
            CurrentApplicationTheme = AppThemeMode.Light;
        }

        AppVersion = $"{GetAssemblyVersion()}";

        WeakEventManager<ThemeManager, ThemeChangedEventArgs>
           .AddHandler(
                ThemeManager.Instance,
                nameof(ThemeManager.Changed),
                OnThemeChanged
            );

        _isInitialized = true;
    }

    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        // todo:当系统触发更改-设置当前选择-触发主题管理器的主题设置-造成错位
        // Update the theme if it has been changed elsewhere than in the settings.
        if (CurrentApplicationTheme != e.AppliedTheme)
        {
            CurrentApplicationTheme = e.AppliedTheme;
        }
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}