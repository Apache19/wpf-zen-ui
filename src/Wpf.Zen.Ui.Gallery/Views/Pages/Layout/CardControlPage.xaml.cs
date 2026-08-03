// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.Layout;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.Layout;

/// <summary>
/// Interaction logic for CardControlPage.xaml
/// </summary>
[GalleryPage("Card control.", SymbolRegular.CheckboxIndeterminate24)]
public partial class CardControlPage : INavigableView<CardControlViewModel>
{
    public CardControlPage(CardControlViewModel viewModel)
    {
        InitializeComponent();
        ViewModel   = viewModel;
    }

    public CardControlViewModel ViewModel { get; }
}
