// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Markup;

namespace Wpf.Ui.Markup;

[ContentProperty(nameof(Key))]
[MarkupExtensionReturnType(typeof(object))]
public class UIResourceExtension : MarkupExtension
{
    // 用“ThemeResource”时在 ReSharper 环境无法使用自动补全

    [ConstructorArgument("key")]
    public ThemeResource Key { get; }

    public UIResourceExtension(ThemeResource key)
    {
        Key = key;
    }

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var key = KeyMapping(Key);
        return new DynamicResourceExtension(key).ProvideValue(serviceProvider);
    }

    internal static ComponentResourceKey KeyMapping(ThemeResource key)
    {
        return key switch
        {
            ThemeResource.SystemAccentColorBrush                          => BrushKeys.SystemAccentColorBrush,
            ThemeResource.SystemAccentColorPrimaryBrush                   => BrushKeys.SystemAccentColorPrimaryBrush,
            ThemeResource.SystemAccentColorSecondaryBrush                 => BrushKeys.SystemAccentColorSecondaryBrush,
            ThemeResource.SystemAccentColorTertiaryBrush                  => BrushKeys.SystemAccentColorTertiaryBrush,
            ThemeResource.AccentTextFillColorPrimaryBrush                 => BrushKeys.AccentTextFillColorPrimaryBrush,
            ThemeResource.AccentTextFillColorSecondaryBrush               => BrushKeys.AccentTextFillColorSecondaryBrush,
            ThemeResource.AccentTextFillColorTertiaryBrush                => BrushKeys.AccentTextFillColorTertiaryBrush,
            ThemeResource.AccentFillColorSelectedTextBackgroundBrush      => BrushKeys.AccentFillColorSelectedTextBackgroundBrush,
            ThemeResource.AccentFillColorDefaultBrush                     => BrushKeys.AccentFillColorDefaultBrush,
            ThemeResource.AccentFillColorSecondaryBrush                   => BrushKeys.AccentFillColorSecondaryBrush,
            ThemeResource.AccentFillColorTertiaryBrush                    => BrushKeys.AccentFillColorTertiaryBrush,
            ThemeResource.AccentFillColorDisabledBrush                    => BrushKeys.AccentFillColorDisabledBrush,
            ThemeResource.SystemFillColorAttentionBrush                   => BrushKeys.SystemFillColorAttentionBrush,
            ThemeResource.KeyboardFocusBorderColorBrush                   => BrushKeys.KeyboardFocusBorderColorBrush,
            ThemeResource.TextFillColorPrimaryBrush                       => BrushKeys.TextFillColorPrimaryBrush,
            ThemeResource.TextFillColorSecondaryBrush                     => BrushKeys.TextFillColorSecondaryBrush,
            ThemeResource.TextFillColorTertiaryBrush                      => BrushKeys.TextFillColorTertiaryBrush,
            ThemeResource.TextFillColorDisabledBrush                      => BrushKeys.TextFillColorDisabledBrush,
            ThemeResource.TextPlaceholderColorBrush                       => BrushKeys.TextPlaceholderColorBrush,
            ThemeResource.TextFillColorInverseBrush                       => BrushKeys.TextFillColorInverseBrush,
            ThemeResource.AccentTextFillColorDisabledBrush                => BrushKeys.AccentTextFillColorDisabledBrush,
            ThemeResource.TextOnAccentFillColorSelectedTextBrush          => BrushKeys.TextOnAccentFillColorSelectedTextBrush,
            ThemeResource.TextOnAccentFillColorPrimaryBrush               => BrushKeys.TextOnAccentFillColorPrimaryBrush,
            ThemeResource.TextOnAccentFillColorSecondaryBrush             => BrushKeys.TextOnAccentFillColorSecondaryBrush,
            ThemeResource.TextOnAccentFillColorDisabledBrush              => BrushKeys.TextOnAccentFillColorDisabledBrush,
            ThemeResource.ControlFillColorDefaultBrush                    => BrushKeys.ControlFillColorDefaultBrush,
            ThemeResource.ControlFillColorSecondaryBrush                  => BrushKeys.ControlFillColorSecondaryBrush,
            ThemeResource.ControlFillColorTertiaryBrush                   => BrushKeys.ControlFillColorTertiaryBrush,
            ThemeResource.ControlFillColorDisabledBrush                   => BrushKeys.ControlFillColorDisabledBrush,
            ThemeResource.ControlFillColorTransparentBrush                => BrushKeys.ControlFillColorTransparentBrush,
            ThemeResource.ControlFillColorInputActiveBrush                => BrushKeys.ControlFillColorInputActiveBrush,
            ThemeResource.ControlStrongFillColorDefaultBrush              => BrushKeys.ControlStrongFillColorDefaultBrush,
            ThemeResource.ControlStrongFillColorDisabledBrush             => BrushKeys.ControlStrongFillColorDisabledBrush,
            ThemeResource.ControlSolidFillColorDefaultBrush               => BrushKeys.ControlSolidFillColorDefaultBrush,
            ThemeResource.SubtleFillColorTransparentBrush                 => BrushKeys.SubtleFillColorTransparentBrush,
            ThemeResource.SubtleFillColorSecondaryBrush                   => BrushKeys.SubtleFillColorSecondaryBrush,
            ThemeResource.SubtleFillColorTertiaryBrush                    => BrushKeys.SubtleFillColorTertiaryBrush,
            ThemeResource.SubtleFillColorDisabledBrush                    => BrushKeys.SubtleFillColorDisabledBrush,
            ThemeResource.ControlAltFillColorTransparentBrush             => BrushKeys.ControlAltFillColorTransparentBrush,
            ThemeResource.ControlAltFillColorSecondaryBrush               => BrushKeys.ControlAltFillColorSecondaryBrush,
            ThemeResource.ControlAltFillColorTertiaryBrush                => BrushKeys.ControlAltFillColorTertiaryBrush,
            ThemeResource.ControlAltFillColorQuarternaryBrush             => BrushKeys.ControlAltFillColorQuarternaryBrush,
            ThemeResource.ControlAltFillColorDisabledBrush                => BrushKeys.ControlAltFillColorDisabledBrush,
            ThemeResource.ControlOnImageFillColorDefaultBrush             => BrushKeys.ControlOnImageFillColorDefaultBrush,
            ThemeResource.ControlOnImageFillColorSecondaryBrush           => BrushKeys.ControlOnImageFillColorSecondaryBrush,
            ThemeResource.ControlOnImageFillColorTertiaryBrush            => BrushKeys.ControlOnImageFillColorTertiaryBrush,
            ThemeResource.ControlOnImageFillColorDisabledBrush            => BrushKeys.ControlOnImageFillColorDisabledBrush,
            ThemeResource.ControlStrokeColorDefaultBrush                  => BrushKeys.ControlStrokeColorDefaultBrush,
            ThemeResource.ControlStrokeColorSecondaryBrush                => BrushKeys.ControlStrokeColorSecondaryBrush,
            ThemeResource.ControlStrokeColorTertiaryBrush                 => BrushKeys.ControlStrokeColorTertiaryBrush,
            ThemeResource.ControlStrokeColorOnAccentDefaultBrush          => BrushKeys.ControlStrokeColorOnAccentDefaultBrush,
            ThemeResource.ControlStrokeColorOnAccentSecondaryBrush        => BrushKeys.ControlStrokeColorOnAccentSecondaryBrush,
            ThemeResource.ControlStrokeColorOnAccentTertiaryBrush         => BrushKeys.ControlStrokeColorOnAccentTertiaryBrush,
            ThemeResource.ControlStrokeColorOnAccentDisabledBrush         => BrushKeys.ControlStrokeColorOnAccentDisabledBrush,
            ThemeResource.ControlStrokeColorForStrongFillWhenOnImageBrush => BrushKeys.ControlStrokeColorForStrongFillWhenOnImageBrush,
            ThemeResource.CardStrokeColorDefaultBrush                     => BrushKeys.CardStrokeColorDefaultBrush,
            ThemeResource.CardStrokeColorDefaultSolidBrush                => BrushKeys.CardStrokeColorDefaultSolidBrush,
            ThemeResource.ControlStrongStrokeColorDefaultBrush            => BrushKeys.ControlStrongStrokeColorDefaultBrush,
            ThemeResource.ControlStrongStrokeColorDisabledBrush           => BrushKeys.ControlStrongStrokeColorDisabledBrush,
            ThemeResource.SurfaceStrokeColorDefaultBrush                  => BrushKeys.SurfaceStrokeColorDefaultBrush,
            ThemeResource.SurfaceStrokeColorFlyoutBrush                   => BrushKeys.SurfaceStrokeColorFlyoutBrush,
            ThemeResource.SurfaceStrokeColorInverseBrush                  => BrushKeys.SurfaceStrokeColorInverseBrush,
            ThemeResource.DividerStrokeColorDefaultBrush                  => BrushKeys.DividerStrokeColorDefaultBrush,
            ThemeResource.FocusStrokeColorOuterBrush                      => BrushKeys.FocusStrokeColorOuterBrush,
            ThemeResource.FocusStrokeColorInnerBrush                      => BrushKeys.FocusStrokeColorInnerBrush,
            ThemeResource.CardBackgroundFillColorDefaultBrush             => BrushKeys.CardBackgroundFillColorDefaultBrush,
            ThemeResource.CardBackgroundFillColorSecondaryBrush           => BrushKeys.CardBackgroundFillColorSecondaryBrush,
            ThemeResource.SmokeFillColorDefaultBrush                      => BrushKeys.SmokeFillColorDefaultBrush,
            ThemeResource.LayerFillColorDefaultBrush                      => BrushKeys.LayerFillColorDefaultBrush,
            ThemeResource.LayerFillColorAltBrush                          => BrushKeys.LayerFillColorAltBrush,
            ThemeResource.LayerOnAcrylicFillColorDefaultBrush             => BrushKeys.LayerOnAcrylicFillColorDefaultBrush,
            ThemeResource.LayerOnAccentAcrylicFillColorDefaultBrush       => BrushKeys.LayerOnAccentAcrylicFillColorDefaultBrush,
            ThemeResource.LayerOnMicaBaseAltFillColorDefaultBrush         => BrushKeys.LayerOnMicaBaseAltFillColorDefaultBrush,
            ThemeResource.LayerOnMicaBaseAltFillColorSecondaryBrush       => BrushKeys.LayerOnMicaBaseAltFillColorSecondaryBrush,
            ThemeResource.LayerOnMicaBaseAltFillColorTertiaryBrush        => BrushKeys.LayerOnMicaBaseAltFillColorTertiaryBrush,
            ThemeResource.LayerOnMicaBaseAltFillColorTransparentBrush     => BrushKeys.LayerOnMicaBaseAltFillColorTransparentBrush,
            ThemeResource.SolidBackgroundFillColorBaseBrush               => BrushKeys.SolidBackgroundFillColorBaseBrush,
            ThemeResource.SolidBackgroundFillColorSecondaryBrush          => BrushKeys.SolidBackgroundFillColorSecondaryBrush,
            ThemeResource.SolidBackgroundFillColorTertiaryBrush           => BrushKeys.SolidBackgroundFillColorTertiaryBrush,
            ThemeResource.SolidBackgroundFillColorQuarternaryBrush        => BrushKeys.SolidBackgroundFillColorQuarternaryBrush,
            ThemeResource.SolidBackgroundFillColorBaseAltBrush            => BrushKeys.SolidBackgroundFillColorBaseAltBrush,
            ThemeResource.SystemFillColorSuccessBrush                     => BrushKeys.SystemFillColorSuccessBrush,
            ThemeResource.SystemFillColorCautionBrush                     => BrushKeys.SystemFillColorCautionBrush,
            ThemeResource.SystemFillColorCriticalBrush                    => BrushKeys.SystemFillColorCriticalBrush,
            ThemeResource.SystemFillColorNeutralBrush                     => BrushKeys.SystemFillColorNeutralBrush,
            ThemeResource.SystemFillColorSolidNeutralBrush                => BrushKeys.SystemFillColorSolidNeutralBrush,
            ThemeResource.SystemFillColorAttentionBackgroundBrush         => BrushKeys.SystemFillColorAttentionBackgroundBrush,
            ThemeResource.SystemFillColorSuccessBackgroundBrush           => BrushKeys.SystemFillColorSuccessBackgroundBrush,
            ThemeResource.SystemFillColorCautionBackgroundBrush           => BrushKeys.SystemFillColorCautionBackgroundBrush,
            ThemeResource.SystemFillColorCriticalBackgroundBrush          => BrushKeys.SystemFillColorCriticalBackgroundBrush,
            ThemeResource.SystemFillColorNeutralBackgroundBrush           => BrushKeys.SystemFillColorNeutralBackgroundBrush,
            ThemeResource.SystemFillColorSolidAttentionBackgroundBrush    => BrushKeys.SystemFillColorSolidAttentionBackgroundBrush,
            ThemeResource.SystemFillColorSolidNeutralBackgroundBrush      => BrushKeys.SystemFillColorSolidNeutralBackgroundBrush,
            ThemeResource.ControlElevationBorderBrush                     => BrushKeys.ControlElevationBorderBrush,
            ThemeResource.CircleElevationBorderBrush                      => BrushKeys.CircleElevationBorderBrush,
            ThemeResource.AccentControlElevationBorderBrush               => BrushKeys.AccentControlElevationBorderBrush,
            ThemeResource.TextControlElevationBorderBrush                 => BrushKeys.TextControlElevationBorderBrush,
            ThemeResource.StripedBackgroundBrush                          => BrushKeys.StripedBackgroundBrush,
            ThemeResource.AcrylicBackgroundFillColorDefaultBrush          => BrushKeys.AcrylicBackgroundFillColorDefaultBrush,
            ThemeResource.WindowBackgroundBrush                           => BrushKeys.WindowBackgroundBrush,
            ThemeResource.WindowForegroundBrush                           => BrushKeys.WindowForegroundBrush,
            ThemeResource.WindowBorderBrush                               => BrushKeys.WindowBorderBrush,
            ThemeResource.WindowBorderInactiveBrush                       => BrushKeys.WindowBorderInactiveBrush,
            ThemeResource.SystemFillColorInformationalBrush               => BrushKeys.SystemFillColorInformationalBrush,
            ThemeResource.LeftNavigationViewSeparatorBrush                => BrushKeys.LeftNavigationViewSeparatorBrush,
            ThemeResource.Unknown                                         => throw new NotSupportedException(),
            _                                                             => throw new IndexOutOfRangeException(),
        };
    }
}