// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ButtonViewModel : ViewModel
{
    [ObservableProperty]
    public partial bool IsSimpleButtonDisabled { get; set; }

    [ObservableProperty]
    public partial bool IsUiButtonDisabled { get; set; }

    [ObservableProperty]
    public partial ControlAppearance UiButtonAppearance { get; set; }
}