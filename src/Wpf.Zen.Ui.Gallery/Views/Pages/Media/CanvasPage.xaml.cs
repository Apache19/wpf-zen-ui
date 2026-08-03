// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Media;

[GalleryPage("Canvas presenter.", SymbolRegular.InkStroke24)]
public partial class CanvasPage : INavigableView<CanvasViewModel>
{
    public CanvasViewModel ViewModel { get; }

    public CanvasPage(CanvasViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
