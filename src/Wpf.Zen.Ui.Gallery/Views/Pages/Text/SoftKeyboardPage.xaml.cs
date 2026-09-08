// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Text;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Text;

[GalleryPage("Embedded, focus-preserving touch keyboard for WPF editors.", SymbolRegular.Keyboard24)]
public partial class SoftKeyboardPage : INavigableView<SoftKeyboardViewModel>
{
    public SoftKeyboardViewModel ViewModel { get; }

    public SoftKeyboardPage(SoftKeyboardViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }
}
