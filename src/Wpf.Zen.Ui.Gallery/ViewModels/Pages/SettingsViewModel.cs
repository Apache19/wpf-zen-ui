// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Extensions;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages;

public sealed partial class SettingsViewModel : ViewModel
{
    public record ThemeItem(string Display, UiThemeMode ThemeMode);

    private readonly INavigationService? _navigationService;

    private bool _isInitialized = false;

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial UiThemeMode CurrentTheme { get; set; } = UiThemeMode.Light;

    [ObservableProperty]
    public partial bool UseSystemAccent { get; set; } = true;

    [ObservableProperty]
    public partial NavigationViewPaneDisplayMode CurrentApplicationNavigationStyle { get; set; } =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    public partial ObservableCollection<ThemeItem> ThemeList { get; set; } =
    [
        new("Light", UiThemeMode.Light),
        new("Dark", UiThemeMode.Dark),
        new("System", UiThemeMode.System)
    ];

    public SettingsViewModel()
    {
    }

    public SettingsViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public override void OnNavigationEntered()
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

    partial void OnUseSystemAccentChanged(bool value)
    {
        ThemeManager.AccentSource = value
            ? AccentColorSource.System
            : AccentColorSource.Default;
    }

    partial void OnCurrentApplicationNavigationStyleChanged(
        NavigationViewPaneDisplayMode oldValue,
        NavigationViewPaneDisplayMode newValue)
    {
        _ = _navigationService?.SetPaneDisplayMode(newValue);
    }

    private void InitializeViewModel()
    {
        AppVersion = $"{GetAssemblyVersion()}";
        UpdateCurrentTheme();

        _isInitialized = true;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }

    public void UpdateCurrentTheme()
    {
        var mode = ThemeManager.ThemeMode;

        // The default theme of the library is Light.
        CurrentTheme = mode == UiThemeMode.Default ? UiThemeMode.Light : mode;
        UseSystemAccent = ThemeManager.AccentSource == AccentColorSource.System;
    }
}
