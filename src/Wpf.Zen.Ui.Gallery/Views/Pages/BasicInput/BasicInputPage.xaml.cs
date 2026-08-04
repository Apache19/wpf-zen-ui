// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Effects;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.BasicInput;

public partial class BasicInputPage : INavigableView<BasicInputViewModel>
{
    private readonly INavigationService _navigationService;
    private          SnowflakeEffect?   _snowflake;

    public BasicInputViewModel ViewModel { get; }

    public BasicInputPage(BasicInputViewModel viewModel, INavigationService navigationService)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        _navigationService = navigationService;

        InitializeComponent();

        Loaded   += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        INavigationView? navigationControl = _navigationService.GetNavigationControl();
        if (
            navigationControl?.BreadcrumbBar != null
         && navigationControl.BreadcrumbBar.Visibility != Visibility.Collapsed
        )
        {
            navigationControl.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        }

        INavigationItem? selectedItem = navigationControl?.SelectedItem as INavigationItem;
        if (selectedItem != null)
        {
            string? newTitle = selectedItem.Content?.ToString();
            if (MainTitle.Text != newTitle)
            {
                MainTitle.SetCurrentValue(System.Windows.Controls.TextBlock.TextProperty, newTitle);
            }

            if (selectedItem.Icon is SymbolRegular selectedIcon && MainFluentIcon.Symbol != selectedIcon)
            {
                MainFluentIcon.SetCurrentValue(FluentIcon.SymbolProperty, selectedIcon);
            }
        }

        _snowflake ??= new(MainCanvas);
        _snowflake.Start();
    }

    private void HandleUnloaded(object sender, RoutedEventArgs e)
    {
        INavigationView? navigationControl = _navigationService.GetNavigationControl();
        if (
            navigationControl?.BreadcrumbBar != null
         && navigationControl.BreadcrumbBar.Visibility != Visibility.Visible
        )
        {
            navigationControl.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        }

        _snowflake?.Stop();
        _snowflake =  null;
        Loaded     -= HandleLoaded;
        Unloaded   -= HandleUnloaded;
    }
}
