// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class RatingViewModel : ViewModel
{
    [ObservableProperty]
    public partial bool IsFirstRatingDisabled { get; set; }

    [ObservableProperty]
    public partial double? FirstRatingValue { get; set; } = 1.5D;

    [ObservableProperty]
    public partial bool IsSecondRatingDisabled { get; set; }

    [ObservableProperty]
    public partial double? SecondRatingValue { get; set; } = 3D;
}
