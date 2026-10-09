// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Stacks variable-sized containers with painted spacing and flat-list virtualization.
/// </summary>
/// <remarks>
/// <para>
/// Use as an ItemsControl.ItemsPanel with ScrollViewer.CanContentScroll enabled. Both orientations,
/// item and pixel scrolling, cache lengths and Standard/Recycling modes are supported. Offscreen
/// sizes are estimated from measured containers, so pixel extents become more accurate as items
/// are visited. Changes to unrealized content are measured when that content is realized again.
/// </para>
/// <para>
/// Spacing is additional to margins and appears only between non-collapsed slots. Hidden items
/// retain a slot. Child properties and bindings are left intact. Without a finite scrolling
/// viewport or with virtualization disabled, all containers are measured. Hierarchical
/// virtualization is not supported: grouped views and TreeView hosts use complete layout,
/// including when IsVirtualizingWhenGrouping is enabled. Use native panels for virtualized trees
/// and groups. Mixing this panel into a native virtualizing hierarchy is not supported.
/// </para>
/// </remarks>
public partial class UiVirtualizingStackPanel : VirtualizingPanel, IScrollInfo
{
    private readonly List<Rect> _gaps = [];

    /// <summary>
    /// Identifies the <see cref="Orientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OrientationProperty
        = UiStackPanel.OrientationProperty.AddOwner(
            typeof(UiVirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                Orientation.Vertical,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    /// <summary>
    /// Gets or sets the stacking direction. The default is Vertical.
    /// </summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="Spacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingProperty
        = UiStackPanel.SpacingProperty.AddOwner(
            typeof(UiVirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                0D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    /// <summary>
    /// Gets or sets the finite, nonnegative distance between item slots. The default is zero.
    /// The distance is included in virtualized size estimates and scrolling geometry.
    /// </summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SpacingBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SpacingBrushProperty
        = UiStackPanel.SpacingBrushProperty.AddOwner(
            typeof(UiVirtualizingStackPanel),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    /// <summary>
    /// Gets or sets the brush filling gap slots across the viewport. The default is null.
    /// Painting is clipped to the panel and does not fill child margins or unused trailing space.
    /// </summary>
    public Brush? SpacingBrush
    {
        get => (Brush?)GetValue(SpacingBrushProperty);
        set => SetValue(SpacingBrushProperty, value);
    }

    private static readonly DependencyPropertyKey IsVirtualizationActivePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(IsVirtualizationActive),
            typeof(bool),
            typeof(UiVirtualizingStackPanel),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only <see cref="IsVirtualizationActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsVirtualizationActiveProperty
        = IsVirtualizationActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the most recent measure used a finite viewport to limit realized containers.
    /// </summary>
    public bool IsVirtualizationActive => (bool)GetValue(IsVirtualizationActiveProperty);

    /// <inheritdoc />
    protected override bool CanHierarchicallyScrollAndVirtualizeCore => false;

    /// <inheritdoc />
    protected override bool HasLogicalOrientation => true;

    /// <inheritdoc />
    protected override Orientation LogicalOrientation => Orientation;

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        // Access InternalChildren first: WPF connects the panel to its item generator here.
        var children = InternalChildren;
        var owner    = ItemsControl.GetItemsOwner(this);

        var itemCount = children.Count;
        if (owner is not null)
        {
            var generator = (ItemContainerGenerator)ItemContainerGenerator;
            itemCount = generator.Items.Count;
        }

        var childConstraint = GetChildMeasureConstraint(availableSize);

        PrepareMeasure(owner, itemCount, childConstraint, availableSize);

        if (owner is null)
        {
            MeasureDirectChildren(children, childConstraint);
        }
        else if (!IsVirtualizationActive)
        {
            MeasureAllContainers(childConstraint);
        }
        else if (itemCount > 0)
        {
            MeasureVisibleContainers(owner, childConstraint);
        }

        return CompleteMeasure(availableSize);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _gaps.Clear();

        var breadth = StackSpacing.Across(finalSize, Orientation);

        foreach (UIElement child in InternalChildren)
        {
            if (!_measuredIndices.TryGetValue(child, out var index) || index >= _itemSizes.Count)
            {
                continue;
            }

            var itemStart = _itemSizes.Start(index, Spacing);
            var position  = itemStart - _stackOffset;

            if (_itemSizes.IsCollapsed(index))
            {
                child.Arrange(new Rect());
                continue;
            }

            // A leading gap belongs to this slot, including when its predecessor is offscreen.
            if (Spacing > 0 && itemStart > 0)
            {
                var gapStart = position - Spacing;
                var gap      = StackSpacing.Rect(gapStart, 0, Spacing, breadth, Orientation);

                _gaps.Add(gap);
            }

            var length       = _itemSizes.Length(index);
            var childBreadth = Math.Max(breadth, _crossExtent);
            var bounds       = StackSpacing.Rect(position, -_crossOffset, length, childBreadth, Orientation);

            child.Arrange(bounds);
        }

        InvalidateVisual();
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        PanelSpacing.Draw(drawingContext, SpacingBrush, _gaps, RenderSize);
    }
}