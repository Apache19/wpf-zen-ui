// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Arranges one <see cref="SoftKeyboardRow"/> by the relative width of each <see cref="SoftKeyboardKey"/>.
/// </summary>
/// <remarks>
/// The panel is public so a replacement keyboard template can reuse the default responsive sizing
/// contract. It reads key models from generated item-container data contexts; non-key children use
/// a weight of one. Finite widths are divided proportionally, allowing a ten-key row to contract to
/// tablet portrait widths while space and editing keys retain larger shares.
/// </remarks>
public sealed class SoftKeyboardRowPanel : Panel
{
    /// <summary>Identifies the <see cref="Spacing"/> dependency property.</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(SoftKeyboardRowPanel),
        new FrameworkPropertyMetadata(4d, FrameworkPropertyMetadataOptions.AffectsMeasure),
        static value => value is double spacing && !double.IsNaN(spacing) && !double.IsInfinity(spacing) && spacing >= 0
    );

    /// <summary>Gets or sets the distance between adjacent key slots. The default is 4 DIP.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        UIElement[] children = InternalChildren.Cast<UIElement>()
            .Where(static child => child.Visibility != Visibility.Collapsed)
            .ToArray();
        if (children.Length == 0)
        {
            return default;
        }

        double spacingWidth = Spacing * Math.Max(0, children.Length - 1);
        double totalWeight = children.Sum(GetWeight);
        bool constrained = !double.IsInfinity(availableSize.Width);
        double usableWidth = constrained ? Math.Max(0, availableSize.Width - spacingWidth) : double.PositiveInfinity;
        double desiredWidth = spacingWidth;
        double desiredHeight = 0;

        foreach (UIElement child in children)
        {
            double width = constrained ? usableWidth * GetWeight(child) / totalWeight : double.PositiveInfinity;
            child.Measure(new Size(width, availableSize.Height));
            desiredHeight = Math.Max(desiredHeight, child.DesiredSize.Height);
            if (!constrained)
            {
                desiredWidth += child.DesiredSize.Width;
            }
        }

        return new Size(constrained ? availableSize.Width : desiredWidth, desiredHeight);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        UIElement[] children = InternalChildren.Cast<UIElement>()
            .Where(static child => child.Visibility != Visibility.Collapsed)
            .ToArray();
        double spacingWidth = Spacing * Math.Max(0, children.Length - 1);
        double usableWidth = Math.Max(0, finalSize.Width - spacingWidth);
        double totalWeight = children.Sum(GetWeight);
        double x = 0;

        foreach (UIElement child in children)
        {
            double width = totalWeight <= 0 ? 0 : usableWidth * GetWeight(child) / totalWeight;
            child.Arrange(new Rect(x, 0, width, finalSize.Height));
            x += width + Spacing;
        }

        return finalSize;
    }

    private static double GetWeight(UIElement child) =>
        child is FrameworkElement { DataContext: SoftKeyboardKey key } ? key.Width : 1d;
}
