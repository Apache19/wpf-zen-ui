// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.BasicInput;

[GalleryPage("Opens a link.", FluentSymbolRegular.Link24)]
public partial class HyperlinkButtonPage : INavigableView<HyperlinkButtonViewModel>
{
    public HyperlinkButtonViewModel ViewModel { get; }

    public HyperlinkButtonPage(HyperlinkButtonViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
