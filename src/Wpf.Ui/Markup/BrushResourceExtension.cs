// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

#pragma warning disable WPF0081 // This markup must be used only on the Brush.

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Id))]
[MarkupExtensionReturnType(typeof(Brush))]
public class BrushResourceExtension : DynamicResourceExtension
{
    [ConstructorArgument("id")]
    public BrushId Id
    {
        get;
        set
        {
            field       = value;
            ResourceKey = KeyMapping(value);
        }
    }

    public BrushResourceExtension(BrushId id)
    {
        Id = id;
    }

    private static ComponentResourceKey KeyMapping(BrushId id)
    {
        return id switch
        {
            // Light.xaml / Dark.xaml / HighContrast.xaml
            BrushId.SystemAccentColorBrush                          => BrushKeys.SystemAccentColorBrush,
            BrushId.SystemAccentColorPrimaryBrush                   => BrushKeys.SystemAccentColorPrimaryBrush,
            BrushId.SystemAccentColorSecondaryBrush                 => BrushKeys.SystemAccentColorSecondaryBrush,
            BrushId.SystemAccentColorTertiaryBrush                  => BrushKeys.SystemAccentColorTertiaryBrush,
            BrushId.SystemColorWindowTextBrush                      => BrushKeys.SystemColorWindowTextBrush,
            BrushId.SystemColorWindowBrush                          => BrushKeys.SystemColorWindowBrush,
            BrushId.SystemColorHighlightTextBrush                   => BrushKeys.SystemColorHighlightTextBrush,
            BrushId.SystemColorHighlightBrush                       => BrushKeys.SystemColorHighlightBrush,
            BrushId.SystemColorControlTextBrush                     => BrushKeys.SystemColorControlTextBrush,
            BrushId.SystemColorControlBrush                         => BrushKeys.SystemColorControlBrush,
            BrushId.SystemColorHotTrackBrush                        => BrushKeys.SystemColorHotTrackBrush,
            BrushId.SystemColorGrayTextBrush                        => BrushKeys.SystemColorGrayTextBrush,
            BrushId.SystemColorWindowFrameBrush                     => BrushKeys.SystemColorWindowFrameBrush,
            BrushId.AccentTextFillColorPrimaryBrush                 => BrushKeys.AccentTextFillColorPrimaryBrush,
            BrushId.AccentTextFillColorSecondaryBrush               => BrushKeys.AccentTextFillColorSecondaryBrush,
            BrushId.AccentTextFillColorTertiaryBrush                => BrushKeys.AccentTextFillColorTertiaryBrush,
            BrushId.AccentFillColorSelectedTextBackgroundBrush      => BrushKeys.AccentFillColorSelectedTextBackgroundBrush,
            BrushId.AccentFillColorDefaultBrush                     => BrushKeys.AccentFillColorDefaultBrush,
            BrushId.AccentFillColorSecondaryBrush                   => BrushKeys.AccentFillColorSecondaryBrush,
            BrushId.AccentFillColorTertiaryBrush                    => BrushKeys.AccentFillColorTertiaryBrush,
            BrushId.AccentFillColorDisabledBrush                    => BrushKeys.AccentFillColorDisabledBrush,
            BrushId.SystemFillColorAttentionBrush                   => BrushKeys.SystemFillColorAttentionBrush,
            BrushId.KeyboardFocusBorderColorBrush                   => BrushKeys.KeyboardFocusBorderColorBrush,
            BrushId.TextFillColorPrimaryBrush                       => BrushKeys.TextFillColorPrimaryBrush,
            BrushId.TextFillColorSecondaryBrush                     => BrushKeys.TextFillColorSecondaryBrush,
            BrushId.TextFillColorTertiaryBrush                      => BrushKeys.TextFillColorTertiaryBrush,
            BrushId.TextFillColorDisabledBrush                      => BrushKeys.TextFillColorDisabledBrush,
            BrushId.TextPlaceholderColorBrush                       => BrushKeys.TextPlaceholderColorBrush,
            BrushId.TextFillColorInverseBrush                       => BrushKeys.TextFillColorInverseBrush,
            BrushId.AccentTextFillColorDisabledBrush                => BrushKeys.AccentTextFillColorDisabledBrush,
            BrushId.TextOnAccentFillColorSelectedTextBrush          => BrushKeys.TextOnAccentFillColorSelectedTextBrush,
            BrushId.TextOnAccentFillColorPrimaryBrush               => BrushKeys.TextOnAccentFillColorPrimaryBrush,
            BrushId.TextOnAccentFillColorSecondaryBrush             => BrushKeys.TextOnAccentFillColorSecondaryBrush,
            BrushId.TextOnAccentFillColorDisabledBrush              => BrushKeys.TextOnAccentFillColorDisabledBrush,
            BrushId.ControlFillColorPrimaryBrush                    => BrushKeys.ControlFillColorPrimaryBrush,
            BrushId.ControlFillColorSecondaryBrush                  => BrushKeys.ControlFillColorSecondaryBrush,
            BrushId.ControlFillColorTertiaryBrush                   => BrushKeys.ControlFillColorTertiaryBrush,
            BrushId.ControlFillColorDisabledBrush                   => BrushKeys.ControlFillColorDisabledBrush,
            BrushId.ControlFillColorInputActiveBrush                => BrushKeys.ControlFillColorInputActiveBrush,
            BrushId.ControlStrongFillColorDefaultBrush              => BrushKeys.ControlStrongFillColorDefaultBrush,
            BrushId.ControlStrongFillColorDisabledBrush             => BrushKeys.ControlStrongFillColorDisabledBrush,
            BrushId.ControlSolidFillColorDefaultBrush               => BrushKeys.ControlSolidFillColorDefaultBrush,
            BrushId.SubtleFillColorTransparentBrush                 => BrushKeys.SubtleFillColorTransparentBrush,
            BrushId.SubtleFillColorSecondaryBrush                   => BrushKeys.SubtleFillColorSecondaryBrush,
            BrushId.SubtleFillColorTertiaryBrush                    => BrushKeys.SubtleFillColorTertiaryBrush,
            BrushId.SubtleFillColorDisabledBrush                    => BrushKeys.SubtleFillColorDisabledBrush,
            BrushId.ControlAltFillColorTransparentBrush             => BrushKeys.ControlAltFillColorTransparentBrush,
            BrushId.ControlAltFillColorSecondaryBrush               => BrushKeys.ControlAltFillColorSecondaryBrush,
            BrushId.ControlAltFillColorTertiaryBrush                => BrushKeys.ControlAltFillColorTertiaryBrush,
            BrushId.ControlAltFillColorQuarternaryBrush             => BrushKeys.ControlAltFillColorQuarternaryBrush,
            BrushId.ControlAltFillColorDisabledBrush                => BrushKeys.ControlAltFillColorDisabledBrush,
            BrushId.ControlOnImageFillColorDefaultBrush             => BrushKeys.ControlOnImageFillColorDefaultBrush,
            BrushId.ControlOnImageFillColorSecondaryBrush           => BrushKeys.ControlOnImageFillColorSecondaryBrush,
            BrushId.ControlOnImageFillColorTertiaryBrush            => BrushKeys.ControlOnImageFillColorTertiaryBrush,
            BrushId.ControlOnImageFillColorDisabledBrush            => BrushKeys.ControlOnImageFillColorDisabledBrush,
            BrushId.ControlStrokeColorDefaultBrush                  => BrushKeys.ControlStrokeColorDefaultBrush,
            BrushId.ControlStrokeColorSecondaryBrush                => BrushKeys.ControlStrokeColorSecondaryBrush,
            BrushId.ControlStrokeColorTertiaryBrush                 => BrushKeys.ControlStrokeColorTertiaryBrush,
            BrushId.ControlStrokeColorOnAccentDefaultBrush          => BrushKeys.ControlStrokeColorOnAccentDefaultBrush,
            BrushId.ControlStrokeColorOnAccentSecondaryBrush        => BrushKeys.ControlStrokeColorOnAccentSecondaryBrush,
            BrushId.ControlStrokeColorOnAccentTertiaryBrush         => BrushKeys.ControlStrokeColorOnAccentTertiaryBrush,
            BrushId.ControlStrokeColorOnAccentDisabledBrush         => BrushKeys.ControlStrokeColorOnAccentDisabledBrush,
            BrushId.ControlStrokeColorForStrongFillWhenOnImageBrush => BrushKeys.ControlStrokeColorForStrongFillWhenOnImageBrush,
            BrushId.CardStrokeColorDefaultBrush                     => BrushKeys.CardStrokeColorDefaultBrush,
            BrushId.CardStrokeColorDefaultSolidBrush                => BrushKeys.CardStrokeColorDefaultSolidBrush,
            BrushId.ControlStrongStrokeColorDefaultBrush            => BrushKeys.ControlStrongStrokeColorDefaultBrush,
            BrushId.ControlStrongStrokeColorDisabledBrush           => BrushKeys.ControlStrongStrokeColorDisabledBrush,
            BrushId.SurfaceStrokeColorDefaultBrush                  => BrushKeys.SurfaceStrokeColorDefaultBrush,
            BrushId.SurfaceStrokeColorFlyoutBrush                   => BrushKeys.SurfaceStrokeColorFlyoutBrush,
            BrushId.SurfaceStrokeColorInverseBrush                  => BrushKeys.SurfaceStrokeColorInverseBrush,
            BrushId.DividerStrokeColorDefaultBrush                  => BrushKeys.DividerStrokeColorDefaultBrush,
            BrushId.FocusStrokeColorOuterBrush                      => BrushKeys.FocusStrokeColorOuterBrush,
            BrushId.FocusStrokeColorInnerBrush                      => BrushKeys.FocusStrokeColorInnerBrush,
            BrushId.CardBackgroundFillColorDefaultBrush             => BrushKeys.CardBackgroundFillColorDefaultBrush,
            BrushId.CardBackgroundFillColorSecondaryBrush           => BrushKeys.CardBackgroundFillColorSecondaryBrush,
            BrushId.EmbeddedFillColorDefaultBrush                   => BrushKeys.EmbeddedFillColorDefaultBrush,
            BrushId.EmbeddedFillColorStrongBrush                    => BrushKeys.EmbeddedFillColorStrongBrush,
            BrushId.EmbeddedFillColorDeepBrush                      => BrushKeys.EmbeddedFillColorDeepBrush,
            BrushId.SmokeFillColorDefaultBrush                      => BrushKeys.SmokeFillColorDefaultBrush,
            BrushId.LayerFillColorDefaultBrush                      => BrushKeys.LayerFillColorDefaultBrush,
            BrushId.LayerFillColorAltBrush                          => BrushKeys.LayerFillColorAltBrush,
            BrushId.LayerOnAcrylicFillColorDefaultBrush             => BrushKeys.LayerOnAcrylicFillColorDefaultBrush,
            BrushId.LayerOnAccentAcrylicFillColorDefaultBrush       => BrushKeys.LayerOnAccentAcrylicFillColorDefaultBrush,
            BrushId.LayerOnMicaBaseAltFillColorDefaultBrush         => BrushKeys.LayerOnMicaBaseAltFillColorDefaultBrush,
            BrushId.LayerOnMicaBaseAltFillColorSecondaryBrush       => BrushKeys.LayerOnMicaBaseAltFillColorSecondaryBrush,
            BrushId.LayerOnMicaBaseAltFillColorTertiaryBrush        => BrushKeys.LayerOnMicaBaseAltFillColorTertiaryBrush,
            BrushId.LayerOnMicaBaseAltFillColorTransparentBrush     => BrushKeys.LayerOnMicaBaseAltFillColorTransparentBrush,
            BrushId.SolidBackgroundFillColorBaseBrush               => BrushKeys.SolidBackgroundFillColorBaseBrush,
            BrushId.SolidBackgroundFillColorSecondaryBrush          => BrushKeys.SolidBackgroundFillColorSecondaryBrush,
            BrushId.SolidBackgroundFillColorTertiaryBrush           => BrushKeys.SolidBackgroundFillColorTertiaryBrush,
            BrushId.SolidBackgroundFillColorQuarternaryBrush        => BrushKeys.SolidBackgroundFillColorQuarternaryBrush,
            BrushId.SolidBackgroundFillColorBaseAltBrush            => BrushKeys.SolidBackgroundFillColorBaseAltBrush,
            BrushId.SystemFillColorSuccessBrush                     => BrushKeys.SystemFillColorSuccessBrush,
            BrushId.SystemFillColorCautionBrush                     => BrushKeys.SystemFillColorCautionBrush,
            BrushId.SystemFillColorCriticalBrush                    => BrushKeys.SystemFillColorCriticalBrush,
            BrushId.SystemFillColorNeutralBrush                     => BrushKeys.SystemFillColorNeutralBrush,
            BrushId.SystemFillColorSolidNeutralBrush                => BrushKeys.SystemFillColorSolidNeutralBrush,
            BrushId.SystemFillColorAttentionBackgroundBrush         => BrushKeys.SystemFillColorAttentionBackgroundBrush,
            BrushId.SystemFillColorSuccessBackgroundBrush           => BrushKeys.SystemFillColorSuccessBackgroundBrush,
            BrushId.SystemFillColorCautionBackgroundBrush           => BrushKeys.SystemFillColorCautionBackgroundBrush,
            BrushId.SystemFillColorCriticalBackgroundBrush          => BrushKeys.SystemFillColorCriticalBackgroundBrush,
            BrushId.SystemFillColorNeutralBackgroundBrush           => BrushKeys.SystemFillColorNeutralBackgroundBrush,
            BrushId.SystemFillColorSolidAttentionBackgroundBrush    => BrushKeys.SystemFillColorSolidAttentionBackgroundBrush,
            BrushId.SystemFillColorSolidNeutralBackgroundBrush      => BrushKeys.SystemFillColorSolidNeutralBackgroundBrush,
            BrushId.ControlElevationBorderBrush                     => BrushKeys.ControlElevationBorderBrush,
            BrushId.CircleElevationBorderBrush                      => BrushKeys.CircleElevationBorderBrush,
            BrushId.AccentControlElevationBorderBrush               => BrushKeys.AccentControlElevationBorderBrush,
            // BrushId.TextControlElevationBorderBrush                 => BrushKeys.TextControlElevationBorderBrush,
            BrushId.StripedBackgroundBrush                 => BrushKeys.StripedBackgroundBrush,
            BrushId.AcrylicBackgroundFillColorDefaultBrush => BrushKeys.AcrylicBackgroundFillColorDefaultBrush,
            BrushId.WindowBackgroundBrush                  => BrushKeys.WindowBackgroundBrush,
            BrushId.WindowForegroundBrush                  => BrushKeys.WindowForegroundBrush,
            BrushId.WindowBorderBrush                      => BrushKeys.WindowBorderBrush,
            BrushId.WindowBorderInactiveBrush              => BrushKeys.WindowBorderInactiveBrush,
            BrushId.SystemFillColorInformationalBrush      => BrushKeys.SystemFillColorInformationalBrush,
            BrushId.LeftNavigationViewSeparatorBrush       => BrushKeys.LeftNavigationViewSeparatorBrush,

            // Palette.xaml:
            BrushId.PalettePrimaryBrush          => BrushKeys.PalettePrimaryBrush,
            BrushId.PaletteRedBrush              => BrushKeys.PaletteRedBrush,
            BrushId.PaletteRedBrushBright        => BrushKeys.PaletteRedBrushBright,
            BrushId.PaletteRedBrushDim           => BrushKeys.PaletteRedBrushDim,
            BrushId.PalettePinkBrush             => BrushKeys.PalettePinkBrush,
            BrushId.PalettePinkBrushBright       => BrushKeys.PalettePinkBrushBright,
            BrushId.PalettePinkBrushDim          => BrushKeys.PalettePinkBrushDim,
            BrushId.PalettePurpleBrush           => BrushKeys.PalettePurpleBrush,
            BrushId.PaletteDeepPurpleBrush       => BrushKeys.PaletteDeepPurpleBrush,
            BrushId.PaletteIndigoBrush           => BrushKeys.PaletteIndigoBrush,
            BrushId.PaletteBlueBrush             => BrushKeys.PaletteBlueBrush,
            BrushId.PaletteBlueBrushBright       => BrushKeys.PaletteBlueBrushBright,
            BrushId.PaletteBlueBrushDim          => BrushKeys.PaletteBlueBrushDim,
            BrushId.PaletteLightBlueBrush        => BrushKeys.PaletteLightBlueBrush,
            BrushId.PaletteLightBlueBrushBright  => BrushKeys.PaletteLightBlueBrushBright,
            BrushId.PaletteLightBlueBrushDim     => BrushKeys.PaletteLightBlueBrushDim,
            BrushId.PaletteCyanBrush             => BrushKeys.PaletteCyanBrush,
            BrushId.PaletteCyanBrushBright       => BrushKeys.PaletteCyanBrushBright,
            BrushId.PaletteCyanBrushDim          => BrushKeys.PaletteCyanBrushDim,
            BrushId.PaletteTealBrush             => BrushKeys.PaletteTealBrush,
            BrushId.PaletteGreenBrush            => BrushKeys.PaletteGreenBrush,
            BrushId.PaletteGreenBrushBright      => BrushKeys.PaletteGreenBrushBright,
            BrushId.PaletteGreenBrushDim         => BrushKeys.PaletteGreenBrushDim,
            BrushId.PaletteLightGreenBrush       => BrushKeys.PaletteLightGreenBrush,
            BrushId.PaletteLimeBrush             => BrushKeys.PaletteLimeBrush,
            BrushId.PaletteYellowBrush           => BrushKeys.PaletteYellowBrush,
            BrushId.PaletteYellowBrushBright     => BrushKeys.PaletteYellowBrushBright,
            BrushId.PaletteYellowBrushDim        => BrushKeys.PaletteYellowBrushDim,
            BrushId.PaletteAmberBrush            => BrushKeys.PaletteAmberBrush,
            BrushId.PaletteOrangeBrush           => BrushKeys.PaletteOrangeBrush,
            BrushId.PaletteOrangeBrushBright     => BrushKeys.PaletteOrangeBrushBright,
            BrushId.PaletteOrangeBrushDim        => BrushKeys.PaletteOrangeBrushDim,
            BrushId.PaletteDeepOrangeBrush       => BrushKeys.PaletteDeepOrangeBrush,
            BrushId.PaletteDeepOrangeBrushBright => BrushKeys.PaletteDeepOrangeBrushBright,
            BrushId.PaletteDeepOrangeBrushDim    => BrushKeys.PaletteDeepOrangeBrushDim,
            BrushId.PaletteBrownBrush            => BrushKeys.PaletteBrownBrush,
            BrushId.PaletteGreyBrush             => BrushKeys.PaletteGreyBrush,
            BrushId.PaletteBlueGreyBrush         => BrushKeys.PaletteBlueGreyBrush,

            // StaticColors.xaml
            BrushId.TextFillColorLightPrimaryBrush       => BrushKeys.TextFillColorLightPrimaryBrush,
            BrushId.TextFillColorLightSecondaryBrush     => BrushKeys.TextFillColorLightSecondaryBrush,
            BrushId.TextFillColorLightTertiaryBrush      => BrushKeys.TextFillColorLightTertiaryBrush,
            BrushId.TextFillColorLightDisabledBrush      => BrushKeys.TextFillColorLightDisabledBrush,
            BrushId.TextFillColorLightInverseBrush       => BrushKeys.TextFillColorLightInverseBrush,
            BrushId.TextFillColorDarkPrimaryBrush        => BrushKeys.TextFillColorDarkPrimaryBrush,
            BrushId.TextFillColorDarkSecondaryBrush      => BrushKeys.TextFillColorDarkSecondaryBrush,
            BrushId.TextFillColorDarkTertiaryBrush       => BrushKeys.TextFillColorDarkTertiaryBrush,
            BrushId.TextFillColorDarkDisabledBrush       => BrushKeys.TextFillColorDarkDisabledBrush,
            BrushId.TextFillColorDarkInverseBrush        => BrushKeys.TextFillColorDarkInverseBrush,
            BrushId.ApplicationBackgroundColorLightBrush => BrushKeys.ApplicationBackgroundColorLightBrush,
            BrushId.ApplicationBackgroundColorDarkBrush  => BrushKeys.ApplicationBackgroundColorDarkBrush,
            BrushId.ControlStrongFillColorLightBrush     => BrushKeys.ControlStrongFillColorLightBrush,
            BrushId.ControlStrongFillColorDarkBrush      => BrushKeys.ControlStrongFillColorDarkBrush,

            _ => BrushKeys.Unknown,
        };
    }
}