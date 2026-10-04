// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Describes an immutable font glyph that can be drawn without creating an icon element.
/// </summary>
/// <remarks>
/// Implement this contract for additional font icon families. Replace the source to change the
/// glyph; consumers may cache its text layout. Size and foreground belong to the drawing host.
/// </remarks>
public abstract class FontIconSource : IconSource
{
    /// <summary>
    /// Gets the Unicode text representing the icon, including any surrogate pair.
    /// </summary>
    public abstract string Glyph { get; }

    /// <summary>
    /// Gets the default font family containing the glyph, independent of any presentation host.
    /// </summary>
    public abstract FontFamily FontFamily { get; }

    /// <summary>
    /// Gets the optional font resource resolved in a host's local resource scope.
    /// The default null uses FontFamily directly. Resource changes belong to the host;
    /// the shared source and its default font remain immutable.
    /// </summary>
    public virtual object? FontFamilyResourceKey => null;

    /// <summary>
    /// Returns an immutable description for a presentation request without changing this source.
    /// </summary>
    /// <param name="variant">The host's requested appearance.</param>
    /// <returns>This source by default, including for unsupported variants.</returns>
    /// <remarks>
    /// Override when the family supports state variants. Return a stable, non-null description
    /// with matching Glyph, FontFamily and FontFamilyResourceKey. Hosts never interpret the
    /// family's symbol catalogue. Family-specific variants may also be set in its constructor.
    /// </remarks>
    public virtual FontIconSource WithVariant(IconVariant variant)
    {
        return this;
    }
}

