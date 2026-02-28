// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace Wpf.Ui.Demo.Simple.Views.Pages;

/// <summary>
/// Interaction logic for SettingsPage.xaml
/// </summary>
public partial class SettingsPage
{
    private bool _suppressThemeComboBoxChangedEvent = false;

    public SettingsPage()
    {
        InitializeComponent();
        AppVersionTextBlock.Text = $"WPF UI - Simple Demo - {GetAssemblyVersion()}";

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _suppressThemeComboBoxChangedEvent = true;

        ThemeComboBox.SelectedItem = ThemeManager.ThemeMode switch
        {
            UiThemeMode.Auto  => "System",
            UiThemeMode.Light => "Light",
            UiThemeMode.Dark  => "Dark",
            _                 => throw new ArgumentOutOfRangeException()
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

        ThemeManager.ThemeMode = ThemeComboBox.SelectedItem switch
        {
            "Light" => UiThemeMode.Light,
            "Dark"  => UiThemeMode.Dark,
            _       => UiThemeMode.Auto,
        };
    }
}