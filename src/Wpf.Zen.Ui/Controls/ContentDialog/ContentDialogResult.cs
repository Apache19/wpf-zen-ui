// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Identifies the completed choice, or None for dismissal without a choice.</summary>
public enum ContentDialogResult
{
    /// <summary>No semantic action.</summary>
    None,

    /// <summary>The primary choice.</summary>
    Primary,

    /// <summary>The secondary choice.</summary>
    Secondary,

    /// <summary>The third choice, independent of header dismissal.</summary>
    Tertiary,
}
