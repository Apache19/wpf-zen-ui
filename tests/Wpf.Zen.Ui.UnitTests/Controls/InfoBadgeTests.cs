// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies the badge's content pipeline and shared appearance settings at runtime.
/// </summary>
public class InfoBadgeTests
{
    /// <summary>
    /// Ensures the default presenter forwards native content templates and formatting.
    /// </summary>
    [Fact]
    public void DefaultStyle_ForwardsContentPresentationProperties()
    {
        WpfTestThread.Run(() =>
        {
            var resources = LoadStyles();
            var template = new DataTemplate();
            var badge = new InfoBadge
            {
                Style = (Style)resources[typeof(InfoBadge)],
                Content = 42,
                ContentTemplate = template,
                ContentStringFormat = "{0:D3}",
                HorizontalContentAlignment = HorizontalAlignment.Right,
            };

            badge.ApplyTemplate();
            var border = Assert.IsType<Border>(VisualTreeHelper.GetChild(badge, 0));
            var presenter = Assert.IsType<ContentPresenter>(badge.Template.FindName("ContentPresenter", badge));

            Assert.Equal(42, presenter.Content);
            Assert.Same(template, presenter.ContentTemplate);
            Assert.Equal("{0:D3}", presenter.ContentStringFormat);
            var layout = Assert.IsType<Grid>(border.Child);
            Assert.Equal(HorizontalAlignment.Right, layout.HorizontalAlignment);

            badge.ContentTemplate = null;
            badge.Content = "Updated";
            Assert.Null(presenter.ContentTemplate);
            Assert.Equal("Updated", presenter.Content);
        });
    }

    /// <summary>
    /// Checks that local appearance values are preserved as severity changes.
    /// </summary>
    [Fact]
    public void DefaultStyle_HonorsLocalAppearanceAndDoesNotMultiplyOpacity()
    {
        WpfTestThread.Run(() =>
        {
            var resources = LoadStyles();
            var badge = new InfoBadge
            {
                Style = (Style)resources[typeof(InfoBadge)],
                Background = Brushes.Purple,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5),
                Opacity = 0.5,
            };

            badge.ApplyTemplate();
            badge.Severity = BadgeSeverity.Critical;
            var border = Assert.IsType<Border>(VisualTreeHelper.GetChild(badge, 0));

            Assert.Same(Brushes.Purple, border.Background);
            Assert.Same(Brushes.Black, border.BorderBrush);
            Assert.Equal(badge.BorderThickness, border.BorderThickness);
            Assert.Equal(badge.CornerRadius, border.CornerRadius);
            Assert.Equal(badge.Padding, border.Padding);
            Assert.Equal(1.0, border.Opacity);
        });
    }

    /// <summary>
    /// Verifies icon ordering and spacing as the two content slots change.
    /// </summary>
    [Fact]
    public void IconPlacement_UpdatesOrderAndOptionalSpacing()
    {
        WpfTestThread.Run(() =>
        {
            var resources = LoadStyles();
            var badge = new InfoBadge
            {
                Style = (Style)resources[typeof(InfoBadge)],
                Icon = new Border { Width = 12, Height = 12 },
                Content = "Status",
            };
            badge.ApplyTemplate();
            var icon = (ContentPresenter)badge.Template.FindName("IconPresenter", badge);
            var content = (ContentPresenter)badge.Template.FindName("ContentPresenter", badge);
            var spacing = (Border)badge.Template.FindName("IconSpacing", badge);

            Assert.Same(badge.Icon, icon.Content);
            Assert.Equal(0, Grid.GetColumn(icon));
            Assert.Equal(2, Grid.GetColumn(content));
            Assert.Equal(Visibility.Visible, spacing.Visibility);

            badge.IconPlacement = BadgeIconPlacement.Right;
            Assert.Equal(2, Grid.GetColumn(icon));
            Assert.Equal(0, Grid.GetColumn(content));

            badge.Content = null;
            Assert.Equal(Visibility.Collapsed, spacing.Visibility);
            badge.ContentTemplate = new DataTemplate();
            Assert.Equal(Visibility.Visible, spacing.Visibility);
            badge.ContentTemplate = null;
            badge.Content = "";
            Assert.Equal(Visibility.Collapsed, spacing.Visibility);
            badge.Content = "Status";
            badge.Icon = null;
            Assert.Equal(Visibility.Collapsed, icon.Visibility);
            Assert.Equal(Visibility.Collapsed, spacing.Visibility);
            Assert.Throws<ArgumentException>(() => badge.IconPlacement = (BadgeIconPlacement)99);
        });
    }

    /// <summary>
    /// Empty badges retain the size and rounded border selected by the caller and parent.
    /// </summary>
    [Fact]
    public void EmptyContent_PreservesExplicitSizeAndStretchLayout()
    {
        WpfTestThread.Run(() =>
        {
            var resources = LoadStyles();
            var badge = new InfoBadge
            {
                Style = (Style)resources[typeof(InfoBadge)],
                CornerRadius = new CornerRadius(4),
            };
            var parent = new Grid { Width = 120, Height = 40 };
            parent.Children.Add(badge);
            parent.Measure(new Size(120, 40));
            parent.Arrange(new Rect(0, 0, 120, 40));

            Assert.Equal(120, badge.ActualWidth);
            Assert.Equal(40, badge.ActualHeight);
            Assert.Equal(new CornerRadius(4), badge.CornerRadius);

            badge.Width = 6;
            badge.Height = 6;
            badge.Padding = new Thickness(0);
            badge.CornerRadius = new CornerRadius(3);
            parent.Measure(new Size(120, 40));
            parent.Arrange(new Rect(0, 0, 120, 40));

            Assert.Equal(6, badge.ActualWidth);
            Assert.Equal(6, badge.ActualHeight);
        });
    }
    /// <summary>
    /// Loads the compiled dictionary so tests exercise WPF template bindings.
    /// </summary>
    private static ResourceDictionary LoadStyles()
    {
        return new ResourceDictionary
        {
            Source = new Uri(
                "pack://application:,,,/Wpf.Zen.Ui;component/Controls/InfoBadge/InfoBadge.xaml"
            ),
        };
    }
}
