// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;
using Wpf.Ui.Gallery.Services.Contracts;
using Wpf.Ui.Gallery.ViewModels.Windows;
using Wpf.Ui.Gallery.Views.Pages;
using UiComboBox = Wpf.Ui.Controls.ComboBox;

namespace Wpf.Ui.Gallery.Views.Windows;

public partial class MainWindow : IWindow
{
    public MainWindow(
        MainWindowViewModel viewModel,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        ISnackbarService snackbarService,
        IContentDialogService contentDialogService
    )
    {
        ViewModel   = viewModel;
        DataContext = this;

        InitializeComponent();

        snackbarService.SetSnackbarPresenter(SnackbarPresenter);
        navigationService.SetNavigationControl(NavigationView);
        contentDialogService.SetDialogHost(RootContentDialog);
    }

    public MainWindowViewModel ViewModel { get; }

    private bool _isUserClosedPane;

    private bool _isPaneOpenedOrClosedFromCode;

    private void OnNavigationSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not NavigationView navigationView)
        {
            return;
        }

        NavigationView.SetCurrentValue(
            NavigationView.HeaderVisibilityProperty,
            navigationView.SelectedItem?.TargetPageType != typeof(DashboardPage)
                ? Visibility.Visible
                : Visibility.Collapsed
        );
    }

    private void MainWindow_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Optional: auto-collapse pane on small window width
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
        var theme = ThemeManager.CurrentTheme == ApplicationTheme.Default
            ? ApplicationTheme.Light
            : ThemeManager.CurrentTheme;

        ThemeManager.ThemeMode =
            theme == ApplicationTheme.Light
                ? UiThemeMode.Dark
                : UiThemeMode.Light;
    }
}
