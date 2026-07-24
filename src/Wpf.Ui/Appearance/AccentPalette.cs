// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Appearance;

/// <summary>
/// Contains the UI-facing accent roles generated from one opaque base color.
/// </summary>
/// <remarks>
/// The palette intentionally exposes no raw Windows light/dark ramp. Windows documents how to
/// obtain the base accent color but does not document a stable ramp-generation algorithm. WPF UI
/// therefore owns this deterministic palette and maps only semantic roles into theme resources.
/// </remarks>
internal sealed record AccentPalette(
    Color AccentFillPrimary,
    Color AccentFillSecondary,
    Color AccentFillTertiary,
    Color AccentTextPrimary,
    Color AccentTextSecondary,
    Color TextOnAccentPrimary,
    Color AccentStroke,
    Color AccentSelection)
{
    private const double NormalTextContrast    = 4.5;
    private const double SecondaryTextContrast = 3.0;

    private static readonly Color LightBackground = Color.FromRgb(250, 250, 250);
    private static readonly Color DarkBackground  = Color.FromRgb(32,  32,  32);
    private static readonly Color Black           = Color.FromRgb(0,   0,   0);
    private static readonly Color White           = Color.FromRgb(255, 255, 255);

    /// <summary>
    /// Creates theme-specific UI roles from a single base accent color.
    /// </summary>
    /// <param name="accentColor">The base accent color. Its alpha channel is ignored.</param>
    /// <param name="themeMode">The light or dark theme for which resources are being generated.</param>
    /// <returns>A complete set of opaque accent role colors.</returns>
    internal static AccentPalette Create(Color accentColor, UiThemeMode themeMode)
    {
        if (themeMode is not (UiThemeMode.Light or UiThemeMode.Dark))
        {
            throw new ArgumentOutOfRangeException(
                nameof(themeMode),
                themeMode,
                "An accent palette requires a light or dark theme."
            );
        }

        var isDark = themeMode == UiThemeMode.Dark;
        var source = Oklch.FromColor(Color.FromRgb(accentColor.R, accentColor.G, accentColor.B));

        // A dark surface needs a substantially lighter fill than the personalization color,
        // whereas a light surface needs a slightly darker fill. Clamping the usable tone range
        // also keeps near-white and near-black user colors interactive instead of losing all
        // distinction against the surrounding surface.
        // Keep the complete three-tone family on one side of the black/white contrast crossover.
        // Otherwise a mid-tone custom accent could require black text for Primary and white text
        // for Tertiary even though the public palette intentionally provides one on-accent token.
        var normalFillLightness = isDark
            ? Clamp(source.L + 0.20, 0.68, 0.82)
            : Clamp(source.L - 0.07, 0.28, 0.50);

        var fillSecondary = source.WithLightness(normalFillLightness).ToColor();
        var fillPrimary   = source.WithLightness(Clamp(normalFillLightness + 0.045, 0.0, 1.0)).ToColor();
        var fillTertiary  = source.WithLightness(Clamp(normalFillLightness - 0.055, 0.0, 1.0)).ToColor();

        var background        = isDark ? DarkBackground : LightBackground;
        var accentTextPrimary = EnsureContrast(source, background, NormalTextContrast, lighten: isDark);

        // Secondary accent text is deliberately closer to the surface, but remains suitable for
        // large text and non-text indicators at the WCAG 3:1 threshold.
        var secondaryText = Oklch.FromColor(accentTextPrimary).WithLightness(
            Clamp(Oklch.FromColor(accentTextPrimary).L + (isDark ? -0.07 : 0.07), 0.0, 1.0)
        );
        var accentTextSecondary = EnsureContrast(secondaryText, background, SecondaryTextContrast, lighten: isDark);

        // One on-accent token is shared by all three fill levels. Choose the neutral that has the
        // best worst-case contrast rather than assuming that light themes always use white text
        // or dark themes always use black text; custom yellow or very dark accents break that rule.
        var textOnAccent = MinimumContrast(Black, fillPrimary, fillSecondary, fillTertiary)
                        >= MinimumContrast(White, fillPrimary, fillSecondary, fillTertiary)
            ? Black
            : White;

        return new AccentPalette(
            fillPrimary,
            fillSecondary,
            fillTertiary,
            accentTextPrimary,
            accentTextSecondary,
            textOnAccent,
            accentTextPrimary,
            fillSecondary
        );
    }

    /// <summary>
    /// Returns the WCAG contrast ratio between two opaque sRGB colors.
    /// </summary>
    internal static double GetContrastRatio(Color first, Color second)
    {
        var firstLuminance  = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        var lighter         = Math.Max(firstLuminance, secondLuminance);
        var darker          = Math.Min(firstLuminance, secondLuminance);

        return (lighter + 0.05) / (darker + 0.05);
    }

    /// <summary>
    /// Returns the perceptual lightness component used to order generated tones in tests and
    /// internal diagnostics.
    /// </summary>
    internal static double GetPerceptualLightness(Color color)
    {
        return Oklch.FromColor(color).L;
    }

    private static Color EnsureContrast(Oklch candidate, Color background, double minimumContrast, bool lighten)
    {
        var result = candidate.ToColor();
        if (GetContrastRatio(result, background) >= minimumContrast)
        {
            return result;
        }

        // Binary-search only lightness and preserve hue. Oklch.ToColor performs chroma reduction
        // when a requested tone leaves sRGB, preventing clipped channels from shifting hue.
        var failingLightness = candidate.L;
        var passingLightness = lighten ? 1.0 : 0.0;

        for (var i = 0; i < 24; i++)
        {
            var midpoint      = (failingLightness + passingLightness) / 2.0;
            var midpointColor = candidate.WithLightness(midpoint).ToColor();

            if (GetContrastRatio(midpointColor, background) >= minimumContrast)
            {
                passingLightness = midpoint;
                result           = midpointColor;
            }
            else
            {
                failingLightness = midpoint;
            }
        }

        return result;
    }

    private static double MinimumContrast(Color foreground, params Color[] backgrounds)
    {
        var minimum = double.MaxValue;

        foreach (var background in backgrounds)
        {
            minimum = Math.Min(minimum, GetContrastRatio(foreground, background));
        }

        return minimum;
    }

    private static double GetRelativeLuminance(Color color)
    {
        var red   = ToLinear(color.R / 255.0);
        var green = ToLinear(color.G / 255.0);
        var blue  = ToLinear(color.B / 255.0);

        return (0.2126 * red) + (0.7152 * green) + (0.0722 * blue);
    }

    private static double ToLinear(double value)
    {
        return value <= 0.04045
            ? value / 12.92
            : Math.Pow((value + 0.055) / 1.055, 2.4);
    }

    private static double ToSrgb(double value)
    {
        return value <= 0.0031308
            ? 12.92 * value
            : (1.055 * Math.Pow(value, 1.0 / 2.4)) - 0.055;
    }

    private static double Clamp(double value, double minimum, double maximum)
    {
        return Math.Max(minimum, Math.Min(maximum, value));
    }

    private readonly record struct Oklch(double L, double C, double H)
    {
        internal static Oklch FromColor(Color color)
        {
            var red   = ToLinear(color.R / 255.0);
            var green = ToLinear(color.G / 255.0);
            var blue  = ToLinear(color.B / 255.0);

            var l = (0.4122214708 * red) + (0.5363325363 * green) + (0.0514459929 * blue);
            var m = (0.2119034982 * red) + (0.6806995451 * green) + (0.1073969566 * blue);
            var s = (0.0883024619 * red) + (0.2817188376 * green) + (0.6299787005 * blue);

            var lRoot = Math.Pow(l, 1.0 / 3.0);
            var mRoot = Math.Pow(m, 1.0 / 3.0);
            var sRoot = Math.Pow(s, 1.0 / 3.0);

            var lightness = (0.2104542553 * lRoot) + (0.7936177850 * mRoot) - (0.0040720468 * sRoot);
            var a         = (1.9779984951 * lRoot) - (2.4285922050 * mRoot) + (0.4505937099 * sRoot);
            var b         = (0.0259040371 * lRoot) + (0.7827717662 * mRoot) - (0.8086757660 * sRoot);

            return new Oklch(lightness, Math.Sqrt((a * a) + (b * b)), Math.Atan2(b, a));
        }

        internal Oklch WithLightness(double lightness)
        {
            return new Oklch(lightness, C, H);
        }

        internal Color ToColor()
        {
            var chroma    = C;
            var candidate = ToLinearRgb(chroma);

            if (!candidate.IsInGamut)
            {
                // Preserve the requested lightness and hue while reducing chroma to the nearest
                // representable sRGB color. This is preferable to per-channel clipping, which can
                // visibly rotate saturated user-selected hues.
                var lower = 0.0;
                var upper = chroma;

                for (var i = 0; i < 24; i++)
                {
                    var midpoint          = (lower + upper) / 2.0;
                    var midpointCandidate = ToLinearRgb(midpoint);

                    if (midpointCandidate.IsInGamut)
                    {
                        lower     = midpoint;
                        candidate = midpointCandidate;
                    }
                    else
                    {
                        upper = midpoint;
                    }
                }
            }

            return Color.FromRgb(
                ToByte(ToSrgb(Clamp(candidate.Red,   0.0, 1.0))),
                ToByte(ToSrgb(Clamp(candidate.Green, 0.0, 1.0))),
                ToByte(ToSrgb(Clamp(candidate.Blue,  0.0, 1.0)))
            );
        }

        private LinearRgb ToLinearRgb(double chroma)
        {
            var a = chroma * Math.Cos(H);
            var b = chroma * Math.Sin(H);

            var lRoot = L + (0.3963377774 * a) + (0.2158037573 * b);
            var mRoot = L - (0.1055613458 * a) - (0.0638541728 * b);
            var sRoot = L - (0.0894841775 * a) - (1.2914855480 * b);

            var l = lRoot * lRoot * lRoot;
            var m = mRoot * mRoot * mRoot;
            var s = sRoot * sRoot * sRoot;

            return new LinearRgb(
                (4.0767416621 * l) - (3.3077115913 * m) + (0.2309699292 * s),
                (-1.2684380046 * l) + (2.6097574011 * m) - (0.3413193965 * s),
                (-0.0041960863 * l) - (0.7034186147 * m) + (1.7076147010 * s)
            );
        }

        private static byte ToByte(double value)
        {
            return (byte)Math.Round(Clamp(value, 0.0, 1.0) * 255.0);
        }
    }

    private readonly record struct LinearRgb(double Red, double Green, double Blue)
    {
        internal bool IsInGamut =>
            Red is >= 0.0 and <= 1.0
         && Green is >= 0.0 and <= 1.0
         && Blue is >= 0.0 and <= 1.0;
    }
}