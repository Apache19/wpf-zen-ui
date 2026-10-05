// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
    /// Tracks only text and icon sources that can contribute visible content to the badge.
    /// </summary>
    [Fact]
    public void HasContent_TracksDrawableTextAndIconGlyphs()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge();
            Assert.False(badge.HasContent);

            badge.Text = "Status";
            Assert.True(badge.HasContent);

            badge.Text = string.Empty;
            Assert.False(badge.HasContent);

            badge.Icon = new FluentIconSource(FluentSymbolRegular.Empty);
            Assert.False(badge.HasContent);

            badge.Icon = new TestFontIcon();
            Assert.True(badge.HasContent);

            badge.Icon = null;
            Assert.False(badge.HasContent);
        });
    }

    /// <summary>
    /// Applies the six-DIP dot defaults only while the badge has no drawable content.
    /// </summary>
    [Fact]
    public void DefaultStyle_EmptyContentMeasuresAsSixDipDot()
    {
        WpfTestThread.Run(() =>
        {
            var badge = CreateStyledBadge();
            badge.Measure(new Size(100, 100));

            Assert.Equal(new Thickness(), badge.Padding);
            Assert.Equal(6, badge.MinWidth);
            Assert.Equal(6, badge.MinHeight);
            Assert.Equal(new Size(6, 6), badge.DesiredSize);

            badge.Text = "1";
            badge.Measure(new Size(100, 100));

            Assert.Equal(new Thickness(4, 2, 4, 2), badge.Padding);
            Assert.Equal(6, badge.MinWidth);
            Assert.Equal(6, badge.MinHeight);
            Assert.True(badge.DesiredSize.Width > 6);
            Assert.True(badge.DesiredSize.Height > 6);
        });
    }

    /// <summary>
    /// Keeps derived-style minimum dimensions above the library's ordinary default setters.
    /// </summary>
    [Fact]
    public void DefaultStyle_DerivedStyleOverridesMinimumDimensions()
    {
        WpfTestThread.Run(() =>
        {
            var style = new Style(typeof(Badge), CreateBadgeStyle());
            style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 9D));
            style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 11D));
            var badge = new Badge { Style = style };
            badge.Measure(new Size(100, 100));

            Assert.Equal(9, badge.MinWidth);
            Assert.Equal(11, badge.MinHeight);
            Assert.Equal(new Size(9, 11), badge.DesiredSize);
        });
    }

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
    /// Checks simultaneous text and icon drawing, source replacement and removal.
    /// </summary>
    [Fact]
    public void Icon_DrawsAlongsideTextWithoutChildVisuals()
    {
        WpfTestThread.Run(() =>
        {
            var icon = new FluentIconSource(FluentSymbolRegular.Checkmark16, true);
            var badge = new Badge { OverridesDefaultStyle = true, Text = "A long status", Icon = icon, FontSize = 16 };
            byte[] iconPixels = Render(badge, 100, 40);
            Assert.Contains(iconPixels, pixel => pixel != 0);
            badge.Text = "Different text";
            Assert.NotEqual(iconPixels, Render(badge, 100, 40));
            badge.Icon = new TestFontIcon();
            Assert.NotEqual(iconPixels, Render(badge, 100, 40));
            badge.Icon = null;
            Assert.NotEqual(iconPixels, Render(badge, 100, 40));
            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
        });
    }

    /// <summary>
    /// Checks automatic capsules in both orientations and circles after resizing the same badge.
    /// </summary>
    [Fact]
    public void CornerRadius_NullAdaptsToRenderSizeAndCanBeRestored()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge { OverridesDefaultStyle = true, Background = Brushes.Blue };
            Assert.Null(badge.CornerRadius);
            var reference = new Border { Background = Brushes.Blue };
            foreach (var size in new[] { new Size(80, 30), new Size(30, 80), new Size(40, 40) })
            {
                int width = (int)size.Width;
                int height = (int)size.Height;
                double radius = Math.Min(width, height) / 2.0;
                reference.CornerRadius = new CornerRadius(radius);
                byte[] expected = Render(reference, width, height);
                Assert.Equal(expected, Render(badge, width, height));
                badge.CornerRadius = new CornerRadius(0);
                Assert.NotEqual(expected, Render(badge, width, height));
                badge.CornerRadius = null;
                Assert.Equal(expected, Render(badge, width, height));
            }
        });
    }

    /// <summary>
    /// Keeps the automatic outside contour circular despite nonuniform border widths.
    /// </summary>
    [Fact]
    public void CornerRadius_AutomaticOuterContourIsIndependentOfBorderThickness()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge
            {
                OverridesDefaultStyle = true, BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(1, 3, 5, 7),
            };
            Render(badge, 40, 40);
            var drawing = VisualTreeHelper.GetDrawing(badge);
            var outline = Assert.IsType<GeometryDrawing>(Assert.Single(drawing.Children));
            var geometry = outline.Geometry;
            // Circle points at equal angular positions must have the same outside boundary.
            Assert.False(geometry.FillContains(new Point(5, 5)));
            Assert.False(geometry.FillContains(new Point(35, 5)));
            Assert.False(geometry.FillContains(new Point(5, 35)));
            Assert.False(geometry.FillContains(new Point(35, 35)));
            Assert.True(geometry.FillContains(new Point(6, 6)));
            Assert.True(geometry.FillContains(new Point(34, 6)));
            Assert.True(geometry.FillContains(new Point(6, 34)));
            Assert.True(geometry.FillContains(new Point(34, 34)));
        });
    }

    /// <summary>
    /// Reserves the gap only for two nonempty layouts, including empty Fluent glyph sources.
    /// </summary>
    [Fact]
    public void IconSpacing_MeasuresCombinedContentAndIgnoresAbsentContent()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge { OverridesDefaultStyle = true, Text = "Status", FontSize = 16 };
            var available = new Size(500, 100);
            badge.Measure(available);
            var textSize = badge.DesiredSize;
            badge.Text = "";
            badge.Icon = new TestFontIcon();
            badge.Measure(available);
            var iconSize = badge.DesiredSize;
            badge.Text = "Status";
            badge.IconSpacing = 7;
            badge.Measure(available);
            Assert.Equal(textSize.Width + iconSize.Width + 7, badge.DesiredSize.Width, 5);
            Assert.Equal(Math.Max(textSize.Height, iconSize.Height), badge.DesiredSize.Height, 5);
            badge.IconSpacing = 13;
            badge.Measure(available);
            Assert.Equal(textSize.Width + iconSize.Width + 13, badge.DesiredSize.Width, 5);
            badge.Icon = new FluentIconSource(FluentSymbolRegular.Empty);
            badge.Measure(available);
            Assert.Equal(textSize, badge.DesiredSize);
            badge.Icon = null;
            badge.Measure(available);
            Assert.Equal(textSize, badge.DesiredSize);
            badge.Text = "";
            badge.Icon = new TestFontIcon();
            badge.Measure(available);
            Assert.Equal(iconSize, badge.DesiredSize);
        });
    }

    /// <summary>
    /// Checks two retained glyph runs and group alignment as spacing changes in either direction.
    /// </summary>
    [Theory]
    [InlineData(FlowDirection.LeftToRight, BadgeIconPlacement.Left)]
    [InlineData(FlowDirection.RightToLeft, BadgeIconPlacement.Left)]
    [InlineData(FlowDirection.LeftToRight, BadgeIconPlacement.Right)]
    [InlineData(FlowDirection.RightToLeft, BadgeIconPlacement.Right)]
    public void IconSpacing_DrawsBothRunsAndCentersTheCombinedRow(FlowDirection direction,
        BadgeIconPlacement placement)
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge
            {
                OverridesDefaultStyle = true, Text = "A", Icon = new TestFontIcon(),
                FontSize = 16, IconSpacing = 4, FlowDirection = direction,
                IconPlacement = placement,
                Padding = new Thickness(8),
            };
            Render(badge, 160, 60);
            var drawing = VisualTreeHelper.GetDrawing(badge);
            var before = new List<GlyphRun>();
            CollectGlyphRuns(drawing, before);
            Assert.Equal(2, before.Count);
            badge.IconSpacing = 14;
            Render(badge, 160, 60);
            drawing = VisualTreeHelper.GetDrawing(badge);
            var after = new List<GlyphRun>();
            CollectGlyphRuns(drawing, after);
            Assert.Equal(2, after.Count);
            double shift = direction == FlowDirection.LeftToRight ? -5 : 5;
            if (placement == BadgeIconPlacement.Right)
            {
                shift = -shift;
            }

            Assert.Equal(before[0].BaselineOrigin.X + shift, after[0].BaselineOrigin.X, 5);
            Assert.Equal(before[1].BaselineOrigin.X - shift, after[1].BaselineOrigin.X, 5);
            Assert.Equal(before[0].BaselineOrigin.Y, after[0].BaselineOrigin.Y, 5);
            Assert.Equal(before[1].BaselineOrigin.Y, after[1].BaselineOrigin.Y, 5);
            var iconBounds = after[0].ComputeInkBoundingBox();
            iconBounds.Offset(after[0].BaselineOrigin.X, after[0].BaselineOrigin.Y);
            var textBounds = after[1].ComputeInkBoundingBox();
            textBounds.Offset(after[1].BaselineOrigin.X, after[1].BaselineOrigin.Y);
            if (shift < 0)
            {
                Assert.True(iconBounds.Right < textBounds.Left);
            }
            else
            {
                Assert.True(textBounds.Right < iconBounds.Left);
            }

            Assert.Equal(0, VisualTreeHelper.GetChildrenCount(badge));
        });
    }

    /// <summary>
    /// Verifies inherited presentation settings, local overrides and live fallback in drawings.
    /// </summary>
    [Fact]
    public void IconPresentation_SharesAttachedPropertiesAndFallsBackToTextProperties()
    {
        WpfTestThread.Run(() =>
        {
            Assert.Same(IconPresenter.IconSizeProperty, Badge.IconSizeProperty);
            Assert.Same(IconPresenter.IconForegroundProperty, Badge.IconForegroundProperty);
            var badge = new Badge
            {
                OverridesDefaultStyle = true, Text = "A", Icon = new TestFontIcon(),
                FontSize = 16, Foreground = Brushes.Blue,
            };
            var host = new Border { Child = badge };
            AssertIconPresentation(badge, 16, Brushes.Blue, 16, Brushes.Blue);
            IconPresenter.SetIconSize(host, 24);
            IconPresenter.SetIconForeground(host, Brushes.Red);
            AssertIconPresentation(badge, 24, Brushes.Red, 16, Brushes.Blue);
            badge.IconSize = 32;
            badge.IconForeground = Brushes.Green;
            badge.FontSize = 18;
            badge.Foreground = Brushes.Purple;
            AssertIconPresentation(badge, 32, Brushes.Green, 18, Brushes.Purple);
            badge.ClearValue(Badge.IconSizeProperty);
            badge.ClearValue(Badge.IconForegroundProperty);
            AssertIconPresentation(badge, 24, Brushes.Red, 18, Brushes.Purple);
            badge.IconSize = double.NaN;
            badge.IconForeground = null;
            AssertIconPresentation(badge, 18, Brushes.Purple, 18, Brushes.Purple);
            badge.FontSize = 20;
            badge.Foreground = Brushes.Orange;
            AssertIconPresentation(badge, 20, Brushes.Orange, 20, Brushes.Orange);
            IconPresenter.SetIconSize(badge, 28);
            IconPresenter.SetIconForeground(badge, Brushes.Black);
            AssertIconPresentation(badge, 28, Brushes.Black, 20, Brushes.Orange);
        });
    }

    /// <summary>
    /// Checks that resizing the icon updates desired size while preserving text metrics.
    /// </summary>
    [Fact]
    public void IconSize_UpdatesCombinedMeasurement()
    {
        WpfTestThread.Run(() =>
        {
            var badge = new Badge
            {
                OverridesDefaultStyle = true, Text = "A", Icon = new TestFontIcon(), FontSize = 12,
            };
            var available = new Size(500, 100);
            badge.Measure(available);
            var original = badge.DesiredSize;
            badge.IconSize = 48;
            badge.Measure(available);
            Assert.True(badge.DesiredSize.Width > original.Width);
            Assert.True(badge.DesiredSize.Height > original.Height);
            badge.IconSize = double.NaN;
            badge.Measure(available);
            Assert.Equal(original, badge.DesiredSize);
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
            Assert.Null(badge.CornerRadius);
            badge.Resources[BrushKeys.SystemFillColorInformationalForegroundBrush] = Brushes.Blue;
            badge.Resources[BrushKeys.SystemFillColorCriticalForegroundBrush] = Brushes.Red;
            Assert.Same(Brushes.Blue, badge.Background);
            badge.Severity = BadgeSeverity.Critical;
            Assert.Same(Brushes.Red, badge.Background);
            badge.Resources[BrushKeys.SystemFillColorCriticalForegroundBrush] = Brushes.Green;
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
            Assert.Throws<ArgumentException>(() => badge.IconSpacing = -1);
            Assert.Throws<ArgumentException>(() => badge.IconSpacing = double.NaN);
            Assert.Throws<ArgumentException>(() => badge.IconSpacing = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => badge.IconSize = 0);
            Assert.Throws<ArgumentException>(() => badge.IconSize = -1);
            Assert.Throws<ArgumentException>(() => badge.IconSize = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => badge.IconPlacement = (BadgeIconPlacement)99);
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
    /// Loads the production theme explicitly because these tests run without an application host.
    /// </summary>
    private static Badge CreateStyledBadge()
    {
        return new Badge { Style = CreateBadgeStyle() };
    }

    /// <summary>
    /// Loads the production badge style explicitly for tests that run without an application host.
    /// </summary>
    private static Style CreateBadgeStyle()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        var applicationUri = new Uri("application:///", UriKind.Absolute);
        var resourceUri = new Uri(
            "/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative
        );
        var themeUri = System.IO.Packaging.PackUriHelper.Create(applicationUri, resourceUri);
        var resources = new ResourceDictionary { Source = themeUri };
        return Assert.IsType<Style>(resources[typeof(Badge)]);
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

    /// <summary>
    /// Inspects retained glyph sizes and brushes to verify text and icon styling independently.
    /// </summary>
    private static void AssertIconPresentation(Badge badge, double iconSize, Brush iconBrush,
        double textSize, Brush textBrush)
    {
        Render(badge, 200, 80);
        var drawing = VisualTreeHelper.GetDrawing(badge);
        var runs = new List<GlyphRunDrawing>();
        CollectGlyphDrawings(drawing, runs);
        Assert.Equal(2, runs.Count);
        Assert.Equal(iconSize, runs[0].GlyphRun.FontRenderingEmSize);
        Assert.Equal(iconBrush, runs[0].ForegroundBrush);
        Assert.Equal(textSize, runs[1].GlyphRun.FontRenderingEmSize);
        Assert.Equal(textBrush, runs[1].ForegroundBrush);
    }

    /// <summary>
    /// Collects glyph drawings with their brushes through the badge's clipping groups.
    /// </summary>
    private static void CollectGlyphDrawings(Drawing drawing, List<GlyphRunDrawing> runs)
    {
        if (drawing is GlyphRunDrawing glyphDrawing)
        {
            runs.Add(glyphDrawing);
        }
        else if (drawing is DrawingGroup group)
        {
            foreach (var child in group.Children)
            {
                CollectGlyphDrawings(child, runs);
            }
        }
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
