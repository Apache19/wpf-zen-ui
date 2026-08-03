// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

#pragma warning disable SA1516 // Elements should be separated by blank line

public static class FontFamilyKeys
{
    public static ComponentResourceKey ContentControlThemeFontFamily { get; } = Create(nameof(ContentControlThemeFontFamily));
    public static ComponentResourceKey SegoeFluentIcons              { get; } = Create(nameof(SegoeFluentIcons));
    public static ComponentResourceKey FluentSystemIcons             { get; } = Create(nameof(FluentSystemIcons));
    public static ComponentResourceKey FluentSystemIconsFilled       { get; } = Create(nameof(FluentSystemIconsFilled));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}

#pragma warning disable SA1516 // Elements should be separated by blank line