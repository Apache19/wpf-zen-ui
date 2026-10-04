// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.StatusAndInfo;

/// <summary>
/// Supplies the status selection shared by the badge examples.
/// </summary>
public partial class InfoBadgeViewModel : ViewModel
{
    /// <summary>
    /// Gets or sets the status displayed by the examples. Defaults to Attention.
    /// </summary>
    [ObservableProperty]
    public partial BadgeSeverity Severity { get; set; } = BadgeSeverity.Attention;

    private int _infoSeverityComboBoxSelectedIndex = 0;

    /// <summary>
    /// Gets or sets the selected status index and updates the example badges.
    /// </summary>
    public int SeverityComboBoxSelectedIndex
    {
        get => _infoSeverityComboBoxSelectedIndex;
        set
        {
            _ = SetProperty(ref _infoSeverityComboBoxSelectedIndex, value);
            Severity = ConvertIndexToBadgeSeverity(value);
        }
    }

    /// <summary>
    /// Maps the gallery selection to a status, falling back to Attention.
    /// </summary>
    private static BadgeSeverity ConvertIndexToBadgeSeverity(int value)
    {
        return value switch
        {
            1 => BadgeSeverity.Informational,
            2 => BadgeSeverity.Success,
            3 => BadgeSeverity.Caution,
            4 => BadgeSeverity.Critical,
            _ => BadgeSeverity.Attention,
        };
    }
}
