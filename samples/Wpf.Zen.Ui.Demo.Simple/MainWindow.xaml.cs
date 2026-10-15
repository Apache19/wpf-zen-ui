// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Wpf.Zen.Ui.Demo.Simple.Views.Pages;

namespace Wpf.Zen.Ui.Demo.Simple;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "IDisposableAnalyzers.Correctness",
    "IDISP006:Implement IDisposable",
    Justification = "Closing waits for navigation resources before the window is destroyed."
)]
public partial class MainWindow
{
    private readonly INavigationScope _navigationScope;

    private Task? _closeCompletion;
    private bool  _closeAllowed;

    /// <summary>
    /// Creates a container-free navigation session and completes initialization and shutdown while
    /// the window Dispatcher is available.
    /// </summary>
    public MainWindow()
    {
        DataContext = this;

        InitializeComponent();

        // The simple sample intentionally has no DI container. A core NavigationScope still gives
        // it the same contextual service, request-parameter, cache, and cleanup semantics as the
        // dependency-injection package; pages are created through parameterless constructors.
        _navigationScope =  NavigationScope.Attach(RootNavigation);
        Loaded           += OnLoaded;
    }

    /// <summary>
    /// Waits for actual page commit so acceptance is not mistaken for completed initialization.
    /// </summary>
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        var request = NavigationRequest.To<DashboardPage>();
        var result  = await _navigationScope.Navigation.NavigateAsync(request);
        if (result.Failure is { } failure)
        {
            Trace.TraceError("[Demo.Navigation] REQUEST failed; error={0}.", failure);
        }
    }

    /// <inheritdoc />
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeAllowed || e.Cancel)
        {
            return;
        }

        e.Cancel         =   true;
        _closeCompletion ??= CloseAfterCleanupAsync();
    }

    /// <summary>
    /// Drains navigation after Closing returns, then initiates a second close that may complete.
    /// </summary>
    private async Task CloseAfterCleanupAsync()
    {
        await Dispatcher.Yield(DispatcherPriority.Normal);
        try
        {
            IsEnabled = false;
        }
        catch (Exception exception)
        {
            Trace.TraceError("[Demo.Navigation] INPUT shutdown failed; error={0}.", exception);
        }

        try
        {
            await _navigationScope.DisposeAsync();
        }
        catch (Exception exception)
        {
            Trace.TraceError("[Demo.Navigation] SHUTDOWN failed; error={0}.", exception);
        }

        _closeAllowed = true;
        Close();
    }
}