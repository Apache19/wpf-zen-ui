// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Main navigation for the app.", FluentSymbolRegular.PanelLeft24)]
public partial class NavigationViewPage : INavigableView<NavigationViewViewModel>
{
    public NavigationViewViewModel ViewModel { get; }

    public NavigationViewPage(NavigationViewViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
