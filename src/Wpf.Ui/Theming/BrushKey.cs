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
public enum BrushKey
{
    Unknown,

    // Light.xaml / Dark.xaml / HighContrast.xaml
    SystemAccentColorBrush,

    SystemAccentColorPrimaryBrush,
    SystemAccentColorSecondaryBrush,
    SystemAccentColorTertiaryBrush,

    SystemColorWindowTextBrush,
    SystemColorWindowBrush,
    SystemColorHighlightTextBrush,
    SystemColorHighlightBrush,
    SystemColorControlTextBrush,
    SystemColorControlBrush,
    SystemColorHotTrackBrush,
    SystemColorGrayTextBrush,
    SystemColorWindowFrameBrush,

    AccentTextFillColorPrimaryBrush,
    AccentTextFillColorSecondaryBrush,
    AccentTextFillColorTertiaryBrush,
    AccentFillColorSelectedTextBackgroundBrush,
    AccentFillColorDefaultBrush,
    AccentFillColorSecondaryBrush,
    AccentFillColorTertiaryBrush,
    AccentFillColorDisabledBrush,
    SystemFillColorAttentionBrush,

    KeyboardFocusBorderColorBrush,

    TextFillColorPrimaryBrush,
    TextFillColorSecondaryBrush,
    TextFillColorTertiaryBrush,
    TextFillColorDisabledBrush,
    TextPlaceholderColorBrush,
    TextFillColorInverseBrush,

    AccentTextFillColorDisabledBrush,
    TextOnAccentFillColorSelectedTextBrush,
    TextOnAccentFillColorPrimaryBrush,
    TextOnAccentFillColorSecondaryBrush,
    TextOnAccentFillColorDisabledBrush,

    ControlFillColorDefaultBrush,
    ControlFillColorSecondaryBrush,
    ControlFillColorTertiaryBrush,
    ControlFillColorDisabledBrush,
    ControlFillColorTransparentBrush,
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
    TextControlElevationBorderBrush,

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
    PalettePinkBrush,
    PalettePurpleBrush,
    PaletteDeepPurpleBrush,
    PaletteIndigoBrush,
    PaletteBlueBrush,
    PaletteLightBlueBrush,
    PaletteCyanBrush,
    PaletteTealBrush,
    PaletteGreenBrush,
    PaletteLightGreenBrush,
    PaletteLimeBrush,
    PaletteYellowBrush,
    PaletteAmberBrush,
    PaletteOrangeBrush,
    PaletteDeepOrangeBrush,
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