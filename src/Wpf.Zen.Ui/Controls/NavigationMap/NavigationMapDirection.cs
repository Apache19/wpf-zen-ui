// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies the direction in which hierarchy levels advance in a
/// <see cref="NavigationMapView"/>.
/// </summary>
public enum NavigationMapDirection
{
    /// <summary>
    /// Places roots above their descendants and advances hierarchy levels from top to bottom.
    /// Sibling branches are distributed horizontally.
    /// </summary>
    TopToBottom,

    /// <summary>
    /// Places roots to the left of their descendants and advances hierarchy levels from left to
    /// right. Sibling branches are distributed vertically.
    /// </summary>
    LeftToRight,
}
