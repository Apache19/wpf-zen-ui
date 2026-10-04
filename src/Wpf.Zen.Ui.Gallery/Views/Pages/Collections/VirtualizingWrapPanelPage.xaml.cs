// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Collections;

/// <summary>
/// Demonstrates automatic responsive item sizing and row virtualization with a synthetic movie
/// poster library.
/// </summary>
[GalleryPage("Responsive virtualized layout with automatic item sizing.", FluentSymbolRegular.MoviesAndTv24)]
public partial class VirtualizingWrapPanelPage : INavigableView<VirtualizingWrapPanelViewModel>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizingWrapPanelPage"/> class.
    /// </summary>
    /// <param name="viewModel">The movie-library sample data resolved by the gallery scope.</param>
    public VirtualizingWrapPanelPage(VirtualizingWrapPanelViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    /// <summary>Gets the view model that supplies the synthetic movie catalogue.</summary>
    public VirtualizingWrapPanelViewModel ViewModel { get; }

    private void HandleMovieItemsControlLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.ItemsControl itemsControl)
        {
            return;
        }

        // ItemsPanelTemplate owns a separate XAML namescope, so bindings in the page header cannot
        // resolve MoviePanel by ElementName. Loaded runs after the first layout has materialized the
        // panel; using that instance as the metrics area's data source keeps all read-only layout
        // dependency properties live without duplicating them in the view model.
        LayoutMetrics.DataContext = FindVisualDescendant<VirtualizingWrapPanel>(itemsControl);
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T matchingChild)
            {
                return matchingChild;
            }

            T? matchingDescendant = FindVisualDescendant<T>(child);
            if (matchingDescendant is not null)
            {
                return matchingDescendant;
            }
        }

        return null;
    }
}
