// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Services.Contracts;
using Wpf.Zen.Ui.Gallery.Views.Pages;
using Wpf.Zen.Ui.Gallery.Views.Windows;

namespace Wpf.Zen.Ui.Gallery.Services;

/// <summary>
/// Managed host of the application.
/// </summary>
public class ApplicationHostService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Stores the application service provider and creates the main window on first activation.
    /// </summary>
    /// <param name="serviceProvider">
    /// The root container owning application windows and services.
    /// </param>
    public ApplicationHostService(IServiceProvider serviceProvider)
    {
        // If you want, you can do something with these services at the beginning of loading the application.
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Triggered when the application host is ready to start the service.
    /// </summary>
    /// <param name="cancellationToken">Indicates that the start process has been aborted.</param>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return HandleActivationAsync();
    }

    /// <summary>
    /// Triggered when the application host is performing a graceful shutdown.
    /// </summary>
    /// <param name="cancellationToken">
    /// Indicates that the shutdown process should no longer be graceful.
    /// </param>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates main window during activation.
    /// </summary>
    private Task HandleActivationAsync()
    {
        if (Application.Current.Windows.OfType<MainWindow>().Any())
        {
            return Task.CompletedTask;
        }

        IWindow mainWindow = _serviceProvider.GetRequiredService<IWindow>();
        if (mainWindow is not null)
        {
            mainWindow.Loaded += OnMainWindowLoaded;
            mainWindow.Show();
        }

        return Task.CompletedTask;
    }

    private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow mainWindow)
        {
            return;
        }

        mainWindow.Loaded -= OnMainWindowLoaded;
        var request = NavigationRequest.To<DashboardPage>();
        var result = await mainWindow.Navigation.NavigateAsync(request);
        if (result.Failure is { } failure)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
