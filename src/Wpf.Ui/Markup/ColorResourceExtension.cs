// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Key))]
[MarkupExtensionReturnType(typeof(Color))]
public sealed class ColorResourceExtension : MarkupExtension
{
    [ConstructorArgument("key")]
    public ColorKey Key { get; }

    public ColorResourceExtension(ColorKey key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = KeyMapping(Key);
        return new DynamicResourceExtension(key).ProvideValue(serviceProvider);
    }

    private static ComponentResourceKey KeyMapping(ColorKey key)
    {
        return key switch
        {
            ColorKey.SystemAccentColor                          => ColorKeys.SystemAccentColor,
            ColorKey.SystemAccentColorPrimary                   => ColorKeys.SystemAccentColorPrimary,
            ColorKey.SystemAccentColorSecondary                 => ColorKeys.SystemAccentColorSecondary,
            ColorKey.SystemAccentColorTertiary                  => ColorKeys.SystemAccentColorTertiary,
            ColorKey.SystemColorWindowTextColor                 => ColorKeys.SystemColorWindowTextColor,
            ColorKey.SystemColorWindowColor                     => ColorKeys.SystemColorWindowColor,
            ColorKey.SystemColorHighlightTextColor              => ColorKeys.SystemColorHighlightTextColor,
            ColorKey.SystemColorHighlightColor                  => ColorKeys.SystemColorHighlightColor,
            ColorKey.SystemColorControlTextColor                => ColorKeys.SystemColorControlTextColor,
            ColorKey.SystemColorControlColor                    => ColorKeys.SystemColorControlColor,
            ColorKey.SystemColorHotTrackColor                   => ColorKeys.SystemColorHotTrackColor,
            ColorKey.SystemColorGrayTextColor                   => ColorKeys.SystemColorGrayTextColor,
            ColorKey.SystemColorWindowFrameColor                => ColorKeys.SystemColorWindowFrameColor,
            ColorKey.WindowBackgroundColor                      => ColorKeys.WindowBackgroundColor,
            ColorKey.WindowBorderColor                          => ColorKeys.WindowBorderColor,
            ColorKey.WindowBorderColorInactive                  => ColorKeys.WindowBorderColorInactive,
            ColorKey.KeyboardFocusBorderColor                   => ColorKeys.KeyboardFocusBorderColor,
            ColorKey.TextFillColorPrimary                       => ColorKeys.TextFillColorPrimary,
            ColorKey.TextFillColorSecondary                     => ColorKeys.TextFillColorSecondary,
            ColorKey.TextFillColorTertiary                      => ColorKeys.TextFillColorTertiary,
            ColorKey.TextFillColorDisabled                      => ColorKeys.TextFillColorDisabled,
            ColorKey.TextPlaceholderColor                       => ColorKeys.TextPlaceholderColor,
            ColorKey.TextFillColorInverse                       => ColorKeys.TextFillColorInverse,
            ColorKey.AccentTextFillColorDisabled                => ColorKeys.AccentTextFillColorDisabled,
            ColorKey.TextOnAccentFillColorSelectedText          => ColorKeys.TextOnAccentFillColorSelectedText,
            ColorKey.TextOnAccentFillColorPrimary               => ColorKeys.TextOnAccentFillColorPrimary,
            ColorKey.TextOnAccentFillColorSecondary             => ColorKeys.TextOnAccentFillColorSecondary,
            ColorKey.TextOnAccentFillColorDisabled              => ColorKeys.TextOnAccentFillColorDisabled,
            ColorKey.ControlFillColorDefault                    => ColorKeys.ControlFillColorDefault,
            ColorKey.ControlFillColorSecondary                  => ColorKeys.ControlFillColorSecondary,
            ColorKey.ControlFillColorTertiary                   => ColorKeys.ControlFillColorTertiary,
            ColorKey.ControlFillColorDisabled                   => ColorKeys.ControlFillColorDisabled,
            ColorKey.ControlFillColorTransparent                => ColorKeys.ControlFillColorTransparent,
            ColorKey.ControlFillColorInputActive                => ColorKeys.ControlFillColorInputActive,
            ColorKey.ControlStrongFillColorDefault              => ColorKeys.ControlStrongFillColorDefault,
            ColorKey.ControlStrongFillColorDisabled             => ColorKeys.ControlStrongFillColorDisabled,
            ColorKey.ControlSolidFillColorDefault               => ColorKeys.ControlSolidFillColorDefault,
            ColorKey.SubtleFillColorTransparent                 => ColorKeys.SubtleFillColorTransparent,
            ColorKey.SubtleFillColorSecondary                   => ColorKeys.SubtleFillColorSecondary,
            ColorKey.SubtleFillColorTertiary                    => ColorKeys.SubtleFillColorTertiary,
            ColorKey.SubtleFillColorDisabled                    => ColorKeys.SubtleFillColorDisabled,
            ColorKey.ControlAltFillColorTransparent             => ColorKeys.ControlAltFillColorTransparent,
            ColorKey.ControlAltFillColorSecondary               => ColorKeys.ControlAltFillColorSecondary,
            ColorKey.ControlAltFillColorTertiary                => ColorKeys.ControlAltFillColorTertiary,
            ColorKey.ControlAltFillColorQuarternary             => ColorKeys.ControlAltFillColorQuarternary,
            ColorKey.ControlAltFillColorDisabled                => ColorKeys.ControlAltFillColorDisabled,
            ColorKey.ControlOnImageFillColorDefault             => ColorKeys.ControlOnImageFillColorDefault,
            ColorKey.ControlOnImageFillColorSecondary           => ColorKeys.ControlOnImageFillColorSecondary,
            ColorKey.ControlOnImageFillColorTertiary            => ColorKeys.ControlOnImageFillColorTertiary,
            ColorKey.ControlOnImageFillColorDisabled            => ColorKeys.ControlOnImageFillColorDisabled,
            ColorKey.AccentFillColorDisabled                    => ColorKeys.AccentFillColorDisabled,
            ColorKey.ControlStrokeColorDefault                  => ColorKeys.ControlStrokeColorDefault,
            ColorKey.ControlStrokeColorSecondary                => ColorKeys.ControlStrokeColorSecondary,
            ColorKey.ControlStrokeColorTertiary                 => ColorKeys.ControlStrokeColorTertiary,
            ColorKey.ControlStrokeColorOnAccentDefault          => ColorKeys.ControlStrokeColorOnAccentDefault,
            ColorKey.ControlStrokeColorOnAccentSecondary        => ColorKeys.ControlStrokeColorOnAccentSecondary,
            ColorKey.ControlStrokeColorOnAccentTertiary         => ColorKeys.ControlStrokeColorOnAccentTertiary,
            ColorKey.ControlStrokeColorOnAccentDisabled         => ColorKeys.ControlStrokeColorOnAccentDisabled,
            ColorKey.ControlStrokeColorForStrongFillWhenOnImage => ColorKeys.ControlStrokeColorForStrongFillWhenOnImage,
            ColorKey.CardStrokeColorDefault                     => ColorKeys.CardStrokeColorDefault,
            ColorKey.CardStrokeColorDefaultSolid                => ColorKeys.CardStrokeColorDefaultSolid,
            ColorKey.ControlStrongStrokeColorDefault            => ColorKeys.ControlStrongStrokeColorDefault,
            ColorKey.ControlStrongStrokeColorDisabled           => ColorKeys.ControlStrongStrokeColorDisabled,
            ColorKey.SurfaceStrokeColorDefault                  => ColorKeys.SurfaceStrokeColorDefault,
            ColorKey.SurfaceStrokeColorFlyout                   => ColorKeys.SurfaceStrokeColorFlyout,
            ColorKey.SurfaceStrokeColorInverse                  => ColorKeys.SurfaceStrokeColorInverse,
            ColorKey.DividerStrokeColorDefault                  => ColorKeys.DividerStrokeColorDefault,
            ColorKey.FocusStrokeColorOuter                      => ColorKeys.FocusStrokeColorOuter,
            ColorKey.FocusStrokeColorInner                      => ColorKeys.FocusStrokeColorInner,
            ColorKey.CardBackgroundFillColorDefault             => ColorKeys.CardBackgroundFillColorDefault,
            ColorKey.CardBackgroundFillColorSecondary           => ColorKeys.CardBackgroundFillColorSecondary,
            ColorKey.SmokeFillColorDefault                      => ColorKeys.SmokeFillColorDefault,
            ColorKey.LayerFillColorDefault                      => ColorKeys.LayerFillColorDefault,
            ColorKey.LayerFillColorAlt                          => ColorKeys.LayerFillColorAlt,
            ColorKey.LayerOnAcrylicFillColorDefault             => ColorKeys.LayerOnAcrylicFillColorDefault,
            ColorKey.LayerOnAccentAcrylicFillColorDefault       => ColorKeys.LayerOnAccentAcrylicFillColorDefault,
            ColorKey.SnowflakeGradientTop                       => ColorKeys.SnowflakeGradientTop,
            ColorKey.SnowflakeGradientBottom                    => ColorKeys.SnowflakeGradientBottom,
            ColorKey.AcrylicBackgroundFillColorDefault          => ColorKeys.AcrylicBackgroundFillColorDefault,
            ColorKey.LayerOnMicaBaseAltFillColorDefault         => ColorKeys.LayerOnMicaBaseAltFillColorDefault,
            ColorKey.LayerOnMicaBaseAltFillColorSecondary       => ColorKeys.LayerOnMicaBaseAltFillColorSecondary,
            ColorKey.LayerOnMicaBaseAltFillColorTertiary        => ColorKeys.LayerOnMicaBaseAltFillColorTertiary,
            ColorKey.LayerOnMicaBaseAltFillColorTransparent     => ColorKeys.LayerOnMicaBaseAltFillColorTransparent,
            ColorKey.SolidBackgroundFillColorBase               => ColorKeys.SolidBackgroundFillColorBase,
            ColorKey.SolidBackgroundFillColorSecondary          => ColorKeys.SolidBackgroundFillColorSecondary,
            ColorKey.SolidBackgroundFillColorTertiary           => ColorKeys.SolidBackgroundFillColorTertiary,
            ColorKey.SolidBackgroundFillColorQuarternary        => ColorKeys.SolidBackgroundFillColorQuarternary,
            ColorKey.SolidBackgroundFillColorTransparent        => ColorKeys.SolidBackgroundFillColorTransparent,
            ColorKey.SolidBackgroundFillColorBaseAlt            => ColorKeys.SolidBackgroundFillColorBaseAlt,
            ColorKey.SystemFillColorAttention                   => ColorKeys.SystemFillColorAttention,
            ColorKey.SystemFillColorInformational               => ColorKeys.SystemFillColorInformational,
            ColorKey.SystemFillColorSuccess                     => ColorKeys.SystemFillColorSuccess,
            ColorKey.SystemFillColorCaution                     => ColorKeys.SystemFillColorCaution,
            ColorKey.SystemFillColorCritical                    => ColorKeys.SystemFillColorCritical,
            ColorKey.SystemFillColorNeutral                     => ColorKeys.SystemFillColorNeutral,
            ColorKey.SystemFillColorSolidNeutral                => ColorKeys.SystemFillColorSolidNeutral,
            ColorKey.SystemFillColorAttentionBackground         => ColorKeys.SystemFillColorAttentionBackground,
            ColorKey.SystemFillColorSuccessBackground           => ColorKeys.SystemFillColorSuccessBackground,
            ColorKey.SystemFillColorCautionBackground           => ColorKeys.SystemFillColorCautionBackground,
            ColorKey.SystemFillColorCriticalBackground          => ColorKeys.SystemFillColorCriticalBackground,
            ColorKey.SystemFillColorNeutralBackground           => ColorKeys.SystemFillColorNeutralBackground,
            ColorKey.SystemFillColorSolidAttentionBackground    => ColorKeys.SystemFillColorSolidAttentionBackground,
            ColorKey.SystemFillColorSolidNeutralBackground      => ColorKeys.SystemFillColorSolidNeutralBackground,
            ColorKey.PalettePrimaryColor                        => ColorKeys.PalettePrimaryColor,
            ColorKey.PaletteRedColor                            => ColorKeys.PaletteRedColor,
            ColorKey.PalettePinkColor                           => ColorKeys.PalettePinkColor,
            ColorKey.PalettePurpleColor                         => ColorKeys.PalettePurpleColor,
            ColorKey.PaletteDeepPurpleColor                     => ColorKeys.PaletteDeepPurpleColor,
            ColorKey.PaletteIndigoColor                         => ColorKeys.PaletteIndigoColor,
            ColorKey.PaletteBlueColor                           => ColorKeys.PaletteBlueColor,
            ColorKey.PaletteLightBlueColor                      => ColorKeys.PaletteLightBlueColor,
            ColorKey.PaletteCyanColor                           => ColorKeys.PaletteCyanColor,
            ColorKey.PaletteTealColor                           => ColorKeys.PaletteTealColor,
            ColorKey.PaletteGreenColor                          => ColorKeys.PaletteGreenColor,
            ColorKey.PaletteLightGreenColor                     => ColorKeys.PaletteLightGreenColor,
            ColorKey.PaletteLimeColor                           => ColorKeys.PaletteLimeColor,
            ColorKey.PaletteYellowColor                         => ColorKeys.PaletteYellowColor,
            ColorKey.PaletteAmberColor                          => ColorKeys.PaletteAmberColor,
            ColorKey.PaletteOrangeColor                         => ColorKeys.PaletteOrangeColor,
            ColorKey.PaletteDeepOrangeColor                     => ColorKeys.PaletteDeepOrangeColor,
            ColorKey.PaletteBrownColor                          => ColorKeys.PaletteBrownColor,
            ColorKey.PaletteGreyColor                           => ColorKeys.PaletteGreyColor,
            ColorKey.PaletteBlueGreyColor                       => ColorKeys.PaletteBlueGreyColor,
            ColorKey.TextFillColorLightPrimary                  => ColorKeys.TextFillColorLightPrimary,
            ColorKey.TextFillColorLightSecondary                => ColorKeys.TextFillColorLightSecondary,
            ColorKey.TextFillColorLightTertiary                 => ColorKeys.TextFillColorLightTertiary,
            ColorKey.TextFillColorLightDisabled                 => ColorKeys.TextFillColorLightDisabled,
            ColorKey.TextFillColorLightInverse                  => ColorKeys.TextFillColorLightInverse,
            ColorKey.TextFillColorDarkPrimary                   => ColorKeys.TextFillColorDarkPrimary,
            ColorKey.TextFillColorDarkSecondary                 => ColorKeys.TextFillColorDarkSecondary,
            ColorKey.TextFillColorDarkTertiary                  => ColorKeys.TextFillColorDarkTertiary,
            ColorKey.TextFillColorDarkDisabled                  => ColorKeys.TextFillColorDarkDisabled,
            ColorKey.TextFillColorDarkInverse                   => ColorKeys.TextFillColorDarkInverse,
            ColorKey.ApplicationBackgroundColorLight            => ColorKeys.ApplicationBackgroundColorLight,
            ColorKey.ApplicationBackgroundColorDark             => ColorKeys.ApplicationBackgroundColorDark,
            ColorKey.ControlStrongFillColorLight                => ColorKeys.ControlStrongFillColorLight,
            ColorKey.ControlStrongFillColorDark                 => ColorKeys.ControlStrongFillColorDark,
            ColorKey.Unknown                                    => throw new NotSupportedException(),
            _                                                   => throw new IndexOutOfRangeException(),
        };
    }
}
