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
    public record ThemeItem(string Display, UiThemeMode ThemeMode);

    private bool _isInitialized = false;

    [ObservableProperty]
    private string _appVersion = string.Empty;

    [ObservableProperty]
    private UiThemeMode _currentTheme = UiThemeMode.Light;

    [ObservableProperty]
    private NavigationViewPaneDisplayMode _currentApplicationNavigationStyle =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    private ObservableCollection<ThemeItem> _themeList =
    [
        new("Light", UiThemeMode.Light),
        new("Dark", UiThemeMode.Dark),
        new("FollowSystem", UiThemeMode.Auto)
    ];

    public override void OnNavigatedTo()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    partial void OnCurrentThemeChanged(UiThemeMode oldValue, UiThemeMode newValue)
    {
        ThemeManager.ThemeMode = newValue;
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
        CurrentTheme = ThemeManager.ThemeMode;
        AppVersion = $"{GetAssemblyVersion()}";
        _isInitialized = true;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }
}