// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>Agreement and validation state for one terms-of-use presentation.</summary>
public partial class TermsOfUseDialogData : ObservableObject
{
    /// <summary>Gets or sets whether the user agrees to the terms. Defaults to false.</summary>
    [ObservableProperty]
    public partial bool IsAccepted { get; set; }

    /// <summary>Gets or sets whether an unsuccessful acceptance attempt should display an error.</summary>
    [ObservableProperty]
    public partial bool HasValidationError { get; set; }

    /// <summary>Validates acceptance and updates the error state bound by the view.</summary>
    /// <returns>True when the agreement can be accepted.</returns>
    public bool TryAccept()
    {
        HasValidationError = !IsAccepted;
        return IsAccepted;
    }
}
