// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages;

public sealed partial class SettingsViewModel : ViewModel
{
    public record ThemeItem(string Display, ThemePreference Preference);

    private readonly MainWindowViewModel? _mainWindowViewModel;

    private bool _isInitialized = false;

    [ObservableProperty]
    public partial string AppVersion { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ThemePreference SelectedPreference { get; set; } = ThemePreference.Light;

    [ObservableProperty]
    public partial bool UseSystemAccent { get; set; } = true;

    [ObservableProperty]
    public partial NavigationViewPaneDisplayMode CurrentApplicationNavigationStyle { get; set; } =
        NavigationViewPaneDisplayMode.Left;

    [ObservableProperty]
    public partial ObservableCollection<ThemeItem> ThemeList { get; set; } =
    [
        new("Light", ThemePreference.Light),
        new("Dark", ThemePreference.Dark),
        new("System", ThemePreference.System)
    ];

    public SettingsViewModel()
    {
    }

    public SettingsViewModel(MainWindowViewModel mainWindowViewModel)
    {
        _mainWindowViewModel = mainWindowViewModel;
        CurrentApplicationNavigationStyle = mainWindowViewModel.NavigationPaneDisplayMode;
    }

    public override void OnNavigationEntered()
    {
        if (!_isInitialized)
        {
            InitializeViewModel();
        }
    }

    partial void OnSelectedPreferenceChanged(ThemePreference oldValue, ThemePreference newValue)
    {
        ThemeManager.Preference = newValue;
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
        if (_mainWindowViewModel is not null)
        {
            _mainWindowViewModel.NavigationPaneDisplayMode = newValue;
        }
    }

    private void InitializeViewModel()
    {
        AppVersion = $"{GetAssemblyVersion()}";
        UpdateSelectedPreference();

        _isInitialized = true;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? string.Empty;
    }

    public void UpdateSelectedPreference()
    {
        ThemePreference preference = ThemeManager.Preference;

        // The default theme of the library is Light.
        SelectedPreference = preference == ThemePreference.Default ? ThemePreference.Light : preference;
        UseSystemAccent = ThemeManager.AccentSource == AccentColorSource.System;
    }
}
