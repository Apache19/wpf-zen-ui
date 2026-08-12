// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Lepo.i18n.Wpf;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.DependencyInjection;
using Wpf.Zen.Ui.Gallery.DependencyModel;
using Wpf.Zen.Ui.Gallery.Models;
using Wpf.Zen.Ui.Gallery.Resources;
using Wpf.Zen.Ui.Gallery.Services;
using Wpf.Zen.Ui.Gallery.Services.Contracts;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;
using Wpf.Zen.Ui.Gallery.Views.Windows;

namespace Wpf.Zen.Ui.Gallery;

public partial class App
{
    // The .NET Generic Host provides dependency injection, configuration, logging, and other services.
    // https://docs.microsoft.com/dotnet/core/extensions/generic-host
    // https://docs.microsoft.com/dotnet/core/extensions/dependency-injection
    // https://docs.microsoft.com/dotnet/core/extensions/configuration
    // https://docs.microsoft.com/dotnet/core/extensions/logging
    private static readonly IHost _host = Host.CreateDefaultBuilder()
        .ConfigureAppConfiguration(c =>
        {
            _ = c.SetBasePath(AppContext.BaseDirectory);
        })
        .ConfigureServices(
            (_1, services) =>
            {
                _ = services.AddNavigation();

                // App Host
                _ = services.AddHostedService<ApplicationHostService>();

                // Main window container with navigation
                _ = services.AddSingleton<IWindow, MainWindow>();
                _ = services.AddScoped<MainWindowViewModel>();

                // These services target the overlay presenters attached to one navigation scope;
                // the scope may be a Window or another independently hosted WPF visual tree.
                _ = services.AddScoped<ISnackbarService, SnackbarService>();
                _ = services.AddScoped<IContentDialogService, ContentDialogService>();
                _ = services.AddSingleton<WindowsProviderService>();

                // Page types are intentionally not registered. AddNavigation activates concrete
                // pages from each host scope and injects their registered view models.
                _ = services.AddTransient<DashboardViewModel>();
                _ = services.AddTransient<AllControlsViewModel>();
                _ = services.AddTransient<SettingsViewModel>();

                // View models and other non-page dependencies retain ordinary DI lifetimes.
                _ = services.AddTransientFromNamespace("Wpf.Zen.Ui.Gallery.ViewModels", GalleryAssembly.Asssembly);
            }
        )
        .Build();

    /// <summary>
    /// Gets registered service.
    /// </summary>
    /// <typeparam name="T">Type of the service to get.</typeparam>
    /// <returns>Instance of the service or <see langword="null"/>.</returns>
    public static T GetRequiredService<T>()
        where T : class
    {
        return _host.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Lepo.i18n's WPF integration owns its localization provider; routing it through the
        // application DI container adds registrations without providing a useful lifetime boundary.
        _ = this.UseStringLocalizer(builder =>
        {
            builder.FromResource<Translations>(new("pl-PL"));
        });

        /*
        var theme = LoadAppTheme();

        var options = new ThemeManagerOptions();

        // Request deferred configuration
        options.BeginInit();

        options.ThemeMode = theme.ThemeMode;
        options.AccentSource = theme.AccentSource;

        // To explicitly clear a previously set URI, set the property to null:
        // options.AdditionalLightThemeSource = null;

        // Apply theme configuration
        options.EndInit();
        */

        _host.Start();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            // Use a DispatcherFrame to continue processing UI messages while waiting for StopAsync
            // to complete. This prevents deadlocks that can occur when synchronously blocking the UI thread.
            var stopTask = _host.StopAsync();

            var frame = new DispatcherFrame();

            stopTask.ContinueWith(
                _ => frame.Continue = false,
                TaskScheduler.FromCurrentSynchronizationContext()
            );

            // Enter a message loop until stopTask completes
            Dispatcher.PushFrame(frame);

            stopTask.GetAwaiter().GetResult();
        }
        catch
        {
            // Ignore exceptions from StopAsync during shutdown to avoid interfering with further cleanup
        }
        finally
        {
            // Ensure the Host is disposed regardless of whether StopAsync succeeded
            try
            {
                #pragma warning disable IDISP007 // Don't dispose injected
                _host.Dispose();
                #pragma warning restore IDISP007 // Don't dispose injected
            }
            catch
            {
                // Ignore Dispose exceptions during shutdown
            }
        }
    }

    /// <summary>
    /// Occurs when an exception is thrown by an application but not handled.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // For more info see https://docs.microsoft.com/en-us/dotnet/api/system.windows.application.dispatcherunhandledexception?view=windowsdesktop-6.0
    }

    private static AppTheme LoadAppTheme()
    {
        return new AppTheme()
        {
            ThemeMode    = UiThemeMode.Dark,
            AccentSource = AccentColorSource.System
        };
    }
}
