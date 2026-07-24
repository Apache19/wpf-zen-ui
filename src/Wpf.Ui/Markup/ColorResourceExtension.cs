// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

#pragma warning disable WPF0081 // This markup must be used only on the Color.

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(Color))]
public sealed class ColorResourceExtension : DynamicResourceExtension
{
    [ConstructorArgument("id")]
    public ColorId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = KeyMapping(value);
        }
    }

    public ColorResourceExtension(ColorId id)
    {
        Id = id;
    }

    internal static ComponentResourceKey KeyMapping(ColorId key)
    {
        return key switch
        {
            ColorId.AccentFillPrimary                          => ColorKeys.AccentFillPrimary,
            ColorId.AccentFillSecondary                        => ColorKeys.AccentFillSecondary,
            ColorId.AccentFillTertiary                         => ColorKeys.AccentFillTertiary,
            ColorId.AccentTextPrimary                          => ColorKeys.AccentTextPrimary,
            ColorId.AccentTextSecondary                        => ColorKeys.AccentTextSecondary,
            ColorId.TextOnAccentPrimary                        => ColorKeys.TextOnAccentPrimary,
            ColorId.AccentStroke                               => ColorKeys.AccentStroke,
            ColorId.AccentSelection                            => ColorKeys.AccentSelection,
            ColorId.SystemColorWindowTextColor                 => ColorKeys.SystemColorWindowTextColor,
            ColorId.SystemColorWindowColor                     => ColorKeys.SystemColorWindowColor,
            ColorId.SystemColorHighlightTextColor              => ColorKeys.SystemColorHighlightTextColor,
            ColorId.SystemColorHighlightColor                  => ColorKeys.SystemColorHighlightColor,
            ColorId.SystemColorControlTextColor                => ColorKeys.SystemColorControlTextColor,
            ColorId.SystemColorControlColor                    => ColorKeys.SystemColorControlColor,
            ColorId.SystemColorHotTrackColor                   => ColorKeys.SystemColorHotTrackColor,
            ColorId.SystemColorGrayTextColor                   => ColorKeys.SystemColorGrayTextColor,
            ColorId.SystemColorWindowFrameColor                => ColorKeys.SystemColorWindowFrameColor,
            ColorId.WindowBackgroundColor                      => ColorKeys.WindowBackgroundColor,
            ColorId.WindowBorderColor                          => ColorKeys.WindowBorderColor,
            ColorId.WindowBorderColorInactive                  => ColorKeys.WindowBorderColorInactive,
            ColorId.KeyboardFocusBorderColor                   => ColorKeys.KeyboardFocusBorderColor,
            ColorId.TextFillColorPrimary                       => ColorKeys.TextFillColorPrimary,
            ColorId.TextFillColorSecondary                     => ColorKeys.TextFillColorSecondary,
            ColorId.TextFillColorTertiary                      => ColorKeys.TextFillColorTertiary,
            ColorId.TextFillColorDisabled                      => ColorKeys.TextFillColorDisabled,
            ColorId.TextPlaceholderColor                       => ColorKeys.TextPlaceholderColor,
            ColorId.TextFillColorInverse                       => ColorKeys.TextFillColorInverse,
            ColorId.ControlFillColorPrimary                    => ColorKeys.ControlFillColorPrimary,
            ColorId.ControlFillColorSecondary                  => ColorKeys.ControlFillColorSecondary,
            ColorId.ControlFillColorTertiary                   => ColorKeys.ControlFillColorTertiary,
            ColorId.ControlFillColorDisabled                   => ColorKeys.ControlFillColorDisabled,
            ColorId.ControlFillColorInputActive                => ColorKeys.ControlFillColorInputActive,
            ColorId.ControlStrongFillColorDefault              => ColorKeys.ControlStrongFillColorDefault,
            ColorId.ControlStrongFillColorDisabled             => ColorKeys.ControlStrongFillColorDisabled,
            ColorId.ControlSolidFillColorDefault               => ColorKeys.ControlSolidFillColorDefault,
            ColorId.SubtleFillColorTransparent                 => ColorKeys.SubtleFillColorTransparent,
            ColorId.SubtleFillColorSecondary                   => ColorKeys.SubtleFillColorSecondary,
            ColorId.SubtleFillColorTertiary                    => ColorKeys.SubtleFillColorTertiary,
            ColorId.SubtleFillColorDisabled                    => ColorKeys.SubtleFillColorDisabled,
            ColorId.ControlAltFillColorTransparent             => ColorKeys.ControlAltFillColorTransparent,
            ColorId.ControlAltFillColorSecondary               => ColorKeys.ControlAltFillColorSecondary,
            ColorId.ControlAltFillColorTertiary                => ColorKeys.ControlAltFillColorTertiary,
            ColorId.ControlAltFillColorQuarternary             => ColorKeys.ControlAltFillColorQuarternary,
            ColorId.ControlAltFillColorDisabled                => ColorKeys.ControlAltFillColorDisabled,
            ColorId.ControlOnImageFillColorDefault             => ColorKeys.ControlOnImageFillColorDefault,
            ColorId.ControlOnImageFillColorSecondary           => ColorKeys.ControlOnImageFillColorSecondary,
            ColorId.ControlOnImageFillColorTertiary            => ColorKeys.ControlOnImageFillColorTertiary,
            ColorId.ControlOnImageFillColorDisabled            => ColorKeys.ControlOnImageFillColorDisabled,
            ColorId.ControlStrokeColorDefault                  => ColorKeys.ControlStrokeColorDefault,
            ColorId.ControlStrokeColorSecondary                => ColorKeys.ControlStrokeColorSecondary,
            ColorId.ControlStrokeColorTertiary                 => ColorKeys.ControlStrokeColorTertiary,
            ColorId.ControlStrokeColorOnAccentDefault          => ColorKeys.ControlStrokeColorOnAccentDefault,
            ColorId.ControlStrokeColorOnAccentSecondary        => ColorKeys.ControlStrokeColorOnAccentSecondary,
            ColorId.ControlStrokeColorOnAccentTertiary         => ColorKeys.ControlStrokeColorOnAccentTertiary,
            ColorId.ControlStrokeColorOnAccentDisabled         => ColorKeys.ControlStrokeColorOnAccentDisabled,
            ColorId.ControlStrokeColorForStrongFillWhenOnImage => ColorKeys.ControlStrokeColorForStrongFillWhenOnImage,
            ColorId.CardStrokeColorDefault                     => ColorKeys.CardStrokeColorDefault,
            ColorId.CardStrokeColorDefaultSolid                => ColorKeys.CardStrokeColorDefaultSolid,
            ColorId.ControlStrongStrokeColorDefault            => ColorKeys.ControlStrongStrokeColorDefault,
            ColorId.ControlStrongStrokeColorDisabled           => ColorKeys.ControlStrongStrokeColorDisabled,
            ColorId.SurfaceStrokeColorDefault                  => ColorKeys.SurfaceStrokeColorDefault,
            ColorId.SurfaceStrokeColorFlyout                   => ColorKeys.SurfaceStrokeColorFlyout,
            ColorId.SurfaceStrokeColorInverse                  => ColorKeys.SurfaceStrokeColorInverse,
            ColorId.DividerStrokeColorDefault                  => ColorKeys.DividerStrokeColorDefault,
            ColorId.FocusStrokeColorOuter                      => ColorKeys.FocusStrokeColorOuter,
            ColorId.FocusStrokeColorInner                      => ColorKeys.FocusStrokeColorInner,
            ColorId.CardBackgroundFillColorDefault             => ColorKeys.CardBackgroundFillColorDefault,
            ColorId.CardBackgroundFillColorSecondary           => ColorKeys.CardBackgroundFillColorSecondary,
            ColorId.EmbeddedFillColorDefault                   => ColorKeys.EmbeddedFillColorDefault,
            ColorId.EmbeddedFillColorStrong                    => ColorKeys.EmbeddedFillColorStrong,
            ColorId.EmbeddedFillColorDeep                      => ColorKeys.EmbeddedFillColorDeep,
            ColorId.SmokeFillColorDefault                      => ColorKeys.SmokeFillColorDefault,
            ColorId.LayerFillColorDefault                      => ColorKeys.LayerFillColorDefault,
            ColorId.LayerFillColorAlt                          => ColorKeys.LayerFillColorAlt,
            ColorId.LayerOnAcrylicFillColorDefault             => ColorKeys.LayerOnAcrylicFillColorDefault,
            ColorId.LayerOnAccentAcrylicFillColorDefault       => ColorKeys.LayerOnAccentAcrylicFillColorDefault,
            ColorId.SnowflakeGradientTop                       => ColorKeys.SnowflakeGradientTop,
            ColorId.SnowflakeGradientBottom                    => ColorKeys.SnowflakeGradientBottom,
            ColorId.AcrylicBackgroundFillColorDefault          => ColorKeys.AcrylicBackgroundFillColorDefault,
            ColorId.LayerOnMicaBaseAltFillColorDefault         => ColorKeys.LayerOnMicaBaseAltFillColorDefault,
            ColorId.LayerOnMicaBaseAltFillColorSecondary       => ColorKeys.LayerOnMicaBaseAltFillColorSecondary,
            ColorId.LayerOnMicaBaseAltFillColorTertiary        => ColorKeys.LayerOnMicaBaseAltFillColorTertiary,
            ColorId.LayerOnMicaBaseAltFillColorTransparent     => ColorKeys.LayerOnMicaBaseAltFillColorTransparent,
            ColorId.SolidBackgroundFillColorBase               => ColorKeys.SolidBackgroundFillColorBase,
            ColorId.SolidBackgroundFillColorSecondary          => ColorKeys.SolidBackgroundFillColorSecondary,
            ColorId.SolidBackgroundFillColorTertiary           => ColorKeys.SolidBackgroundFillColorTertiary,
            ColorId.SolidBackgroundFillColorQuarternary        => ColorKeys.SolidBackgroundFillColorQuarternary,
            ColorId.SolidBackgroundFillColorTransparent        => ColorKeys.SolidBackgroundFillColorTransparent,
            ColorId.SolidBackgroundFillColorBaseAlt            => ColorKeys.SolidBackgroundFillColorBaseAlt,
            ColorId.SystemFillColorAttention                   => ColorKeys.SystemFillColorAttention,
            ColorId.SystemFillColorInformational               => ColorKeys.SystemFillColorInformational,
            ColorId.SystemFillColorSuccess                     => ColorKeys.SystemFillColorSuccess,
            ColorId.SystemFillColorCaution                     => ColorKeys.SystemFillColorCaution,
            ColorId.SystemFillColorCritical                    => ColorKeys.SystemFillColorCritical,
            ColorId.SystemFillColorNeutral                     => ColorKeys.SystemFillColorNeutral,
            ColorId.SystemFillColorSolidNeutral                => ColorKeys.SystemFillColorSolidNeutral,
            ColorId.SystemFillColorAttentionBackground         => ColorKeys.SystemFillColorAttentionBackground,
            ColorId.SystemFillColorSuccessBackground           => ColorKeys.SystemFillColorSuccessBackground,
            ColorId.SystemFillColorCautionBackground           => ColorKeys.SystemFillColorCautionBackground,
            ColorId.SystemFillColorCriticalBackground          => ColorKeys.SystemFillColorCriticalBackground,
            ColorId.SystemFillColorNeutralBackground           => ColorKeys.SystemFillColorNeutralBackground,
            ColorId.SystemFillColorSolidAttentionBackground    => ColorKeys.SystemFillColorSolidAttentionBackground,
            ColorId.SystemFillColorSolidNeutralBackground      => ColorKeys.SystemFillColorSolidNeutralBackground,
            ColorId.PalettePrimaryColor                        => ColorKeys.PalettePrimaryColor,
            ColorId.PaletteRedColor                            => ColorKeys.PaletteRedColor,
            ColorId.PaletteRedColorBright                      => ColorKeys.PaletteRedColorBright,
            ColorId.PaletteRedColorDim                         => ColorKeys.PaletteRedColorDim,
            ColorId.PalettePinkColor                           => ColorKeys.PalettePinkColor,
            ColorId.PalettePinkColorBright                     => ColorKeys.PalettePinkColorBright,
            ColorId.PalettePinkColorDim                        => ColorKeys.PalettePinkColorDim,
            ColorId.PalettePurpleColor                         => ColorKeys.PalettePurpleColor,
            ColorId.PaletteDeepPurpleColor                     => ColorKeys.PaletteDeepPurpleColor,
            ColorId.PaletteIndigoColor                         => ColorKeys.PaletteIndigoColor,
            ColorId.PaletteBlueColor                           => ColorKeys.PaletteBlueColor,
            ColorId.PaletteBlueColorBright                     => ColorKeys.PaletteBlueColorBright,
            ColorId.PaletteBlueColorDim                        => ColorKeys.PaletteBlueColorDim,
            ColorId.PaletteLightBlueColor                      => ColorKeys.PaletteLightBlueColor,
            ColorId.PaletteLightBlueColorBright                => ColorKeys.PaletteLightBlueColorBright,
            ColorId.PaletteLightBlueColorDim                   => ColorKeys.PaletteLightBlueColorDim,
            ColorId.PaletteCyanColor                           => ColorKeys.PaletteCyanColor,
            ColorId.PaletteCyanColorBright                     => ColorKeys.PaletteCyanColorBright,
            ColorId.PaletteCyanColorDim                        => ColorKeys.PaletteCyanColorDim,
            ColorId.PaletteTealColor                           => ColorKeys.PaletteTealColor,
            ColorId.PaletteGreenColor                          => ColorKeys.PaletteGreenColor,
            ColorId.PaletteGreenColorBright                    => ColorKeys.PaletteGreenColorBright,
            ColorId.PaletteGreenColorDim                       => ColorKeys.PaletteGreenColorDim,
            ColorId.PaletteLightGreenColor                     => ColorKeys.PaletteLightGreenColor,
            ColorId.PaletteLimeColor                           => ColorKeys.PaletteLimeColor,
            ColorId.PaletteYellowColor                         => ColorKeys.PaletteYellowColor,
            ColorId.PaletteYellowColorBright                   => ColorKeys.PaletteYellowColorBright,
            ColorId.PaletteYellowColorDim                      => ColorKeys.PaletteYellowColorDim,
            ColorId.PaletteAmberColor                          => ColorKeys.PaletteAmberColor,
            ColorId.PaletteOrangeColor                         => ColorKeys.PaletteOrangeColor,
            ColorId.PaletteOrangeColorBright                   => ColorKeys.PaletteOrangeColorBright,
            ColorId.PaletteOrangeColorDim                      => ColorKeys.PaletteOrangeColorDim,
            ColorId.PaletteDeepOrangeColor                     => ColorKeys.PaletteDeepOrangeColor,
            ColorId.PaletteDeepOrangeColorBright               => ColorKeys.PaletteDeepOrangeColorBright,
            ColorId.PaletteDeepOrangeColorDim                  => ColorKeys.PaletteDeepOrangeColorDim,
            ColorId.PaletteBrownColor                          => ColorKeys.PaletteBrownColor,
            ColorId.PaletteGreyColor                           => ColorKeys.PaletteGreyColor,
            ColorId.PaletteBlueGreyColor                       => ColorKeys.PaletteBlueGreyColor,
            ColorId.TextFillColorLightPrimary                  => ColorKeys.TextFillColorLightPrimary,
            ColorId.TextFillColorLightSecondary                => ColorKeys.TextFillColorLightSecondary,
            ColorId.TextFillColorLightTertiary                 => ColorKeys.TextFillColorLightTertiary,
            ColorId.TextFillColorLightDisabled                 => ColorKeys.TextFillColorLightDisabled,
            ColorId.TextFillColorLightInverse                  => ColorKeys.TextFillColorLightInverse,
            ColorId.TextFillColorDarkPrimary                   => ColorKeys.TextFillColorDarkPrimary,
            ColorId.TextFillColorDarkSecondary                 => ColorKeys.TextFillColorDarkSecondary,
            ColorId.TextFillColorDarkTertiary                  => ColorKeys.TextFillColorDarkTertiary,
            ColorId.TextFillColorDarkDisabled                  => ColorKeys.TextFillColorDarkDisabled,
            ColorId.TextFillColorDarkInverse                   => ColorKeys.TextFillColorDarkInverse,
            ColorId.ApplicationBackgroundColorLight            => ColorKeys.ApplicationBackgroundColorLight,
            ColorId.ApplicationBackgroundColorDark             => ColorKeys.ApplicationBackgroundColorDark,
            ColorId.ControlStrongFillColorLight                => ColorKeys.ControlStrongFillColorLight,
            ColorId.ControlStrongFillColorDark                 => ColorKeys.ControlStrongFillColorDark,

            _ => ColorKeys.Unknown,
        };
    }
}
