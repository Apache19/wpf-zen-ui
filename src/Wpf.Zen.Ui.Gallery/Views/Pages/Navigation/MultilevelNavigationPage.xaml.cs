// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Navigation;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Navigation;

[GalleryPage("Navigation with multi level Breadcrumb.", FluentSymbolRegular.PanelRightContract24)]
public partial class MultilevelNavigationPage : INavigableView<MultilevelNavigationSample>
{
    public MultilevelNavigationPage(MultilevelNavigationSample viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public MultilevelNavigationSample ViewModel { get; }
}
