// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;
using Wpf.Ui.Theming;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Key))]
[MarkupExtensionReturnType(typeof(Brush))]
public class BrushResourceExtension : MarkupExtension
{
    // 类名用“ThemeResourceExtension”时在 ReSharper 环境无法使用自动补全

    [ConstructorArgument("key")]
    public ThemeBrush Key { get; }

    public BrushResourceExtension(ThemeBrush key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = KeyMapping(Key);
        return new DynamicResourceExtension(key).ProvideValue(serviceProvider);
    }

    private static ComponentResourceKey KeyMapping(ThemeBrush key)
    {
        return key switch
        {
            // Light.xaml / Dark.xaml / HighContrast.xaml
            ThemeBrush.SystemAccentColorBrush                          => BrushKeys.SystemAccentColorBrush,
            ThemeBrush.SystemAccentColorPrimaryBrush                   => BrushKeys.SystemAccentColorPrimaryBrush,
            ThemeBrush.SystemAccentColorSecondaryBrush                 => BrushKeys.SystemAccentColorSecondaryBrush,
            ThemeBrush.SystemAccentColorTertiaryBrush                  => BrushKeys.SystemAccentColorTertiaryBrush,
            ThemeBrush.AccentTextFillColorPrimaryBrush                 => BrushKeys.AccentTextFillColorPrimaryBrush,
            ThemeBrush.AccentTextFillColorSecondaryBrush               => BrushKeys.AccentTextFillColorSecondaryBrush,
            ThemeBrush.AccentTextFillColorTertiaryBrush                => BrushKeys.AccentTextFillColorTertiaryBrush,
            ThemeBrush.AccentFillColorSelectedTextBackgroundBrush      => BrushKeys.AccentFillColorSelectedTextBackgroundBrush,
            ThemeBrush.AccentFillColorDefaultBrush                     => BrushKeys.AccentFillColorDefaultBrush,
            ThemeBrush.AccentFillColorSecondaryBrush                   => BrushKeys.AccentFillColorSecondaryBrush,
            ThemeBrush.AccentFillColorTertiaryBrush                    => BrushKeys.AccentFillColorTertiaryBrush,
            ThemeBrush.AccentFillColorDisabledBrush                    => BrushKeys.AccentFillColorDisabledBrush,
            ThemeBrush.SystemFillColorAttentionBrush                   => BrushKeys.SystemFillColorAttentionBrush,
            ThemeBrush.KeyboardFocusBorderColorBrush                   => BrushKeys.KeyboardFocusBorderColorBrush,
            ThemeBrush.TextFillColorPrimaryBrush                       => BrushKeys.TextFillColorPrimaryBrush,
            ThemeBrush.TextFillColorSecondaryBrush                     => BrushKeys.TextFillColorSecondaryBrush,
            ThemeBrush.TextFillColorTertiaryBrush                      => BrushKeys.TextFillColorTertiaryBrush,
            ThemeBrush.TextFillColorDisabledBrush                      => BrushKeys.TextFillColorDisabledBrush,
            ThemeBrush.TextPlaceholderColorBrush                       => BrushKeys.TextPlaceholderColorBrush,
            ThemeBrush.TextFillColorInverseBrush                       => BrushKeys.TextFillColorInverseBrush,
            ThemeBrush.AccentTextFillColorDisabledBrush                => BrushKeys.AccentTextFillColorDisabledBrush,
            ThemeBrush.TextOnAccentFillColorSelectedTextBrush          => BrushKeys.TextOnAccentFillColorSelectedTextBrush,
            ThemeBrush.TextOnAccentFillColorPrimaryBrush               => BrushKeys.TextOnAccentFillColorPrimaryBrush,
            ThemeBrush.TextOnAccentFillColorSecondaryBrush             => BrushKeys.TextOnAccentFillColorSecondaryBrush,
            ThemeBrush.TextOnAccentFillColorDisabledBrush              => BrushKeys.TextOnAccentFillColorDisabledBrush,
            ThemeBrush.ControlFillColorDefaultBrush                    => BrushKeys.ControlFillColorDefaultBrush,
            ThemeBrush.ControlFillColorSecondaryBrush                  => BrushKeys.ControlFillColorSecondaryBrush,
            ThemeBrush.ControlFillColorTertiaryBrush                   => BrushKeys.ControlFillColorTertiaryBrush,
            ThemeBrush.ControlFillColorDisabledBrush                   => BrushKeys.ControlFillColorDisabledBrush,
            ThemeBrush.ControlFillColorTransparentBrush                => BrushKeys.ControlFillColorTransparentBrush,
            ThemeBrush.ControlFillColorInputActiveBrush                => BrushKeys.ControlFillColorInputActiveBrush,
            ThemeBrush.ControlStrongFillColorDefaultBrush              => BrushKeys.ControlStrongFillColorDefaultBrush,
            ThemeBrush.ControlStrongFillColorDisabledBrush             => BrushKeys.ControlStrongFillColorDisabledBrush,
            ThemeBrush.ControlSolidFillColorDefaultBrush               => BrushKeys.ControlSolidFillColorDefaultBrush,
            ThemeBrush.SubtleFillColorTransparentBrush                 => BrushKeys.SubtleFillColorTransparentBrush,
            ThemeBrush.SubtleFillColorSecondaryBrush                   => BrushKeys.SubtleFillColorSecondaryBrush,
            ThemeBrush.SubtleFillColorTertiaryBrush                    => BrushKeys.SubtleFillColorTertiaryBrush,
            ThemeBrush.SubtleFillColorDisabledBrush                    => BrushKeys.SubtleFillColorDisabledBrush,
            ThemeBrush.ControlAltFillColorTransparentBrush             => BrushKeys.ControlAltFillColorTransparentBrush,
            ThemeBrush.ControlAltFillColorSecondaryBrush               => BrushKeys.ControlAltFillColorSecondaryBrush,
            ThemeBrush.ControlAltFillColorTertiaryBrush                => BrushKeys.ControlAltFillColorTertiaryBrush,
            ThemeBrush.ControlAltFillColorQuarternaryBrush             => BrushKeys.ControlAltFillColorQuarternaryBrush,
            ThemeBrush.ControlAltFillColorDisabledBrush                => BrushKeys.ControlAltFillColorDisabledBrush,
            ThemeBrush.ControlOnImageFillColorDefaultBrush             => BrushKeys.ControlOnImageFillColorDefaultBrush,
            ThemeBrush.ControlOnImageFillColorSecondaryBrush           => BrushKeys.ControlOnImageFillColorSecondaryBrush,
            ThemeBrush.ControlOnImageFillColorTertiaryBrush            => BrushKeys.ControlOnImageFillColorTertiaryBrush,
            ThemeBrush.ControlOnImageFillColorDisabledBrush            => BrushKeys.ControlOnImageFillColorDisabledBrush,
            ThemeBrush.ControlStrokeColorDefaultBrush                  => BrushKeys.ControlStrokeColorDefaultBrush,
            ThemeBrush.ControlStrokeColorSecondaryBrush                => BrushKeys.ControlStrokeColorSecondaryBrush,
            ThemeBrush.ControlStrokeColorTertiaryBrush                 => BrushKeys.ControlStrokeColorTertiaryBrush,
            ThemeBrush.ControlStrokeColorOnAccentDefaultBrush          => BrushKeys.ControlStrokeColorOnAccentDefaultBrush,
            ThemeBrush.ControlStrokeColorOnAccentSecondaryBrush        => BrushKeys.ControlStrokeColorOnAccentSecondaryBrush,
            ThemeBrush.ControlStrokeColorOnAccentTertiaryBrush         => BrushKeys.ControlStrokeColorOnAccentTertiaryBrush,
            ThemeBrush.ControlStrokeColorOnAccentDisabledBrush         => BrushKeys.ControlStrokeColorOnAccentDisabledBrush,
            ThemeBrush.ControlStrokeColorForStrongFillWhenOnImageBrush => BrushKeys.ControlStrokeColorForStrongFillWhenOnImageBrush,
            ThemeBrush.CardStrokeColorDefaultBrush                     => BrushKeys.CardStrokeColorDefaultBrush,
            ThemeBrush.CardStrokeColorDefaultSolidBrush                => BrushKeys.CardStrokeColorDefaultSolidBrush,
            ThemeBrush.ControlStrongStrokeColorDefaultBrush            => BrushKeys.ControlStrongStrokeColorDefaultBrush,
            ThemeBrush.ControlStrongStrokeColorDisabledBrush           => BrushKeys.ControlStrongStrokeColorDisabledBrush,
            ThemeBrush.SurfaceStrokeColorDefaultBrush                  => BrushKeys.SurfaceStrokeColorDefaultBrush,
            ThemeBrush.SurfaceStrokeColorFlyoutBrush                   => BrushKeys.SurfaceStrokeColorFlyoutBrush,
            ThemeBrush.SurfaceStrokeColorInverseBrush                  => BrushKeys.SurfaceStrokeColorInverseBrush,
            ThemeBrush.DividerStrokeColorDefaultBrush                  => BrushKeys.DividerStrokeColorDefaultBrush,
            ThemeBrush.FocusStrokeColorOuterBrush                      => BrushKeys.FocusStrokeColorOuterBrush,
            ThemeBrush.FocusStrokeColorInnerBrush                      => BrushKeys.FocusStrokeColorInnerBrush,
            ThemeBrush.CardBackgroundFillColorDefaultBrush             => BrushKeys.CardBackgroundFillColorDefaultBrush,
            ThemeBrush.CardBackgroundFillColorSecondaryBrush           => BrushKeys.CardBackgroundFillColorSecondaryBrush,
            ThemeBrush.SmokeFillColorDefaultBrush                      => BrushKeys.SmokeFillColorDefaultBrush,
            ThemeBrush.LayerFillColorDefaultBrush                      => BrushKeys.LayerFillColorDefaultBrush,
            ThemeBrush.LayerFillColorAltBrush                          => BrushKeys.LayerFillColorAltBrush,
            ThemeBrush.LayerOnAcrylicFillColorDefaultBrush             => BrushKeys.LayerOnAcrylicFillColorDefaultBrush,
            ThemeBrush.LayerOnAccentAcrylicFillColorDefaultBrush       => BrushKeys.LayerOnAccentAcrylicFillColorDefaultBrush,
            ThemeBrush.LayerOnMicaBaseAltFillColorDefaultBrush         => BrushKeys.LayerOnMicaBaseAltFillColorDefaultBrush,
            ThemeBrush.LayerOnMicaBaseAltFillColorSecondaryBrush       => BrushKeys.LayerOnMicaBaseAltFillColorSecondaryBrush,
            ThemeBrush.LayerOnMicaBaseAltFillColorTertiaryBrush        => BrushKeys.LayerOnMicaBaseAltFillColorTertiaryBrush,
            ThemeBrush.LayerOnMicaBaseAltFillColorTransparentBrush     => BrushKeys.LayerOnMicaBaseAltFillColorTransparentBrush,
            ThemeBrush.SolidBackgroundFillColorBaseBrush               => BrushKeys.SolidBackgroundFillColorBaseBrush,
            ThemeBrush.SolidBackgroundFillColorSecondaryBrush          => BrushKeys.SolidBackgroundFillColorSecondaryBrush,
            ThemeBrush.SolidBackgroundFillColorTertiaryBrush           => BrushKeys.SolidBackgroundFillColorTertiaryBrush,
            ThemeBrush.SolidBackgroundFillColorQuarternaryBrush        => BrushKeys.SolidBackgroundFillColorQuarternaryBrush,
            ThemeBrush.SolidBackgroundFillColorBaseAltBrush            => BrushKeys.SolidBackgroundFillColorBaseAltBrush,
            ThemeBrush.SystemFillColorSuccessBrush                     => BrushKeys.SystemFillColorSuccessBrush,
            ThemeBrush.SystemFillColorCautionBrush                     => BrushKeys.SystemFillColorCautionBrush,
            ThemeBrush.SystemFillColorCriticalBrush                    => BrushKeys.SystemFillColorCriticalBrush,
            ThemeBrush.SystemFillColorNeutralBrush                     => BrushKeys.SystemFillColorNeutralBrush,
            ThemeBrush.SystemFillColorSolidNeutralBrush                => BrushKeys.SystemFillColorSolidNeutralBrush,
            ThemeBrush.SystemFillColorAttentionBackgroundBrush         => BrushKeys.SystemFillColorAttentionBackgroundBrush,
            ThemeBrush.SystemFillColorSuccessBackgroundBrush           => BrushKeys.SystemFillColorSuccessBackgroundBrush,
            ThemeBrush.SystemFillColorCautionBackgroundBrush           => BrushKeys.SystemFillColorCautionBackgroundBrush,
            ThemeBrush.SystemFillColorCriticalBackgroundBrush          => BrushKeys.SystemFillColorCriticalBackgroundBrush,
            ThemeBrush.SystemFillColorNeutralBackgroundBrush           => BrushKeys.SystemFillColorNeutralBackgroundBrush,
            ThemeBrush.SystemFillColorSolidAttentionBackgroundBrush    => BrushKeys.SystemFillColorSolidAttentionBackgroundBrush,
            ThemeBrush.SystemFillColorSolidNeutralBackgroundBrush      => BrushKeys.SystemFillColorSolidNeutralBackgroundBrush,
            ThemeBrush.ControlElevationBorderBrush                     => BrushKeys.ControlElevationBorderBrush,
            ThemeBrush.CircleElevationBorderBrush                      => BrushKeys.CircleElevationBorderBrush,
            ThemeBrush.AccentControlElevationBorderBrush               => BrushKeys.AccentControlElevationBorderBrush,
            ThemeBrush.TextControlElevationBorderBrush                 => BrushKeys.TextControlElevationBorderBrush,
            ThemeBrush.StripedBackgroundBrush                          => BrushKeys.StripedBackgroundBrush,
            ThemeBrush.AcrylicBackgroundFillColorDefaultBrush          => BrushKeys.AcrylicBackgroundFillColorDefaultBrush,
            ThemeBrush.WindowBackgroundBrush                           => BrushKeys.WindowBackgroundBrush,
            ThemeBrush.WindowForegroundBrush                           => BrushKeys.WindowForegroundBrush,
            ThemeBrush.WindowBorderBrush                               => BrushKeys.WindowBorderBrush,
            ThemeBrush.WindowBorderInactiveBrush                       => BrushKeys.WindowBorderInactiveBrush,
            ThemeBrush.SystemFillColorInformationalBrush               => BrushKeys.SystemFillColorInformationalBrush,
            ThemeBrush.LeftNavigationViewSeparatorBrush                => BrushKeys.LeftNavigationViewSeparatorBrush,

            // Palette.xaml:
            ThemeBrush.PalettePrimaryBrush                             => BrushKeys.PalettePrimaryBrush,
            ThemeBrush.PaletteRedBrush                                 => BrushKeys.PaletteRedBrush,
            ThemeBrush.PalettePinkBrush                                => BrushKeys.PalettePinkBrush,
            ThemeBrush.PalettePurpleBrush                              => BrushKeys.PalettePurpleBrush,
            ThemeBrush.PaletteDeepPurpleBrush                          => BrushKeys.PaletteDeepPurpleBrush,
            ThemeBrush.PaletteIndigoBrush                              => BrushKeys.PaletteIndigoBrush,
            ThemeBrush.PaletteBlueBrush                                => BrushKeys.PaletteBlueBrush,
            ThemeBrush.PaletteLightBlueBrush                           => BrushKeys.PaletteLightBlueBrush,
            ThemeBrush.PaletteCyanBrush                                => BrushKeys.PaletteCyanBrush,
            ThemeBrush.PaletteTealBrush                                => BrushKeys.PaletteTealBrush,
            ThemeBrush.PaletteGreenBrush                               => BrushKeys.PaletteGreenBrush,
            ThemeBrush.PaletteLightGreenBrush                          => BrushKeys.PaletteLightGreenBrush,
            ThemeBrush.PaletteLimeBrush                                => BrushKeys.PaletteLimeBrush,
            ThemeBrush.PaletteYellowBrush                              => BrushKeys.PaletteYellowBrush,
            ThemeBrush.PaletteAmberBrush                               => BrushKeys.PaletteAmberBrush,
            ThemeBrush.PaletteOrangeBrush                              => BrushKeys.PaletteOrangeBrush,
            ThemeBrush.PaletteDeepOrangeBrush                          => BrushKeys.PaletteDeepOrangeBrush,
            ThemeBrush.PaletteBrownBrush                               => BrushKeys.PaletteBrownBrush,
            ThemeBrush.PaletteGreyBrush                                => BrushKeys.PaletteGreyBrush,
            ThemeBrush.PaletteBlueGreyBrush                            => BrushKeys.PaletteBlueGreyBrush,

            // StaticColors.xaml
            ThemeBrush.TextFillColorLightPrimaryBrush                  => BrushKeys.TextFillColorLightPrimaryBrush,
            ThemeBrush.TextFillColorLightSecondaryBrush                => BrushKeys.TextFillColorLightSecondaryBrush,
            ThemeBrush.TextFillColorLightTertiaryBrush                 => BrushKeys.TextFillColorLightTertiaryBrush,
            ThemeBrush.TextFillColorLightDisabledBrush                 => BrushKeys.TextFillColorLightDisabledBrush,
            ThemeBrush.TextFillColorLightInverseBrush                  => BrushKeys.TextFillColorLightInverseBrush,
            ThemeBrush.TextFillColorDarkPrimaryBrush                   => BrushKeys.TextFillColorDarkPrimaryBrush,
            ThemeBrush.TextFillColorDarkSecondaryBrush                 => BrushKeys.TextFillColorDarkSecondaryBrush,
            ThemeBrush.TextFillColorDarkTertiaryBrush                  => BrushKeys.TextFillColorDarkTertiaryBrush,
            ThemeBrush.TextFillColorDarkDisabledBrush                  => BrushKeys.TextFillColorDarkDisabledBrush,
            ThemeBrush.TextFillColorDarkInverseBrush                   => BrushKeys.TextFillColorDarkInverseBrush,
            ThemeBrush.ApplicationBackgroundColorLightBrush            => BrushKeys.ApplicationBackgroundColorLightBrush,
            ThemeBrush.ApplicationBackgroundColorDarkBrush             => BrushKeys.ApplicationBackgroundColorDarkBrush,
            ThemeBrush.ControlStrongFillColorLightBrush                => BrushKeys.ControlStrongFillColorLightBrush,
            ThemeBrush.ControlStrongFillColorDarkBrush                 => BrushKeys.ControlStrongFillColorDarkBrush,
            ThemeBrush.Unknown                                         => throw new NotSupportedException(),
            _                                                          => throw new IndexOutOfRangeException(),
        };
    }
}