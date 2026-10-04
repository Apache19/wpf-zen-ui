// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DialogsAndFlyouts;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DialogsAndFlyouts;

[GalleryPage("Card covering the app content.", FluentSymbolRegular.CalendarMultiple24)]
public partial class ContentDialogPage : INavigableView<ContentDialogViewModel>
{
    public ContentDialogPage(ContentDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }

    public ContentDialogViewModel ViewModel { get; }
}
