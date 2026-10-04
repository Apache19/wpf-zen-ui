// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes the semantic status conveyed by a badge.
/// </summary>
public enum BadgeSeverity
{
    /// <summary>
    /// Highlights an item that needs the user's attention.
    /// </summary>
    Attention = 0,

    /// <summary>
    /// Presents neutral information or a general status.
    /// </summary>
    Informational = 1,

    /// <summary>
    /// Indicates successful completion or a positive status.
    /// </summary>
    Success = 2,

    /// <summary>
    /// Warns about a condition that may require action.
    /// </summary>
    Caution = 3,

    /// <summary>
    /// Indicates an error or an urgent condition.
    /// </summary>
    Critical = 4,
}
