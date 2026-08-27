// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies the navigation layout of a paging control.
/// </summary>
public enum PagingBoxDisplayMode
{
    /// <summary>Displays only the previous-page button, current-page input, and next-page button.</summary>
    Compact,

    /// <summary>Displays only the page-number list.</summary>
    List,

    /// <summary>Displays both the page-number list and the compact navigation area.</summary>
    Complete
}
