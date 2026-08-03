// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.Demo.Simple.Views.Pages;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage
{
    private bool _suppressThemeComboBoxChangedEvent = false;

    public SettingsPage()
    {
        InitializeComponent();
        AppVersionTextBlock.Text = $"WPF Zen UI - Simple Demo - {GetAssemblyVersion()}";

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _suppressThemeComboBoxChangedEvent = true;

        ThemeComboBox.SelectedIndex = ThemeManager.ThemeMode switch
        {
            UiThemeMode.Light   => 0,
            UiThemeMode.Dark    => 1,
            UiThemeMode.Default => 0, // Default is Light,
            UiThemeMode.System  => 2,
            _                   => 0
        };

        _suppressThemeComboBoxChangedEvent = false;
    }

    private static string GetAssemblyVersion()
    {
        return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? string.Empty;
    }

    private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeComboBoxChangedEvent)
        {
            return;
        }

        if (ThemeComboBox.SelectedItem is not ComboBoxItem selectedItem)
        {
            return;
        }

        ThemeManager.ThemeMode = (selectedItem.Content as string) switch
        {
            "Light" => UiThemeMode.Light,
            "Dark"  => UiThemeMode.Dark,
            _       => UiThemeMode.System,
        };
    }
}