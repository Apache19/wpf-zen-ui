// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using Wpf.Zen.Ui.Demo.Simple.Views.Pages;

namespace Wpf.Zen.Ui.Demo.WinForms;

/// <summary>
/// WpfContent.xaml
/// </summary>
public partial class WpfContent : UserControl
{
    public WpfContent()
    {
        InitializeComponent();

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootNavigation.Navigate(typeof(DashboardPage));
    }
}