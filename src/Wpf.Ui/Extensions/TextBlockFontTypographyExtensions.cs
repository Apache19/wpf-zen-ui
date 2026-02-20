// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;
using Wpf.Ui.Markup;

namespace Wpf.Ui.Extensions;

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
            FontTypography.Caption    => TypographyStyleKeys.CaptionFontTypography,
            FontTypography.Body       => TypographyStyleKeys.BodyFontTypography,
            FontTypography.BodyStrong => TypographyStyleKeys.BodyStrongFontTypography,
            FontTypography.Subtitle   => TypographyStyleKeys.SubtitleFontTypography,
            FontTypography.Title      => TypographyStyleKeys.TitleFontTypography,
            FontTypography.TitleLarge => TypographyStyleKeys.TitleLargeFontTypography,
            FontTypography.Display    => TypographyStyleKeys.DisplayFontTypography,
            _                         => throw new ArgumentOutOfRangeException(nameof(typography), typography, null),
        };
    }
}