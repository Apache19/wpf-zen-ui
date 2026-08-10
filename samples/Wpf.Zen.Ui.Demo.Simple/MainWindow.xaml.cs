// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using Wpf.Zen.Ui.Demo.Simple.Views.Pages;

namespace Wpf.Zen.Ui.Demo.Simple;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "IDisposableAnalyzers.Correctness",
    "IDISP006:Implement IDisposable",
    Justification = "WPF Window owns the navigation scope and deterministically releases it from OnClosed."
)]
public partial class MainWindow
{
    private readonly INavigationScope _navigationScope;

    public MainWindow()
    {
        DataContext = this;

        InitializeComponent();

        // The simple sample intentionally has no DI container. A core NavigationScope still gives
        // it the same contextual service, request-parameter, cache, and cleanup semantics as the
        // dependency-injection package; pages are created through parameterless constructors.
        _navigationScope = NavigationScope.Attach(RootNavigation);
        Loaded += (_, _) => _ = _navigationScope.Navigation.Navigate(typeof(DashboardPage));
    }

    protected override void OnClosed(EventArgs e)
    {
        _navigationScope.Dispose();
        base.OnClosed(e);
    }
}
