// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.Extensions;

/// <summary>
/// Extension that converts the text color type enumeration to the name of the resource that represents it.
/// </summary>
public static class TextColorExtensions
{
    /// <summary>
    /// Converts the text color type enumeration to the name of the resource that represents it.
    /// </summary>
    /// <returns>Name of the resource matching the <see cref="TextColor"/>. <see cref="ArgumentOutOfRangeException"/> otherwise.</returns>
    public static ComponentResourceKey ToResourceKey(this TextColor textColor)
    {
        return textColor switch
        {
            TextColor.Primary   => BrushKeys.TextFillColorPrimaryBrush,
            TextColor.Secondary => BrushKeys.TextFillColorSecondaryBrush,
            TextColor.Tertiary  => BrushKeys.TextFillColorTertiaryBrush,
            TextColor.Disabled  => BrushKeys.TextFillColorDisabledBrush,
            _ => throw new ArgumentOutOfRangeException(nameof(textColor), textColor, null),
        };
    }
}