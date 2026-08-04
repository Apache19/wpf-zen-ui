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
/// <para>
/// A Fluent System Icons glyph is identified by its regular symbol name and by the requested
/// regular or filled form. Keeping both values in one immutable model prevents content controls
/// from losing the form when WPF resolves an implicit data template.
/// </para>
/// <para>
/// The model deliberately contains no WPF visual state and can therefore be shared by styles,
/// view models, and multiple content presenters. The default template creates a separate
/// <see cref="SymbolIcon"/> for every presentation.
/// </para>
/// </remarks>
public sealed class FluentSystemIcon
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FluentSystemIcon"/> class.
    /// </summary>
    /// <param name="symbol">The glyph identity from the regular symbol catalogue.</param>
    /// <param name="filled">
    /// <see langword="true"/> to request the matching filled glyph; otherwise,
    /// <see langword="false"/> to request the regular glyph.
    /// </param>
    public FluentSystemIcon(SymbolRegular symbol, bool filled = false)
    {
        Symbol = symbol;
        Filled = filled;
    }

    /// <summary>
    /// Gets the glyph identity shared by the regular and filled Fluent System Icons fonts.
    /// </summary>
    public SymbolRegular Symbol { get; }

    /// <summary>
    /// Gets a value indicating whether the filled form of <see cref="Symbol"/> is requested.
    /// </summary>
    public bool Filled { get; }
}
