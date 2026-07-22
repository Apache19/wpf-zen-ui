// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Gallery.ViewModels.Pages.BasicInput;

public partial class CheckBoxViewModel : ViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectAllCheckBoxChecked))]
    public partial bool OptionOneCheckBoxChecked { get; set; } = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectAllCheckBoxChecked))]
    public partial bool OptionTwoCheckBoxChecked { get; set; } = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectAllCheckBoxChecked))]
    public partial bool OptionThreeCheckBoxChecked { get; set; } = false;

    public bool? SelectAllCheckBoxChecked
    {
        get
        {
            bool allChecked = OptionOneCheckBoxChecked && OptionTwoCheckBoxChecked && OptionThreeCheckBoxChecked;
            bool allUnchecked = !OptionOneCheckBoxChecked && !OptionTwoCheckBoxChecked && !OptionThreeCheckBoxChecked;

            return allChecked ? true : allUnchecked ? false : null;
        }
        set
        {
            // Indeterminate state: resolve direction — if any unchecked, check all; else uncheck all.
            if (value is null)
            {
                bool allChecked = OptionOneCheckBoxChecked && OptionTwoCheckBoxChecked && OptionThreeCheckBoxChecked;
                value = !allChecked;
            }

            OptionOneCheckBoxChecked = value.Value;
            OptionTwoCheckBoxChecked = value.Value;
            OptionThreeCheckBoxChecked = value.Value;

            OnPropertyChanged();
        }
    }
}
