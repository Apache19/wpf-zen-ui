// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies four-way placement priorities and geometric boundary conditions without a desktop.
/// </summary>
public class KeyboardAvoidanceLayoutTests
{
    /// <summary>
    /// Each collision edge uses its own decorative inset, including asymmetric presentation.
    /// </summary>
    [Theory]
    [InlineData(0, 500, 1000, 300, 300, 240)]
    [InlineData(0, 0, 1000, 300, 300, 280)]
    [InlineData(0, 0, 350, 800, 340, 250)]
    [InlineData(650, 0, 350, 800, 280, 250)]
    public void OcclusionPadding_UsesMatchingEdge(
        double x, double y, double width, double height, double expectedX, double expectedY)
    {
        Rect result = KeyboardAvoidanceLayout.Place(new Rect(0, 0, 1000, 800),
            new Rect(300, 250, 400, 300), new Rect(x, y, width, height), new Thickness(10, 20, 30, 40));
        Assert.Equal(new Rect(expectedX, expectedY, 400, 300), result);
    }

    /// <summary>
    /// Docked and corner occlusions use the smallest translation that preserves the original size.
    /// </summary>
    [Theory]
    [InlineData(0, 500, 1000, 300, 300, 200)]
    [InlineData(0, 0, 1000, 300, 300, 300)]
    [InlineData(0, 0, 350, 800, 350, 250)]
    [InlineData(650, 0, 350, 800, 250, 250)]
    [InlineData(650, 500, 200, 100, 300, 200)]
    public void Occlusion_MovesShortestDistanceWithoutShrinking(
        double x, double y, double width, double height, double expectedX, double expectedY)
    {
        Rect result = KeyboardAvoidanceLayout.Place(
            new Rect(0, 0, 1000, 800), new Rect(300, 250, 400, 300), new Rect(x, y, width, height));

        Assert.Equal(new Rect(expectedX, expectedY, 400, 300), result);
    }

    /// <summary>
    /// Empty, adjacent and off-host rectangles leave the original placement intact.
    /// </summary>
    [Theory]
    [InlineData(700, 250, 300, 300)] // Touching an edge is not occlusion.
    [InlineData(0, 0, 100, 100)]
    [InlineData(-500, 200, 200, 500)] // Keyboard on a different monitor.
    [InlineData(0, 0, 0, 800)]
    public void UnobstructedContent_RemainsAtOriginalPosition(double x, double y, double width, double height)
    {
        var preferred = new Rect(300, 250, 400, 300);
        Assert.Equal(preferred, KeyboardAvoidanceLayout.Place(
            new Rect(0, 0, 1000, 800), preferred, new Rect(x, y, width, height)));
        Assert.Equal(preferred, KeyboardAvoidanceLayout.Place(new Rect(0, 0, 1000, 800), preferred, Rect.Empty));
    }

    /// <summary>
    /// Preserving size takes priority over a closer location that would require compression.
    /// </summary>
    [Fact]
    public void FloatingKeyboard_PrefersTranslationEvenWhenAnotherDirectionHasShorterCompressedTravel()
    {
        var host = new Rect(0, 0, 1000, 800);
        var preferred = new Rect(300, 250, 400, 300);
        var keyboard = new Rect(350, 300, 100, 300);

        Assert.Equal(new Rect(450, 250, 400, 300), KeyboardAvoidanceLayout.Place(host, preferred, keyboard));
    }

    /// <summary>
    /// When translation cannot work, only the obstructed dimension is reduced for edge keyboards.
    /// </summary>
    [Theory]
    [InlineData(0, 400, 1000, 400, 200, 0, 600, 400)]
    [InlineData(0, 0, 1000, 400, 200, 400, 600, 400)]
    [InlineData(300, 0, 700, 800, 0, 100, 300, 600)]
    [InlineData(0, 0, 700, 800, 700, 100, 300, 600)]
    public void NoDirectionFits_RetainsLargestArea(
        double x, double y, double width, double height,
        double expectedX, double expectedY, double expectedWidth, double expectedHeight)
    {
        Rect result = KeyboardAvoidanceLayout.Place(new Rect(0, 0, 1000, 800),
            new Rect(200, 100, 600, 600), new Rect(x, y, width, height));

        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), result);
    }

    /// <summary>
    /// A larger side strip must not win over a usable upper region when minimum width cannot fit.
    /// </summary>
    [Fact]
    public void Compression_PrefersRegionThatSatisfiesMinimumSize()
    {
        var host = new Rect(0, 0, 1000, 800);
        var preferred = new Rect(160, 100, 680, 600);
        var keyboard = new Rect(350, 280, 650, 520);
        Assert.Equal(new Rect(160, 0, 680, 280), KeyboardAvoidanceLayout.Place(
            host, preferred, keyboard, new Thickness(), new Size(360, 84)));
    }

    /// <summary>
    /// An entirely covered host retains usable layout rather than receiving a zero-sized slot.
    /// </summary>
    [Fact]
    public void FullyCoveredHost_RetainsLayoutInsteadOfCollapsingContent()
    {
        var host = new Rect(0, 0, 1000, 800);
        var preferred = new Rect(300, 250, 400, 300);
        Assert.Equal(preferred, KeyboardAvoidanceLayout.Place(host, preferred, host));
    }

    /// <summary>
    /// A deterministic range of floating and partially off-host rectangles respects both bounds.
    /// </summary>
    [Fact]
    public void ArbitraryOcclusion_PlacementStaysWithinHostAndOutsideKeyboard()
    {
        var random = new Random(42);
        var host = new Rect(0, 0, 1000, 800);
        var preferred = new Rect(300, 250, 400, 300);
        for (int index = 0; index < 1000; index++)
        {
            var keyboard = new Rect(random.Next(-500, 1200), random.Next(-400, 1000),
                random.Next(1, 700), random.Next(1, 600));
            Rect result = KeyboardAvoidanceLayout.Place(host, preferred, keyboard);
            Rect overlap = Rect.Intersect(result, keyboard);

            Assert.True(host.Contains(result));
            Assert.True(overlap.IsEmpty || overlap.Width == 0 || overlap.Height == 0);
            Assert.True(result.Width <= preferred.Width && result.Height <= preferred.Height);
        }
    }
}
