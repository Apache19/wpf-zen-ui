// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes a Fluent System Icons glyph independently of the visual element that presents it.
/// </summary>
/// <remarks>
/// The immutable source can be stored in view models and resources or shared by any number of
/// controls. The library's implicit data template creates a distinct <see cref="FluentIcon"/> for
/// each presentation, avoiding WPF visual-parent conflicts. The containing
/// <see cref="IconPresenter"/> may request a temporary <see cref="IconVariant"/> without mutating
/// this source.
/// </remarks>
public sealed class FluentIconSource : IconSource
{
    /// <summary>Initializes a new instance of the <see cref="FluentIconSource"/> class.</summary>
    /// <param name="symbol">The glyph identity from the regular symbol catalogue.</param>
    /// <param name="filled">
    /// <see langword="true"/> to use the filled form by default; otherwise,
    /// <see langword="false"/> to use the regular form.
    /// </param>
    public FluentIconSource(SymbolRegular symbol, bool filled = false)
    {
        Symbol = symbol;
        Filled = filled;
    }

    /// <summary>Gets the glyph identity shared by the regular and filled Fluent icon fonts.</summary>
    public SymbolRegular Symbol { get; }

    /// <summary>Gets a value indicating whether the source prefers the filled form by default.</summary>
    public bool Filled { get; }
}
