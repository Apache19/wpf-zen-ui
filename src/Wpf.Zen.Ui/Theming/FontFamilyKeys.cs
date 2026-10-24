// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides resource keys for the library's embedded Fluent System Icons font families.
/// </summary>
public static class FontFamilyKeys
{
    /// <summary>
    /// Gets the key for the embedded regular Fluent System Icons font family.
    /// </summary>
    public static ComponentResourceKey FluentSystemIcons { get; } = Create(nameof(FluentSystemIcons));

    /// <summary>
    /// Gets the key for the embedded filled Fluent System Icons font family.
    /// </summary>
    public static ComponentResourceKey FluentSystemIconsFilled { get; } = Create(nameof(FluentSystemIconsFilled));

    /// <summary>
    /// Creates an assembly-scoped key resolved through the library's theme resource dictionary.
    /// </summary>
    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
