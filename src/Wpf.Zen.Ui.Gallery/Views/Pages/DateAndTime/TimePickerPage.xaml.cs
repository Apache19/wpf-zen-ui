// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("allows a user to pick a time value.", SymbolRegular.Clock24)]
public partial class TimePickerPage : INavigableView<TimePickerViewModel>
{
    public TimePickerViewModel ViewModel { get; init; }

    public TimePickerPage(TimePickerViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
