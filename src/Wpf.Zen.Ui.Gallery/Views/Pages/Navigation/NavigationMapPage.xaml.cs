// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Visualizes a semantic navigation graph as an interactive application map.", SymbolRegular.Map24)]
public partial class NavigationMapPage : INavigableView<NavigationMapViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="NavigationMapPage"/> class.</summary>
    /// <param name="viewModel">The view model resolved from the active navigation scope.</param>
    public NavigationMapPage(NavigationMapViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    /// <summary>Gets the view model used by this sample page.</summary>
    public NavigationMapViewModel ViewModel { get; }
}
