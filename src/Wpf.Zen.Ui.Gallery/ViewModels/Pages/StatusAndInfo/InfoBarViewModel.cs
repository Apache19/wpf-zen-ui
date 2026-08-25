// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

public partial class InfoBarViewModel : ViewModel
{
    [ObservableProperty]
    public partial bool IsShortInfoBarVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowIcon { get; set; } = true;

    [ObservableProperty]
    public partial bool IsLongInfoBarVisible { get; set; } = true;

    [ObservableProperty]
    public partial InfoBarSeverity ShortInfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial InfoBarSeverity LongInfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    private int _shortInfoBarSeverityComboBoxSelectedIndex = 0;

    public int ShortInfoBarSeverityComboBoxSelectedIndex
    {
        get => _shortInfoBarSeverityComboBoxSelectedIndex;
        set
        {
            _ = SetProperty(ref _shortInfoBarSeverityComboBoxSelectedIndex, value);

            ShortInfoBarSeverity = ConvertIndexToInfoBarSeverity(value);
        }
    }

    private int _longInfoBarSeverityComboBoxSelectedIndex = 0;

    public int LongInfoBarSeverityComboBoxSelectedIndex
    {
        get => _longInfoBarSeverityComboBoxSelectedIndex;
        set
        {
            _ = SetProperty(ref _longInfoBarSeverityComboBoxSelectedIndex, value);

            LongInfoBarSeverity = ConvertIndexToInfoBarSeverity(value);
        }
    }

    private static InfoBarSeverity ConvertIndexToInfoBarSeverity(int value)
    {
        return value switch
        {
            1 => InfoBarSeverity.Success,
            2 => InfoBarSeverity.Warning,
            3 => InfoBarSeverity.Error,
            _ => InfoBarSeverity.Informational,
        };
    }
}
