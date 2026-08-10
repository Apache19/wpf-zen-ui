// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Effects;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DateAndTime;

public partial class DateAndTimePage : INavigableView<DateAndTimeViewModel>
{
    private SnowflakeEffect? _snowflake;
    private          NavigationView?    _navigationView;

    public DateAndTimeViewModel ViewModel { get; }

    public DateAndTimePage(DateAndTimeViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        if (
            _navigationView?.BreadcrumbBar != null
            && _navigationView.BreadcrumbBar.Visibility != Visibility.Collapsed
        )
        {
            _navigationView.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Collapsed);
        }

        INavigationItem? selectedItem = _navigationView?.SelectedItem as INavigationItem;
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
        if (
            _navigationView?.BreadcrumbBar != null
            && _navigationView.BreadcrumbBar.Visibility != Visibility.Visible
        )
        {
            _navigationView.BreadcrumbBar.SetCurrentValue(VisibilityProperty, Visibility.Visible);
        }

        _snowflake?.Stop();
        _snowflake = null;
        Loaded -= HandleLoaded;
        Unloaded -= HandleUnloaded;
    }
}
