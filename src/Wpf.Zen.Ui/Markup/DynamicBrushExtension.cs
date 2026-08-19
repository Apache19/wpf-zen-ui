// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Zen.Ui.Theming;

#pragma warning disable WPF0081 // This markup must be used only on the Brush.

namespace Wpf.Zen.Ui.Markup;

/// <summary>
/// Creates a WPF dynamic resource reference for a strongly typed <see cref="BrushToken"/>.
/// </summary>
/// <remarks>
/// Dynamic lookup keeps existing dependency properties synchronized when WPF Zen UI replaces theme or accent resources.
/// The enum constructor is also the contract used by XAML editors to provide token completion.
/// </remarks>
[ContentProperty(nameof(Token))]
[MarkupExtensionReturnType(typeof(Brush))]
public sealed class DynamicBrushExtension : DynamicResourceExtension
{
    /// <summary>Gets or sets the logical brush token resolved by this reference.</summary>
    [ConstructorArgument("token")]
    public BrushToken Token
    {
        get;
        set
        {
            var resourceKey = BrushTokenResourceKey.Get(value);
            field       = value;
            ResourceKey = resourceKey;
        }
    }

    /// <summary>Initializes a new instance of the <see cref="DynamicBrushExtension"/> class for the specified token.</summary>
    /// <param name="token">The brush role to resolve from the active resource scope.</param>
    public DynamicBrushExtension(BrushToken token)
    {
        Token = token;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
internal static class BrushTokenResourceKey
{
    internal static ComponentResourceKey Get(BrushToken token)
    {
        return token switch
        {
            // Light.xaml / Dark.xaml / HighContrast.xaml
            BrushToken.AccentFillPrimary                     => BrushKeys.AccentFillPrimaryBrush,
            BrushToken.AccentFillSecondary                   => BrushKeys.AccentFillSecondaryBrush,
            BrushToken.AccentFillTertiary                    => BrushKeys.AccentFillTertiaryBrush,
            BrushToken.AccentTextPrimary                     => BrushKeys.AccentTextPrimaryBrush,
            BrushToken.AccentTextSecondary                   => BrushKeys.AccentTextSecondaryBrush,
            BrushToken.TextOnAccentPrimary                   => BrushKeys.TextOnAccentPrimaryBrush,
            BrushToken.TextOnAccentSecondary                 => BrushKeys.TextOnAccentSecondaryBrush,
            BrushToken.AccentStroke                          => BrushKeys.AccentStrokeBrush,
            BrushToken.AccentSelection                       => BrushKeys.AccentSelectionBrush,
            BrushToken.SystemWindowText                      => BrushKeys.SystemColorWindowTextBrush,
            BrushToken.SystemWindow                          => BrushKeys.SystemColorWindowBrush,
            BrushToken.SystemHighlightText                   => BrushKeys.SystemColorHighlightTextBrush,
            BrushToken.SystemHighlight                       => BrushKeys.SystemColorHighlightBrush,
            BrushToken.SystemControlText                     => BrushKeys.SystemColorControlTextBrush,
            BrushToken.SystemControl                         => BrushKeys.SystemColorControlBrush,
            BrushToken.SystemHotTrack                        => BrushKeys.SystemColorHotTrackBrush,
            BrushToken.SystemGrayText                        => BrushKeys.SystemColorGrayTextBrush,
            BrushToken.SystemWindowFrame                     => BrushKeys.SystemColorWindowFrameBrush,
            BrushToken.KeyboardFocusBorder                   => BrushKeys.KeyboardFocusBorderColorBrush,
            BrushToken.TextFillPrimary                       => BrushKeys.TextFillColorPrimaryBrush,
            BrushToken.TextFillSecondary                     => BrushKeys.TextFillColorSecondaryBrush,
            BrushToken.TextFillTertiary                      => BrushKeys.TextFillColorTertiaryBrush,
            BrushToken.TextFillDisabled                      => BrushKeys.TextFillColorDisabledBrush,
            BrushToken.TextPlaceholder                       => BrushKeys.TextPlaceholderColorBrush,
            BrushToken.TextFillInverse                       => BrushKeys.TextFillColorInverseBrush,
            BrushToken.ControlFillPrimary                    => BrushKeys.ControlFillColorPrimaryBrush,
            BrushToken.ControlFillSecondary                  => BrushKeys.ControlFillColorSecondaryBrush,
            BrushToken.ControlFillTertiary                   => BrushKeys.ControlFillColorTertiaryBrush,
            BrushToken.ControlFillDisabled                   => BrushKeys.ControlFillColorDisabledBrush,
            BrushToken.ControlFillInputActive                => BrushKeys.ControlFillColorInputActiveBrush,
            BrushToken.ControlStrongFillPrimary              => BrushKeys.ControlStrongFillColorPrimaryBrush,
            BrushToken.ControlStrongFillSecondary            => BrushKeys.ControlStrongFillColorSecondaryBrush,
            BrushToken.ControlStrongFillTertiary             => BrushKeys.ControlStrongFillColorTertiaryBrush,
            BrushToken.ControlStrongFillDefault              => BrushKeys.ControlStrongFillColorDefaultBrush,
            BrushToken.ControlStrongFillDisabled             => BrushKeys.ControlStrongFillColorDisabledBrush,
            BrushToken.ControlSolidFillDefault               => BrushKeys.ControlSolidFillColorDefaultBrush,
            BrushToken.SubtleFillTransparent                 => BrushKeys.SubtleFillColorTransparentBrush,
            BrushToken.SubtleFillSecondary                   => BrushKeys.SubtleFillColorSecondaryBrush,
            BrushToken.SubtleFillTertiary                    => BrushKeys.SubtleFillColorTertiaryBrush,
            BrushToken.SubtleFillDisabled                    => BrushKeys.SubtleFillColorDisabledBrush,
            BrushToken.ControlAltFillTransparent             => BrushKeys.ControlAltFillColorTransparentBrush,
            BrushToken.ControlAltFillSecondary               => BrushKeys.ControlAltFillColorSecondaryBrush,
            BrushToken.ControlAltFillTertiary                => BrushKeys.ControlAltFillColorTertiaryBrush,
            BrushToken.ControlAltFillQuarternary             => BrushKeys.ControlAltFillColorQuarternaryBrush,
            BrushToken.ControlAltFillDisabled                => BrushKeys.ControlAltFillColorDisabledBrush,
            BrushToken.ControlOnImageFillDefault             => BrushKeys.ControlOnImageFillColorDefaultBrush,
            BrushToken.ControlOnImageFillSecondary           => BrushKeys.ControlOnImageFillColorSecondaryBrush,
            BrushToken.ControlOnImageFillTertiary            => BrushKeys.ControlOnImageFillColorTertiaryBrush,
            BrushToken.ControlOnImageFillDisabled            => BrushKeys.ControlOnImageFillColorDisabledBrush,
            BrushToken.ControlStrokeDefault                  => BrushKeys.ControlStrokeColorDefaultBrush,
            BrushToken.ControlStrokeSecondary                => BrushKeys.ControlStrokeColorSecondaryBrush,
            BrushToken.ControlStrokeTertiary                 => BrushKeys.ControlStrokeColorTertiaryBrush,
            BrushToken.ControlStrokeOnAccentDefault          => BrushKeys.ControlStrokeColorOnAccentDefaultBrush,
            BrushToken.ControlStrokeOnAccentSecondary        => BrushKeys.ControlStrokeColorOnAccentSecondaryBrush,
            BrushToken.ControlStrokeOnAccentTertiary         => BrushKeys.ControlStrokeColorOnAccentTertiaryBrush,
            BrushToken.ControlStrokeOnAccentDisabled         => BrushKeys.ControlStrokeColorOnAccentDisabledBrush,
            BrushToken.ControlStrokeForStrongFillWhenOnImage => BrushKeys.ControlStrokeColorForStrongFillWhenOnImageBrush,
            BrushToken.CardStrokeDefault                     => BrushKeys.CardStrokeColorDefaultBrush,
            BrushToken.CardStrokeDefaultSolid                => BrushKeys.CardStrokeColorDefaultSolidBrush,
            BrushToken.ControlStrongStrokeDefault            => BrushKeys.ControlStrongStrokeColorDefaultBrush,
            BrushToken.ControlStrongStrokeDisabled           => BrushKeys.ControlStrongStrokeColorDisabledBrush,
            BrushToken.SurfaceStrokeDefault                  => BrushKeys.SurfaceStrokeColorDefaultBrush,
            BrushToken.SurfaceStrokeFlyout                   => BrushKeys.SurfaceStrokeColorFlyoutBrush,
            BrushToken.SurfaceStrokeInverse                  => BrushKeys.SurfaceStrokeColorInverseBrush,
            BrushToken.DividerStrokeDefault                  => BrushKeys.DividerStrokeColorDefaultBrush,
            BrushToken.FocusStrokeOuter                      => BrushKeys.FocusStrokeColorOuterBrush,
            BrushToken.FocusStrokeInner                      => BrushKeys.FocusStrokeColorInnerBrush,
            BrushToken.CardBackgroundFillDefault             => BrushKeys.CardBackgroundFillColorDefaultBrush,
            BrushToken.CardBackgroundFillSecondary           => BrushKeys.CardBackgroundFillColorSecondaryBrush,
            BrushToken.EmbeddedFillDefault                   => BrushKeys.EmbeddedFillColorDefaultBrush,
            BrushToken.EmbeddedFillStrong                    => BrushKeys.EmbeddedFillColorStrongBrush,
            BrushToken.EmbeddedFillDeep                      => BrushKeys.EmbeddedFillColorDeepBrush,
            BrushToken.SmokeFillDefault                      => BrushKeys.SmokeFillColorDefaultBrush,
            BrushToken.LayerFillDefault                      => BrushKeys.LayerFillColorDefaultBrush,
            BrushToken.LayerFillAlt                          => BrushKeys.LayerFillColorAltBrush,
            BrushToken.LayerOnAcrylicFillDefault             => BrushKeys.LayerOnAcrylicFillColorDefaultBrush,
            BrushToken.LayerOnAccentAcrylicFillDefault       => BrushKeys.LayerOnAccentAcrylicFillColorDefaultBrush,
            BrushToken.LayerOnMicaBaseAltFillDefault         => BrushKeys.LayerOnMicaBaseAltFillColorDefaultBrush,
            BrushToken.LayerOnMicaBaseAltFillSecondary       => BrushKeys.LayerOnMicaBaseAltFillColorSecondaryBrush,
            BrushToken.LayerOnMicaBaseAltFillTertiary        => BrushKeys.LayerOnMicaBaseAltFillColorTertiaryBrush,
            BrushToken.LayerOnMicaBaseAltFillTransparent     => BrushKeys.LayerOnMicaBaseAltFillColorTransparentBrush,
            BrushToken.SolidBackgroundFillBase               => BrushKeys.SolidBackgroundFillColorBaseBrush,
            BrushToken.SolidBackgroundFillSecondary          => BrushKeys.SolidBackgroundFillColorSecondaryBrush,
            BrushToken.SolidBackgroundFillTertiary           => BrushKeys.SolidBackgroundFillColorTertiaryBrush,
            BrushToken.SolidBackgroundFillQuarternary        => BrushKeys.SolidBackgroundFillColorQuarternaryBrush,
            BrushToken.SolidBackgroundFillBaseAlt            => BrushKeys.SolidBackgroundFillColorBaseAltBrush,
            BrushToken.SystemFillAttentionForeground         => BrushKeys.SystemFillColorAttentionForegroundBrush,
            BrushToken.SystemFillInformationalForeground     => BrushKeys.SystemFillColorInformationalForegroundBrush,
            BrushToken.SystemFillSuccessForeground           => BrushKeys.SystemFillColorSuccessForegroundBrush,
            BrushToken.SystemFillCautionForeground           => BrushKeys.SystemFillColorCautionForegroundBrush,
            BrushToken.SystemFillCriticalForeground          => BrushKeys.SystemFillColorCriticalForegroundBrush,
            BrushToken.SystemFillNeutralForeground           => BrushKeys.SystemFillColorNeutralForegroundBrush,
            BrushToken.SystemFillSolidNeutralForeground      => BrushKeys.SystemFillColorSolidNeutralForegroundBrush,
            BrushToken.SystemFillAttentionBackground         => BrushKeys.SystemFillColorAttentionBackgroundBrush,
            BrushToken.SystemFillInformationalBackground     => BrushKeys.SystemFillColorInformationalBackgroundBrush,
            BrushToken.SystemFillSuccessBackground           => BrushKeys.SystemFillColorSuccessBackgroundBrush,
            BrushToken.SystemFillCautionBackground           => BrushKeys.SystemFillColorCautionBackgroundBrush,
            BrushToken.SystemFillCriticalBackground          => BrushKeys.SystemFillColorCriticalBackgroundBrush,
            BrushToken.SystemFillNeutralBackground           => BrushKeys.SystemFillColorNeutralBackgroundBrush,
            BrushToken.SystemFillSolidAttentionBackground    => BrushKeys.SystemFillColorSolidAttentionBackgroundBrush,
            BrushToken.SystemFillSolidNeutralBackground      => BrushKeys.SystemFillColorSolidNeutralBackgroundBrush,
            BrushToken.SystemFillAttentionBorder             => BrushKeys.SystemFillColorAttentionBorderBrush,
            BrushToken.SystemFillInformationalBorder         => BrushKeys.SystemFillColorInformationalBorderBrush,
            BrushToken.SystemFillSuccessBorder               => BrushKeys.SystemFillColorSuccessBorderBrush,
            BrushToken.SystemFillCautionBorder               => BrushKeys.SystemFillColorCautionBorderBrush,
            BrushToken.SystemFillCriticalBorder              => BrushKeys.SystemFillColorCriticalBorderBrush,
            BrushToken.SystemFillNeutralBorder               => BrushKeys.SystemFillColorNeutralBorderBrush,
            BrushToken.SystemFillSolidNeutralBorder          => BrushKeys.SystemFillColorSolidNeutralBorderBrush,
            BrushToken.SystemFillAttentionMuted              => BrushKeys.SystemFillColorAttentionMutedBrush,
            BrushToken.SystemFillInformationalMuted          => BrushKeys.SystemFillColorInformationalMutedBrush,
            BrushToken.SystemFillSuccessMuted                => BrushKeys.SystemFillColorSuccessMutedBrush,
            BrushToken.SystemFillCautionMuted                => BrushKeys.SystemFillColorCautionMutedBrush,
            BrushToken.SystemFillCriticalMuted               => BrushKeys.SystemFillColorCriticalMutedBrush,
            BrushToken.SystemFillNeutralMuted                => BrushKeys.SystemFillColorNeutralMutedBrush,
            BrushToken.SystemFillSolidNeutralMuted           => BrushKeys.SystemFillColorSolidNeutralMutedBrush,
            BrushToken.ControlElevationBorder                => BrushKeys.ControlElevationBorderBrush,
            BrushToken.CircleElevationBorder                 => BrushKeys.CircleElevationBorderBrush,
            BrushToken.AccentControlElevationBorder          => BrushKeys.AccentControlElevationBorderBrush,
            BrushToken.StripedBackground                     => BrushKeys.StripedBackgroundBrush,
            BrushToken.AcrylicBackgroundFillDefault          => BrushKeys.AcrylicBackgroundFillColorDefaultBrush,
            BrushToken.WindowBackground                      => BrushKeys.WindowBackgroundBrush,
            BrushToken.WindowForeground                      => BrushKeys.WindowForegroundBrush,
            BrushToken.WindowBorder                          => BrushKeys.WindowBorderBrush,
            BrushToken.WindowBorderInactive                  => BrushKeys.WindowBorderInactiveBrush,
            BrushToken.LeftNavigationViewSeparator           => BrushKeys.LeftNavigationViewSeparatorBrush,

            // Palette.xaml:
            BrushToken.PalettePrimary          => BrushKeys.PalettePrimaryBrush,
            BrushToken.PaletteRed              => BrushKeys.PaletteRedBrush,
            BrushToken.PaletteRedBright        => BrushKeys.PaletteRedBrushBright,
            BrushToken.PaletteRedDim           => BrushKeys.PaletteRedBrushDim,
            BrushToken.PalettePink             => BrushKeys.PalettePinkBrush,
            BrushToken.PalettePinkBright       => BrushKeys.PalettePinkBrushBright,
            BrushToken.PalettePinkDim          => BrushKeys.PalettePinkBrushDim,
            BrushToken.PalettePurple           => BrushKeys.PalettePurpleBrush,
            BrushToken.PaletteDeepPurple       => BrushKeys.PaletteDeepPurpleBrush,
            BrushToken.PaletteIndigo           => BrushKeys.PaletteIndigoBrush,
            BrushToken.PaletteBlue             => BrushKeys.PaletteBlueBrush,
            BrushToken.PaletteBlueBright       => BrushKeys.PaletteBlueBrushBright,
            BrushToken.PaletteBlueDim          => BrushKeys.PaletteBlueBrushDim,
            BrushToken.PaletteLightBlue        => BrushKeys.PaletteLightBlueBrush,
            BrushToken.PaletteLightBlueBright  => BrushKeys.PaletteLightBlueBrushBright,
            BrushToken.PaletteLightBlueDim     => BrushKeys.PaletteLightBlueBrushDim,
            BrushToken.PaletteCyan             => BrushKeys.PaletteCyanBrush,
            BrushToken.PaletteCyanBright       => BrushKeys.PaletteCyanBrushBright,
            BrushToken.PaletteCyanDim          => BrushKeys.PaletteCyanBrushDim,
            BrushToken.PaletteTeal             => BrushKeys.PaletteTealBrush,
            BrushToken.PaletteGreen            => BrushKeys.PaletteGreenBrush,
            BrushToken.PaletteGreenBright      => BrushKeys.PaletteGreenBrushBright,
            BrushToken.PaletteGreenDim         => BrushKeys.PaletteGreenBrushDim,
            BrushToken.PaletteLightGreen       => BrushKeys.PaletteLightGreenBrush,
            BrushToken.PaletteLime             => BrushKeys.PaletteLimeBrush,
            BrushToken.PaletteYellow           => BrushKeys.PaletteYellowBrush,
            BrushToken.PaletteYellowBright     => BrushKeys.PaletteYellowBrushBright,
            BrushToken.PaletteYellowDim        => BrushKeys.PaletteYellowBrushDim,
            BrushToken.PaletteAmber            => BrushKeys.PaletteAmberBrush,
            BrushToken.PaletteOrange           => BrushKeys.PaletteOrangeBrush,
            BrushToken.PaletteOrangeBright     => BrushKeys.PaletteOrangeBrushBright,
            BrushToken.PaletteOrangeDim        => BrushKeys.PaletteOrangeBrushDim,
            BrushToken.PaletteDeepOrange       => BrushKeys.PaletteDeepOrangeBrush,
            BrushToken.PaletteDeepOrangeBright => BrushKeys.PaletteDeepOrangeBrushBright,
            BrushToken.PaletteDeepOrangeDim    => BrushKeys.PaletteDeepOrangeBrushDim,
            BrushToken.PaletteBrown            => BrushKeys.PaletteBrownBrush,
            BrushToken.PaletteGrey             => BrushKeys.PaletteGreyBrush,
            BrushToken.PaletteBlueGrey         => BrushKeys.PaletteBlueGreyBrush,

            // StaticColors.xaml
            BrushToken.TextFillLightPrimary       => BrushKeys.TextFillColorLightPrimaryBrush,
            BrushToken.TextFillLightSecondary     => BrushKeys.TextFillColorLightSecondaryBrush,
            BrushToken.TextFillLightTertiary      => BrushKeys.TextFillColorLightTertiaryBrush,
            BrushToken.TextFillLightDisabled      => BrushKeys.TextFillColorLightDisabledBrush,
            BrushToken.TextFillLightInverse       => BrushKeys.TextFillColorLightInverseBrush,
            BrushToken.TextFillDarkPrimary        => BrushKeys.TextFillColorDarkPrimaryBrush,
            BrushToken.TextFillDarkSecondary      => BrushKeys.TextFillColorDarkSecondaryBrush,
            BrushToken.TextFillDarkTertiary       => BrushKeys.TextFillColorDarkTertiaryBrush,
            BrushToken.TextFillDarkDisabled       => BrushKeys.TextFillColorDarkDisabledBrush,
            BrushToken.TextFillDarkInverse        => BrushKeys.TextFillColorDarkInverseBrush,
            BrushToken.ApplicationBackgroundLight => BrushKeys.ApplicationBackgroundColorLightBrush,
            BrushToken.ApplicationBackgroundDark  => BrushKeys.ApplicationBackgroundColorDarkBrush,
            BrushToken.ControlStrongFillLight     => BrushKeys.ControlStrongFillColorLightBrush,
            BrushToken.ControlStrongFillDark      => BrushKeys.ControlStrongFillColorDarkBrush,

            _ => throw new ArgumentOutOfRangeException(nameof(token), token, "The brush token is not registered."),
        };
    }
}
#pragma warning restore SA1402 // File may only contain a single type
