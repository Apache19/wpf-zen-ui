// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using CheckBox = System.Windows.Controls.CheckBox;

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class ButtonViewModel : ViewModel
{
    [ObservableProperty]
    private bool _isSimpleButtonEnabled = true;

    [ObservableProperty]
    private bool _isUiButtonEnabled = true;

    [ObservableProperty]
    private ControlAppearance _buttonAppearance = ControlAppearance.Secondary;

    [RelayCommand]
    private void OnSimpleButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsSimpleButtonEnabled = !(checkbox?.IsChecked ?? false);
    }

    [RelayCommand]
    private void OnUiButtonCheckboxChecked(object sender)
    {
        if (sender is not CheckBox checkbox)
        {
            return;
        }

        IsUiButtonEnabled = !(checkbox?.IsChecked ?? false);
    }
}
