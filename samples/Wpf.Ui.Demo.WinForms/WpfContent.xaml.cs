// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Demo.Simple.Views.Pages;

namespace Wpf.Ui.Demo.WinForms;

/// <summary>
/// WpfContent.xaml
/// </summary>
public partial class WpfContent : UserControl
{
    public WpfContent()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(DashboardPage));

        WeakEventManager<ThemeManager, ThemeChangedEventArgs>.AddHandler(
            ThemeManager.Instance,
            nameof(ThemeManager.Changed),
            ThemeChanged
        );
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        WeakEventManager<ThemeManager, ThemeChangedEventArgs>.RemoveHandler(
            ThemeManager.Instance,
            nameof(ThemeManager.Changed),
            ThemeChanged
        );
    }

    private void ThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        ThemeManager.ApplyThemeToElement(this, e.AppliedTheme, ThemeManager.UseSystemAccentColor);
    }
}