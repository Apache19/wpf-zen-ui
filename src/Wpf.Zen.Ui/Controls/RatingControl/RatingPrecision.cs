// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies the smallest rating unit that a <see cref="RatingControl"/> displays and accepts
/// during user interaction.
/// </summary>
public enum RatingPrecision
{
    /// <summary>
    /// Ratings are presented and selected in whole-star increments.
    /// </summary>
    FullStar,

    /// <summary>
    /// Ratings are presented and selected in half-star increments.
    /// </summary>
    HalfStar,

    /// <summary>
    /// Ratings are presented and selected in quarter-star increments.
    /// </summary>
    QuarterStar,
}
