// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ThumbRateViewModel : ViewModel
{
    public string ThumbRateStateText => ThumbRateState.ToString();

    public string ThumbRateStateCodeText => $"<ui:ThumbRate State=\"{ThumbRateState}\" />";

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(ThumbRateStateText),
        nameof(ThumbRateStateCodeText)
    )]
    public partial ThumbRateState ThumbRateState { get; set; } = ThumbRateState.Liked;
}