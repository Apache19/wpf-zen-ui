// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes a Popup's selected target-relative candidate side, or an estimated side when candidate state is unavailable.
/// </summary>
/// <remarks>
/// Unlike <see cref="System.Windows.Controls.Primitives.Popup.Placement"/>, this value accounts for WPF's fallback
/// candidate selection. Candidate sides precede monitor-edge nudging and are logical, not necessarily screen-space
/// directions after transforms or offsets. <see cref="PopupPlacementTracker.IsPlacementEstimatedProperty"/> identifies
/// results inferred from final window geometry, including Custom and relative placements.
/// </remarks>
public enum PopupPlacementDirection
{
    /// <summary>
    /// The Popup is closed, has no target-relative placement, or its resolved direction cannot be determined.
    /// </summary>
    None,

    /// <summary>
    /// The upper candidate side, or an estimated position above the target.
    /// </summary>
    Top,

    /// <summary>
    /// The lower candidate side, or an estimated position below the target.
    /// </summary>
    Bottom,

    /// <summary>
    /// The left candidate side, or an estimated position to the left of the target.
    /// </summary>
    Left,

    /// <summary>
    /// The right candidate side, or an estimated position to the right of the target.
    /// </summary>
    Right,

    /// <summary>
    /// The Popup overlaps or is centered on its placement target, so no outside edge is dominant.
    /// </summary>
    Center,
}
