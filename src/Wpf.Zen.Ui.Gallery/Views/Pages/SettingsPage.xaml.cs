// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages;

namespace Wpf.Zen.Ui.Gallery.Views.Pages;

public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        InitializeComponent();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Changed += OnThemeChanged;
        ViewModel.UpdateCurrentTheme();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ThemeManager.Changed -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, Appearance.ThemeChangedEventArgs e)
    {
        ViewModel.UpdateCurrentTheme();
    }
}
