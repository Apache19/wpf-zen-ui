// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Extensions;

/// <summary>
/// Extension that converts the typography type enumeration to the name of the resource that represents it.
/// </summary>
public static class TextBlockFontTypographyExtensions
{
    /// <summary>
    ///  Converts the typography type enumeration to the name of the resource that represents it.
    /// </summary>
    /// <returns>Name of the resource matching the <see cref="FontTypography"/>. <see cref="ArgumentOutOfRangeException"/> otherwise.</returns>
    public static ComponentResourceKey ToResourceKey(this FontTypography typography)
    {
        return typography switch
        {
            FontTypography.Caption         => FontTypographyKeys.CaptionFontTypography,
            FontTypography.CaptionStrong   => FontTypographyKeys.CaptionStrongFontTypography,
            FontTypography.Body            => FontTypographyKeys.BodyFontTypography,
            FontTypography.BodyStrong      => FontTypographyKeys.BodyStrongFontTypography,
            FontTypography.BodyLarge       => FontTypographyKeys.BodyLargeFontTypography,
            FontTypography.BodyLargeStrong => FontTypographyKeys.BodyLargeStrongFontTypography,
            FontTypography.SubtitleSmall   => FontTypographyKeys.SubtitleSmallFontTypography,
            FontTypography.Subtitle        => FontTypographyKeys.SubtitleFontTypography,
            FontTypography.Title           => FontTypographyKeys.TitleFontTypography,
            FontTypography.TitleLarge      => FontTypographyKeys.TitleLargeFontTypography,
            FontTypography.Display         => FontTypographyKeys.DisplayFontTypography,

            _ => throw new ArgumentOutOfRangeException(nameof(typography), typography, null),
        };
    }
}