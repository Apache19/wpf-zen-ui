// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Gallery.Models;

namespace Wpf.Zen.Ui.Gallery.ViewModels.Pages.Collections;

/// <summary>
/// Supplies a varied synthetic movie catalogue for the responsive
/// <see cref="Wpf.Zen.Ui.Controls.VirtualizingWrapPanel"/> gallery page.
/// </summary>
public sealed class VirtualizingWrapPanelViewModel : ViewModel
{
    /// <summary>
    /// Initializes a new instance of the <see cref="VirtualizingWrapPanelViewModel"/> class.
    /// </summary>
    public VirtualizingWrapPanelViewModel()
    {
        // Use an explicit List target here. Targeting IReadOnlyList<T> directly with a collection
        // expression makes the compiler emit a global-namespace wrapper type; the gallery's DI
        // convention scanner currently expects every assembly type to have a namespace.
        Movies = new List<MoviePoster>
        {
            CreateMovie("NEON HORIZON", "Beyond the edge of memory", "SCI-FI", 2026, "8.7", "#081B33", "#116B8C", "#52E5FF"),
            CreateMovie("CHROMA ZERO", "Color is the last signal", "THRILLER", 2025, "8.2", "#24103F", "#8B1E68", "#FF4FD8"),
            CreateMovie("ORBITAL DAWN", "A new sun. An old secret.", "SPACE", 2027, "9.1", "#2A1707", "#A94F13", "#FFC857"),
            CreateMovie("VOID RUNNER", "There is no way back", "ACTION", 2024, "7.9", "#071E1B", "#0B6B5C", "#4CFFD0"),
            CreateMovie("SYNTHETIC SKY", "Dreams compiled at midnight", "DRAMA", 2026, "8.5", "#11182F", "#4456A6", "#9AA8FF"),
            CreateMovie("RED VECTOR", "Every path leaves a trace", "MYSTERY", 2025, "8.0", "#310D17", "#9A1F42", "#FF5A7D"),
            CreateMovie("PHOTON CITY", "The night learned to glow", "CYBERPUNK", 2027, "8.9", "#151031", "#5225A7", "#7CFFEB"),
            CreateMovie("ECHO PROTOCOL", "Silence was never empty", "ESPIONAGE", 2024, "8.3", "#0A2230", "#174F68", "#57C8FF"),
            CreateMovie("GLASS PLANET", "Fragile worlds cast long shadows", "ADVENTURE", 2026, "7.8", "#132A28", "#35756C", "#B7FFE1"),
            CreateMovie("NIGHT CIRCUIT", "One city. Infinite loops.", "NOIR", 2025, "8.6", "#191324", "#5B315E", "#E38BFF"),
            CreateMovie("SOLARIS 9", "The signal came from home", "SCI-FI", 2028, "9.0", "#301B06", "#A15B0B", "#FFE26A"),
            CreateMovie("QUANTUM TIDE", "Time moves in both directions", "SCI-FI", 2026, "8.4", "#082133", "#096A8A", "#52B8FF"),
            CreateMovie("VELVET STATIC", "Tune in before it fades", "MUSIC", 2025, "7.7", "#291027", "#812859", "#FF76B7"),
            CreateMovie("ARCTIC SIGNAL", "The cold keeps perfect records", "MYSTERY", 2027, "8.1", "#102333", "#2A6880", "#B7F4FF"),
            CreateMovie("EMBER CODE", "The firewall is alive", "THRILLER", 2026, "8.8", "#321108", "#A53A18", "#FF9D52"),
            CreateMovie("LUNAR ARCHIVE", "Nothing on the moon stays buried", "SPACE", 2028, "9.2", "#10182B", "#354B7A", "#B8C8FF"),
            CreateMovie("MIRROR STATE", "Reality has a duplicate", "DRAMA", 2025, "8.0", "#132626", "#3A7471", "#7FFFD4"),
            CreateMovie("COBALT RAIN", "The forecast changed everything", "NOIR", 2024, "7.6", "#091D3A", "#164F9A", "#4E9FFF"),
            CreateMovie("PARALLAX", "Look twice. Decide once.", "MYSTERY", 2027, "8.6", "#21133A", "#6137A3", "#C091FF"),
            CreateMovie("AFTER IMAGE", "Some memories refuse to fade", "DRAMA", 2026, "8.3", "#35111F", "#943451", "#FF8EAA"),
            CreateMovie("ION STORM", "No shelter above the clouds", "ACTION", 2025, "8.1", "#08292D", "#087B83", "#75F5FF"),
            CreateMovie("GHOST FREQUENCY", "Every machine has a voice", "HORROR", 2027, "8.4", "#171229", "#4E357B", "#B584FF"),
            CreateMovie("APHELION", "Farthest from the light", "SPACE", 2028, "8.9", "#261A09", "#765214", "#FFD773"),
            CreateMovie("DATA BLOOM", "A garden grew inside the network", "CYBERPUNK", 2026, "8.7", "#0E281B", "#1C7545", "#6CFF9F"),
        };
    }

    /// <summary>Gets the movie entries rendered by the sample's virtualizing items control.</summary>
    public IReadOnlyList<MoviePoster> Movies { get; }

    private static MoviePoster CreateMovie(
        string title,
        string tagline,
        string genre,
        int year,
        string rating,
        string shadow,
        string midtone,
        string highlight
    )
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0D, 0D),
            EndPoint = new Point(1D, 1D),
            GradientStops =
            {
                new GradientStop((Color)ColorConverter.ConvertFromString(highlight), 0D),
                new GradientStop((Color)ColorConverter.ConvertFromString(midtone), 0.58D),
                new GradientStop((Color)ColorConverter.ConvertFromString(shadow), 1D),
            },
        };

        // Movie entries never mutate their palette. Freezing turns every gradient into an
        // immutable composition resource that WPF can safely share with the render thread.
        brush.Freeze();

        string[] words = title.Split(' ');
        string monogram = words.Length > 1
            ? $"{words[0][0]}{words[1][0]}"
            : title[..Math.Min(2, title.Length)];

        return new MoviePoster
        {
            Title = title,
            Tagline = tagline,
            Genre = genre,
            Year = year,
            Rating = rating,
            PosterBrush = brush,
            Monogram = monogram,
        };
    }
}
