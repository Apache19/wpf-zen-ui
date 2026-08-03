// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Media;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Media;

[GalleryPage("Image presenter.", SymbolRegular.ImageMultiple24)]
public partial class ImagePage : INavigableView<ImageViewModel>
{
    public ImageViewModel ViewModel { get; }

    public ImagePage(ImageViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
