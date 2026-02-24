// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
#pragma warning disable CS8618

namespace Wpf.Ui.Theming;

public static class ColorKeys
{
    // Light.xaml / Dark.xaml / HighContrast.xaml

    public static ComponentResourceKey SystemAccentColor { get; private set; }
    public static ComponentResourceKey SystemAccentColorPrimary { get; private set; }
    public static ComponentResourceKey SystemAccentColorSecondary { get; private set; }
    public static ComponentResourceKey SystemAccentColorTertiary { get; private set; }
    public static ComponentResourceKey WindowBackgroundColor { get; private set; }
    public static ComponentResourceKey WindowBorderColor { get; private set; }
    public static ComponentResourceKey WindowBorderColorInactive { get; private set; }
    public static ComponentResourceKey KeyboardFocusBorderColor { get; private set; }
    public static ComponentResourceKey TextFillColorPrimary { get; private set; }
    public static ComponentResourceKey TextFillColorSecondary { get; private set; }
    public static ComponentResourceKey TextFillColorTertiary { get; private set; }
    public static ComponentResourceKey TextFillColorDisabled { get; private set; }
    public static ComponentResourceKey TextPlaceholderColor { get; private set; }
    public static ComponentResourceKey TextFillColorInverse { get; private set; }
    public static ComponentResourceKey AccentTextFillColorDisabled { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorSelectedText { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorPrimary { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorSecondary { get; private set; }
    public static ComponentResourceKey TextOnAccentFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlFillColorDefault { get; private set; }
    public static ComponentResourceKey ControlFillColorSecondary { get; private set; }
    public static ComponentResourceKey ControlFillColorTertiary { get; private set; }
    public static ComponentResourceKey ControlFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlFillColorTransparent { get; private set; }
    public static ComponentResourceKey ControlFillColorInputActive { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDefault { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlSolidFillColorDefault { get; private set; }
    public static ComponentResourceKey SubtleFillColorTransparent { get; private set; }
    public static ComponentResourceKey SubtleFillColorSecondary { get; private set; }
    public static ComponentResourceKey SubtleFillColorTertiary { get; private set; }
    public static ComponentResourceKey SubtleFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlAltFillColorTransparent { get; private set; }
    public static ComponentResourceKey ControlAltFillColorSecondary { get; private set; }
    public static ComponentResourceKey ControlAltFillColorTertiary { get; private set; }
    public static ComponentResourceKey ControlAltFillColorQuarternary { get; private set; }
    public static ComponentResourceKey ControlAltFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorDefault { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorSecondary { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorTertiary { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorDisabled { get; private set; }
    public static ComponentResourceKey AccentFillColorDisabled { get; private set; }
    public static ComponentResourceKey ControlStrokeColorDefault { get; private set; }
    public static ComponentResourceKey ControlStrokeColorSecondary { get; private set; }
    public static ComponentResourceKey ControlStrokeColorTertiary { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDefault { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentSecondary { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentTertiary { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDisabled { get; private set; }
    public static ComponentResourceKey ControlStrokeColorForStrongFillWhenOnImage { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefault { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefaultSolid { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDefault { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDisabled { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorDefault { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorFlyout { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorInverse { get; private set; }
    public static ComponentResourceKey DividerStrokeColorDefault { get; private set; }
    public static ComponentResourceKey FocusStrokeColorOuter { get; private set; }
    public static ComponentResourceKey FocusStrokeColorInner { get; private set; }
    public static ComponentResourceKey CardBackgroundFillColorDefault { get; private set; }
    public static ComponentResourceKey CardBackgroundFillColorSecondary { get; private set; }
    public static ComponentResourceKey SmokeFillColorDefault { get; private set; }
    public static ComponentResourceKey LayerFillColorDefault { get; private set; }
    public static ComponentResourceKey LayerFillColorAlt { get; private set; }
    public static ComponentResourceKey LayerOnAcrylicFillColorDefault { get; private set; }
    public static ComponentResourceKey LayerOnAccentAcrylicFillColorDefault { get; private set; }
    public static ComponentResourceKey SnowflakeGradientTop { get; private set; }
    public static ComponentResourceKey SnowflakeGradientBottom { get; private set; }
    public static ComponentResourceKey AcrylicBackgroundFillColorDefault { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorDefault { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorSecondary { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTertiary { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTransparent { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorBase { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorSecondary { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorTertiary { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorQuarternary { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorTransparent { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorBaseAlt { get; private set; }
    public static ComponentResourceKey SystemFillColorAttention { get; private set; }
    public static ComponentResourceKey SystemFillColorInformational { get; private set; }
    public static ComponentResourceKey SystemFillColorSuccess { get; private set; }
    public static ComponentResourceKey SystemFillColorCaution { get; private set; }
    public static ComponentResourceKey SystemFillColorCritical { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutral { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutral { get; private set; }
    public static ComponentResourceKey SystemFillColorAttentionBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorSuccessBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorCautionBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorCriticalBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutralBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidAttentionBackground { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutralBackground { get; private set; }

    // Palette.xaml
    public static ComponentResourceKey PalettePrimaryColor { get; private set; }
    public static ComponentResourceKey PaletteRedColor { get; private set; }
    public static ComponentResourceKey PalettePinkColor { get; private set; }
    public static ComponentResourceKey PalettePurpleColor { get; private set; }
    public static ComponentResourceKey PaletteDeepPurpleColor { get; private set; }
    public static ComponentResourceKey PaletteIndigoColor { get; private set; }
    public static ComponentResourceKey PaletteBlueColor { get; private set; }
    public static ComponentResourceKey PaletteLightBlueColor { get; private set; }
    public static ComponentResourceKey PaletteCyanColor { get; private set; }
    public static ComponentResourceKey PaletteTealColor { get; private set; }
    public static ComponentResourceKey PaletteGreenColor { get; private set; }
    public static ComponentResourceKey PaletteLightGreenColor { get; private set; }
    public static ComponentResourceKey PaletteLimeColor { get; private set; }
    public static ComponentResourceKey PaletteYellowColor { get; private set; }
    public static ComponentResourceKey PaletteAmberColor { get; private set; }
    public static ComponentResourceKey PaletteOrangeColor { get; private set; }
    public static ComponentResourceKey PaletteDeepOrangeColor { get; private set; }
    public static ComponentResourceKey PaletteBrownColor { get; private set; }
    public static ComponentResourceKey PaletteGreyColor { get; private set; }
    public static ComponentResourceKey PaletteBlueGreyColor { get; private set; }

    // StaticColors.xaml
    public static ComponentResourceKey TextFillColorLightPrimary { get; private set; }
    public static ComponentResourceKey TextFillColorLightSecondary { get; private set; }
    public static ComponentResourceKey TextFillColorLightTertiary { get; private set; }
    public static ComponentResourceKey TextFillColorLightDisabled { get; private set; }
    public static ComponentResourceKey TextFillColorLightInverse { get; private set; }
    public static ComponentResourceKey TextFillColorDarkPrimary { get; private set; }
    public static ComponentResourceKey TextFillColorDarkSecondary { get; private set; }
    public static ComponentResourceKey TextFillColorDarkTertiary { get; private set; }
    public static ComponentResourceKey TextFillColorDarkDisabled { get; private set; }
    public static ComponentResourceKey TextFillColorDarkInverse { get; private set; }
    public static ComponentResourceKey ApplicationBackgroundColorLight { get; private set; }
    public static ComponentResourceKey ApplicationBackgroundColorDark { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorLight { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDark { get; private set; }

    static ColorKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(ColorKeys);
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
