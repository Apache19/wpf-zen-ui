// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for color resources published by WPF Zen UI.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicColor</c> or <c>StaticColor</c>. The key properties remain
/// available for resource declarations and programmatic lookup.
/// </remarks>
public static class ColorKeys
{
    // Light.xaml / Dark.xaml / HighContrast.xaml
    public static ComponentResourceKey AccentFillPrimary                   { get; } = Create(nameof(AccentFillPrimary));
    public static ComponentResourceKey AccentFillSecondary                 { get; } = Create(nameof(AccentFillSecondary));
    public static ComponentResourceKey AccentFillTertiary                  { get; } = Create(nameof(AccentFillTertiary));
    public static ComponentResourceKey AccentTextPrimary                   { get; } = Create(nameof(AccentTextPrimary));
    public static ComponentResourceKey AccentTextSecondary                 { get; } = Create(nameof(AccentTextSecondary));
    public static ComponentResourceKey TextOnAccentPrimary                 { get; } = Create(nameof(TextOnAccentPrimary));
    public static ComponentResourceKey AccentStroke                        { get; } = Create(nameof(AccentStroke));
    public static ComponentResourceKey AccentSelection                     { get; } = Create(nameof(AccentSelection));
    public static ComponentResourceKey SystemColorWindowTextColor          { get; } = Create(nameof(SystemColorWindowTextColor));
    public static ComponentResourceKey SystemColorWindowColor              { get; } = Create(nameof(SystemColorWindowColor));
    public static ComponentResourceKey SystemColorHighlightTextColor       { get; } = Create(nameof(SystemColorHighlightTextColor));
    public static ComponentResourceKey SystemColorHighlightColor           { get; } = Create(nameof(SystemColorHighlightColor));
    public static ComponentResourceKey SystemColorControlTextColor         { get; } = Create(nameof(SystemColorControlTextColor));
    public static ComponentResourceKey SystemColorControlColor             { get; } = Create(nameof(SystemColorControlColor));
    public static ComponentResourceKey SystemColorHotTrackColor            { get; } = Create(nameof(SystemColorHotTrackColor));
    public static ComponentResourceKey SystemColorGrayTextColor            { get; } = Create(nameof(SystemColorGrayTextColor));
    public static ComponentResourceKey SystemColorWindowFrameColor         { get; } = Create(nameof(SystemColorWindowFrameColor));
    public static ComponentResourceKey WindowBackgroundColor               { get; } = Create(nameof(WindowBackgroundColor));
    public static ComponentResourceKey WindowBorderColor                   { get; } = Create(nameof(WindowBorderColor));
    public static ComponentResourceKey WindowBorderColorInactive           { get; } = Create(nameof(WindowBorderColorInactive));
    public static ComponentResourceKey KeyboardFocusBorderColor            { get; } = Create(nameof(KeyboardFocusBorderColor));
    public static ComponentResourceKey TextFillColorPrimary                { get; } = Create(nameof(TextFillColorPrimary));
    public static ComponentResourceKey TextFillColorSecondary              { get; } = Create(nameof(TextFillColorSecondary));
    public static ComponentResourceKey TextFillColorTertiary               { get; } = Create(nameof(TextFillColorTertiary));
    public static ComponentResourceKey TextFillColorDisabled               { get; } = Create(nameof(TextFillColorDisabled));
    public static ComponentResourceKey TextPlaceholderColor                { get; } = Create(nameof(TextPlaceholderColor));
    public static ComponentResourceKey TextFillColorInverse                { get; } = Create(nameof(TextFillColorInverse));
    public static ComponentResourceKey ControlFillColorPrimary             { get; } = Create(nameof(ControlFillColorPrimary));
    public static ComponentResourceKey ControlFillColorSecondary           { get; } = Create(nameof(ControlFillColorSecondary));
    public static ComponentResourceKey ControlFillColorTertiary            { get; } = Create(nameof(ControlFillColorTertiary));
    public static ComponentResourceKey ControlFillColorDisabled            { get; } = Create(nameof(ControlFillColorDisabled));
    public static ComponentResourceKey ControlFillColorInputActive         { get; } = Create(nameof(ControlFillColorInputActive));
    public static ComponentResourceKey ControlStrongFillColorDefault       { get; } = Create(nameof(ControlStrongFillColorDefault));
    public static ComponentResourceKey ControlStrongFillColorDisabled      { get; } = Create(nameof(ControlStrongFillColorDisabled));
    public static ComponentResourceKey ControlSolidFillColorDefault        { get; } = Create(nameof(ControlSolidFillColorDefault));
    public static ComponentResourceKey SubtleFillColorTransparent          { get; } = Create(nameof(SubtleFillColorTransparent));
    public static ComponentResourceKey SubtleFillColorSecondary            { get; } = Create(nameof(SubtleFillColorSecondary));
    public static ComponentResourceKey SubtleFillColorTertiary             { get; } = Create(nameof(SubtleFillColorTertiary));
    public static ComponentResourceKey SubtleFillColorDisabled             { get; } = Create(nameof(SubtleFillColorDisabled));
    public static ComponentResourceKey ControlAltFillColorTransparent      { get; } = Create(nameof(ControlAltFillColorTransparent));
    public static ComponentResourceKey ControlAltFillColorSecondary        { get; } = Create(nameof(ControlAltFillColorSecondary));
    public static ComponentResourceKey ControlAltFillColorTertiary         { get; } = Create(nameof(ControlAltFillColorTertiary));
    public static ComponentResourceKey ControlAltFillColorQuarternary      { get; } = Create(nameof(ControlAltFillColorQuarternary));
    public static ComponentResourceKey ControlAltFillColorDisabled         { get; } = Create(nameof(ControlAltFillColorDisabled));
    public static ComponentResourceKey ControlOnImageFillColorDefault      { get; } = Create(nameof(ControlOnImageFillColorDefault));
    public static ComponentResourceKey ControlOnImageFillColorSecondary    { get; } = Create(nameof(ControlOnImageFillColorSecondary));
    public static ComponentResourceKey ControlOnImageFillColorTertiary     { get; } = Create(nameof(ControlOnImageFillColorTertiary));
    public static ComponentResourceKey ControlOnImageFillColorDisabled     { get; } = Create(nameof(ControlOnImageFillColorDisabled));
    public static ComponentResourceKey ControlStrokeColorDefault           { get; } = Create(nameof(ControlStrokeColorDefault));
    public static ComponentResourceKey ControlStrokeColorSecondary         { get; } = Create(nameof(ControlStrokeColorSecondary));
    public static ComponentResourceKey ControlStrokeColorTertiary          { get; } = Create(nameof(ControlStrokeColorTertiary));
    public static ComponentResourceKey ControlStrokeColorOnAccentDefault   { get; } = Create(nameof(ControlStrokeColorOnAccentDefault));
    public static ComponentResourceKey ControlStrokeColorOnAccentSecondary { get; } = Create(nameof(ControlStrokeColorOnAccentSecondary));
    public static ComponentResourceKey ControlStrokeColorOnAccentTertiary  { get; } = Create(nameof(ControlStrokeColorOnAccentTertiary));
    public static ComponentResourceKey ControlStrokeColorOnAccentDisabled  { get; } = Create(nameof(ControlStrokeColorOnAccentDisabled));

    public static ComponentResourceKey ControlStrokeColorForStrongFillWhenOnImage { get; } =
        Create(nameof(ControlStrokeColorForStrongFillWhenOnImage));

    public static ComponentResourceKey CardStrokeColorDefault                  { get; } = Create(nameof(CardStrokeColorDefault));
    public static ComponentResourceKey CardStrokeColorDefaultSolid             { get; } = Create(nameof(CardStrokeColorDefaultSolid));
    public static ComponentResourceKey ControlStrongStrokeColorDefault         { get; } = Create(nameof(ControlStrongStrokeColorDefault));
    public static ComponentResourceKey ControlStrongStrokeColorDisabled        { get; } = Create(nameof(ControlStrongStrokeColorDisabled));
    public static ComponentResourceKey SurfaceStrokeColorDefault               { get; } = Create(nameof(SurfaceStrokeColorDefault));
    public static ComponentResourceKey SurfaceStrokeColorFlyout                { get; } = Create(nameof(SurfaceStrokeColorFlyout));
    public static ComponentResourceKey SurfaceStrokeColorInverse               { get; } = Create(nameof(SurfaceStrokeColorInverse));
    public static ComponentResourceKey DividerStrokeColorDefault               { get; } = Create(nameof(DividerStrokeColorDefault));
    public static ComponentResourceKey FocusStrokeColorOuter                   { get; } = Create(nameof(FocusStrokeColorOuter));
    public static ComponentResourceKey FocusStrokeColorInner                   { get; } = Create(nameof(FocusStrokeColorInner));
    public static ComponentResourceKey CardBackgroundFillColorDefault          { get; } = Create(nameof(CardBackgroundFillColorDefault));
    public static ComponentResourceKey CardBackgroundFillColorSecondary        { get; } = Create(nameof(CardBackgroundFillColorSecondary));
    public static ComponentResourceKey EmbeddedFillColorDefault                { get; } = Create(nameof(EmbeddedFillColorDefault));
    public static ComponentResourceKey EmbeddedFillColorStrong                 { get; } = Create(nameof(EmbeddedFillColorStrong));
    public static ComponentResourceKey EmbeddedFillColorDeep                   { get; } = Create(nameof(EmbeddedFillColorDeep));
    public static ComponentResourceKey SmokeFillColorDefault                   { get; } = Create(nameof(SmokeFillColorDefault));
    public static ComponentResourceKey LayerFillColorDefault                   { get; } = Create(nameof(LayerFillColorDefault));
    public static ComponentResourceKey LayerFillColorAlt                       { get; } = Create(nameof(LayerFillColorAlt));
    public static ComponentResourceKey LayerOnAcrylicFillColorDefault          { get; } = Create(nameof(LayerOnAcrylicFillColorDefault));
    public static ComponentResourceKey LayerOnAccentAcrylicFillColorDefault    { get; } = Create(nameof(LayerOnAccentAcrylicFillColorDefault));
    public static ComponentResourceKey SnowflakeGradientTop                    { get; } = Create(nameof(SnowflakeGradientTop));
    public static ComponentResourceKey SnowflakeGradientBottom                 { get; } = Create(nameof(SnowflakeGradientBottom));
    public static ComponentResourceKey AcrylicBackgroundFillColorDefault       { get; } = Create(nameof(AcrylicBackgroundFillColorDefault));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorDefault      { get; } = Create(nameof(LayerOnMicaBaseAltFillColorDefault));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorSecondary    { get; } = Create(nameof(LayerOnMicaBaseAltFillColorSecondary));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTertiary     { get; } = Create(nameof(LayerOnMicaBaseAltFillColorTertiary));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTransparent  { get; } = Create(nameof(LayerOnMicaBaseAltFillColorTransparent));
    public static ComponentResourceKey SolidBackgroundFillColorBase            { get; } = Create(nameof(SolidBackgroundFillColorBase));
    public static ComponentResourceKey SolidBackgroundFillColorSecondary       { get; } = Create(nameof(SolidBackgroundFillColorSecondary));
    public static ComponentResourceKey SolidBackgroundFillColorTertiary        { get; } = Create(nameof(SolidBackgroundFillColorTertiary));
    public static ComponentResourceKey SolidBackgroundFillColorQuarternary     { get; } = Create(nameof(SolidBackgroundFillColorQuarternary));
    public static ComponentResourceKey SolidBackgroundFillColorTransparent     { get; } = Create(nameof(SolidBackgroundFillColorTransparent));
    public static ComponentResourceKey SolidBackgroundFillColorBaseAlt         { get; } = Create(nameof(SolidBackgroundFillColorBaseAlt));
    public static ComponentResourceKey SystemFillColorAttention                { get; } = Create(nameof(SystemFillColorAttention));
    public static ComponentResourceKey SystemFillColorInformational            { get; } = Create(nameof(SystemFillColorInformational));
    public static ComponentResourceKey SystemFillColorSuccess                  { get; } = Create(nameof(SystemFillColorSuccess));
    public static ComponentResourceKey SystemFillColorCaution                  { get; } = Create(nameof(SystemFillColorCaution));
    public static ComponentResourceKey SystemFillColorCritical                 { get; } = Create(nameof(SystemFillColorCritical));
    public static ComponentResourceKey SystemFillColorNeutral                  { get; } = Create(nameof(SystemFillColorNeutral));
    public static ComponentResourceKey SystemFillColorSolidNeutral             { get; } = Create(nameof(SystemFillColorSolidNeutral));
    public static ComponentResourceKey SystemFillColorAttentionBackground      { get; } = Create(nameof(SystemFillColorAttentionBackground));
    public static ComponentResourceKey SystemFillColorSuccessBackground        { get; } = Create(nameof(SystemFillColorSuccessBackground));
    public static ComponentResourceKey SystemFillColorCautionBackground        { get; } = Create(nameof(SystemFillColorCautionBackground));
    public static ComponentResourceKey SystemFillColorCriticalBackground       { get; } = Create(nameof(SystemFillColorCriticalBackground));
    public static ComponentResourceKey SystemFillColorNeutralBackground        { get; } = Create(nameof(SystemFillColorNeutralBackground));
    public static ComponentResourceKey SystemFillColorSolidAttentionBackground { get; } = Create(nameof(SystemFillColorSolidAttentionBackground));
    public static ComponentResourceKey SystemFillColorSolidNeutralBackground   { get; } = Create(nameof(SystemFillColorSolidNeutralBackground));

    // Palette.xaml
    public static ComponentResourceKey PalettePrimaryColor          { get; } = Create(nameof(PalettePrimaryColor));
    public static ComponentResourceKey PaletteRedColor              { get; } = Create(nameof(PaletteRedColor));
    public static ComponentResourceKey PaletteRedColorBright        { get; } = Create(nameof(PaletteRedColorBright));
    public static ComponentResourceKey PaletteRedColorDim           { get; } = Create(nameof(PaletteRedColorDim));
    public static ComponentResourceKey PalettePinkColor             { get; } = Create(nameof(PalettePinkColor));
    public static ComponentResourceKey PalettePinkColorBright       { get; } = Create(nameof(PalettePinkColorBright));
    public static ComponentResourceKey PalettePinkColorDim          { get; } = Create(nameof(PalettePinkColorDim));
    public static ComponentResourceKey PalettePurpleColor           { get; } = Create(nameof(PalettePurpleColor));
    public static ComponentResourceKey PaletteDeepPurpleColor       { get; } = Create(nameof(PaletteDeepPurpleColor));
    public static ComponentResourceKey PaletteIndigoColor           { get; } = Create(nameof(PaletteIndigoColor));
    public static ComponentResourceKey PaletteBlueColor             { get; } = Create(nameof(PaletteBlueColor));
    public static ComponentResourceKey PaletteBlueColorBright       { get; } = Create(nameof(PaletteBlueColorBright));
    public static ComponentResourceKey PaletteBlueColorDim          { get; } = Create(nameof(PaletteBlueColorDim));
    public static ComponentResourceKey PaletteLightBlueColor        { get; } = Create(nameof(PaletteLightBlueColor));
    public static ComponentResourceKey PaletteLightBlueColorBright  { get; } = Create(nameof(PaletteLightBlueColorBright));
    public static ComponentResourceKey PaletteLightBlueColorDim     { get; } = Create(nameof(PaletteLightBlueColorDim));
    public static ComponentResourceKey PaletteCyanColor             { get; } = Create(nameof(PaletteCyanColor));
    public static ComponentResourceKey PaletteCyanColorBright       { get; } = Create(nameof(PaletteCyanColorBright));
    public static ComponentResourceKey PaletteCyanColorDim          { get; } = Create(nameof(PaletteCyanColorDim));
    public static ComponentResourceKey PaletteTealColor             { get; } = Create(nameof(PaletteTealColor));
    public static ComponentResourceKey PaletteGreenColor            { get; } = Create(nameof(PaletteGreenColor));
    public static ComponentResourceKey PaletteGreenColorBright      { get; } = Create(nameof(PaletteGreenColorBright));
    public static ComponentResourceKey PaletteGreenColorDim         { get; } = Create(nameof(PaletteGreenColorDim));
    public static ComponentResourceKey PaletteLightGreenColor       { get; } = Create(nameof(PaletteLightGreenColor));
    public static ComponentResourceKey PaletteLimeColor             { get; } = Create(nameof(PaletteLimeColor));
    public static ComponentResourceKey PaletteYellowColor           { get; } = Create(nameof(PaletteYellowColor));
    public static ComponentResourceKey PaletteYellowColorBright     { get; } = Create(nameof(PaletteYellowColorBright));
    public static ComponentResourceKey PaletteYellowColorDim        { get; } = Create(nameof(PaletteYellowColorDim));
    public static ComponentResourceKey PaletteAmberColor            { get; } = Create(nameof(PaletteAmberColor));
    public static ComponentResourceKey PaletteOrangeColor           { get; } = Create(nameof(PaletteOrangeColor));
    public static ComponentResourceKey PaletteOrangeColorBright     { get; } = Create(nameof(PaletteOrangeColorBright));
    public static ComponentResourceKey PaletteOrangeColorDim        { get; } = Create(nameof(PaletteOrangeColorDim));
    public static ComponentResourceKey PaletteDeepOrangeColor       { get; } = Create(nameof(PaletteDeepOrangeColor));
    public static ComponentResourceKey PaletteDeepOrangeColorBright { get; } = Create(nameof(PaletteDeepOrangeColorBright));
    public static ComponentResourceKey PaletteDeepOrangeColorDim    { get; } = Create(nameof(PaletteDeepOrangeColorDim));
    public static ComponentResourceKey PaletteBrownColor            { get; } = Create(nameof(PaletteBrownColor));
    public static ComponentResourceKey PaletteGreyColor             { get; } = Create(nameof(PaletteGreyColor));
    public static ComponentResourceKey PaletteBlueGreyColor         { get; } = Create(nameof(PaletteBlueGreyColor));

    // StaticColors.xaml
    public static ComponentResourceKey TextFillColorLightPrimary       { get; } = Create(nameof(TextFillColorLightPrimary));
    public static ComponentResourceKey TextFillColorLightSecondary     { get; } = Create(nameof(TextFillColorLightSecondary));
    public static ComponentResourceKey TextFillColorLightTertiary      { get; } = Create(nameof(TextFillColorLightTertiary));
    public static ComponentResourceKey TextFillColorLightDisabled      { get; } = Create(nameof(TextFillColorLightDisabled));
    public static ComponentResourceKey TextFillColorLightInverse       { get; } = Create(nameof(TextFillColorLightInverse));
    public static ComponentResourceKey TextFillColorDarkPrimary        { get; } = Create(nameof(TextFillColorDarkPrimary));
    public static ComponentResourceKey TextFillColorDarkSecondary      { get; } = Create(nameof(TextFillColorDarkSecondary));
    public static ComponentResourceKey TextFillColorDarkTertiary       { get; } = Create(nameof(TextFillColorDarkTertiary));
    public static ComponentResourceKey TextFillColorDarkDisabled       { get; } = Create(nameof(TextFillColorDarkDisabled));
    public static ComponentResourceKey TextFillColorDarkInverse        { get; } = Create(nameof(TextFillColorDarkInverse));
    public static ComponentResourceKey ApplicationBackgroundColorLight { get; } = Create(nameof(ApplicationBackgroundColorLight));
    public static ComponentResourceKey ApplicationBackgroundColorDark  { get; } = Create(nameof(ApplicationBackgroundColorDark));
    public static ComponentResourceKey ControlStrongFillColorLight     { get; } = Create(nameof(ControlStrongFillColorLight));
    public static ComponentResourceKey ControlStrongFillColorDark      { get; } = Create(nameof(ControlStrongFillColorDark));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
