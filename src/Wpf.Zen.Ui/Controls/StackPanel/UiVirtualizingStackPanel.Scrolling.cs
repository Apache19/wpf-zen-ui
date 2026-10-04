// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Implements axis-aware scroll commands and conversion between item and pixel offsets.
/// </summary>
public partial class UiVirtualizingStackPanel
{
    // Internal geometry is always in DIPs: stack follows Orientation, cross is perpendicular.
    // Logical item units are exposed only through IScrollInfo and converted at that boundary.
    private double _stackOffset;
    private double _stackViewport;
    private double _crossExtent;
    private double _crossOffset;
    private double _crossViewport;
    private bool   _usesPixelScrolling;

    // Requests remain pending until measurement has corrected estimated sizes.
    private bool _scrollToEnd;
    private int? _requestedItemIndex;

    private bool CanScrollAlong
    {
        get => Orientation == Orientation.Vertical ? CanVerticallyScroll : CanHorizontallyScroll;
    }

    private bool CanScrollAcross
    {
        get => Orientation == Orientation.Vertical ? CanHorizontallyScroll : CanVerticallyScroll;
    }

    private (double Extent, double Viewport, double Offset, double CrossExtent,
        double CrossViewport, double CrossOffset) _publishedScrollInfo;

    /// <inheritdoc />
    public bool CanHorizontallyScroll
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateMeasure();
            }
        }
    }

    /// <inheritdoc />
    public bool CanVerticallyScroll
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateMeasure();
            }
        }
    }

    /// <inheritdoc />
    public ScrollViewer? ScrollOwner
    {
        get;
        set
        {
            if (!ReferenceEquals(field, value))
            {
                field = value;
                InvalidateMeasure();
            }
        }
    }

    private double AlongExtent
    {
        get => _usesPixelScrolling ? _itemSizes.Extent(Spacing) : _itemCounts.Extent(0);
    }

    // Fractional terminal units let the final item meet the viewport edge even when item lengths
    // differ. Line commands still select item boundaries. Cross-axis units are always pixels.
    private double AlongViewport
    {
        get
        {
            if (_usesPixelScrolling)
            {
                return _stackViewport;
            }

            var physicalExtent = _itemSizes.Extent(Spacing);
            var maximumOffset  = Math.Max(0, physicalExtent - _stackViewport);
            var logicalOffset  = PixelToItemOffset(maximumOffset);
            var logicalExtent  = _itemCounts.Extent(0);

            return logicalExtent - logicalOffset;
        }
    }

    private double AlongOffset
    {
        get => _usesPixelScrolling ? _stackOffset : PixelToItemOffset(_stackOffset);
    }

    /// <inheritdoc />
    public double ExtentWidth
    {
        get => Orientation == Orientation.Horizontal ? AlongExtent : _crossExtent;
    }

    /// <inheritdoc />
    public double ExtentHeight
    {
        get => Orientation == Orientation.Vertical ? AlongExtent : _crossExtent;
    }

    /// <inheritdoc />
    public double ViewportWidth
    {
        get => Orientation == Orientation.Horizontal ? AlongViewport : _crossViewport;
    }

    /// <inheritdoc />
    public double ViewportHeight
    {
        get => Orientation == Orientation.Vertical ? AlongViewport : _crossViewport;
    }

    /// <inheritdoc />
    public double HorizontalOffset
    {
        get => Orientation == Orientation.Horizontal ? AlongOffset : _crossOffset;
    }

    /// <inheritdoc />
    public double VerticalOffset
    {
        get => Orientation == Orientation.Vertical ? AlongOffset : _crossOffset;
    }

    /// <inheritdoc />
    public void SetHorizontalOffset(double offset)
    {
        SetOffset(offset, Orientation == Orientation.Horizontal);
    }

    /// <inheritdoc />
    public void SetVerticalOffset(double offset)
    {
        SetOffset(offset, Orientation == Orientation.Vertical);
    }

    /// <inheritdoc />
    public void LineUp()
    {
        ScrollBy(horizontal: false, direction: -1, page: false);
    }

    /// <inheritdoc />
    public void LineDown()
    {
        ScrollBy(horizontal: false, direction: 1, page: false);
    }

    /// <inheritdoc />
    public void LineLeft()
    {
        ScrollBy(horizontal: true, direction: -1, page: false);
    }

    /// <inheritdoc />
    public void LineRight()
    {
        ScrollBy(horizontal: true, direction: 1, page: false);
    }

    /// <inheritdoc />
    public void PageUp()
    {
        ScrollBy(horizontal: false, direction: -1, page: true);
    }

    /// <inheritdoc />
    public void PageDown()
    {
        ScrollBy(horizontal: false, direction: 1, page: true);
    }

    /// <inheritdoc />
    public void PageLeft()
    {
        ScrollBy(horizontal: true, direction: -1, page: true);
    }

    /// <inheritdoc />
    public void PageRight()
    {
        ScrollBy(horizontal: true, direction: 1, page: true);
    }

    /// <inheritdoc />
    public void MouseWheelUp()
    {
        ScrollByWheel(horizontal: false, direction: -1);
    }

    /// <inheritdoc />
    public void MouseWheelDown()
    {
        ScrollByWheel(horizontal: false, direction: 1);
    }

    /// <inheritdoc />
    public void MouseWheelLeft()
    {
        ScrollByWheel(horizontal: true, direction: -1);
    }

    /// <inheritdoc />
    public void MouseWheelRight()
    {
        ScrollByWheel(horizontal: true, direction: 1);
    }

    /// <summary>
    /// Scrolls the requested descendant rectangle into view on both axes and returns its visible
    /// portion in panel coordinates. Unrelated visuals and empty rectangles are ignored.
    /// </summary>
    /// <param name="visual">A realized descendant of this panel.</param>
    /// <param name="rectangle">The requested bounds in the descendant's coordinate space.</param>
    /// <returns>The portion of the requested rectangle within the resulting viewport.</returns>
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        if (rectangle.IsEmpty || visual is null || visual == this || !IsAncestorOf(visual))
        {
            return Rect.Empty;
        }

        var transform = visual.TransformToAncestor(this);
        var bounds    = transform.TransformBounds(rectangle);

        var oldAlong  = _stackOffset;
        var oldAcross = _crossOffset;

        var start      = Orientation == Orientation.Vertical ? bounds.Y : bounds.X;
        var crossStart = Orientation == Orientation.Vertical ? bounds.X : bounds.Y;

        if (CanScrollAlong)
        {
            var length    = StackSpacing.Along(bounds.Size, Orientation);
            var requested = GetRevealedOffset(oldAlong, start, length, _stackViewport);
            var extent    = _itemSizes.Extent(Spacing);

            _stackOffset = ClampOffset(requested, extent, _stackViewport);
        }

        if (CanScrollAcross)
        {
            var breadth   = StackSpacing.Across(bounds.Size, Orientation);
            var requested = GetRevealedOffset(oldAcross, crossStart, breadth, _crossViewport);

            _crossOffset = ClampOffset(requested, _crossExtent, _crossViewport);
        }

        _scrollToEnd = false;

        PublishScrollInfo();
        InvalidateMeasure();

        var alongShift = oldAlong - _stackOffset;
        var crossShift = oldAcross - _crossOffset;

        var horizontalShift = Orientation == Orientation.Vertical ? crossShift : alongShift;
        var verticalShift   = Orientation == Orientation.Vertical ? alongShift : crossShift;

        bounds.Offset(horizontalShift, verticalShift);

        var viewportSize   = StackSpacing.Size(_stackViewport, _crossViewport, Orientation);
        var viewportBounds = new Rect(viewportSize);

        bounds.Intersect(viewportBounds);

        return bounds;
    }

    /// <summary>
    /// Realizes an item requested by ItemsControl.ScrollIntoView and aligns its slot with the
    /// viewport start, clamping near the collection end. The next layout measures its actual size.
    /// </summary>
    /// <param name="index">The zero-based leaf index, including when the view is grouped.</param>
    protected override void BringIndexIntoView(int index)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        if (owner?.Items.Groups is not null)
        {
            if (index < 0 || index >= owner.Items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            // Grouped views use complete layout. Bring an already generated leaf into view
            // without interpreting its flattened index as an index into the group sequence.
            UpdateLayout();

            var ownerGenerator = owner.ItemContainerGenerator;
            var element        = ownerGenerator.ContainerFromIndex(index) as FrameworkElement;

            element?.BringIntoView();

            return;
        }

        var count = InternalChildren.Count;
        if (IsItemsHost)
        {
            var generator = (ItemContainerGenerator)ItemContainerGenerator;
            count = generator.Items.Count;
        }

        if (index < 0 || index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        _requestedItemIndex = index;

        InvalidateMeasure();
        UpdateLayout();

        if (!IsVirtualizationActive && IsItemsHost)
        {
            var generator = (ItemContainerGenerator)ItemContainerGenerator;
            var element   = generator.ContainerFromIndex(index) as FrameworkElement;

            element?.BringIntoView();
        }
    }

    /// <summary>
    /// Returns an immediate child's offset from the start of this panel, independent of scrolling.
    /// Pixel mode includes spacing; item mode counts preceding non-collapsed slots.
    /// Descendants within a slot are not included in the logical count.
    /// </summary>
    /// <param name="child">The immediate container whose offset is requested.</param>
    /// <returns>The offset in the active scroll unit, or zero for an unrelated child.</returns>
    protected override double GetItemOffsetCore(UIElement child)
    {
        if (child is null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        int index;
        if (IsItemsHost)
        {
            var generator = (ItemContainerGenerator)ItemContainerGenerator;
            index = generator.IndexFromContainer(child, true);
        }
        else
        {
            index = InternalChildren.IndexOf(child);
        }

        if (index < 0 || index >= _itemSizes.Count)
        {
            return 0;
        }

        return _usesPixelScrolling
            ? _itemSizes.Start(index, Spacing)
            : _itemCounts.Start(index, 0);
    }

    /// <summary>
    /// Applies the system wheel setting, including the special setting for page scrolling.
    /// </summary>
    private void ScrollByWheel(bool horizontal, int direction)
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines < 0)
        {
            ScrollBy(horizontal, direction, page: true);
        }
        else if (lines > 0)
        {
            ScrollBy(horizontal, direction * lines, page: false);
        }
    }

    /// <summary>
    /// Chooses a line or page increment in the exposed scroll unit. Logical line movement snaps
    /// to item boundaries; pixel movement and the cross axis use a fixed physical line increment.
    /// </summary>
    private void ScrollBy(bool horizontal, int direction, bool page)
    {
        var    along       = horizontal == (Orientation == Orientation.Horizontal);
        var    offset      = along ? AlongOffset : _crossOffset;
        var    logicalLine = along && !_usesPixelScrolling && !page;

        double amount;

        if (page)
        {
            amount = along ? AlongViewport : _crossViewport;
        }
        else
        {
            amount = logicalLine ? 1 : 16;
        }

        if (logicalLine)
        {
            offset = direction < 0 ? Math.Ceiling(offset) : Math.Floor(offset);
        }

        SetOffset(offset + (direction * amount), along);
    }

    /// <summary>
    /// Converts the requested scroll unit to a physical offset and schedules realization. An end
    /// request remains pending through measurement so corrected estimates still reveal the end.
    /// </summary>
    private void SetOffset(double offset, bool along)
    {
        if (double.IsNaN(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        if (along)
        {
            _scrollToEnd = double.IsPositiveInfinity(offset);

            var requested     = _usesPixelScrolling ? offset : ItemToPixelOffset(offset);
            var extent        = _itemSizes.Extent(Spacing);
            var clampedOffset = ClampOffset(requested, extent, _stackViewport);

            _stackOffset = CanScrollAlong ? clampedOffset : 0;
        }
        else
        {
            _crossOffset = CanScrollAcross ? ClampOffset(offset, _crossExtent, _crossViewport) : 0;
        }

        PublishScrollInfo();
        InvalidateMeasure();
    }

    /// <summary>
    /// Computes the smallest physical movement revealing a viewport-relative target rectangle.
    /// A target spanning both viewport edges already fills the view and leaves the offset intact.
    /// </summary>
    private static double GetRevealedOffset(double offset, double start, double length, double viewport)
    {
        if (start <= 0 && start + length >= viewport)
        {
            return offset;
        }

        if (start < 0)
        {
            return offset + start;
        }

        if (start + length <= viewport)
        {
            return offset;
        }

        var overflow = start + length - viewport;
        var movement = Math.Min(start, overflow);

        return offset + movement;
    }

    /// <summary>
    /// Converts a physical content offset to a possibly fractional item offset using slot sizes.
    /// Unknown slots use the measured average; collapsed slots contribute zero logical units.
    /// </summary>
    private double PixelToItemOffset(double pixel)
    {
        if (_itemSizes.Count == 0 || pixel <= 0)
        {
            return 0;
        }

        if (pixel >= _itemSizes.Extent(Spacing))
        {
            return _itemCounts.Extent(0);
        }

        var index           = _itemSizes.Find(pixel, Spacing);
        var trailingSpacing = index + 1 < _itemSizes.Count ? Spacing : 0;
        var itemLength      = _itemSizes.Length(index);
        var slotLength      = itemLength + trailingSpacing;
        var localPixels     = pixel - _itemSizes.Start(index, Spacing);
        var fraction        = slotLength > 0 ? localPixels / slotLength : 0;
        var logicalStart    = _itemCounts.Start(index, 0);
        var logicalLength   = _itemCounts.Length(index);

        return logicalStart + (fraction * logicalLength);
    }

    /// <summary>
    /// Maps an exposed item offset back into physical content coordinates before realization.
    /// Fractional terminal offsets allow the final item to meet the viewport's trailing edge.
    /// </summary>
    private double ItemToPixelOffset(double logical)
    {
        if (logical <= 0 || _itemSizes.Count == 0)
        {
            return 0;
        }

        if (logical >= _itemCounts.Extent(0))
        {
            return _itemSizes.Extent(Spacing);
        }

        var index           = _itemCounts.Find(logical, 0);
        var localItems      = logical - _itemCounts.Start(index, 0);
        var logicalLength   = _itemCounts.Length(index);
        var divisor         = Math.Max(1, logicalLength);
        var fraction        = localItems / divisor;
        var trailingSpacing = index + 1 < _itemSizes.Count ? Spacing : 0;
        var itemLength      = _itemSizes.Length(index);
        var slotLength      = itemLength + trailingSpacing;
        var physicalStart   = _itemSizes.Start(index, Spacing);

        return physicalStart + (fraction * slotLength);
    }

    /// <summary>
    /// Notifies the ScrollViewer only when the exposed geometry changes, avoiding layout churn.
    /// </summary>
    private void PublishScrollInfo()
    {
        var current = (AlongExtent, AlongViewport, AlongOffset, _crossExtent, _crossViewport, _crossOffset);

        if (_publishedScrollInfo != current)
        {
            _publishedScrollInfo = current;
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    /// <summary>
    /// Restricts an offset to the scrollable content, treating positive infinity as the end.
    /// </summary>
    private static double ClampOffset(double offset, double extent, double viewport)
    {
        var maximumOffset = Math.Max(0, extent - viewport);
        var boundedOffset = Math.Min(offset, maximumOffset);

        return Math.Max(0, boundedOffset);
    }
}