// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Zen.Ui;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Services.Contracts;
using Wpf.Zen.Ui.Gallery.ViewModels.Windows;
using Wpf.Zen.Ui.Gallery.Views.Pages;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;

namespace Wpf.Zen.Ui.Gallery.Views.Windows;

[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "IDisposableAnalyzers.Correctness",
    "IDISP006:Implement IDisposable",
    Justification = "WPF Window owns the navigation scope and deterministically releases it from OnClosed."
)]
public partial class MainWindow : IWindow
{
    private readonly INavigationScope _navigationScope;

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

    public MainWindowViewModel ViewModel { get; }

    /// <summary>Gets the navigator scoped to the gallery's primary navigation surface.</summary>
    public INavigationService Navigation => _navigationScope.Navigation;

    private bool _isUserClosedPane;

    private bool _isPaneOpenedOrClosedFromCode;

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
        if (sender is not MenuItem menuItem)
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

    private void NavigateToPage(Type pageType)
    {
        try
        {
            _ = Navigation.Navigate(pageType);
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
    /// Navigates an independently presented breadcrumb segment through this window's scoped service.
    /// </summary>
    private void BreadcrumbBar_OnItemClicked(
        BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs e)
    {
        if (e.Item is BreadcrumbSegment segment)
        {
            _ = Navigation.Navigate(segment);
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

    /// <summary>Releases pages and scoped services owned by this navigation surface.</summary>
    protected override void OnClosed(EventArgs e)
    {
        _navigationScope.Dispose();
        base.OnClosed(e);
    }
}
