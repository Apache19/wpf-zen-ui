// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Docks children in collection order with optional painted spacing between layout slots.
/// </summary>
/// <remarks>
/// Spacing is additional to child margins and follows each participating child that has a later
/// participating sibling. Collapsed children do not participate; Hidden and zero-sized children
/// do. The inherited LastChildFill property still applies to the last child in the collection,
/// including when that child is Collapsed. Dock values retain their native WPF meaning.
/// </remarks>
public class UiDockPanel : DockPanel
{
    private readonly List<Rect> _gaps = [];

    /// <summary>
    /// Identifies the <see cref="Spacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing),
        typeof(double),
        typeof(UiDockPanel),
        new FrameworkPropertyMetadata(0D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        PanelSpacing.IsValid
    );

    /// <summary>
    /// Gets or sets the finite, nonnegative gap in device-independent units. The default is zero.
    /// Left and Right docking consume horizontal spacing; Top and Bottom consume vertical spacing.
    /// No extra spacing is added at the panel's outer edges or after the last participating child.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SpacingBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingBrushProperty = DependencyProperty.Register(
        nameof(SpacingBrush),
        typeof(Brush),
        typeof(UiDockPanel),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>
    /// Gets or sets the gap fill brush. The default is null, leaving the background visible.
    /// Gaps span the remaining region perpendicular to the docking direction and are painted
    /// above Background and below children. Margins and unused fill space are not gap regions.
    /// </summary>
    public Brush? SpacingBrush
    {
        get => (Brush?)GetValue(SpacingBrushProperty);
        set => SetValue(SpacingBrushProperty, value);
    }

    /// <summary>
    /// Measures children against the remaining space, including preceding docked slots and gaps.
    /// </summary>
    /// <param name="constraint">
    /// The available size, which may be infinite in either dimension.
    /// </param>
    /// <returns>The desired size of the docked children and intervening gaps.</returns>
    protected override Size MeasureOverride(Size constraint)
    {
        var lastParticipant = FindLastParticipant();

        var usedWidth    = 0D;
        var usedHeight   = 0D;
        var desiredWidth = 0D;
        var desiredHeight = 0D;

        // Match WPF DockPanel's sequential measurement, including measurement of the fill child
        // according to its Dock value. Only the space consumed between slots is extended.
        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];

            var remainingWidth  = Math.Max(0, constraint.Width - usedWidth);
            var remainingHeight = Math.Max(0, constraint.Height - usedHeight);

            var available = new Size(remainingWidth, remainingHeight);

            child.Measure(available);

            if (child.Visibility == Visibility.Collapsed)
            {
                continue;
            }

            var gap     = index < lastParticipant ? Spacing : 0;
            var desired = child.DesiredSize;
            var dock    = GetDock(child);

            if (dock == Dock.Left || dock == Dock.Right)
            {
                desiredHeight = Math.Max(desiredHeight, usedHeight + desired.Height);
                usedWidth    += desired.Width + gap;
            }
            else
            {
                desiredWidth = Math.Max(desiredWidth, usedWidth + desired.Width);
                usedHeight  += desired.Height + gap;
            }
        }

        desiredWidth  = Math.Max(desiredWidth, usedWidth);
        desiredHeight = Math.Max(desiredHeight, usedHeight);
        return new Size(desiredWidth, desiredHeight);
    }

    /// <summary>
    /// Arranges native docking slots and records the actual separators for subsequent painting.
    /// </summary>
    /// <param name="arrangeSize">The final panel size.</param>
    /// <returns>The supplied final size.</returns>
    protected override Size ArrangeOverride(Size arrangeSize)
    {
        _gaps.Clear();

        var lastParticipant = FindLastParticipant();
        var nonFillCount    = InternalChildren.Count - (LastChildFill ? 1 : 0);

        var left   = 0D;
        var top    = 0D;
        var right  = 0D;
        var bottom = 0D;

        for (var index = 0; index < InternalChildren.Count; index++)
        {
            var child = InternalChildren[index];
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(new Rect());
                continue;
            }

            var remainingWidth  = Math.Max(0, arrangeSize.Width - left - right);
            var remainingHeight = Math.Max(0, arrangeSize.Height - top - bottom);

            var bounds    = new Rect(left, top, remainingWidth, remainingHeight);
            var remaining = bounds;

            var gap       = index < lastParticipant ? Spacing : 0;
            var separator = Rect.Empty;

            if (index < nonFillCount)
            {
                var desired = child.DesiredSize;

                switch (GetDock(child))
                {
                    case Dock.Left:
                        bounds.Width = desired.Width;
                        left += desired.Width;
                        separator = new Rect(left, top, gap, remainingHeight);
                        left += gap;
                        break;

                    case Dock.Right:
                        right += desired.Width;
                        bounds.X = Math.Max(0, arrangeSize.Width - right);
                        bounds.Width = desired.Width;
                        separator = new Rect(bounds.X - gap, top, gap, remainingHeight);
                        right += gap;
                        break;

                    case Dock.Top:
                        bounds.Height = desired.Height;
                        top += desired.Height;
                        separator = new Rect(left, top, remainingWidth, gap);
                        top += gap;
                        break;
                    case Dock.Bottom:
                        bottom += desired.Height;
                        bounds.Y = Math.Max(0, arrangeSize.Height - bottom);
                        bounds.Height = desired.Height;
                        separator = new Rect(left, bounds.Y - gap, remainingWidth, gap);
                        bottom += gap;
                        break;
                }
            }

            if (gap > 0 && !separator.IsEmpty)
            {
                // An oversized child can consume more than the remaining region. Restrict its
                // separator to that region so earlier docked slots never become painted gaps.
                separator.Intersect(remaining);
                _gaps.Add(separator);
            }

            child.Arrange(bounds);
        }

        // Child geometry can change while RenderSize stays constant.
        InvalidateVisual();
        return arrangeSize;
    }

    /// <summary>
    /// Paints arranged gaps over the native panel background, clipped to the panel bounds.
    /// </summary>
    /// <param name="drawingContext">The drawing context for this panel.</param>
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        PanelSpacing.Draw(drawingContext, SpacingBrush, _gaps, RenderSize);
    }

    /// <summary>
    /// Finds the final slot that can contribute spacing, without changing native fill ownership.
    /// </summary>
    private int FindLastParticipant()
    {
        for (var index = InternalChildren.Count - 1; index >= 0; index--)
        {
            if (InternalChildren[index].Visibility != Visibility.Collapsed)
            {
                return index;
            }
        }

        return -1;
    }
}