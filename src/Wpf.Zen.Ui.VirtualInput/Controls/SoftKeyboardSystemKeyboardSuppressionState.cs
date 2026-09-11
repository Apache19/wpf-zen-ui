// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Describes the result of suppressing the Windows input pane.
/// </summary>
public enum SoftKeyboardSystemKeyboardSuppressionState
{
    /// <summary>
    /// The control owns no active suppression request. Windows may independently show or hide its pane.
    /// </summary>
    NotRequested,

    /// <summary>
    /// Windows accepted the request to hide its input pane.
    /// </summary>
    Hidden,

    /// <summary>
    /// The window-specific input-pane API is unavailable.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Windows rejected the hide request, normally because the window is not foreground.
    /// </summary>
    Rejected,
}
