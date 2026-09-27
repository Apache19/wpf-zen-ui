// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Converters;

/// <summary>
/// Selects the actual element dimension used to calculate a pill corner radius.
/// </summary>
public enum PillCornerDimension
{
    /// <summary>
    /// Uses the shorter side for a complete horizontal or vertical pill, or a circle.
    /// </summary>
    ShorterSide,

    /// <summary>
    /// Uses the actual width, useful for vertically stacked pill segments.
    /// </summary>
    Width,

    /// <summary>
    /// Uses the actual height, useful for horizontally arranged pill segments.
    /// </summary>
    Height,
}
