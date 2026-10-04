// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.Theming;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Exercises the badge's passive drawing, layout invalidation, and theme resource behavior.
/// </summary>
public class BadgeTests
{
    /// <summary>
    /// Keeps element, inline, and self-drawn icons synchronized with scoped font resources.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FontResources_UpdateEveryPresentationWithoutMutatingSharedSource(bool filled)
    {
        WpfTestThread.Run(() =>
        {
            var source = new FluentIconSource(FluentSymbolRegular.Checkmark16, filled);
            var alternate = new FluentIconSource(FluentSymbolRegular.Checkmark16, !filled);
            var defaultFont = source.FontFamily;
            var replacementFont = alternate.FontFamily;
            var resourceKey = filled
                ? FontFamilyKeys.FluentSystemIconsFilled
                : FontFamilyKeys.FluentSystemIcons;
            var badge = new Badge { Icon = source };
            var icon = new FontIcon(source);
            var span = new FontIconSpan(source);
            var text = new System.Windows.Controls.TextBlock();
            text.Inlines.Add(span);
            var host = new StackPanel { Children = { badge, icon, text } };
            var resources = new ResourceDictionary { [resourceKey] = defaultFont };
            host.Resources.MergedDictionaries.Add(resources);

            Assert.Equal(source.Glyph, icon.Text);
            Assert.Equal(source.Glyph, span.Text);
            Assert.Same(defaultFont, icon.FontFamily);
            Assert.Same(defaultFont, span.FontFamily);
            AssertBadgeFont(badge, defaultFont);

            // Replacing the dictionary exercises the same invalidation as theme replacement.
            var replacement = new ResourceDictionary { [resourceKey] = replacementFont };
            host.Resources.MergedDictionaries[0] = replacement;
            Assert.Same(replacementFont, icon.FontFamily);
            Assert.Same(replacementFont, span.FontFamily);
            AssertBadgeFont(badge, replacementFont);
            Assert.Same(defaultFont, source.FontFamily);

            replacement[resourceKey] = defaultFont;
            AssertBadgeFont(badge, defaultFont);
            Assert.Same(defaultFont, icon.FontFamily);
            Assert.Same(defaultFont, span.FontFamily);

            var otherHost = new StackPanel();
            otherHost.Resources[resourceKey] = replacementFont;
            host.Children.Remove(badge);
            otherHost.Children.Add(badge);
            AssertBadgeFont(badge, replacementFont);
            otherHost.Resources.Remove(resourceKey);
            AssertBadgeFont(badge, defaultFont);

            // A source switch must release the old Fluent resource expression.
            otherHost.Resources[resourceKey] = replacementFont;
            var custom = new TestFontIcon();
            badge.Icon = custom;
            AssertBadgeFont(badge, custom.FontFamily);
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
        });
    }

    /// <summary>
    /// Keeps explicit and presenter-selected variants on the same character mapping.
    /// </summary>
    [Fact]
    public void Variant_UsesTheSameMappingForSourceElementAndInline()
    {
        WpfTestThread.Run(() =>
        {
            var icon = new FontIcon(new FluentIconSource(FluentSymbolRegular.Home24));
            var span = new FontIconSpan(new FluentIconSource(FluentSymbolRegular.Home24));
            icon.Source = new FluentIconSource(FluentSymbolRegular.Settings24);
            span.Source = icon.Source;
            var regular = new FluentIconSource(FluentSymbolRegular.Settings24);
            var filled = new FluentIconSource(FluentSymbolRegular.Settings24, true);
            Assert.Equal(regular.Glyph, icon.Text);
            Assert.Equal(regular.Glyph, span.Text);
            icon.Variant = IconVariant.Filled;
            span.Variant = IconVariant.Filled;
            Assert.Equal(filled.Glyph, icon.Text);
            Assert.Equal(filled.Glyph, span.Text);
            icon.Variant = IconVariant.Default;
            span.Variant = IconVariant.Default;
            Assert.Equal(regular.Glyph, icon.Text);
            Assert.Equal(regular.Glyph, span.Text);
            Assert.False(regular.Filled);
        });
    }

    /// <summary>
    /// Resolves the embedded icon font and checks that the requested symbol has a real glyph.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FluentIconSource_ContainsRequestedGlyph(bool filled)
    {
        WpfTestThread.Run(() =>
        {
            var icon = new FluentIconSource(FluentSymbolRegular.Checkmark16, filled);
            var typeface = new Typeface(icon.FontFamily, FontStyles.Normal,
                FontWeights.Normal, FontStretches.Normal);
            Assert.True(typeface.TryGetGlyphTypeface(out var glyphTypeface), icon.FontFamily.Source);
            int codepoint = char.ConvertToUtf32(icon.Glyph, 0);
            Assert.True(glyphTypeface.CharacterToGlyphMap.TryGetValue(codepoint, out ushort glyph),
                $"Font={glyphTypeface.FontUri}, codepoint={codepoint:X}");
            Assert.NotEqual((ushort)0, glyph);

            var badge = new Badge { Icon = icon, FontSize = 16 };
            Render(badge, 28, 28);
            var drawing = VisualTreeHelper.GetDrawing(badge);
            var runs = new List<GlyphRun>();
            CollectGlyphRuns(drawing, runs);
            var run = Assert.Single(runs);
            Assert.Equal(glyphTypeface.FontUri, run.GlyphTypeface.FontUri);
            Assert.Equal(glyph, Assert.Single(run.GlyphIndices));
        });
    }

    /// <summary>
    /// Ensures input coercion and the child boundary remain effective after layout.
    /// </summary>
    [Fact]
    public void DisplayOnly_RejectsChildrenAndInputWithoutCreatingVisuals()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge { OverridesDefaultStyle = true, Text = "42", Focusable = true, IsHitTestVisible = true };
            KeyboardNavigation.SetIsTabStop(badge, true);
            Render(badge, 80, 40);
            Assert.IsAssignableFrom<Decorator>(badge);
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
            Assert.False(badge.Focusable);
            Assert.False(badge.IsHitTestVisible);
            Assert.False(KeyboardNavigation.GetIsTabStop(badge));
            Assert.Throws<InvalidOperationException>(() => badge.Child = new Border());
        });
    }

    /// <summary>
    /// Compares nonuniform and overlapping contours with native Border rendering.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Chrome_MatchesWpfBorderPixels(int scenario)
    {
        WpfTestThread.Run(() =>
        {
            var thickness = scenario == 2 ? new Thickness(60) : new Thickness(1, 3, 5, 2);
            var radius = scenario == 1 ? new CornerRadius(80, 40, 60, 100) : new CornerRadius(4, 8, 2, 6);
            var background = new SolidColorBrush(Color.FromArgb(120, 20, 90, 200));
            var stroke = new SolidColorBrush(Color.FromArgb(150, 220, 30, 60));
            var badge = new Badge
            {
                OverridesDefaultStyle = true,
                BorderThickness = thickness, CornerRadius = radius,
                Background = background, BorderBrush = stroke,
            };
            var reference = new ConstrainedBorder
            {
                BorderThickness = thickness, CornerRadius = radius,
                Background = background, BorderBrush = stroke,
            };

            Assert.Equal(Render(reference, 80, 40), Render(badge, 80, 40));
        });
    }

    /// <summary>
    /// Checks inherited font changes and the space reserved for border and padding.
    /// </summary>
    [Fact]
    public void Text_TracksInheritedTypographyAndPadding()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge { OverridesDefaultStyle = true, Text = "Status" };
            var parent = new Border { Child = badge };
            System.Windows.Documents.TextElement.SetFontSize(parent, 12);
            parent.Measure(new Size(500, 100));
            badge.Measure(new Size(500, 100));
            var original = badge.DesiredSize;
            System.Windows.Documents.TextElement.SetFontSize(parent, 24);
            parent.Measure(new Size(500, 100));
            badge.Measure(new Size(500, 100));
            Assert.True(badge.DesiredSize.Width > original.Width);
            var textSize = badge.DesiredSize;
            badge.Padding = new Thickness(2, 3, 4, 5);
            badge.BorderThickness = new Thickness(1);
            parent.Measure(new Size(500, 100));
            badge.Measure(new Size(500, 100));
            Assert.Equal(textSize.Width + 8, badge.DesiredSize.Width, 5);
            Assert.Equal(textSize.Height + 10, badge.DesiredSize.Height, 5);
            badge.Text = "";
            parent.Measure(new Size(500, 100));
            badge.Measure(new Size(500, 100));
            Assert.Equal(new Size(8, 10), badge.DesiredSize);
        });
    }

    /// <summary>
    /// Checks Fluent and third-party sources without adding child visuals.
    /// </summary>
    [Fact]
    public void Icon_UsesSharedFontSourceAndRevertsToText()
    {
        WpfTestThread.Run(() =>
        {
            var icon = new FluentIconSource(FluentSymbolRegular.Checkmark16, true);
            var badge = new Badge { OverridesDefaultStyle = true, Text = "A long status", Icon = icon, FontSize = 16 };
            byte[] iconPixels = Render(badge, 100, 40);
            Assert.Contains(iconPixels, pixel => pixel != 0);
            badge.Text = "Different text";
            Assert.Equal(iconPixels, Render(badge, 100, 40));
            badge.Icon = new TestFontIcon();
            Assert.NotEqual(iconPixels, Render(badge, 100, 40));
            badge.Icon = null;
            Assert.NotEqual(iconPixels, Render(badge, 100, 40));
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
        });
    }

    /// <summary>
    /// Checks alignment changes and clips overflowing text to the padded rectangle.
    /// </summary>
    [Fact]
    public void AlignmentAndClipping_KeepTextInsidePadding()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge
            {
                OverridesDefaultStyle = true,
                Text = "MMMMMMMMMMMM", FontSize = 24, Padding = new Thickness(8),
                HorizontalContentAlignment = HorizontalAlignment.Right,
                VerticalContentAlignment = VerticalAlignment.Bottom,
            };
            byte[] pixels = Render(badge, 64, 40);
            for (int y = 0; y < 40; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    if (x < 8 || x >= 56 || y < 8 || y >= 32)
                    {
                        Assert.Equal(0, pixels[(y * 64 + x) * 4 + 3]);
                    }
                }
            }

            badge.Text = "A";
            byte[] right = Render(badge, 64, 40);
            badge.HorizontalContentAlignment = HorizontalAlignment.Left;
            badge.VerticalContentAlignment = VerticalAlignment.Top;
            Assert.NotEqual(right, Render(badge, 64, 40));
            badge.FlowDirection = FlowDirection.RightToLeft;
            Assert.Contains(Render(badge, 64, 40), pixel => pixel != 0);
        });
    }

    /// <summary>
    /// Checks severity triggers, resource replacement, and dependency-property precedence.
    /// </summary>
    [Fact]
    public void SeverityStyle_ResolvesDynamicResourcesAndPreservesLocalBrushes()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Controls/Badge/Badge.xaml"),
            };
            var badge = new Badge { OverridesDefaultStyle = true, Style = (Style)resources[typeof(Badge)] };
            badge.Resources[BrushKeys.SystemFillColorInformationalBackgroundBrush] = Brushes.Blue;
            badge.Resources[BrushKeys.SystemFillColorCriticalBackgroundBrush] = Brushes.Red;
            Assert.Same(Brushes.Blue, badge.Background);
            badge.Severity = BadgeSeverity.Critical;
            Assert.Same(Brushes.Red, badge.Background);
            badge.Resources[BrushKeys.SystemFillColorCriticalBackgroundBrush] = Brushes.Green;
            Assert.Same(Brushes.Green, badge.Background);
            badge.Background = Brushes.Purple;
            badge.Severity = BadgeSeverity.Success;
            Assert.Same(Brushes.Purple, badge.Background);
            Render(badge, 50, 30);
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
        });
    }

    /// <summary>
    /// Rejects unsupported values before they enter the drawing calculations.
    /// </summary>
    [Fact]
    public void InvalidDimensions_AreRejectedBeforeLayout()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge();
            Assert.Throws<ArgumentException>(() => badge.Padding = new Thickness(-1));
            Assert.Throws<ArgumentException>(() => badge.BorderThickness = new Thickness(double.NaN));
            Assert.Throws<ArgumentException>(() => badge.CornerRadius = new CornerRadius(double.PositiveInfinity));
            Assert.Throws<ArgumentException>(() => badge.Severity = (BadgeSeverity)99);
        });
    }

    /// <summary>
    /// Renders an element to premultiplied pixels for comparison with native WPF output.
    /// </summary>
    private static byte[] Render(FrameworkElement element, int width, int height)
    {
        element.Width = width;
        element.Height = height;
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0, 0, width, height));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(element);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        return pixels;
    }

    /// <summary>
    /// Reads actual retained drawing commands, including text nested inside clipping groups.
    /// </summary>
    private static void CollectGlyphRuns(Drawing drawing, List<GlyphRun> runs)
    {
        if (drawing is GlyphRunDrawing glyphDrawing)
        {
            runs.Add(glyphDrawing.GlyphRun);
        }
        else if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                CollectGlyphRuns(child, runs);
            }
        }
    }

    /// <summary>
    /// Verifies the actual font retained by the drawing, including after cache invalidation.
    /// </summary>
    private static void AssertBadgeFont(Badge badge, FontFamily family)
    {
        var expected = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
        Assert.True(expected.TryGetGlyphTypeface(out var expectedGlyphTypeface));
        Render(badge, 40, 40);
        var drawing = VisualTreeHelper.GetDrawing(badge);
        var runs = new List<GlyphRun>();
        CollectGlyphRuns(drawing, runs);
        var run = Assert.Single(runs);
        Assert.Equal(expectedGlyphTypeface.FontUri, run.GlyphTypeface.FontUri);
    }

    private sealed class TestFontIcon : FontIconSource
    {
        /// <inheritdoc />
        public override string Glyph => "★";

        /// <inheritdoc />
        public override FontFamily FontFamily { get; } = new FontFamily("Segoe UI Symbol");
    }

    /// <summary>
    /// Uses Badge's constrained sizing policy so both elements draw at the same RenderSize,
    /// including when the border thickness consumes the entire available surface.
    /// </summary>
    private sealed class ConstrainedBorder : Border
    {
        /// <inheritdoc />
        protected override Size MeasureOverride(Size constraint)
        {
            var desired = base.MeasureOverride(constraint);
            return new Size(Math.Min(desired.Width, constraint.Width),
                Math.Min(desired.Height, constraint.Height));
        }
    }
}
