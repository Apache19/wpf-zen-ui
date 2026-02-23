// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 'required' 修饰符或声明为可以为 null。

namespace Wpf.Ui.Markup;

public static class ThemeKeys
{
    public static ComponentResourceKey SystemAccentColorBrush { get; private set; }

    public static ComponentResourceKey SystemAccentColorPrimaryBrush { get; private set; }
    public static ComponentResourceKey SystemAccentColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey SystemAccentColorTertiaryBrush { get; private set; }

    public static ComponentResourceKey AccentTextFillColorPrimaryBrush { get; private set; }
    public static ComponentResourceKey AccentTextFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey AccentTextFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey AccentFillColorSelectedTextBackgroundBrush { get; private set; }
    public static ComponentResourceKey AccentFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey AccentFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey AccentFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey AccentFillColorDisabledBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorAttentionBrush { get; private set; }

    public static ComponentResourceKey KeyboardFocusBorderColorBrush { get; private set; }

    public static ComponentResourceKey TextFillColorPrimaryBrush { get; private set; }
    public static ComponentResourceKey TextFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey TextFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey TextFillColorDisabledBrush { get; private set; }
    public static ComponentResourceKey TextPlaceholderColorBrush { get; private set; }
    public static ComponentResourceKey TextFillColorInverseBrush { get; private set; }

    public static ComponentResourceKey AccentTextFillColorDisabledBrush { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorSelectedTextBrush { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorPrimaryBrush { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorDisabledBrush { get; private set; }

    public static ComponentResourceKey ControlFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey ControlFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey ControlFillColorDisabledBrush { get; private set; }
    public static ComponentResourceKey ControlFillColorTransparentBrush { get; private set; }
    public static ComponentResourceKey ControlFillColorInputActiveBrush { get; private set; }

    public static ComponentResourceKey ControlStrongFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDisabledBrush { get; private set; }
    public static ComponentResourceKey ControlSolidFillColorDefaultBrush { get; private set; }

    public static ComponentResourceKey SubtleFillColorTransparentBrush { get; private set; }
    public static ComponentResourceKey SubtleFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey SubtleFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey SubtleFillColorDisabledBrush { get; private set; }

    public static ComponentResourceKey ControlAltFillColorTransparentBrush { get; private set; }
    public static ComponentResourceKey ControlAltFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey ControlAltFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey ControlAltFillColorQuarternaryBrush { get; private set; }
    public static ComponentResourceKey ControlAltFillColorDisabledBrush { get; private set; }

    public static ComponentResourceKey ControlOnImageFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorDisabledBrush { get; private set; }

    public static ComponentResourceKey ControlStrokeColorDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentSecondaryBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentTertiaryBrush { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDisabledBrush { get; private set; }

    public static ComponentResourceKey ControlStrokeColorForStrongFillWhenOnImageBrush { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefaultBrush { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefaultSolidBrush { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDefaultBrush { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDisabledBrush { get; private set; }

    public static ComponentResourceKey SurfaceStrokeColorDefaultBrush { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorFlyoutBrush { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorInverseBrush { get; private set; }

    public static ComponentResourceKey DividerStrokeColorDefaultBrush { get; private set; }

    public static ComponentResourceKey FocusStrokeColorOuterBrush { get; private set; }
    public static ComponentResourceKey FocusStrokeColorInnerBrush { get; private set; }

    public static ComponentResourceKey CardBackgroundFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey CardBackgroundFillColorSecondaryBrush { get; private set; }

    public static ComponentResourceKey SmokeFillColorDefaultBrush { get; private set; }

    public static ComponentResourceKey LayerFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey LayerFillColorAltBrush { get; private set; }
    public static ComponentResourceKey LayerOnAcrylicFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey LayerOnAccentAcrylicFillColorDefaultBrush { get; private set; }

    public static ComponentResourceKey LayerOnMicaBaseAltFillColorDefaultBrush { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTransparentBrush { get; private set; }

    public static ComponentResourceKey SolidBackgroundFillColorBaseBrush { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorSecondaryBrush { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorTertiaryBrush { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorQuarternaryBrush { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorBaseAltBrush { get; private set; }

    public static ComponentResourceKey SystemFillColorSuccessBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorCautionBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorCriticalBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutralBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutralBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorAttentionBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorSuccessBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorCautionBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorCriticalBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutralBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidAttentionBackgroundBrush { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutralBackgroundBrush { get; private set; }

    public static ComponentResourceKey ControlElevationBorderBrush { get; private set; }
    public static ComponentResourceKey CircleElevationBorderBrush { get; private set; }
    public static ComponentResourceKey AccentControlElevationBorderBrush { get; private set; }
    public static ComponentResourceKey TextControlElevationBorderBrush { get; private set; }

    public static ComponentResourceKey StripedBackgroundBrush { get; private set; }

    public static ComponentResourceKey AcrylicBackgroundFillColorDefaultBrush { get; private set; }

    public static ComponentResourceKey WindowBackgroundBrush { get; private set; }
    public static ComponentResourceKey WindowForegroundBrush { get; private set; }
    public static ComponentResourceKey WindowBorderBrush { get; private set; }
    public static ComponentResourceKey WindowBorderInactiveBrush { get; private set; }

    public static ComponentResourceKey SystemFillColorInformationalBrush { get; private set; }

    public static ComponentResourceKey LeftNavigationViewSeparatorBrush { get; private set; }

    static ThemeKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(ThemeKeys);
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
        foreach (var property in properties)
        {
            if (property.PropertyType == typeof(ComponentResourceKey))
            {
                var key = Create(property.Name);
                property.SetValue(null, key);
            }
        }
    }

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
