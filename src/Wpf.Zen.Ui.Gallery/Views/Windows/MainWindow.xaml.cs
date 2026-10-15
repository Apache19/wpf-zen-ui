// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.ComponentModel;
using Wpf.Zen.Ui;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Services.Contracts;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;
using Wpf.Zen.Ui.Gallery.Views.Pages;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;

namespace Wpf.Zen.Ui.Gallery.Views.Windows;

/// <summary>
/// Hosts gallery navigation and awaits page and application service cleanup before the window
/// closes.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "IDisposableAnalyzers.Correctness",
    "IDISP006:Implement IDisposable",
    Justification = "The window awaits navigation and service shutdown before allowing Closing to finish."
)]
public partial class MainWindow : IWindow
{
    private readonly INavigationScope _navigationScope;
    private Task? _closeCompletion;
    private bool _closeAllowed;

    /// <summary>
    /// Creates the window navigation scope and connects dialog and notification services to its
    /// presentation hosts.
    /// </summary>
    /// <param name="navigationScopes">
    /// The factory creating independent navigation dependencies for this window.
    /// </param>
    public MainWindow(INavigationScopeFactory navigationScopes)
    {
        InitializeComponent();

        _navigationScope = navigationScopes.Attach(NavigationView, "gallery-main");
        ViewModel = _navigationScope.Services.GetRequiredService<MainWindowViewModel>();
        NavigationView.SetCurrentValue(NavigationView.NavigationMapProperty, ViewModel.NavigationMap);
        DataContext = this;

        // Pages and their view models resolve these instances from the same presentation scope.
        // Direct, one-time host attachment also works when the WPF tree has no Window owner.
        _navigationScope.Services.GetRequiredService<ISnackbarService>().Attach(SnackbarPresenter);
        _navigationScope.Services.GetRequiredService<IContentDialogService>().Attach(RootContentDialog);
        SetupTrayMenuEvents();
    }

    /// <summary>
    /// Gets the menu, navigation map, and window presentation model owned by the window navigation
    /// scope.
    /// </summary>
    public MainWindowViewModel ViewModel { get; }

    /// <summary>
    /// Gets the navigator scoped to the gallery's primary navigation surface.
    /// </summary>
    public INavigationService Navigation => _navigationScope.Navigation;

    private bool _isUserClosedPane;

    private bool _isPaneOpenedOrClosedFromCode;

    /// <summary>
    /// Focuses the navigation search input when the window's Find command is executed.
    /// </summary>
    /// <remarks>
    /// AutoSuggestBox forwards focus to the text input supplied by its control template.
    /// </remarks>
    private void FindCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        _ = AutoSuggestBox.Focus();
        e.Handled = true;
    }

    private void SetupTrayMenuEvents()
    {
        foreach (var menuItem in ViewModel.TrayMenuItems)
        {
            if (menuItem is MenuItem item)
            {
                item.Click += OnTrayMenuItemClick;
            }
        }
    }

    private void OnTrayMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (_closeCompletion is not null || sender is not MenuItem menuItem)
        {
            return;
        }

        var tag = menuItem.Tag?.ToString() ?? string.Empty;

        Debug.WriteLine($"System Tray Click: {menuItem.Header}, Tag: {tag}");

        switch (tag)
        {
            case "tray_home":
                HandleTrayHomeClick();
                break;
            case "tray_settings":
                HandleTraySettingsClick();
                break;
            case "tray_close":
                HandleTrayCloseClick();
                break;
            default:
                if (!string.IsNullOrEmpty(tag))
                {
                    Debug.WriteLine($"unknown Tag: {tag}");
                }

                break;
        }
    }

    private void HandleTrayHomeClick()
    {
        Debug.WriteLine("Tray menu - Home Click");

        ShowAndActivateWindow();

        NavigateToPage(typeof(DashboardPage));
    }

    private void HandleTraySettingsClick()
    {
        Debug.WriteLine("Tray menu - Settings Click");

        ShowAndActivateWindow();

        NavigateToPage(typeof(SettingsPage));
    }

    private static void HandleTrayCloseClick()
    {
        Debug.WriteLine("Tray menu - Close Click");

        Application.Current.MainWindow?.Close();
    }

    private void ShowAndActivateWindow()
    {
        if (WindowState == WindowState.Minimized)
        {
            SetCurrentValue(WindowStateProperty, WindowState.Normal);
        }

        Show();
        _ = Activate();
        _ = Focus();
    }

    private async void NavigateToPage(Type pageType)
    {
        try
        {
            var request = NavigationRequest.To(pageType);
            var result = await Navigation.NavigateAsync(request);
            if (result.Failure is { } failure)
            {
                Trace.TraceError("[Gallery.Navigation] REQUEST failed; error={0}.", failure);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NavigateToPage {pageType.Name} Error: {ex.Message}");
        }
    }

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
        {
            return;
        }

        NavigationView.SetCurrentValue(
            NavigationView.HeaderVisibilityProperty,
            (navigationView.SelectedItem as INavigationItem)?.TargetPageType != typeof(DashboardPage)
                ? Visibility.Visible
                : Visibility.Collapsed
        );
    }

    /// <summary>
    /// Navigates an independently presented breadcrumb segment through this window's scoped
    /// service.
    /// </summary>
    private async void BreadcrumbBar_OnItemClicked(
        BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs e)
    {
        if (_closeCompletion is null && e.Item is BreadcrumbSegment segment)
        {
            var submitted = Navigation.Navigate(segment);
            var result = await submitted.Completion;
            if (result.Failure is { } failure)
            {
                Trace.TraceError("[Gallery.Navigation] BREADCRUMB failed; error={0}.", failure);
            }
        }
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // if (_isUserClosedPane)
        // {
        //     return;
        // }
        //
        // _isPaneOpenedOrClosedFromCode = true;
        // NavigationView.SetCurrentValue(NavigationView.IsPaneOpenProperty, e.NewSize.Width > 1200);
        // _isPaneOpenedOrClosedFromCode = false;
    }

    private void NavigationView_OnPaneOpened(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = false;
    }

    private void NavigationView_OnPaneClosed(NavigationView sender, RoutedEventArgs args)
    {
        if (_isPaneOpenedOrClosedFromCode)
        {
            return;
        }

        _isUserClosedPane = true;
    }

    private void BackdropComboBox_OnSelected(object sender, RoutedEventArgs e)
    {
        if (sender is UiComboBox cb)
        {
            var item = cb.SelectedItem as ComboBoxItem;
            if (item?.Tag is WindowBackdropType wbt)
            {
                SetCurrentValue(WindowBackdropTypeProperty, wbt);
            }
        }
    }

    private void ThemeButton_OnClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.Preference =
            ThemeManager.EffectiveTheme == ThemeVariant.Light
                ? ThemePreference.Dark
                : ThemePreference.Light;
    }

    /// <summary>
    /// Drains navigation and services while the Dispatcher is available; repeated close requests
    /// share cleanup.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (_closeAllowed || e.Cancel)
        {
            return;
        }

        e.Cancel = true;
        _closeCompletion ??= CloseAfterCleanupAsync();
    }

    /// <summary>
    /// Attempts navigation and root service disposal independently and reports failures before
    /// allowing WPF to destroy the window.
    /// </summary>
    private async Task CloseAfterCleanupAsync()
    {
        await Dispatcher.Yield(DispatcherPriority.Normal);
        var failures = new List<Exception>();
        try
        {
            IsEnabled = false;
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await _navigationScope.DisposeAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        try
        {
            await App.StopServicesAsync();
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }

        foreach (Exception failure in failures)
        {
            Trace.TraceError("[Gallery.Navigation] SHUTDOWN failed; error={0}.", failure);
        }

        _closeAllowed = true;
        Close();
    }
}
