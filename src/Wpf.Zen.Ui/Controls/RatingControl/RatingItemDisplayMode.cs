// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Identifies the score source currently used to present a <see cref="RatingItem"/>.
/// </summary>
/// <remarks>
/// This distinction lets an item template present an unrated placeholder or a transient pointer
/// preview differently from a committed rating without inspecting the parent control.
/// </remarks>
public enum RatingItemDisplayMode
{
    /// <summary>The item reflects the committed <see cref="RatingControl.Rating"/>.</summary>
    Rating,

    /// <summary>
    /// The item reflects <see cref="RatingControl.PlaceholderRating"/> because the control is
    /// currently unrated.
    /// </summary>
    Placeholder,

    /// <summary>The item reflects the transient score under the pointer.</summary>
    Preview,
}
