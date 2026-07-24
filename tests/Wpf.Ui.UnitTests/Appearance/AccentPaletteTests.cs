// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace Wpf.Ui.UnitTests.Appearance;

public class AccentPaletteTests
{
    private static readonly Color DefaultAccent = Color.FromRgb(0x00, 0x78, 0xD4);

    [Theory]
    [InlineData(0x00, 0x78, 0xD4)]
    [InlineData(0xFF, 0xB9, 0x00)]
    [InlineData(0x00, 0x00, 0x00)]
    [InlineData(0xFF, 0xFF, 0xFF)]
    [InlineData(0xD1, 0x34, 0x38)]
    public void FillRoles_AreOrderedByPerceptualLightness(byte red, byte green, byte blue)
    {
        var accent = Color.FromRgb(red, green, blue);

        foreach (var mode in new[] { UiThemeMode.Light, UiThemeMode.Dark })
        {
            var palette = AccentPalette.Create(accent, mode);

            Assert.True(
                AccentPalette.GetPerceptualLightness(palette.AccentFillPrimary)
                > AccentPalette.GetPerceptualLightness(palette.AccentFillSecondary)
            );
            Assert.True(
                AccentPalette.GetPerceptualLightness(palette.AccentFillSecondary)
                > AccentPalette.GetPerceptualLightness(palette.AccentFillTertiary)
            );
        }
    }

    [Fact]
    public void DefaultAccent_ProducesDifferentLightAndDarkFillPalettes()
    {
        var light = AccentPalette.Create(DefaultAccent, UiThemeMode.Light);
        var dark = AccentPalette.Create(DefaultAccent, UiThemeMode.Dark);

        Assert.NotEqual(light.AccentFillSecondary, dark.AccentFillSecondary);
        Assert.True(
            AccentPalette.GetPerceptualLightness(dark.AccentFillSecondary)
            > AccentPalette.GetPerceptualLightness(light.AccentFillSecondary)
        );
    }

    [Theory]
    [InlineData(UiThemeMode.Light, 250, 250, 250)]
    [InlineData(UiThemeMode.Dark, 32, 32, 32)]
    public void AccentTextPrimary_MeetsNormalTextContrast(
        UiThemeMode mode,
        byte backgroundRed,
        byte backgroundGreen,
        byte backgroundBlue)
    {
        var background = Color.FromRgb(backgroundRed, backgroundGreen, backgroundBlue);

        foreach (var accent in GetEdgeCaseAccents())
        {
            var palette = AccentPalette.Create(accent, mode);

            Assert.True(
                AccentPalette.GetContrastRatio(palette.AccentTextPrimary, background) >= 4.5
            );
        }
    }

    [Theory]
    [InlineData(UiThemeMode.Light)]
    [InlineData(UiThemeMode.Dark)]
    public void TextOnAccentPrimary_MeetsNormalTextContrastAgainstEveryFill(UiThemeMode mode)
    {
        foreach (var accent in GetEdgeCaseAccents())
        {
            var palette = AccentPalette.Create(accent, mode);

            Assert.True(AccentPalette.GetContrastRatio(palette.TextOnAccentPrimary, palette.AccentFillPrimary) >= 4.5);
            Assert.True(AccentPalette.GetContrastRatio(palette.TextOnAccentPrimary, palette.AccentFillSecondary) >= 4.5);
            Assert.True(AccentPalette.GetContrastRatio(palette.TextOnAccentPrimary, palette.AccentFillTertiary) >= 4.5);
        }
    }

    [Fact]
    public void Create_IgnoresAccentAlpha()
    {
        var opaque = AccentPalette.Create(DefaultAccent, UiThemeMode.Light);
        var transparent = AccentPalette.Create(Color.FromArgb(0x10, 0x00, 0x78, 0xD4), UiThemeMode.Light);

        Assert.Equal(opaque, transparent);
    }

    private static IEnumerable<Color> GetEdgeCaseAccents()
    {
        yield return DefaultAccent;
        yield return Color.FromRgb(0xFF, 0xB9, 0x00);
        yield return Color.FromRgb(0x00, 0x00, 0x00);
        yield return Color.FromRgb(0xFF, 0xFF, 0xFF);
        yield return Color.FromRgb(0xD1, 0x34, 0x38);
    }
}
