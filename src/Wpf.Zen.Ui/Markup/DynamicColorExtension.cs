// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

#pragma warning disable WPF0081 // This markup must be used only on the Color.

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a WPF dynamic resource reference for a strongly typed <see cref="ColorToken"/>.
/// </summary>
/// <remarks>
/// Use this extension on dependency properties whose color must follow theme or accent resource replacement.
/// The enum constructor preserves XAML IntelliSense completion for the published color roles.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Color))]
public sealed class DynamicColorExtension : DynamicResourceExtension
{
    /// <summary>Gets or sets the logical color token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public ColorToken Token
    {
        get;
        set
        {
            var resourceKey = ColorTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="DynamicColorExtension"/> class for the specified token.</summary>
    /// <param name="token">The color role to resolve from the active resource scope.</param>
    public DynamicColorExtension(ColorToken token)
    {
        Token = token;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal static class ColorTokenResourceKey
{
    internal static ComponentResourceKey Get(ColorToken token)
    {
        return token switch
        {
            ColorToken.AccentFillPrimary                     => ColorKeys.AccentFillPrimary,
            ColorToken.AccentFillSecondary                   => ColorKeys.AccentFillSecondary,
            ColorToken.AccentFillTertiary                    => ColorKeys.AccentFillTertiary,
            ColorToken.AccentTextPrimary                     => ColorKeys.AccentTextPrimary,
            ColorToken.AccentTextSecondary                   => ColorKeys.AccentTextSecondary,
            ColorToken.TextOnAccentPrimary                   => ColorKeys.TextOnAccentPrimary,
            ColorToken.AccentStroke                          => ColorKeys.AccentStroke,
            ColorToken.AccentSelection                       => ColorKeys.AccentSelection,
            ColorToken.SystemWindowText                      => ColorKeys.SystemColorWindowTextColor,
            ColorToken.SystemWindow                          => ColorKeys.SystemColorWindowColor,
            ColorToken.SystemHighlightText                   => ColorKeys.SystemColorHighlightTextColor,
            ColorToken.SystemHighlight                       => ColorKeys.SystemColorHighlightColor,
            ColorToken.SystemControlText                     => ColorKeys.SystemColorControlTextColor,
            ColorToken.SystemControl                         => ColorKeys.SystemColorControlColor,
            ColorToken.SystemHotTrack                        => ColorKeys.SystemColorHotTrackColor,
            ColorToken.SystemGrayText                        => ColorKeys.SystemColorGrayTextColor,
            ColorToken.SystemWindowFrame                     => ColorKeys.SystemColorWindowFrameColor,
            ColorToken.WindowBackground                      => ColorKeys.WindowBackgroundColor,
            ColorToken.WindowBorder                          => ColorKeys.WindowBorderColor,
            ColorToken.WindowBorderInactive                  => ColorKeys.WindowBorderColorInactive,
            ColorToken.KeyboardFocusBorder                   => ColorKeys.KeyboardFocusBorderColor,
            ColorToken.TextFillPrimary                       => ColorKeys.TextFillColorPrimary,
            ColorToken.TextFillSecondary                     => ColorKeys.TextFillColorSecondary,
            ColorToken.TextFillTertiary                      => ColorKeys.TextFillColorTertiary,
            ColorToken.TextFillDisabled                      => ColorKeys.TextFillColorDisabled,
            ColorToken.TextPlaceholder                       => ColorKeys.TextPlaceholderColor,
            ColorToken.TextFillInverse                       => ColorKeys.TextFillColorInverse,
            ColorToken.ControlFillPrimary                    => ColorKeys.ControlFillColorPrimary,
            ColorToken.ControlFillSecondary                  => ColorKeys.ControlFillColorSecondary,
            ColorToken.ControlFillTertiary                   => ColorKeys.ControlFillColorTertiary,
            ColorToken.ControlFillDisabled                   => ColorKeys.ControlFillColorDisabled,
            ColorToken.ControlFillInputActive                => ColorKeys.ControlFillColorInputActive,
            ColorToken.ControlStrongFillDefault              => ColorKeys.ControlStrongFillColorDefault,
            ColorToken.ControlStrongFillDisabled             => ColorKeys.ControlStrongFillColorDisabled,
            ColorToken.ControlSolidFillDefault               => ColorKeys.ControlSolidFillColorDefault,
            ColorToken.SubtleFillTransparent                 => ColorKeys.SubtleFillColorTransparent,
            ColorToken.SubtleFillSecondary                   => ColorKeys.SubtleFillColorSecondary,
            ColorToken.SubtleFillTertiary                    => ColorKeys.SubtleFillColorTertiary,
            ColorToken.SubtleFillDisabled                    => ColorKeys.SubtleFillColorDisabled,
            ColorToken.ControlAltFillTransparent             => ColorKeys.ControlAltFillColorTransparent,
            ColorToken.ControlAltFillSecondary               => ColorKeys.ControlAltFillColorSecondary,
            ColorToken.ControlAltFillTertiary                => ColorKeys.ControlAltFillColorTertiary,
            ColorToken.ControlAltFillQuarternary             => ColorKeys.ControlAltFillColorQuarternary,
            ColorToken.ControlAltFillDisabled                => ColorKeys.ControlAltFillColorDisabled,
            ColorToken.ControlOnImageFillDefault             => ColorKeys.ControlOnImageFillColorDefault,
            ColorToken.ControlOnImageFillSecondary           => ColorKeys.ControlOnImageFillColorSecondary,
            ColorToken.ControlOnImageFillTertiary            => ColorKeys.ControlOnImageFillColorTertiary,
            ColorToken.ControlOnImageFillDisabled            => ColorKeys.ControlOnImageFillColorDisabled,
            ColorToken.ControlStrokeDefault                  => ColorKeys.ControlStrokeColorDefault,
            ColorToken.ControlStrokeSecondary                => ColorKeys.ControlStrokeColorSecondary,
            ColorToken.ControlStrokeTertiary                 => ColorKeys.ControlStrokeColorTertiary,
            ColorToken.ControlStrokeOnAccentDefault          => ColorKeys.ControlStrokeColorOnAccentDefault,
            ColorToken.ControlStrokeOnAccentSecondary        => ColorKeys.ControlStrokeColorOnAccentSecondary,
            ColorToken.ControlStrokeOnAccentTertiary         => ColorKeys.ControlStrokeColorOnAccentTertiary,
            ColorToken.ControlStrokeOnAccentDisabled         => ColorKeys.ControlStrokeColorOnAccentDisabled,
            ColorToken.ControlStrokeForStrongFillWhenOnImage => ColorKeys.ControlStrokeColorForStrongFillWhenOnImage,
            ColorToken.CardStrokeDefault                     => ColorKeys.CardStrokeColorDefault,
            ColorToken.CardStrokeDefaultSolid                => ColorKeys.CardStrokeColorDefaultSolid,
            ColorToken.ControlStrongStrokeDefault            => ColorKeys.ControlStrongStrokeColorDefault,
            ColorToken.ControlStrongStrokeDisabled           => ColorKeys.ControlStrongStrokeColorDisabled,
            ColorToken.SurfaceStrokeDefault                  => ColorKeys.SurfaceStrokeColorDefault,
            ColorToken.SurfaceStrokeFlyout                   => ColorKeys.SurfaceStrokeColorFlyout,
            ColorToken.SurfaceStrokeInverse                  => ColorKeys.SurfaceStrokeColorInverse,
            ColorToken.DividerStrokeDefault                  => ColorKeys.DividerStrokeColorDefault,
            ColorToken.FocusStrokeOuter                      => ColorKeys.FocusStrokeColorOuter,
            ColorToken.FocusStrokeInner                      => ColorKeys.FocusStrokeColorInner,
            ColorToken.CardBackgroundFillDefault             => ColorKeys.CardBackgroundFillColorDefault,
            ColorToken.CardBackgroundFillSecondary           => ColorKeys.CardBackgroundFillColorSecondary,
            ColorToken.EmbeddedFillDefault                   => ColorKeys.EmbeddedFillColorDefault,
            ColorToken.EmbeddedFillStrong                    => ColorKeys.EmbeddedFillColorStrong,
            ColorToken.EmbeddedFillDeep                      => ColorKeys.EmbeddedFillColorDeep,
            ColorToken.SmokeFillDefault                      => ColorKeys.SmokeFillColorDefault,
            ColorToken.LayerFillDefault                      => ColorKeys.LayerFillColorDefault,
            ColorToken.LayerFillAlt                          => ColorKeys.LayerFillColorAlt,
            ColorToken.LayerOnAcrylicFillDefault             => ColorKeys.LayerOnAcrylicFillColorDefault,
            ColorToken.LayerOnAccentAcrylicFillDefault       => ColorKeys.LayerOnAccentAcrylicFillColorDefault,
            ColorToken.SnowflakeGradientTop                  => ColorKeys.SnowflakeGradientTop,
            ColorToken.SnowflakeGradientBottom               => ColorKeys.SnowflakeGradientBottom,
            ColorToken.AcrylicBackgroundFillDefault          => ColorKeys.AcrylicBackgroundFillColorDefault,
            ColorToken.LayerOnMicaBaseAltFillDefault         => ColorKeys.LayerOnMicaBaseAltFillColorDefault,
            ColorToken.LayerOnMicaBaseAltFillSecondary       => ColorKeys.LayerOnMicaBaseAltFillColorSecondary,
            ColorToken.LayerOnMicaBaseAltFillTertiary        => ColorKeys.LayerOnMicaBaseAltFillColorTertiary,
            ColorToken.LayerOnMicaBaseAltFillTransparent     => ColorKeys.LayerOnMicaBaseAltFillColorTransparent,
            ColorToken.SolidBackgroundFillBase               => ColorKeys.SolidBackgroundFillColorBase,
            ColorToken.SolidBackgroundFillSecondary          => ColorKeys.SolidBackgroundFillColorSecondary,
            ColorToken.SolidBackgroundFillTertiary           => ColorKeys.SolidBackgroundFillColorTertiary,
            ColorToken.SolidBackgroundFillQuarternary        => ColorKeys.SolidBackgroundFillColorQuarternary,
            ColorToken.SolidBackgroundFillTransparent        => ColorKeys.SolidBackgroundFillColorTransparent,
            ColorToken.SolidBackgroundFillBaseAlt            => ColorKeys.SolidBackgroundFillColorBaseAlt,
            ColorToken.SystemFillAttention                   => ColorKeys.SystemFillColorAttention,
            ColorToken.SystemFillInformational               => ColorKeys.SystemFillColorInformational,
            ColorToken.SystemFillSuccess                     => ColorKeys.SystemFillColorSuccess,
            ColorToken.SystemFillCaution                     => ColorKeys.SystemFillColorCaution,
            ColorToken.SystemFillCritical                    => ColorKeys.SystemFillColorCritical,
            ColorToken.SystemFillNeutral                     => ColorKeys.SystemFillColorNeutral,
            ColorToken.SystemFillSolidNeutral                => ColorKeys.SystemFillColorSolidNeutral,
            ColorToken.SystemFillAttentionBackground         => ColorKeys.SystemFillColorAttentionBackground,
            ColorToken.SystemFillSuccessBackground           => ColorKeys.SystemFillColorSuccessBackground,
            ColorToken.SystemFillCautionBackground           => ColorKeys.SystemFillColorCautionBackground,
            ColorToken.SystemFillCriticalBackground          => ColorKeys.SystemFillColorCriticalBackground,
            ColorToken.SystemFillNeutralBackground           => ColorKeys.SystemFillColorNeutralBackground,
            ColorToken.SystemFillSolidAttentionBackground    => ColorKeys.SystemFillColorSolidAttentionBackground,
            ColorToken.SystemFillSolidNeutralBackground      => ColorKeys.SystemFillColorSolidNeutralBackground,
            ColorToken.PalettePrimary                        => ColorKeys.PalettePrimaryColor,
            ColorToken.PaletteRed                            => ColorKeys.PaletteRedColor,
            ColorToken.PaletteRedBright                      => ColorKeys.PaletteRedColorBright,
            ColorToken.PaletteRedDim                         => ColorKeys.PaletteRedColorDim,
            ColorToken.PalettePink                           => ColorKeys.PalettePinkColor,
            ColorToken.PalettePinkBright                     => ColorKeys.PalettePinkColorBright,
            ColorToken.PalettePinkDim                        => ColorKeys.PalettePinkColorDim,
            ColorToken.PalettePurple                         => ColorKeys.PalettePurpleColor,
            ColorToken.PaletteDeepPurple                     => ColorKeys.PaletteDeepPurpleColor,
            ColorToken.PaletteIndigo                         => ColorKeys.PaletteIndigoColor,
            ColorToken.PaletteBlue                           => ColorKeys.PaletteBlueColor,
            ColorToken.PaletteBlueBright                     => ColorKeys.PaletteBlueColorBright,
            ColorToken.PaletteBlueDim                        => ColorKeys.PaletteBlueColorDim,
            ColorToken.PaletteLightBlue                      => ColorKeys.PaletteLightBlueColor,
            ColorToken.PaletteLightBlueBright                => ColorKeys.PaletteLightBlueColorBright,
            ColorToken.PaletteLightBlueDim                   => ColorKeys.PaletteLightBlueColorDim,
            ColorToken.PaletteCyan                           => ColorKeys.PaletteCyanColor,
            ColorToken.PaletteCyanBright                     => ColorKeys.PaletteCyanColorBright,
            ColorToken.PaletteCyanDim                        => ColorKeys.PaletteCyanColorDim,
            ColorToken.PaletteTeal                           => ColorKeys.PaletteTealColor,
            ColorToken.PaletteGreen                          => ColorKeys.PaletteGreenColor,
            ColorToken.PaletteGreenBright                    => ColorKeys.PaletteGreenColorBright,
            ColorToken.PaletteGreenDim                       => ColorKeys.PaletteGreenColorDim,
            ColorToken.PaletteLightGreen                     => ColorKeys.PaletteLightGreenColor,
            ColorToken.PaletteLime                           => ColorKeys.PaletteLimeColor,
            ColorToken.PaletteYellow                         => ColorKeys.PaletteYellowColor,
            ColorToken.PaletteYellowBright                   => ColorKeys.PaletteYellowColorBright,
            ColorToken.PaletteYellowDim                      => ColorKeys.PaletteYellowColorDim,
            ColorToken.PaletteAmber                          => ColorKeys.PaletteAmberColor,
            ColorToken.PaletteOrange                         => ColorKeys.PaletteOrangeColor,
            ColorToken.PaletteOrangeBright                   => ColorKeys.PaletteOrangeColorBright,
            ColorToken.PaletteOrangeDim                      => ColorKeys.PaletteOrangeColorDim,
            ColorToken.PaletteDeepOrange                     => ColorKeys.PaletteDeepOrangeColor,
            ColorToken.PaletteDeepOrangeBright               => ColorKeys.PaletteDeepOrangeColorBright,
            ColorToken.PaletteDeepOrangeDim                  => ColorKeys.PaletteDeepOrangeColorDim,
            ColorToken.PaletteBrown                          => ColorKeys.PaletteBrownColor,
            ColorToken.PaletteGrey                           => ColorKeys.PaletteGreyColor,
            ColorToken.PaletteBlueGrey                       => ColorKeys.PaletteBlueGreyColor,
            ColorToken.TextFillLightPrimary                  => ColorKeys.TextFillColorLightPrimary,
            ColorToken.TextFillLightSecondary                => ColorKeys.TextFillColorLightSecondary,
            ColorToken.TextFillLightTertiary                 => ColorKeys.TextFillColorLightTertiary,
            ColorToken.TextFillLightDisabled                 => ColorKeys.TextFillColorLightDisabled,
            ColorToken.TextFillLightInverse                  => ColorKeys.TextFillColorLightInverse,
            ColorToken.TextFillDarkPrimary                   => ColorKeys.TextFillColorDarkPrimary,
            ColorToken.TextFillDarkSecondary                 => ColorKeys.TextFillColorDarkSecondary,
            ColorToken.TextFillDarkTertiary                  => ColorKeys.TextFillColorDarkTertiary,
            ColorToken.TextFillDarkDisabled                  => ColorKeys.TextFillColorDarkDisabled,
            ColorToken.TextFillDarkInverse                   => ColorKeys.TextFillColorDarkInverse,
            ColorToken.ApplicationBackgroundLight            => ColorKeys.ApplicationBackgroundColorLight,
            ColorToken.ApplicationBackgroundDark             => ColorKeys.ApplicationBackgroundColorDark,
            ColorToken.ControlStrongFillLight                => ColorKeys.ControlStrongFillColorLight,
            ColorToken.ControlStrongFillDark                 => ColorKeys.ControlStrongFillColorDark,

            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "The color token is not registered."),
        };
    }
}
#pragma warning restore SA1402 // File may only contain a single type
