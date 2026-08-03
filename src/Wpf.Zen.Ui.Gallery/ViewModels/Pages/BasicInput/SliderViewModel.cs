// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class SliderViewModel : ViewModel
{
    [ObservableProperty]
    public partial int SimpleSliderValue { get; set; } = 0;

    [ObservableProperty]
    public partial int RangeSliderValue { get; set; } = 500;

    [ObservableProperty]
    public partial int MarksSliderValue { get; set; } = 0;

    [ObservableProperty]
    public partial int VerticalSliderValue { get; set; } = 0;
}
