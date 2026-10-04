// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Gallery.ControlsLookup;
using Wpf.Zen.Ui.Gallery.ViewModels.Pages.DateAndTime;

namespace Wpf.Zen.Ui.Gallery.Views.Pages.DateAndTime;

[GalleryPage("Button opening Calendar.", FluentSymbolRegular.CalendarRtl24)]
public partial class CalendarDatePickerPage : INavigableView<CalendarDatePickerViewModel>
{
    public CalendarDatePickerViewModel ViewModel { get; init; }

    public CalendarDatePickerPage(CalendarDatePickerViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;

        InitializeComponent();
    }
}
