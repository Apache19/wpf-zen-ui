// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

#pragma warning disable SA1516 // Elements should be separated by blank line

public static class FontTypographyKeys
{
    public static ComponentResourceKey CaptionFontTypography         { get; } = Create(nameof(CaptionFontTypography));
    public static ComponentResourceKey CaptionStrongFontTypography   { get; } = Create(nameof(CaptionStrongFontTypography));
    public static ComponentResourceKey BodyFontTypography            { get; } = Create(nameof(BodyFontTypography));
    public static ComponentResourceKey BodyStrongFontTypography      { get; } = Create(nameof(BodyStrongFontTypography));
    public static ComponentResourceKey BodyLargeFontTypography       { get; } = Create(nameof(BodyLargeFontTypography));
    public static ComponentResourceKey BodyLargeStrongFontTypography { get; } = Create(nameof(BodyLargeStrongFontTypography));
    public static ComponentResourceKey SubtitleSmallFontTypography   { get; } = Create(nameof(SubtitleSmallFontTypography));
    public static ComponentResourceKey SubtitleFontTypography        { get; } = Create(nameof(SubtitleFontTypography));
    public static ComponentResourceKey TitleFontTypography           { get; } = Create(nameof(TitleFontTypography));
    public static ComponentResourceKey TitleLargeFontTypography      { get; } = Create(nameof(TitleLargeFontTypography));
    public static ComponentResourceKey DisplayFontTypography         { get; } = Create(nameof(DisplayFontTypography));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}

#pragma warning disable SA1516 // Elements should be separated by blank line