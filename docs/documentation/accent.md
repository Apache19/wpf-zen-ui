# Accent colors

WPF UI obtains one base accent color and generates separate light- and dark-theme palettes from it. Controls consume UI-facing role resources; the raw Windows color and Windows' undocumented registry palette are deliberately not exposed as UI resources.

## Configure the accent source

The default source is `System`:

```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ui:ThemeManagerOptions
                AccentSource="System"
                ThemeMode="System" />
        </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

The available values are:

- `System` reads the personalization accent through the documented Windows `UISettings.GetColorValue(Accent)` API. On Windows 7 and other systems where that API is unavailable, WPF UI uses its default blue.
- `Custom` uses `CustomAccentColor` as the base color.
- `Default` always uses WPF UI's built-in `#0078D4` base color.

Configure a custom color in XAML:

```xml
<ui:ThemeManagerOptions
    AccentSource="Custom"
    CustomAccentColor="#7A3FD0"
    ThemeMode="Dark" />
```

Or configure it at runtime:

```csharp
ThemeManager.Configure(
    themeMode: UiThemeMode.Dark,
    accentSource: AccentColorSource.Custom,
    customAccentColor: Color.FromRgb(0x7A, 0x3F, 0xD0)
);
```

The alpha channel of `CustomAccentColor` is ignored. Opacity is a property of the control or its state, not of the shared accent palette.

## Accent resources

Each role is available as a color (`ColorKeys` and `ColorToken`) and as a brush (`BrushKeys` and `BrushToken`):

- `AccentFillPrimary` is the brighter fill level.
- `AccentFillSecondary` is the normal fill level.
- `AccentFillTertiary` is the dimmer fill level.
- `AccentTextPrimary` is the primary accent-colored text and glyph level.
- `AccentTextSecondary` is the lower-emphasis accent-colored text and glyph level.
- `TextOnAccentPrimary` is black or white, selected to remain readable over all three accent fills.
- `AccentStroke` is intended for accent borders, focus indicators, and similar strokes.
- `AccentSelection` is intended for selection backgrounds and selection indicators.

The names express a reusable color hierarchy, not fixed control states. Each control decides how to use them. For example, an accent button may choose Secondary for its normal state, Primary while the pointer is over it, and Tertiary while pressed:

```xml
<Style TargetType="ui:Button">
    <Setter Property="Background" Value="{ui:DynamicBrush AccentFillSecondary}" />
    <Setter Property="MouseOverBackground" Value="{ui:DynamicBrush AccentFillPrimary}" />
    <Setter Property="PressedBackground" Value="{ui:DynamicBrush AccentFillTertiary}" />
    <Setter Property="Foreground" Value="{ui:DynamicBrush TextOnAccentPrimary}" />
</Style>
```

Use general theme resources for disabled states. For example, use `ControlFillColorDisabledBrush` and `TextFillColorDisabledBrush` rather than deriving another accent color.

## Palette generation

Windows exposes the base accent through a public API, but it does not document a stable algorithm for recreating the surrounding personalization ramp. WPF UI therefore generates its own deterministic palette in OKLCH:

- hue is preserved where the sRGB gamut permits;
- chroma is reduced instead of clipping individual RGB channels;
- light and dark themes receive different usable fill tones;
- Primary is perceptually lighter than Secondary, and Secondary is lighter than Tertiary;
- `AccentTextPrimary` is adjusted to at least 4.5:1 contrast against the theme surface;
- one `TextOnAccentPrimary` color is selected to provide at least 4.5:1 contrast against every accent fill level.

This means WPF UI's generated roles are internally consistent but are not promised to match Windows' private Light1/Dark1 palette values.

## High contrast

High contrast mode bypasses both the generated palette and the configured accent source. Accent roles map directly to the corresponding WPF `SystemColors` resources, such as Highlight, HighlightText, and HotTrack, so user accessibility settings remain authoritative.

## Runtime updates

When `AccentSource` is `System`, Windows preference notifications cause `ThemeManager` to read the base color again and rebuild both palettes. Resource consumers should use `BrushResource`/`ColorResource` or an equivalent `DynamicResource` reference so existing controls update in place.

Use `ThemeManager.Changed` when application code must react after the refreshed resources have been applied:

```csharp
ThemeManager.Changed += (_, args) =>
{
    Debug.WriteLine($"Theme changed to {args.AppliedTheme}.");
};
```
