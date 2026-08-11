// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.Effects;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Media;

public partial class MediaPage : INavigableView<MediaViewModel>
{
    private SnowflakeEffect? _snowflake;
    private          NavigationView?    _navigationView;

    public MediaViewModel ViewModel { get; }

    public MediaPage(MediaViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
    }

    private void HandleLoaded(object sender, RoutedEventArgs e)
    {
        _navigationView = Wpf.Zen.Ui.Navigation.GetHost(this) as NavigationView;

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
        _snowflake?.Stop();
        _snowflake = null;
        Loaded -= HandleLoaded;
        Unloaded -= HandleUnloaded;
    }
}
