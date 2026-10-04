// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Models;

/// <summary>
/// Describes one searchable Fluent symbol in the icon catalogue.
/// </summary>
public struct DisplayableIcon
{
    /// <summary>
    /// Gets or sets the catalogue identity used by selection commands.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the display name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the hexadecimal Unicode code point.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets the character shown in the catalogue details.
    /// </summary>
    public string Symbol { get; set; }

    /// <summary>
    /// Gets the reusable font icon description displayed by the gallery.
    /// </summary>
    public FontIconSource IconSource => new FluentIconSource(Icon);

    /// <summary>
    /// Gets or sets the Fluent symbol represented by this item.
    /// </summary>
    public FluentSymbolRegular Icon { get; set; }
}
