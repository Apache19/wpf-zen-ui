// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Theming;

/// <summary>
/// Collection of brush resources.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:TextBox Foreground={ui:BrushResource TextFillColorSecondaryBrush} /&gt;
/// </code>
/// </example>
public enum BrushId
{
    Unknown,

    // Light.xaml / Dark.xaml / HighContrast.xaml

    /// <summary>The brighter accent fill brush role.</summary>
    AccentFillPrimaryBrush,

    /// <summary>The normal accent fill brush role.</summary>
    AccentFillSecondaryBrush,

    /// <summary>The dimmer accent fill brush role.</summary>
    AccentFillTertiaryBrush,

    /// <summary>The primary accent text and glyph brush role.</summary>
    AccentTextPrimaryBrush,

    /// <summary>The secondary accent text and glyph brush role.</summary>
    AccentTextSecondaryBrush,

    /// <summary>The primary text and glyph brush role displayed over an accent fill.</summary>
    TextOnAccentPrimaryBrush,

    /// <summary>The accent stroke and focus indicator brush role.</summary>
    AccentStrokeBrush,

    /// <summary>The accent selection background brush role.</summary>
    AccentSelectionBrush,

    SystemColorWindowTextBrush,
    SystemColorWindowBrush,
    SystemColorHighlightTextBrush,
    SystemColorHighlightBrush,
    SystemColorControlTextBrush,
    SystemColorControlBrush,
    SystemColorHotTrackBrush,
    SystemColorGrayTextBrush,
    SystemColorWindowFrameBrush,

    SystemFillColorAttentionBrush,

    KeyboardFocusBorderColorBrush,

    TextFillColorPrimaryBrush,
    TextFillColorSecondaryBrush,
    TextFillColorTertiaryBrush,
    TextFillColorDisabledBrush,
    TextPlaceholderColorBrush,
    TextFillColorInverseBrush,

    ControlFillColorPrimaryBrush,
    ControlFillColorSecondaryBrush,
    ControlFillColorTertiaryBrush,
    ControlFillColorDisabledBrush,
    ControlFillColorInputActiveBrush,

    ControlStrongFillColorDefaultBrush,
    ControlStrongFillColorDisabledBrush,
    ControlSolidFillColorDefaultBrush,

    SubtleFillColorTransparentBrush,
    SubtleFillColorSecondaryBrush,
    SubtleFillColorTertiaryBrush,
    SubtleFillColorDisabledBrush,

    ControlAltFillColorTransparentBrush,
    ControlAltFillColorSecondaryBrush,
    ControlAltFillColorTertiaryBrush,
    ControlAltFillColorQuarternaryBrush,
    ControlAltFillColorDisabledBrush,

    ControlOnImageFillColorDefaultBrush,
    ControlOnImageFillColorSecondaryBrush,
    ControlOnImageFillColorTertiaryBrush,
    ControlOnImageFillColorDisabledBrush,

    ControlStrokeColorDefaultBrush,
    ControlStrokeColorSecondaryBrush,
    ControlStrokeColorTertiaryBrush,
    ControlStrokeColorOnAccentDefaultBrush,
    ControlStrokeColorOnAccentSecondaryBrush,
    ControlStrokeColorOnAccentTertiaryBrush,
    ControlStrokeColorOnAccentDisabledBrush,

    ControlStrokeColorForStrongFillWhenOnImageBrush,
    CardStrokeColorDefaultBrush,
    CardStrokeColorDefaultSolidBrush,
    ControlStrongStrokeColorDefaultBrush,
    ControlStrongStrokeColorDisabledBrush,

    SurfaceStrokeColorDefaultBrush,
    SurfaceStrokeColorFlyoutBrush,
    SurfaceStrokeColorInverseBrush,

    DividerStrokeColorDefaultBrush,

    FocusStrokeColorOuterBrush,
    FocusStrokeColorInnerBrush,

    CardBackgroundFillColorDefaultBrush,
    CardBackgroundFillColorSecondaryBrush,

    EmbeddedFillColorDefaultBrush,
    EmbeddedFillColorStrongBrush,
    EmbeddedFillColorDeepBrush,

    SmokeFillColorDefaultBrush,

    LayerFillColorDefaultBrush,
    LayerFillColorAltBrush,
    LayerOnAcrylicFillColorDefaultBrush,
    LayerOnAccentAcrylicFillColorDefaultBrush,

    LayerOnMicaBaseAltFillColorDefaultBrush,
    LayerOnMicaBaseAltFillColorSecondaryBrush,
    LayerOnMicaBaseAltFillColorTertiaryBrush,
    LayerOnMicaBaseAltFillColorTransparentBrush,

    SolidBackgroundFillColorBaseBrush,
    SolidBackgroundFillColorSecondaryBrush,
    SolidBackgroundFillColorTertiaryBrush,
    SolidBackgroundFillColorQuarternaryBrush,
    SolidBackgroundFillColorBaseAltBrush,

    SystemFillColorSuccessBrush,
    SystemFillColorCautionBrush,
    SystemFillColorCriticalBrush,
    SystemFillColorNeutralBrush,
    SystemFillColorSolidNeutralBrush,
    SystemFillColorAttentionBackgroundBrush,
    SystemFillColorSuccessBackgroundBrush,
    SystemFillColorCautionBackgroundBrush,
    SystemFillColorCriticalBackgroundBrush,
    SystemFillColorNeutralBackgroundBrush,
    SystemFillColorSolidAttentionBackgroundBrush,
    SystemFillColorSolidNeutralBackgroundBrush,

    ControlElevationBorderBrush,
    CircleElevationBorderBrush,
    AccentControlElevationBorderBrush,
    // TextControlElevationBorderBrush, deleted

    StripedBackgroundBrush,

    AcrylicBackgroundFillColorDefaultBrush,

    WindowBackgroundBrush,
    WindowForegroundBrush,
    WindowBorderBrush,
    WindowBorderInactiveBrush,

    SystemFillColorInformationalBrush,

    LeftNavigationViewSeparatorBrush,

    // Palette.xaml:
    PalettePrimaryBrush,
    PaletteRedBrush,
    PaletteRedBrushBright,
    PaletteRedBrushDim,
    PalettePinkBrush,
    PalettePinkBrushBright,
    PalettePinkBrushDim,
    PalettePurpleBrush,
    PaletteDeepPurpleBrush,
    PaletteIndigoBrush,
    PaletteBlueBrush,
    PaletteBlueBrushBright,
    PaletteBlueBrushDim,
    PaletteLightBlueBrush,
    PaletteLightBlueBrushBright,
    PaletteLightBlueBrushDim,
    PaletteCyanBrush,
    PaletteCyanBrushBright,
    PaletteCyanBrushDim,
    PaletteTealBrush,
    PaletteGreenBrush,
    PaletteGreenBrushBright,
    PaletteGreenBrushDim,
    PaletteLightGreenBrush,
    PaletteLimeBrush,
    PaletteYellowBrush,
    PaletteYellowBrushBright,
    PaletteYellowBrushDim,
    PaletteAmberBrush,
    PaletteOrangeBrush,
    PaletteOrangeBrushBright,
    PaletteOrangeBrushDim,
    PaletteDeepOrangeBrush,
    PaletteDeepOrangeBrushBright,
    PaletteDeepOrangeBrushDim,
    PaletteBrownBrush,
    PaletteGreyBrush,
    PaletteBlueGreyBrush,

    // StaticColors.xaml
    TextFillColorLightPrimaryBrush,
    TextFillColorLightSecondaryBrush,
    TextFillColorLightTertiaryBrush,
    TextFillColorLightDisabledBrush,
    TextFillColorLightInverseBrush,
    TextFillColorDarkPrimaryBrush,
    TextFillColorDarkSecondaryBrush,
    TextFillColorDarkTertiaryBrush,
    TextFillColorDarkDisabledBrush,
    TextFillColorDarkInverseBrush,
    ApplicationBackgroundColorLightBrush,
    ApplicationBackgroundColorDarkBrush,
    ControlStrongFillColorLightBrush,
    ControlStrongFillColorDarkBrush,
}
