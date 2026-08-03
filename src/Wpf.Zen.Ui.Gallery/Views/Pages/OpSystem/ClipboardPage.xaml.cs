// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.OpSystem;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.OpSystem;

[GalleryPage("System clipboard.", SymbolRegular.Desktop24)]
public partial class ClipboardPage : INavigableView<ClipboardViewModel>
{
    public ClipboardViewModel ViewModel { get; }

    public ClipboardPage(ClipboardViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
