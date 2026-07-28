// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for brush resources published by WPF UI.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicBrush</c> or <c>StaticBrush</c>. The key properties remain
/// available for resource declarations and programmatic lookup.
/// </remarks>
public static class BrushKeys
{
    // Light.xaml / Dark.xaml / HighContrast.xaml
    public static ComponentResourceKey AccentFillPrimaryBrush                   { get; } = Create(nameof(AccentFillPrimaryBrush));
    public static ComponentResourceKey AccentFillSecondaryBrush                 { get; } = Create(nameof(AccentFillSecondaryBrush));
    public static ComponentResourceKey AccentFillTertiaryBrush                  { get; } = Create(nameof(AccentFillTertiaryBrush));
    public static ComponentResourceKey AccentTextPrimaryBrush                   { get; } = Create(nameof(AccentTextPrimaryBrush));
    public static ComponentResourceKey AccentTextSecondaryBrush                 { get; } = Create(nameof(AccentTextSecondaryBrush));
    public static ComponentResourceKey TextOnAccentPrimaryBrush                 { get; } = Create(nameof(TextOnAccentPrimaryBrush));
    public static ComponentResourceKey AccentStrokeBrush                        { get; } = Create(nameof(AccentStrokeBrush));
    public static ComponentResourceKey AccentSelectionBrush                     { get; } = Create(nameof(AccentSelectionBrush));
    public static ComponentResourceKey SystemColorWindowTextBrush               { get; } = Create(nameof(SystemColorWindowTextBrush));
    public static ComponentResourceKey SystemColorWindowBrush                   { get; } = Create(nameof(SystemColorWindowBrush));
    public static ComponentResourceKey SystemColorHighlightTextBrush            { get; } = Create(nameof(SystemColorHighlightTextBrush));
    public static ComponentResourceKey SystemColorHighlightBrush                { get; } = Create(nameof(SystemColorHighlightBrush));
    public static ComponentResourceKey SystemColorControlTextBrush              { get; } = Create(nameof(SystemColorControlTextBrush));
    public static ComponentResourceKey SystemColorControlBrush                  { get; } = Create(nameof(SystemColorControlBrush));
    public static ComponentResourceKey SystemColorHotTrackBrush                 { get; } = Create(nameof(SystemColorHotTrackBrush));
    public static ComponentResourceKey SystemColorGrayTextBrush                 { get; } = Create(nameof(SystemColorGrayTextBrush));
    public static ComponentResourceKey SystemColorWindowFrameBrush              { get; } = Create(nameof(SystemColorWindowFrameBrush));
    public static ComponentResourceKey SystemFillColorAttentionBrush            { get; } = Create(nameof(SystemFillColorAttentionBrush));
    public static ComponentResourceKey KeyboardFocusBorderColorBrush            { get; } = Create(nameof(KeyboardFocusBorderColorBrush));
    public static ComponentResourceKey TextFillColorPrimaryBrush                { get; } = Create(nameof(TextFillColorPrimaryBrush));
    public static ComponentResourceKey TextFillColorSecondaryBrush              { get; } = Create(nameof(TextFillColorSecondaryBrush));
    public static ComponentResourceKey TextFillColorTertiaryBrush               { get; } = Create(nameof(TextFillColorTertiaryBrush));
    public static ComponentResourceKey TextFillColorDisabledBrush               { get; } = Create(nameof(TextFillColorDisabledBrush));
    public static ComponentResourceKey TextPlaceholderColorBrush                { get; } = Create(nameof(TextPlaceholderColorBrush));
    public static ComponentResourceKey TextFillColorInverseBrush                { get; } = Create(nameof(TextFillColorInverseBrush));
    public static ComponentResourceKey ControlFillColorPrimaryBrush             { get; } = Create(nameof(ControlFillColorPrimaryBrush));
    public static ComponentResourceKey ControlFillColorSecondaryBrush           { get; } = Create(nameof(ControlFillColorSecondaryBrush));
    public static ComponentResourceKey ControlFillColorTertiaryBrush            { get; } = Create(nameof(ControlFillColorTertiaryBrush));
    public static ComponentResourceKey ControlFillColorDisabledBrush            { get; } = Create(nameof(ControlFillColorDisabledBrush));
    public static ComponentResourceKey ControlFillColorInputActiveBrush         { get; } = Create(nameof(ControlFillColorInputActiveBrush));
    public static ComponentResourceKey ControlStrongFillColorDefaultBrush       { get; } = Create(nameof(ControlStrongFillColorDefaultBrush));
    public static ComponentResourceKey ControlStrongFillColorDisabledBrush      { get; } = Create(nameof(ControlStrongFillColorDisabledBrush));
    public static ComponentResourceKey ControlSolidFillColorDefaultBrush        { get; } = Create(nameof(ControlSolidFillColorDefaultBrush));
    public static ComponentResourceKey SubtleFillColorTransparentBrush          { get; } = Create(nameof(SubtleFillColorTransparentBrush));
    public static ComponentResourceKey SubtleFillColorSecondaryBrush            { get; } = Create(nameof(SubtleFillColorSecondaryBrush));
    public static ComponentResourceKey SubtleFillColorTertiaryBrush             { get; } = Create(nameof(SubtleFillColorTertiaryBrush));
    public static ComponentResourceKey SubtleFillColorDisabledBrush             { get; } = Create(nameof(SubtleFillColorDisabledBrush));
    public static ComponentResourceKey ControlAltFillColorTransparentBrush      { get; } = Create(nameof(ControlAltFillColorTransparentBrush));
    public static ComponentResourceKey ControlAltFillColorSecondaryBrush        { get; } = Create(nameof(ControlAltFillColorSecondaryBrush));
    public static ComponentResourceKey ControlAltFillColorTertiaryBrush         { get; } = Create(nameof(ControlAltFillColorTertiaryBrush));
    public static ComponentResourceKey ControlAltFillColorQuarternaryBrush      { get; } = Create(nameof(ControlAltFillColorQuarternaryBrush));
    public static ComponentResourceKey ControlAltFillColorDisabledBrush         { get; } = Create(nameof(ControlAltFillColorDisabledBrush));
    public static ComponentResourceKey ControlOnImageFillColorDefaultBrush      { get; } = Create(nameof(ControlOnImageFillColorDefaultBrush));
    public static ComponentResourceKey ControlOnImageFillColorSecondaryBrush    { get; } = Create(nameof(ControlOnImageFillColorSecondaryBrush));
    public static ComponentResourceKey ControlOnImageFillColorTertiaryBrush     { get; } = Create(nameof(ControlOnImageFillColorTertiaryBrush));
    public static ComponentResourceKey ControlOnImageFillColorDisabledBrush     { get; } = Create(nameof(ControlOnImageFillColorDisabledBrush));
    public static ComponentResourceKey ControlStrokeColorDefaultBrush           { get; } = Create(nameof(ControlStrokeColorDefaultBrush));
    public static ComponentResourceKey ControlStrokeColorSecondaryBrush         { get; } = Create(nameof(ControlStrokeColorSecondaryBrush));
    public static ComponentResourceKey ControlStrokeColorTertiaryBrush          { get; } = Create(nameof(ControlStrokeColorTertiaryBrush));
    public static ComponentResourceKey ControlStrokeColorOnAccentDefaultBrush   { get; } = Create(nameof(ControlStrokeColorOnAccentDefaultBrush));
    public static ComponentResourceKey ControlStrokeColorOnAccentSecondaryBrush { get; } = Create(nameof(ControlStrokeColorOnAccentSecondaryBrush));
    public static ComponentResourceKey ControlStrokeColorOnAccentTertiaryBrush  { get; } = Create(nameof(ControlStrokeColorOnAccentTertiaryBrush));
    public static ComponentResourceKey ControlStrokeColorOnAccentDisabledBrush  { get; } = Create(nameof(ControlStrokeColorOnAccentDisabledBrush));

    public static ComponentResourceKey ControlStrokeColorForStrongFillWhenOnImageBrush { get; } =
        Create(nameof(ControlStrokeColorForStrongFillWhenOnImageBrush));

    public static ComponentResourceKey CardStrokeColorDefaultBrush               { get; } = Create(nameof(CardStrokeColorDefaultBrush));
    public static ComponentResourceKey CardStrokeColorDefaultSolidBrush          { get; } = Create(nameof(CardStrokeColorDefaultSolidBrush));
    public static ComponentResourceKey ControlStrongStrokeColorDefaultBrush      { get; } = Create(nameof(ControlStrongStrokeColorDefaultBrush));
    public static ComponentResourceKey ControlStrongStrokeColorDisabledBrush     { get; } = Create(nameof(ControlStrongStrokeColorDisabledBrush));
    public static ComponentResourceKey SurfaceStrokeColorDefaultBrush            { get; } = Create(nameof(SurfaceStrokeColorDefaultBrush));
    public static ComponentResourceKey SurfaceStrokeColorFlyoutBrush             { get; } = Create(nameof(SurfaceStrokeColorFlyoutBrush));
    public static ComponentResourceKey SurfaceStrokeColorInverseBrush            { get; } = Create(nameof(SurfaceStrokeColorInverseBrush));
    public static ComponentResourceKey DividerStrokeColorDefaultBrush            { get; } = Create(nameof(DividerStrokeColorDefaultBrush));
    public static ComponentResourceKey FocusStrokeColorOuterBrush                { get; } = Create(nameof(FocusStrokeColorOuterBrush));
    public static ComponentResourceKey FocusStrokeColorInnerBrush                { get; } = Create(nameof(FocusStrokeColorInnerBrush));
    public static ComponentResourceKey CardBackgroundFillColorDefaultBrush       { get; } = Create(nameof(CardBackgroundFillColorDefaultBrush));
    public static ComponentResourceKey CardBackgroundFillColorSecondaryBrush     { get; } = Create(nameof(CardBackgroundFillColorSecondaryBrush));
    public static ComponentResourceKey EmbeddedFillColorDefaultBrush             { get; } = Create(nameof(EmbeddedFillColorDefaultBrush));
    public static ComponentResourceKey EmbeddedFillColorStrongBrush              { get; } = Create(nameof(EmbeddedFillColorStrongBrush));
    public static ComponentResourceKey EmbeddedFillColorDeepBrush                { get; } = Create(nameof(EmbeddedFillColorDeepBrush));
    public static ComponentResourceKey SmokeFillColorDefaultBrush                { get; } = Create(nameof(SmokeFillColorDefaultBrush));
    public static ComponentResourceKey LayerFillColorDefaultBrush                { get; } = Create(nameof(LayerFillColorDefaultBrush));
    public static ComponentResourceKey LayerFillColorAltBrush                    { get; } = Create(nameof(LayerFillColorAltBrush));
    public static ComponentResourceKey LayerOnAcrylicFillColorDefaultBrush       { get; } = Create(nameof(LayerOnAcrylicFillColorDefaultBrush));
    public static ComponentResourceKey LayerOnAccentAcrylicFillColorDefaultBrush { get; } = Create(nameof(LayerOnAccentAcrylicFillColorDefaultBrush));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorDefaultBrush   { get; } = Create(nameof(LayerOnMicaBaseAltFillColorDefaultBrush));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorSecondaryBrush { get; } = Create(nameof(LayerOnMicaBaseAltFillColorSecondaryBrush));
    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTertiaryBrush  { get; } = Create(nameof(LayerOnMicaBaseAltFillColorTertiaryBrush));

    public static ComponentResourceKey LayerOnMicaBaseAltFillColorTransparentBrush { get; } =
        Create(nameof(LayerOnMicaBaseAltFillColorTransparentBrush));

    public static ComponentResourceKey SolidBackgroundFillColorBaseBrush        { get; } = Create(nameof(SolidBackgroundFillColorBaseBrush));
    public static ComponentResourceKey SolidBackgroundFillColorSecondaryBrush   { get; } = Create(nameof(SolidBackgroundFillColorSecondaryBrush));
    public static ComponentResourceKey SolidBackgroundFillColorTertiaryBrush    { get; } = Create(nameof(SolidBackgroundFillColorTertiaryBrush));
    public static ComponentResourceKey SolidBackgroundFillColorQuarternaryBrush { get; } = Create(nameof(SolidBackgroundFillColorQuarternaryBrush));
    public static ComponentResourceKey SolidBackgroundFillColorBaseAltBrush     { get; } = Create(nameof(SolidBackgroundFillColorBaseAltBrush));
    public static ComponentResourceKey SystemFillColorSuccessBrush              { get; } = Create(nameof(SystemFillColorSuccessBrush));
    public static ComponentResourceKey SystemFillColorCautionBrush              { get; } = Create(nameof(SystemFillColorCautionBrush));
    public static ComponentResourceKey SystemFillColorCriticalBrush             { get; } = Create(nameof(SystemFillColorCriticalBrush));
    public static ComponentResourceKey SystemFillColorNeutralBrush              { get; } = Create(nameof(SystemFillColorNeutralBrush));
    public static ComponentResourceKey SystemFillColorSolidNeutralBrush         { get; } = Create(nameof(SystemFillColorSolidNeutralBrush));
    public static ComponentResourceKey SystemFillColorAttentionBackgroundBrush  { get; } = Create(nameof(SystemFillColorAttentionBackgroundBrush));
    public static ComponentResourceKey SystemFillColorSuccessBackgroundBrush    { get; } = Create(nameof(SystemFillColorSuccessBackgroundBrush));
    public static ComponentResourceKey SystemFillColorCautionBackgroundBrush    { get; } = Create(nameof(SystemFillColorCautionBackgroundBrush));
    public static ComponentResourceKey SystemFillColorCriticalBackgroundBrush   { get; } = Create(nameof(SystemFillColorCriticalBackgroundBrush));
    public static ComponentResourceKey SystemFillColorNeutralBackgroundBrush    { get; } = Create(nameof(SystemFillColorNeutralBackgroundBrush));

    public static ComponentResourceKey SystemFillColorSolidAttentionBackgroundBrush { get; } =
        Create(nameof(SystemFillColorSolidAttentionBackgroundBrush));

    public static ComponentResourceKey SystemFillColorSolidNeutralBackgroundBrush { get; } =
        Create(nameof(SystemFillColorSolidNeutralBackgroundBrush));

    public static ComponentResourceKey ControlElevationBorderBrush            { get; } = Create(nameof(ControlElevationBorderBrush));
    public static ComponentResourceKey CircleElevationBorderBrush             { get; } = Create(nameof(CircleElevationBorderBrush));
    public static ComponentResourceKey AccentControlElevationBorderBrush      { get; } = Create(nameof(AccentControlElevationBorderBrush));
    public static ComponentResourceKey StripedBackgroundBrush                 { get; } = Create(nameof(StripedBackgroundBrush));
    public static ComponentResourceKey AcrylicBackgroundFillColorDefaultBrush { get; } = Create(nameof(AcrylicBackgroundFillColorDefaultBrush));
    public static ComponentResourceKey WindowBackgroundBrush                  { get; } = Create(nameof(WindowBackgroundBrush));
    public static ComponentResourceKey WindowForegroundBrush                  { get; } = Create(nameof(WindowForegroundBrush));
    public static ComponentResourceKey WindowBorderBrush                      { get; } = Create(nameof(WindowBorderBrush));
    public static ComponentResourceKey WindowBorderInactiveBrush              { get; } = Create(nameof(WindowBorderInactiveBrush));
    public static ComponentResourceKey SystemFillColorInformationalBrush      { get; } = Create(nameof(SystemFillColorInformationalBrush));
    public static ComponentResourceKey LeftNavigationViewSeparatorBrush       { get; } = Create(nameof(LeftNavigationViewSeparatorBrush));

    // Palette.xaml:
    public static ComponentResourceKey PalettePrimaryBrush          { get; } = Create(nameof(PalettePrimaryBrush));
    public static ComponentResourceKey PaletteRedBrush              { get; } = Create(nameof(PaletteRedBrush));
    public static ComponentResourceKey PaletteRedBrushBright        { get; } = Create(nameof(PaletteRedBrushBright));
    public static ComponentResourceKey PaletteRedBrushDim           { get; } = Create(nameof(PaletteRedBrushDim));
    public static ComponentResourceKey PalettePinkBrush             { get; } = Create(nameof(PalettePinkBrush));
    public static ComponentResourceKey PalettePinkBrushBright       { get; } = Create(nameof(PalettePinkBrushBright));
    public static ComponentResourceKey PalettePinkBrushDim          { get; } = Create(nameof(PalettePinkBrushDim));
    public static ComponentResourceKey PalettePurpleBrush           { get; } = Create(nameof(PalettePurpleBrush));
    public static ComponentResourceKey PaletteDeepPurpleBrush       { get; } = Create(nameof(PaletteDeepPurpleBrush));
    public static ComponentResourceKey PaletteIndigoBrush           { get; } = Create(nameof(PaletteIndigoBrush));
    public static ComponentResourceKey PaletteBlueBrush             { get; } = Create(nameof(PaletteBlueBrush));
    public static ComponentResourceKey PaletteBlueBrushBright       { get; } = Create(nameof(PaletteBlueBrushBright));
    public static ComponentResourceKey PaletteBlueBrushDim          { get; } = Create(nameof(PaletteBlueBrushDim));
    public static ComponentResourceKey PaletteLightBlueBrush        { get; } = Create(nameof(PaletteLightBlueBrush));
    public static ComponentResourceKey PaletteLightBlueBrushBright  { get; } = Create(nameof(PaletteLightBlueBrushBright));
    public static ComponentResourceKey PaletteLightBlueBrushDim     { get; } = Create(nameof(PaletteLightBlueBrushDim));
    public static ComponentResourceKey PaletteCyanBrush             { get; } = Create(nameof(PaletteCyanBrush));
    public static ComponentResourceKey PaletteCyanBrushBright       { get; } = Create(nameof(PaletteCyanBrushBright));
    public static ComponentResourceKey PaletteCyanBrushDim          { get; } = Create(nameof(PaletteCyanBrushDim));
    public static ComponentResourceKey PaletteTealBrush             { get; } = Create(nameof(PaletteTealBrush));
    public static ComponentResourceKey PaletteGreenBrush            { get; } = Create(nameof(PaletteGreenBrush));
    public static ComponentResourceKey PaletteGreenBrushBright      { get; } = Create(nameof(PaletteGreenBrushBright));
    public static ComponentResourceKey PaletteGreenBrushDim         { get; } = Create(nameof(PaletteGreenBrushDim));
    public static ComponentResourceKey PaletteLightGreenBrush       { get; } = Create(nameof(PaletteLightGreenBrush));
    public static ComponentResourceKey PaletteLimeBrush             { get; } = Create(nameof(PaletteLimeBrush));
    public static ComponentResourceKey PaletteYellowBrush           { get; } = Create(nameof(PaletteYellowBrush));
    public static ComponentResourceKey PaletteYellowBrushBright     { get; } = Create(nameof(PaletteYellowBrushBright));
    public static ComponentResourceKey PaletteYellowBrushDim        { get; } = Create(nameof(PaletteYellowBrushDim));
    public static ComponentResourceKey PaletteAmberBrush            { get; } = Create(nameof(PaletteAmberBrush));
    public static ComponentResourceKey PaletteOrangeBrush           { get; } = Create(nameof(PaletteOrangeBrush));
    public static ComponentResourceKey PaletteOrangeBrushBright     { get; } = Create(nameof(PaletteOrangeBrushBright));
    public static ComponentResourceKey PaletteOrangeBrushDim        { get; } = Create(nameof(PaletteOrangeBrushDim));
    public static ComponentResourceKey PaletteDeepOrangeBrush       { get; } = Create(nameof(PaletteDeepOrangeBrush));
    public static ComponentResourceKey PaletteDeepOrangeBrushBright { get; } = Create(nameof(PaletteDeepOrangeBrushBright));
    public static ComponentResourceKey PaletteDeepOrangeBrushDim    { get; } = Create(nameof(PaletteDeepOrangeBrushDim));
    public static ComponentResourceKey PaletteBrownBrush            { get; } = Create(nameof(PaletteBrownBrush));
    public static ComponentResourceKey PaletteGreyBrush             { get; } = Create(nameof(PaletteGreyBrush));
    public static ComponentResourceKey PaletteBlueGreyBrush         { get; } = Create(nameof(PaletteBlueGreyBrush));

    // StaticColors.xaml
    public static ComponentResourceKey TextFillColorLightPrimaryBrush       { get; } = Create(nameof(TextFillColorLightPrimaryBrush));
    public static ComponentResourceKey TextFillColorLightSecondaryBrush     { get; } = Create(nameof(TextFillColorLightSecondaryBrush));
    public static ComponentResourceKey TextFillColorLightTertiaryBrush      { get; } = Create(nameof(TextFillColorLightTertiaryBrush));
    public static ComponentResourceKey TextFillColorLightDisabledBrush      { get; } = Create(nameof(TextFillColorLightDisabledBrush));
    public static ComponentResourceKey TextFillColorLightInverseBrush       { get; } = Create(nameof(TextFillColorLightInverseBrush));
    public static ComponentResourceKey TextFillColorDarkPrimaryBrush        { get; } = Create(nameof(TextFillColorDarkPrimaryBrush));
    public static ComponentResourceKey TextFillColorDarkSecondaryBrush      { get; } = Create(nameof(TextFillColorDarkSecondaryBrush));
    public static ComponentResourceKey TextFillColorDarkTertiaryBrush       { get; } = Create(nameof(TextFillColorDarkTertiaryBrush));
    public static ComponentResourceKey TextFillColorDarkDisabledBrush       { get; } = Create(nameof(TextFillColorDarkDisabledBrush));
    public static ComponentResourceKey TextFillColorDarkInverseBrush        { get; } = Create(nameof(TextFillColorDarkInverseBrush));
    public static ComponentResourceKey ApplicationBackgroundColorLightBrush { get; } = Create(nameof(ApplicationBackgroundColorLightBrush));
    public static ComponentResourceKey ApplicationBackgroundColorDarkBrush  { get; } = Create(nameof(ApplicationBackgroundColorDarkBrush));
    public static ComponentResourceKey ControlStrongFillColorLightBrush     { get; } = Create(nameof(ControlStrongFillColorLightBrush));
    public static ComponentResourceKey ControlStrongFillColorDarkBrush      { get; } = Create(nameof(ControlStrongFillColorDarkBrush));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
