// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Appearance;
using Wpf.Ui.DependencyInjection;
using Wpf.Ui.Gallery.DependencyModel;
using Wpf.Ui.Gallery.Services;
using Wpf.Ui.Gallery.Services.Contracts;
using Wpf.Ui.Gallery.ViewModels.Pages;
using Wpf.Ui.Gallery.ViewModels.Windows;
using Wpf.Ui.Gallery.Views.Pages;
using Wpf.Ui.Gallery.Views.Windows;

namespace Wpf.Ui.Gallery;

public partial class App
{
    /// <summary>
    /// Simple DI container — no Generic Host, just a plain <see cref="IServiceProvider"/>
    /// built from <see cref="Microsoft.Extensions.DependencyInjection"/>.
    /// </summary>
    private static readonly IServiceProvider _serviceProvider = ConfigureServices();

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Navigation & page resolution through DI
        _ = services.AddNavigationViewPageProvider();

        // Main window container with navigation
        _ = services.AddSingleton<IWindow, MainWindow>();
        _ = services.AddSingleton<MainWindowViewModel>();
        _ = services.AddSingleton<INavigationService, NavigationService>();
        _ = services.AddSingleton<ISnackbarService, SnackbarService>();
        _ = services.AddSingleton<IContentDialogService, ContentDialogService>();
        _ = services.AddSingleton<WindowsProviderService>();

        // Top-level pages
        _ = services.AddSingleton<DashboardPage>();
        _ = services.AddSingleton<DashboardViewModel>();
        _ = services.AddSingleton<AllControlsPage>();
        _ = services.AddSingleton<AllControlsViewModel>();
        _ = services.AddSingleton<SettingsPage>();
        _ = services.AddSingleton<SettingsViewModel>();

        // All other pages and view models
        _ = services.AddTransientFromNamespace("Wpf.Ui.Gallery.Views", GalleryAssembly.Asssembly);
        _ = services.AddTransientFromNamespace("Wpf.Ui.Gallery.ViewModels", GalleryAssembly.Asssembly);

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T GetRequiredService<T>()
        where T : class
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        var mainWindow = _serviceProvider.GetRequiredService<IWindow>();
        mainWindow.Show();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        // No Host to stop — just dispose the container.
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }
}
