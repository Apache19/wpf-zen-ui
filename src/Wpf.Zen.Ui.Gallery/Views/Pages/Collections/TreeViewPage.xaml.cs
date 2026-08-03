// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Collections;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Collections;

#if DEBUG
[GalleryPage("List inside the TreeView.", SymbolRegular.TextBulletListTree24)]
#endif
public partial class TreeViewPage : INavigableView<TreeViewViewModel>
{
    public TreeViewViewModel ViewModel { get; }

    public TreeViewPage(TreeViewViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
