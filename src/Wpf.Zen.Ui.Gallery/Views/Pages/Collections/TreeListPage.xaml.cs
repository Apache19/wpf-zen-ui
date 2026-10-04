// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Collections;

[GalleryPage("Collapsable list.", FluentSymbolRegular.TextBulletListTree24)]
public partial class TreeListPage : INavigableView<TreeListViewModel>
{
    public TreeListViewModel ViewModel { get; }

    public TreeListPage(TreeListViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
