// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes one synthetic movie entry displayed by the responsive poster-library sample.
/// </summary>
/// <remarks>
/// The gallery deliberately keeps the artwork as a brush instead of shipping poster images. This
/// lets the sample remain self-contained while still exercising a visually rich item template at
/// every responsive size selected by the <see cref="Wpf.Zen.Ui.Controls.VirtualizingWrapPanel"/>.
/// </remarks>
public sealed record MoviePoster
{
    /// <summary>Gets the movie title rendered on the poster and in its metadata area.</summary>
    public required string Title { get; init; }

    /// <summary>Gets the short thematic tagline displayed below the title.</summary>
    public required string Tagline { get; init; }

    /// <summary>Gets the compact genre label displayed at the top of the poster.</summary>
    public required string Genre { get; init; }

    /// <summary>Gets the fictional release year used by the movie-library presentation.</summary>
    public required int Year { get; init; }

    /// <summary>Gets the fictional audience rating displayed in the card footer.</summary>
    public required string Rating { get; init; }

    /// <summary>
    /// Gets the frozen gradient brush that supplies the poster's color identity. The geometric
    /// circuit texture is layered over this brush by the item template so one shared texture can
    /// be reused across all cards.
    /// </summary>
    public required Brush PosterBrush { get; init; }

    /// <summary>
    /// Gets the two-character display mark used as an abstract focal element in place of cover art.
    /// </summary>
    public required string Monogram { get; init; }
}
