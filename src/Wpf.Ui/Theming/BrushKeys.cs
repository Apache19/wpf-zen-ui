// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

// ReSharper disable UnusedAutoPropertyAccessor.Local
#pragma warning disable CS8618

namespace Wpf.Ui.Theming;

public static class BrushKeys
{
    public static ComponentResourceKey Unknown { get; private set; }

    // Light.xaml / Dark.xaml / HighContrast.xaml

    /// <summary>Gets the brightest member of the accent fill brush hierarchy.</summary>
    public static ComponentResourceKey AccentFillPrimaryBrush                          { get; private set; }

    /// <summary>Gets the normal accent fill brush key.</summary>
    public static ComponentResourceKey AccentFillSecondaryBrush                        { get; private set; }

    /// <summary>Gets the dimmest member of the accent fill brush hierarchy.</summary>
    public static ComponentResourceKey AccentFillTertiaryBrush                         { get; private set; }

    /// <summary>Gets the primary accent-colored text and glyph brush key.</summary>
    public static ComponentResourceKey AccentTextPrimaryBrush                          { get; private set; }

    /// <summary>Gets the lower-emphasis accent-colored text and glyph brush key.</summary>
    public static ComponentResourceKey AccentTextSecondaryBrush                        { get; private set; }

    /// <summary>Gets the high-contrast text and glyph brush key intended for use over accent fills.</summary>
    public static ComponentResourceKey TextOnAccentPrimaryBrush                        { get; private set; }

    /// <summary>Gets the accent-colored stroke and focus indicator brush key.</summary>
    public static ComponentResourceKey AccentStrokeBrush                               { get; private set; }

    /// <summary>Gets the accent selection background brush key.</summary>
    public static ComponentResourceKey AccentSelectionBrush                            { get; private set; }
    public static ComponentResourceKey SystemColorWindowTextBrush                      { get; private set; }
    public static ComponentResourceKey SystemColorWindowBrush                          { get; private set; }
    public static ComponentResourceKey SystemColorHighlightTextBrush                   { get; private set; }
    public static ComponentResourceKey SystemColorHighlightBrush                       { get; private set; }
    public static ComponentResourceKey SystemColorControlTextBrush                     { get; private set; }
    public static ComponentResourceKey SystemColorControlBrush                         { get; private set; }
    public static ComponentResourceKey SystemColorHotTrackBrush                        { get; private set; }
    public static ComponentResourceKey SystemColorGrayTextBrush                        { get; private set; }
    public static ComponentResourceKey SystemColorWindowFrameBrush                     { get; private set; }
    public static ComponentResourceKey SystemFillColorAttentionBrush                   { get; private set; }
    public static ComponentResourceKey KeyboardFocusBorderColorBrush                   { get; private set; }
    public static ComponentResourceKey TextFillColorPrimaryBrush                       { get; private set; }
    public static ComponentResourceKey TextFillColorSecondaryBrush                     { get; private set; }
    public static ComponentResourceKey TextFillColorTertiaryBrush                      { get; private set; }
    public static ComponentResourceKey TextFillColorDisabledBrush                      { get; private set; }
    public static ComponentResourceKey TextPlaceholderColorBrush                       { get; private set; }
    public static ComponentResourceKey TextFillColorInverseBrush                       { get; private set; }
    public static ComponentResourceKey ControlFillColorPrimaryBrush                    { get; private set; }
    public static ComponentResourceKey ControlFillColorSecondaryBrush                  { get; private set; }
    public static ComponentResourceKey ControlFillColorTertiaryBrush                   { get; private set; }
    public static ComponentResourceKey ControlFillColorDisabledBrush                   { get; private set; }
    public static ComponentResourceKey ControlFillColorInputActiveBrush                { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDefaultBrush              { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDisabledBrush             { get; private set; }
    public static ComponentResourceKey ControlSolidFillColorDefaultBrush               { get; private set; }
    public static ComponentResourceKey SubtleFillColorTransparentBrush                 { get; private set; }
    public static ComponentResourceKey SubtleFillColorSecondaryBrush                   { get; private set; }
    public static ComponentResourceKey SubtleFillColorTertiaryBrush                    { get; private set; }
    public static ComponentResourceKey SubtleFillColorDisabledBrush                    { get; private set; }
    public static ComponentResourceKey ControlAltFillColorTransparentBrush             { get; private set; }
    public static ComponentResourceKey ControlAltFillColorSecondaryBrush               { get; private set; }
    public static ComponentResourceKey ControlAltFillColorTertiaryBrush                { get; private set; }
    public static ComponentResourceKey ControlAltFillColorQuarternaryBrush             { get; private set; }
    public static ComponentResourceKey ControlAltFillColorDisabledBrush                { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorDefaultBrush             { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorSecondaryBrush           { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorTertiaryBrush            { get; private set; }
    public static ComponentResourceKey ControlOnImageFillColorDisabledBrush            { get; private set; }
    public static ComponentResourceKey ControlStrokeColorDefaultBrush                  { get; private set; }
    public static ComponentResourceKey ControlStrokeColorSecondaryBrush                { get; private set; }
    public static ComponentResourceKey ControlStrokeColorTertiaryBrush                 { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDefaultBrush          { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentSecondaryBrush        { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentTertiaryBrush         { get; private set; }
    public static ComponentResourceKey ControlStrokeColorOnAccentDisabledBrush         { get; private set; }
    public static ComponentResourceKey ControlStrokeColorForStrongFillWhenOnImageBrush { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefaultBrush                     { get; private set; }
    public static ComponentResourceKey CardStrokeColorDefaultSolidBrush                { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDefaultBrush            { get; private set; }
    public static ComponentResourceKey ControlStrongStrokeColorDisabledBrush           { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorDefaultBrush                  { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorFlyoutBrush                   { get; private set; }
    public static ComponentResourceKey SurfaceStrokeColorInverseBrush                  { get; private set; }
    public static ComponentResourceKey DividerStrokeColorDefaultBrush                  { get; private set; }
    public static ComponentResourceKey FocusStrokeColorOuterBrush                      { get; private set; }
    public static ComponentResourceKey FocusStrokeColorInnerBrush                      { get; private set; }
    public static ComponentResourceKey CardBackgroundFillColorDefaultBrush             { get; private set; }
    public static ComponentResourceKey CardBackgroundFillColorSecondaryBrush           { get; private set; }
    public static ComponentResourceKey EmbeddedFillColorDefaultBrush                   { get; private set; }
    public static ComponentResourceKey EmbeddedFillColorStrongBrush                    { get; private set; }
    public static ComponentResourceKey EmbeddedFillColorDeepBrush                      { get; private set; }
    public static ComponentResourceKey SmokeFillColorDefaultBrush                      { get; private set; }
    public static ComponentResourceKey LayerFillColorDefaultBrush                      { get; private set; }
    public static ComponentResourceKey LayerFillColorAltBrush                          { get; private set; }
    public static ComponentResourceKey LayerOnAcrylicFillColorDefaultBrush             { get; private set; }
    public static ComponentResourceKey LayerOnAccentAcrylicFillColorDefaultBrush       { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorDefaultBrush         { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorSecondaryBrush       { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTertiaryBrush        { get; private set; }
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTransparentBrush     { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorBaseBrush               { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorSecondaryBrush          { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorTertiaryBrush           { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorQuarternaryBrush        { get; private set; }
    public static ComponentResourceKey SolidBackgroundFillColorBaseAltBrush            { get; private set; }
    public static ComponentResourceKey SystemFillColorSuccessBrush                     { get; private set; }
    public static ComponentResourceKey SystemFillColorCautionBrush                     { get; private set; }
    public static ComponentResourceKey SystemFillColorCriticalBrush                    { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutralBrush                     { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutralBrush                { get; private set; }
    public static ComponentResourceKey SystemFillColorAttentionBackgroundBrush         { get; private set; }
    public static ComponentResourceKey SystemFillColorSuccessBackgroundBrush           { get; private set; }
    public static ComponentResourceKey SystemFillColorCautionBackgroundBrush           { get; private set; }
    public static ComponentResourceKey SystemFillColorCriticalBackgroundBrush          { get; private set; }
    public static ComponentResourceKey SystemFillColorNeutralBackgroundBrush           { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidAttentionBackgroundBrush    { get; private set; }
    public static ComponentResourceKey SystemFillColorSolidNeutralBackgroundBrush      { get; private set; }
    public static ComponentResourceKey ControlElevationBorderBrush                     { get; private set; }
    public static ComponentResourceKey CircleElevationBorderBrush                      { get; private set; }
    public static ComponentResourceKey AccentControlElevationBorderBrush               { get; private set; }
    public static ComponentResourceKey StripedBackgroundBrush                          { get; private set; }
    public static ComponentResourceKey AcrylicBackgroundFillColorDefaultBrush          { get; private set; }
    public static ComponentResourceKey WindowBackgroundBrush                           { get; private set; }
    public static ComponentResourceKey WindowForegroundBrush                           { get; private set; }
    public static ComponentResourceKey WindowBorderBrush                               { get; private set; }
    public static ComponentResourceKey WindowBorderInactiveBrush                       { get; private set; }
    public static ComponentResourceKey SystemFillColorInformationalBrush               { get; private set; }
    public static ComponentResourceKey LeftNavigationViewSeparatorBrush                { get; private set; }

    // Palette.xaml:

    public static ComponentResourceKey PalettePrimaryBrush          { get; private set; }
    public static ComponentResourceKey PaletteRedBrush              { get; private set; }
    public static ComponentResourceKey PaletteRedBrushBright        { get; private set; }
    public static ComponentResourceKey PaletteRedBrushDim           { get; private set; }
    public static ComponentResourceKey PalettePinkBrush             { get; private set; }
    public static ComponentResourceKey PalettePinkBrushBright       { get; private set; }
    public static ComponentResourceKey PalettePinkBrushDim          { get; private set; }
    public static ComponentResourceKey PalettePurpleBrush           { get; private set; }
    public static ComponentResourceKey PaletteDeepPurpleBrush       { get; private set; }
    public static ComponentResourceKey PaletteIndigoBrush           { get; private set; }
    public static ComponentResourceKey PaletteBlueBrush             { get; private set; }
    public static ComponentResourceKey PaletteBlueBrushBright       { get; private set; }
    public static ComponentResourceKey PaletteBlueBrushDim          { get; private set; }
    public static ComponentResourceKey PaletteLightBlueBrush        { get; private set; }
    public static ComponentResourceKey PaletteLightBlueBrushBright  { get; private set; }
    public static ComponentResourceKey PaletteLightBlueBrushDim     { get; private set; }
    public static ComponentResourceKey PaletteCyanBrush             { get; private set; }
    public static ComponentResourceKey PaletteCyanBrushBright       { get; private set; }
    public static ComponentResourceKey PaletteCyanBrushDim          { get; private set; }
    public static ComponentResourceKey PaletteTealBrush             { get; private set; }
    public static ComponentResourceKey PaletteGreenBrush            { get; private set; }
    public static ComponentResourceKey PaletteGreenBrushBright      { get; private set; }
    public static ComponentResourceKey PaletteGreenBrushDim         { get; private set; }
    public static ComponentResourceKey PaletteLightGreenBrush       { get; private set; }
    public static ComponentResourceKey PaletteLimeBrush             { get; private set; }
    public static ComponentResourceKey PaletteYellowBrush           { get; private set; }
    public static ComponentResourceKey PaletteYellowBrushBright     { get; private set; }
    public static ComponentResourceKey PaletteYellowBrushDim        { get; private set; }
    public static ComponentResourceKey PaletteAmberBrush            { get; private set; }
    public static ComponentResourceKey PaletteOrangeBrush           { get; private set; }
    public static ComponentResourceKey PaletteOrangeBrushBright     { get; private set; }
    public static ComponentResourceKey PaletteOrangeBrushDim        { get; private set; }
    public static ComponentResourceKey PaletteDeepOrangeBrush       { get; private set; }
    public static ComponentResourceKey PaletteDeepOrangeBrushBright { get; private set; }
    public static ComponentResourceKey PaletteDeepOrangeBrushDim    { get; private set; }
    public static ComponentResourceKey PaletteBrownBrush            { get; private set; }
    public static ComponentResourceKey PaletteGreyBrush             { get; private set; }
    public static ComponentResourceKey PaletteBlueGreyBrush         { get; private set; }

    // StaticColors.xaml

    public static ComponentResourceKey TextFillColorLightPrimaryBrush       { get; private set; }
    public static ComponentResourceKey TextFillColorLightSecondaryBrush     { get; private set; }
    public static ComponentResourceKey TextFillColorLightTertiaryBrush      { get; private set; }
    public static ComponentResourceKey TextFillColorLightDisabledBrush      { get; private set; }
    public static ComponentResourceKey TextFillColorLightInverseBrush       { get; private set; }
    public static ComponentResourceKey TextFillColorDarkPrimaryBrush        { get; private set; }
    public static ComponentResourceKey TextFillColorDarkSecondaryBrush      { get; private set; }
    public static ComponentResourceKey TextFillColorDarkTertiaryBrush       { get; private set; }
    public static ComponentResourceKey TextFillColorDarkDisabledBrush       { get; private set; }
    public static ComponentResourceKey TextFillColorDarkInverseBrush        { get; private set; }
    public static ComponentResourceKey ApplicationBackgroundColorLightBrush { get; private set; }
    public static ComponentResourceKey ApplicationBackgroundColorDarkBrush  { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorLightBrush     { get; private set; }
    public static ComponentResourceKey ControlStrongFillColorDarkBrush      { get; private set; }

    static BrushKeys()
    {
        // 使用反射统一设置每个属性值
        var type       = typeof(BrushKeys);
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
