// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies the display state of a page item.
/// </summary>
public enum PagingItemState
{
    /// <summary>A regular page.</summary>
    Normal,

    /// <summary>The first page.</summary>
    First,

    /// <summary>The last page.</summary>
    Last,

    /// <summary>The only page, which is both the first and last page.</summary>
    FirstAndLast,

    /// <summary>An ellipsis item that navigates backward.</summary>
    PreviousEllipsis,

    /// <summary>An ellipsis item that navigates forward.</summary>
    NextEllipsis
}
