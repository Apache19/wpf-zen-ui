// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Identifies how a generic prompt dialog was completed.
/// </summary>
public enum PromptDialogResult
{
    /// <summary>
    /// The dialog was dismissed without invoking a configured footer action.
    /// </summary>
    Dismissed,

    /// <summary>
    /// The configured primary action was invoked.
    /// </summary>
    Primary,

    /// <summary>
    /// The configured secondary action was invoked.
    /// </summary>
    Secondary,

    /// <summary>
    /// The configured close action was invoked.
    /// </summary>
    Close,
}
