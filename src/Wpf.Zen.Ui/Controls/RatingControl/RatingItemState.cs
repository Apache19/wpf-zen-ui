// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes the discrete visual fill state of one item presented by a
/// <see cref="RatingControl"/>.
/// </summary>
/// <remarks>
/// The state is calculated after the active rating has been constrained and rounded according to
/// <see cref="RatingControl.Precision"/>. It is intended for use in
/// <see cref="RatingControl.ItemTemplate"/> data triggers.
/// </remarks>
public enum RatingItemState
{
    /// <summary>The item contains no filled star fraction.</summary>
    Empty,

    /// <summary>The item represents one quarter of a filled star.</summary>
    QuarterStar,

    /// <summary>The item represents one half of a filled star.</summary>
    HalfStar,

    /// <summary>The item represents three quarters of a filled star.</summary>
    ThreeQuarterStar,

    /// <summary>The item represents one completely filled star.</summary>
    FullStar,
}
