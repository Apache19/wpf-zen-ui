// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes the resolved screen-space side on which a Popup is currently displayed relative to its placement target.
/// </summary>
/// <remarks>
/// Unlike <see cref="System.Windows.Controls.Primitives.Popup.Placement"/>, this value describes the result after
/// WPF has evaluated its fallback candidates and constrained the Popup to the monitor bounds. It is intended for
/// visual-state decisions such as joining the borders of a placement target and its Popup content.
/// </remarks>
public enum PopupPlacementDirection
{
    /// <summary>
    /// The Popup is closed, has no target-relative placement, or its resolved direction cannot be determined.
    /// </summary>
    None,

    /// <summary>
    /// The Popup is displayed above its placement target.
    /// </summary>
    Top,

    /// <summary>
    /// The Popup is displayed below its placement target.
    /// </summary>
    Bottom,

    /// <summary>
    /// The Popup is displayed to the left of its placement target.
    /// </summary>
    Left,

    /// <summary>
    /// The Popup is displayed to the right of its placement target.
    /// </summary>
    Right,

    /// <summary>
    /// The Popup overlaps or is centered on its placement target, so no outside edge is dominant.
    /// </summary>
    Center,
}
