// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes a gallery navigation destination and its presentation.
/// </summary>
public record NavigationCard
{
    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets the reusable font icon description displayed by the gallery.
    /// </summary>
    public FontIconSource IconSource => new FluentIconSource(Icon);

    /// <summary>
    /// Gets or sets the Fluent symbol represented by this item.
    /// </summary>
    public FluentSymbolRegular Icon { get; init; }

    /// <summary>
    /// Gets the optional destination description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the page type opened when the card is selected.
    /// </summary>
    public Type? PageType { get; init; }
}
