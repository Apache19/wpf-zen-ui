// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Extensions.DependencyInjection;
using Wpf.Zen.Ui;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Demo.Mvvm.Views;

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

    public ViewModels.MainWindowViewModel ViewModel { get; }

    public MainWindow(INavigationScopeFactory navigationScopes)
    {
        InitializeComponent();

        _navigationScope = navigationScopes.Attach(RootNavigation, "main");
        ViewModel = _navigationScope.Services
           .GetRequiredService<ViewModels.MainWindowViewModel>();
        DataContext = ViewModel;
    }

    /// <summary>Gets the navigator scoped to this window's navigation view.</summary>
    public INavigationService Navigation => _navigationScope.Navigation;

    /// <summary>
    /// Raises the closed event.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _navigationScope.Dispose();
        base.OnClosed(e);

        // Make sure that closing this window will begin the process of closing the application.
        Application.Current.Shutdown();
    }
}
