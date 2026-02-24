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
    public BrushKey Key { get; }

    public BrushResourceExtension(BrushKey key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = KeyMapping(Key);
        return new DynamicResourceExtension(key).ProvideValue(serviceProvider);
    }

    private static ComponentResourceKey KeyMapping(BrushKey key)
    {
        return key switch
        {
            // Light.xaml / Dark.xaml / HighContrast.xaml
            BrushKey.SystemAccentColorBrush                          => BrushKeys.SystemAccentColorBrush,
            BrushKey.SystemAccentColorPrimaryBrush                   => BrushKeys.SystemAccentColorPrimaryBrush,
            BrushKey.SystemAccentColorSecondaryBrush                 => BrushKeys.SystemAccentColorSecondaryBrush,
            BrushKey.SystemAccentColorTertiaryBrush                  => BrushKeys.SystemAccentColorTertiaryBrush,
            BrushKey.AccentTextFillColorPrimaryBrush                 => BrushKeys.AccentTextFillColorPrimaryBrush,
            BrushKey.AccentTextFillColorSecondaryBrush               => BrushKeys.AccentTextFillColorSecondaryBrush,
            BrushKey.AccentTextFillColorTertiaryBrush                => BrushKeys.AccentTextFillColorTertiaryBrush,
            BrushKey.AccentFillColorSelectedTextBackgroundBrush      => BrushKeys.AccentFillColorSelectedTextBackgroundBrush,
            BrushKey.AccentFillColorDefaultBrush                     => BrushKeys.AccentFillColorDefaultBrush,
            BrushKey.AccentFillColorSecondaryBrush                   => BrushKeys.AccentFillColorSecondaryBrush,
            BrushKey.AccentFillColorTertiaryBrush                    => BrushKeys.AccentFillColorTertiaryBrush,
            BrushKey.AccentFillColorDisabledBrush                    => BrushKeys.AccentFillColorDisabledBrush,
            BrushKey.SystemFillColorAttentionBrush                   => BrushKeys.SystemFillColorAttentionBrush,
            BrushKey.KeyboardFocusBorderColorBrush                   => BrushKeys.KeyboardFocusBorderColorBrush,
            BrushKey.TextFillColorPrimaryBrush                       => BrushKeys.TextFillColorPrimaryBrush,
            BrushKey.TextFillColorSecondaryBrush                     => BrushKeys.TextFillColorSecondaryBrush,
            BrushKey.TextFillColorTertiaryBrush                      => BrushKeys.TextFillColorTertiaryBrush,
            BrushKey.TextFillColorDisabledBrush                      => BrushKeys.TextFillColorDisabledBrush,
            BrushKey.TextPlaceholderColorBrush                       => BrushKeys.TextPlaceholderColorBrush,
            BrushKey.TextFillColorInverseBrush                       => BrushKeys.TextFillColorInverseBrush,
            BrushKey.AccentTextFillColorDisabledBrush                => BrushKeys.AccentTextFillColorDisabledBrush,
            BrushKey.TextOnAccentFillColorSelectedTextBrush          => BrushKeys.TextOnAccentFillColorSelectedTextBrush,
            BrushKey.TextOnAccentFillColorPrimaryBrush               => BrushKeys.TextOnAccentFillColorPrimaryBrush,
            BrushKey.TextOnAccentFillColorSecondaryBrush             => BrushKeys.TextOnAccentFillColorSecondaryBrush,
            BrushKey.TextOnAccentFillColorDisabledBrush              => BrushKeys.TextOnAccentFillColorDisabledBrush,
            BrushKey.ControlFillColorDefaultBrush                    => BrushKeys.ControlFillColorDefaultBrush,
            BrushKey.ControlFillColorSecondaryBrush                  => BrushKeys.ControlFillColorSecondaryBrush,
            BrushKey.ControlFillColorTertiaryBrush                   => BrushKeys.ControlFillColorTertiaryBrush,
            BrushKey.ControlFillColorDisabledBrush                   => BrushKeys.ControlFillColorDisabledBrush,
            BrushKey.ControlFillColorTransparentBrush                => BrushKeys.ControlFillColorTransparentBrush,
            BrushKey.ControlFillColorInputActiveBrush                => BrushKeys.ControlFillColorInputActiveBrush,
            BrushKey.ControlStrongFillColorDefaultBrush              => BrushKeys.ControlStrongFillColorDefaultBrush,
            BrushKey.ControlStrongFillColorDisabledBrush             => BrushKeys.ControlStrongFillColorDisabledBrush,
            BrushKey.ControlSolidFillColorDefaultBrush               => BrushKeys.ControlSolidFillColorDefaultBrush,
            BrushKey.SubtleFillColorTransparentBrush                 => BrushKeys.SubtleFillColorTransparentBrush,
            BrushKey.SubtleFillColorSecondaryBrush                   => BrushKeys.SubtleFillColorSecondaryBrush,
            BrushKey.SubtleFillColorTertiaryBrush                    => BrushKeys.SubtleFillColorTertiaryBrush,
            BrushKey.SubtleFillColorDisabledBrush                    => BrushKeys.SubtleFillColorDisabledBrush,
            BrushKey.ControlAltFillColorTransparentBrush             => BrushKeys.ControlAltFillColorTransparentBrush,
            BrushKey.ControlAltFillColorSecondaryBrush               => BrushKeys.ControlAltFillColorSecondaryBrush,
            BrushKey.ControlAltFillColorTertiaryBrush                => BrushKeys.ControlAltFillColorTertiaryBrush,
            BrushKey.ControlAltFillColorQuarternaryBrush             => BrushKeys.ControlAltFillColorQuarternaryBrush,
            BrushKey.ControlAltFillColorDisabledBrush                => BrushKeys.ControlAltFillColorDisabledBrush,
            BrushKey.ControlOnImageFillColorDefaultBrush             => BrushKeys.ControlOnImageFillColorDefaultBrush,
            BrushKey.ControlOnImageFillColorSecondaryBrush           => BrushKeys.ControlOnImageFillColorSecondaryBrush,
            BrushKey.ControlOnImageFillColorTertiaryBrush            => BrushKeys.ControlOnImageFillColorTertiaryBrush,
            BrushKey.ControlOnImageFillColorDisabledBrush            => BrushKeys.ControlOnImageFillColorDisabledBrush,
            BrushKey.ControlStrokeColorDefaultBrush                  => BrushKeys.ControlStrokeColorDefaultBrush,
            BrushKey.ControlStrokeColorSecondaryBrush                => BrushKeys.ControlStrokeColorSecondaryBrush,
            BrushKey.ControlStrokeColorTertiaryBrush                 => BrushKeys.ControlStrokeColorTertiaryBrush,
            BrushKey.ControlStrokeColorOnAccentDefaultBrush          => BrushKeys.ControlStrokeColorOnAccentDefaultBrush,
            BrushKey.ControlStrokeColorOnAccentSecondaryBrush        => BrushKeys.ControlStrokeColorOnAccentSecondaryBrush,
            BrushKey.ControlStrokeColorOnAccentTertiaryBrush         => BrushKeys.ControlStrokeColorOnAccentTertiaryBrush,
            BrushKey.ControlStrokeColorOnAccentDisabledBrush         => BrushKeys.ControlStrokeColorOnAccentDisabledBrush,
            BrushKey.ControlStrokeColorForStrongFillWhenOnImageBrush => BrushKeys.ControlStrokeColorForStrongFillWhenOnImageBrush,
            BrushKey.CardStrokeColorDefaultBrush                     => BrushKeys.CardStrokeColorDefaultBrush,
            BrushKey.CardStrokeColorDefaultSolidBrush                => BrushKeys.CardStrokeColorDefaultSolidBrush,
            BrushKey.ControlStrongStrokeColorDefaultBrush            => BrushKeys.ControlStrongStrokeColorDefaultBrush,
            BrushKey.ControlStrongStrokeColorDisabledBrush           => BrushKeys.ControlStrongStrokeColorDisabledBrush,
            BrushKey.SurfaceStrokeColorDefaultBrush                  => BrushKeys.SurfaceStrokeColorDefaultBrush,
            BrushKey.SurfaceStrokeColorFlyoutBrush                   => BrushKeys.SurfaceStrokeColorFlyoutBrush,
            BrushKey.SurfaceStrokeColorInverseBrush                  => BrushKeys.SurfaceStrokeColorInverseBrush,
            BrushKey.DividerStrokeColorDefaultBrush                  => BrushKeys.DividerStrokeColorDefaultBrush,
            BrushKey.FocusStrokeColorOuterBrush                      => BrushKeys.FocusStrokeColorOuterBrush,
            BrushKey.FocusStrokeColorInnerBrush                      => BrushKeys.FocusStrokeColorInnerBrush,
            BrushKey.CardBackgroundFillColorDefaultBrush             => BrushKeys.CardBackgroundFillColorDefaultBrush,
            BrushKey.CardBackgroundFillColorSecondaryBrush           => BrushKeys.CardBackgroundFillColorSecondaryBrush,
            BrushKey.SmokeFillColorDefaultBrush                      => BrushKeys.SmokeFillColorDefaultBrush,
            BrushKey.LayerFillColorDefaultBrush                      => BrushKeys.LayerFillColorDefaultBrush,
            BrushKey.LayerFillColorAltBrush                          => BrushKeys.LayerFillColorAltBrush,
            BrushKey.LayerOnAcrylicFillColorDefaultBrush             => BrushKeys.LayerOnAcrylicFillColorDefaultBrush,
            BrushKey.LayerOnAccentAcrylicFillColorDefaultBrush       => BrushKeys.LayerOnAccentAcrylicFillColorDefaultBrush,
            BrushKey.LayerOnMicaBaseAltFillColorDefaultBrush         => BrushKeys.LayerOnMicaBaseAltFillColorDefaultBrush,
            BrushKey.LayerOnMicaBaseAltFillColorSecondaryBrush       => BrushKeys.LayerOnMicaBaseAltFillColorSecondaryBrush,
            BrushKey.LayerOnMicaBaseAltFillColorTertiaryBrush        => BrushKeys.LayerOnMicaBaseAltFillColorTertiaryBrush,
            BrushKey.LayerOnMicaBaseAltFillColorTransparentBrush     => BrushKeys.LayerOnMicaBaseAltFillColorTransparentBrush,
            BrushKey.SolidBackgroundFillColorBaseBrush               => BrushKeys.SolidBackgroundFillColorBaseBrush,
            BrushKey.SolidBackgroundFillColorSecondaryBrush          => BrushKeys.SolidBackgroundFillColorSecondaryBrush,
            BrushKey.SolidBackgroundFillColorTertiaryBrush           => BrushKeys.SolidBackgroundFillColorTertiaryBrush,
            BrushKey.SolidBackgroundFillColorQuarternaryBrush        => BrushKeys.SolidBackgroundFillColorQuarternaryBrush,
            BrushKey.SolidBackgroundFillColorBaseAltBrush            => BrushKeys.SolidBackgroundFillColorBaseAltBrush,
            BrushKey.SystemFillColorSuccessBrush                     => BrushKeys.SystemFillColorSuccessBrush,
            BrushKey.SystemFillColorCautionBrush                     => BrushKeys.SystemFillColorCautionBrush,
            BrushKey.SystemFillColorCriticalBrush                    => BrushKeys.SystemFillColorCriticalBrush,
            BrushKey.SystemFillColorNeutralBrush                     => BrushKeys.SystemFillColorNeutralBrush,
            BrushKey.SystemFillColorSolidNeutralBrush                => BrushKeys.SystemFillColorSolidNeutralBrush,
            BrushKey.SystemFillColorAttentionBackgroundBrush         => BrushKeys.SystemFillColorAttentionBackgroundBrush,
            BrushKey.SystemFillColorSuccessBackgroundBrush           => BrushKeys.SystemFillColorSuccessBackgroundBrush,
            BrushKey.SystemFillColorCautionBackgroundBrush           => BrushKeys.SystemFillColorCautionBackgroundBrush,
            BrushKey.SystemFillColorCriticalBackgroundBrush          => BrushKeys.SystemFillColorCriticalBackgroundBrush,
            BrushKey.SystemFillColorNeutralBackgroundBrush           => BrushKeys.SystemFillColorNeutralBackgroundBrush,
            BrushKey.SystemFillColorSolidAttentionBackgroundBrush    => BrushKeys.SystemFillColorSolidAttentionBackgroundBrush,
            BrushKey.SystemFillColorSolidNeutralBackgroundBrush      => BrushKeys.SystemFillColorSolidNeutralBackgroundBrush,
            BrushKey.ControlElevationBorderBrush                     => BrushKeys.ControlElevationBorderBrush,
            BrushKey.CircleElevationBorderBrush                      => BrushKeys.CircleElevationBorderBrush,
            BrushKey.AccentControlElevationBorderBrush               => BrushKeys.AccentControlElevationBorderBrush,
            BrushKey.TextControlElevationBorderBrush                 => BrushKeys.TextControlElevationBorderBrush,
            BrushKey.StripedBackgroundBrush                          => BrushKeys.StripedBackgroundBrush,
            BrushKey.AcrylicBackgroundFillColorDefaultBrush          => BrushKeys.AcrylicBackgroundFillColorDefaultBrush,
            BrushKey.WindowBackgroundBrush                           => BrushKeys.WindowBackgroundBrush,
            BrushKey.WindowForegroundBrush                           => BrushKeys.WindowForegroundBrush,
            BrushKey.WindowBorderBrush                               => BrushKeys.WindowBorderBrush,
            BrushKey.WindowBorderInactiveBrush                       => BrushKeys.WindowBorderInactiveBrush,
            BrushKey.SystemFillColorInformationalBrush               => BrushKeys.SystemFillColorInformationalBrush,
            BrushKey.LeftNavigationViewSeparatorBrush                => BrushKeys.LeftNavigationViewSeparatorBrush,

            // Palette.xaml:
            BrushKey.PalettePrimaryBrush                             => BrushKeys.PalettePrimaryBrush,
            BrushKey.PaletteRedBrush                                 => BrushKeys.PaletteRedBrush,
            BrushKey.PalettePinkBrush                                => BrushKeys.PalettePinkBrush,
            BrushKey.PalettePurpleBrush                              => BrushKeys.PalettePurpleBrush,
            BrushKey.PaletteDeepPurpleBrush                          => BrushKeys.PaletteDeepPurpleBrush,
            BrushKey.PaletteIndigoBrush                              => BrushKeys.PaletteIndigoBrush,
            BrushKey.PaletteBlueBrush                                => BrushKeys.PaletteBlueBrush,
            BrushKey.PaletteLightBlueBrush                           => BrushKeys.PaletteLightBlueBrush,
            BrushKey.PaletteCyanBrush                                => BrushKeys.PaletteCyanBrush,
            BrushKey.PaletteTealBrush                                => BrushKeys.PaletteTealBrush,
            BrushKey.PaletteGreenBrush                               => BrushKeys.PaletteGreenBrush,
            BrushKey.PaletteLightGreenBrush                          => BrushKeys.PaletteLightGreenBrush,
            BrushKey.PaletteLimeBrush                                => BrushKeys.PaletteLimeBrush,
            BrushKey.PaletteYellowBrush                              => BrushKeys.PaletteYellowBrush,
            BrushKey.PaletteAmberBrush                               => BrushKeys.PaletteAmberBrush,
            BrushKey.PaletteOrangeBrush                              => BrushKeys.PaletteOrangeBrush,
            BrushKey.PaletteDeepOrangeBrush                          => BrushKeys.PaletteDeepOrangeBrush,
            BrushKey.PaletteBrownBrush                               => BrushKeys.PaletteBrownBrush,
            BrushKey.PaletteGreyBrush                                => BrushKeys.PaletteGreyBrush,
            BrushKey.PaletteBlueGreyBrush                            => BrushKeys.PaletteBlueGreyBrush,

            // StaticColors.xaml
            BrushKey.TextFillColorLightPrimaryBrush                  => BrushKeys.TextFillColorLightPrimaryBrush,
            BrushKey.TextFillColorLightSecondaryBrush                => BrushKeys.TextFillColorLightSecondaryBrush,
            BrushKey.TextFillColorLightTertiaryBrush                 => BrushKeys.TextFillColorLightTertiaryBrush,
            BrushKey.TextFillColorLightDisabledBrush                 => BrushKeys.TextFillColorLightDisabledBrush,
            BrushKey.TextFillColorLightInverseBrush                  => BrushKeys.TextFillColorLightInverseBrush,
            BrushKey.TextFillColorDarkPrimaryBrush                   => BrushKeys.TextFillColorDarkPrimaryBrush,
            BrushKey.TextFillColorDarkSecondaryBrush                 => BrushKeys.TextFillColorDarkSecondaryBrush,
            BrushKey.TextFillColorDarkTertiaryBrush                  => BrushKeys.TextFillColorDarkTertiaryBrush,
            BrushKey.TextFillColorDarkDisabledBrush                  => BrushKeys.TextFillColorDarkDisabledBrush,
            BrushKey.TextFillColorDarkInverseBrush                   => BrushKeys.TextFillColorDarkInverseBrush,
            BrushKey.ApplicationBackgroundColorLightBrush            => BrushKeys.ApplicationBackgroundColorLightBrush,
            BrushKey.ApplicationBackgroundColorDarkBrush             => BrushKeys.ApplicationBackgroundColorDarkBrush,
            BrushKey.ControlStrongFillColorLightBrush                => BrushKeys.ControlStrongFillColorLightBrush,
            BrushKey.ControlStrongFillColorDarkBrush                 => BrushKeys.ControlStrongFillColorDarkBrush,
            BrushKey.Unknown                                         => throw new NotSupportedException(),
            _                                                        => throw new IndexOutOfRangeException(),
        };
    }
}