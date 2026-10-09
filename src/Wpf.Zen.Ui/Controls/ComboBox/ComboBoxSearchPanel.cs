// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Virtualizes native ComboBox containers by mapping result positions to source indices.
/// Vertical scrolling uses result rows; each realized row retains its own measured height.
/// </summary>
/// <remarks>
/// The owner keeps its complete Items collection and selection. This panel supplies only the
/// visible source indices to the owner's generator, so styles and templates receive real items.
/// Result rows are realized against the popup's available height. Grouped views reuse WPF's
/// internal GroupItem.Generator to retain flat source indexing without displaying group headers.
/// This bridge must be checked against grouped search tests when supporting a new WPF runtime.
/// </remarks>
internal sealed class ComboBoxSearchPanel : VirtualizingPanel, IScrollInfo
{
    private readonly List<int>                       _realized     = new();
    private readonly Dictionary<int, GeneratorEntry> _leafEntries  = new();
    private readonly List<GeneratorEntry>            _groupEntries = new();
    private readonly HashSet<DependencyObject>       _activeGroups = new();

    private IReadOnlyList<int>? _indices;

    private double _verticalOffset;
    private double _horizontalOffset;
    private double _extentWidth;
    private double _viewportWidth;
    private double _viewportHeight;

    /// <summary>
    /// Gets the reusable items-panel template installed only during filtering.
    /// </summary>
    internal static ItemsPanelTemplate PanelTemplate { get; } = CreatePanelTemplate();

    private static readonly GroupStyle SearchGroupStyle = new() { Panel = PanelTemplate };

    private static readonly PropertyInfo GroupGenerator = typeof(GroupItem).GetProperty(
        "Generator", BindingFlags.Instance | BindingFlags.NonPublic
    ) ?? throw new NotSupportedException("Filtered ComboBox grouping requires GroupItem.Generator.");

    /// <summary>
    /// Gets the group-style selector that routes grouped views to the filtered root panel.
    /// </summary>
    internal static GroupStyleSelector GroupStyleSelector { get; } = SelectGroupStyle;

    /// <summary>
    /// Creates a panel that clips partially visible rows to the popup viewport.
    /// </summary>
    public ComboBoxSearchPanel()
    {
        ClipToBounds = true;
    }

    /// <summary>
    /// Supplies the root items-host template while retaining the original view's group hierarchy.
    /// </summary>
    private static GroupStyle SelectGroupStyle(CollectionViewGroup? group, int level)
    {
        return SearchGroupStyle;
    }

    /// <summary>
    /// Creates a native items-host template without requiring a separate selector.
    /// </summary>
    private static ItemsPanelTemplate CreatePanelTemplate()
    {
        var factory  = new FrameworkElementFactory(typeof(ComboBoxSearchPanel));
        var template = new ItemsPanelTemplate(factory);
        template.Seal();
        return template;
    }

    /// <summary>
    /// Generates and measures only the filtered rows intersecting the popup viewport.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        var owner = ItemsControl.GetItemsOwner(this) as ComboBox;
        if (owner is null || !owner.IsTextSearchFiltering)
        {
            return default;
        }

        owner.SearchPanel = this;

        var children  = InternalChildren;
        var generator = ItemContainerGenerator;
        var indices   = owner.SearchIndices;

        if (!ReferenceEquals(_indices, indices))
        {
            generator.RemoveAll();

            RemoveInternalChildRange(0, children.Count);

            _realized.Clear();
            _leafEntries.Clear();
            _groupEntries.Clear();

            _indices = indices;

            _verticalOffset   = 0;
            _horizontalOffset = 0;
        }

        var heightLimit = availableSize.Height;
        if (double.IsInfinity(heightLimit))
        {
            heightLimit = owner.MaxDropDownHeight;
        }

        var lastPosition = Math.Max(0, indices.Count - 1);
        var start        = Math.Min((int)_verticalOffset, lastPosition);

        var height       = 0.0;
        var width        = 0.0;
        var viewportRows = 0.0;

        var end = start;
        var measureWidth = CanHorizontallyScroll ? double.PositiveInfinity : availableSize.Width;
        _activeGroups.Clear();

        // Remove rows before the viewport first. Generator positions count realized source
        // containers, whereas the viewport uses positions in the filtered index sequence.
        RemoveOutsideRange(start, indices.Count);

        for (var position = start; position < indices.Count && height < heightLimit; position++)
        {
            var child = MeasureRow(owner, position, measureWidth);

            var rowHeight       = child.DesiredSize.Height;
            var remainingHeight = Math.Max(0, heightLimit - height);

            viewportRows += rowHeight > 0 ? Math.Min(1, remainingHeight / rowHeight) : 1;
            height       += rowHeight;

            width = Math.Max(width, child.DesiredSize.Width);
            end   = position + 1;
        }

        // Backfill the final page using actual row heights. Jumping to the last match must
        // retain a full viewport without measuring every preceding result.
        while (end == indices.Count && start > 0 && height < heightLimit)
        {
            var previous   = start - 1;
            var child      = MeasureRow(owner, previous, measureWidth);
            var nextHeight = height + child.DesiredSize.Height;
            if (nextHeight > heightLimit)
            {
                break;
            }

            start  = previous;
            height = nextHeight;
            viewportRows++;
            width  = Math.Max(width, child.DesiredSize.Width);
        }

        RemoveOutsideRange(start, end);
        ReleaseUnusedGroups();

        _verticalOffset = start;
        // Logical offsets address whole rows. A fractional viewport would give the scrollbar
        // a fractional maximum that SetVerticalOffset rounds down, leaving the last row clipped.
        var fullRows = Math.Floor(viewportRows);
        _viewportHeight = viewportRows > 0 ? Math.Max(1, fullRows) : 0;
        _extentWidth    = width;
        _viewportWidth  = double.IsInfinity(availableSize.Width) ? width : availableSize.Width;

        var maximumHorizontalOffset = Math.Max(0, width - _viewportWidth);
        _horizontalOffset = Math.Min(_horizontalOffset, maximumHorizontalOffset);

        ScrollOwner?.InvalidateScrollInfo();

        var desiredWidth  = Math.Min(width, _viewportWidth);
        var desiredHeight = Math.Min(height, heightLimit);

        return new Size(desiredWidth, desiredHeight);
    }

    /// <summary>
    /// Realizes one result through its native source generator and measures its item template.
    /// </summary>
    private UIElement MeasureRow(ComboBox owner, int position, double width)
    {
        var sourceIndex = _indices![position];
        var entry       = ResolveGenerator(owner, sourceIndex);

        var generator         = entry.Generator;
        var generatorPosition = generator.GeneratorPositionFromIndex(entry.Index);

        using (generator.StartAt(generatorPosition, GeneratorDirection.Forward, true))
        {
            var child = (UIElement)generator.GenerateNext(out var newlyRealized);
            if (newlyRealized)
            {
                var childIndex = _realized.BinarySearch(position);
                childIndex = ~childIndex;

                InsertInternalChild(childIndex, child);
                _realized.Insert(childIndex, position);

                generator.PrepareItemContainer(child);
            }

            _leafEntries[sourceIndex] = entry;
            child.Measure(new Size(width, double.PositiveInfinity));
            return child;
        }
    }

    /// <summary>
    /// Arranges realized containers in filtered source order using their natural row heights.
    /// </summary>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var top = 0.0;

        foreach (UIElement child in InternalChildren)
        {
            var width  = Math.Max(finalSize.Width, child.DesiredSize.Width);
            var bounds = new Rect(-_horizontalOffset, top, width, child.DesiredSize.Height);

            child.Arrange(bounds);

            top += child.DesiredSize.Height;
        }

        return finalSize;
    }

    /// <summary>
    /// Releases containers outside the requested result range through the native generator.
    /// Reverse removal keeps generator positions valid as the realized collection shrinks.
    /// </summary>
    private void RemoveOutsideRange(int start, int end)
    {
        for (var index = _realized.Count - 1; index >= 0; index--)
        {
            var position = _realized[index];
            if (position >= start && position < end)
            {
                continue;
            }

            var sourceIndex       = _indices![position];
            var entry             = _leafEntries[sourceIndex];
            var generatorPosition = entry.Generator.GeneratorPositionFromIndex(entry.Index);

            entry.Generator.Remove(generatorPosition, 1);

            _leafEntries.Remove(sourceIndex);

            RemoveInternalChildRange(index, 1);

            _realized.RemoveAt(index);
        }
    }

    /// <summary>
    /// Resolves the native leaf generator for a flat source index. Group containers retain WPF's
    /// generator hierarchy but are not presented as rows in the flat search results.
    /// </summary>
    private GeneratorEntry ResolveGenerator(ComboBox owner, int sourceIndex)
    {
        var generator = ItemContainerGenerator;

        var groups = owner.Items.Groups;
        if (!owner.IsGrouping || groups is null)
        {
            return new GeneratorEntry(generator, sourceIndex, null);
        }

        System.Collections.IList level = groups;

        var index = sourceIndex;

        while (true)
        {
            var groupIndex = 0;
            CollectionViewGroup? group = null;

            for (; groupIndex < level.Count; groupIndex++)
            {
                group = (CollectionViewGroup)level[groupIndex]!;
                if (index < group.ItemCount)
                {
                    break;
                }

                index -= group.ItemCount;
            }

            var position = generator.GeneratorPositionFromIndex(groupIndex);
            GroupItem container;

            using (generator.StartAt(position, GeneratorDirection.Forward, true))
            {
                container = (GroupItem)generator.GenerateNext(out var newlyRealized);
                if (newlyRealized)
                {
                    // These group containers supply indexing only. Leaf containers are prepared
                    // after insertion into the actual items host so item resources inherit there.
                    generator.PrepareItemContainer(container);
                    _groupEntries.Add(new GeneratorEntry(generator, groupIndex, container));
                }
            }

            _activeGroups.Add(container);

            generator = (IItemContainerGenerator)GroupGenerator.GetValue(container)!;

            if (group!.IsBottomLevel)
            {
                return new GeneratorEntry(generator, index, null);
            }

            level = group.Items;
        }
    }

    /// <summary>
    /// Releases group-index containers after their last visible leaf leaves the viewport.
    /// Descendants are released before ancestors to keep native generator positions valid.
    /// </summary>
    private void ReleaseUnusedGroups()
    {
        for (var index = _groupEntries.Count - 1; index >= 0; index--)
        {
            var entry = _groupEntries[index];
            if (_activeGroups.Contains(entry.Container!))
            {
                continue;
            }

            var position = entry.Generator.GeneratorPositionFromIndex(entry.Index);
            entry.Generator.Remove(position, 1);
            _groupEntries.RemoveAt(index);
        }
    }

    /// <summary>
    /// Identifies a local generator index within WPF's native grouping hierarchy.
    /// </summary>
    private readonly struct GeneratorEntry
    {
        /// <summary>
        /// Records a source index and, for hierarchy nodes, its native group container.
        /// </summary>
        internal GeneratorEntry(IItemContainerGenerator generator, int index, DependencyObject? container)
        {
            Generator = generator;
            Index     = index;
            Container = container;
        }

        /// <summary>
        /// Gets the generator that owns this local index.
        /// </summary>
        internal IItemContainerGenerator Generator { get; }

        /// <summary>
        /// Gets the index within the generator's item or group collection.
        /// </summary>
        internal int Index { get; }

        /// <summary>
        /// Gets the group container when this entry represents a hierarchy node.
        /// </summary>
        internal DependencyObject? Container { get; }
    }

    /// <summary>
    /// Invalidates the snapshot when WPF changes the source or resets container generation.
    /// The search controller ends filtering when the owner's collection changes.
    /// </summary>
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        _indices = null;
        InvalidateMeasure();
    }

    /// <summary>
    /// Drops result positions when the base panel disconnects its generator.
    /// </summary>
    protected override void OnClearChildren()
    {
        _realized.Clear();
        _indices = null;
    }

    /// <summary>
    /// Scrolls an original source index into the filtered viewport without selecting it.
    /// Requests for nonmatching items, including native popup-opening navigation, are ignored.
    /// </summary>
    protected override void BringIndexIntoView(int index)
    {
        if (_indices is null)
        {
            return;
        }

        var position = FindPosition(index);
        if (position < 0)
        {
            return;
        }

        var visibleEnd = _verticalOffset + Math.Floor(_viewportHeight);
        if (position < _verticalOffset || position >= visibleEnd)
        {
            SetVerticalOffsetCore(position, false);
        }
    }

    /// <summary>
    /// Finds a source index in the ordered result mapping without scanning the source items.
    /// </summary>
    private int FindPosition(int sourceIndex)
    {
        var low  = 0;
        var high = _indices!.Count - 1;

        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var value  = _indices[middle];

            if (value == sourceIndex)
            {
                return middle;
            }

            if (value < sourceIndex)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return -1;
    }

    /// <summary>
    /// Gets or sets whether horizontal overflow can be scrolled in pixels.
    /// </summary>
    public bool CanHorizontallyScroll { get; set; }

    /// <summary>
    /// Gets or sets whether the scroll viewer permits vertical scrolling.
    /// </summary>
    public bool CanVerticallyScroll { get; set; }

    /// <summary>
    /// Gets the widest realized row in pixels.
    /// </summary>
    public double ExtentWidth => _extentWidth;

    /// <summary>
    /// Gets the number of matching source rows.
    /// </summary>
    public double ExtentHeight => _indices?.Count ?? 0;

    /// <summary>
    /// Gets the available viewport width in pixels.
    /// </summary>
    public double ViewportWidth => _viewportWidth;

    /// <summary>
    /// Gets the fully visible row count. An oversized first row occupies one logical row.
    /// </summary>
    public double ViewportHeight => _viewportHeight;

    /// <summary>
    /// Gets the horizontal pixel offset.
    /// </summary>
    public double HorizontalOffset => _horizontalOffset;

    /// <summary>
    /// Gets the first visible result position.
    /// </summary>
    public double VerticalOffset => _verticalOffset;

    /// <summary>
    /// Gets or sets the scroll viewer receiving extent and viewport updates.
    /// </summary>
    public System.Windows.Controls.ScrollViewer? ScrollOwner { get; set; }

    /// <summary>
    /// Moves the viewport upward by one matching row.
    /// </summary>
    public void LineUp()
    {
        SetVerticalOffset(_verticalOffset - 1);
    }

    /// <summary>
    /// Moves the viewport downward by one matching row.
    /// </summary>
    public void LineDown()
    {
        SetVerticalOffset(_verticalOffset + 1);
    }

    /// <summary>
    /// Moves the horizontal viewport left by sixteen pixels.
    /// </summary>
    public void LineLeft()
    {
        SetHorizontalOffset(_horizontalOffset - 16);
    }

    /// <summary>
    /// Moves the horizontal viewport right by sixteen pixels.
    /// </summary>
    public void LineRight()
    {
        SetHorizontalOffset(_horizontalOffset + 16);
    }

    /// <summary>
    /// Moves up by the number of fully visible rows in the viewport.
    /// </summary>
    public void PageUp()
    {
        var visibleRows = Math.Floor(_viewportHeight);
        var page        = Math.Max(1, visibleRows);
        SetVerticalOffset(_verticalOffset - page);
    }

    /// <summary>
    /// Moves down by the number of fully visible rows in the viewport.
    /// </summary>
    public void PageDown()
    {
        var visibleRows = Math.Floor(_viewportHeight);
        var page        = Math.Max(1, visibleRows);
        SetVerticalOffset(_verticalOffset + page);
    }

    /// <summary>
    /// Moves left by one viewport width.
    /// </summary>
    public void PageLeft()
    {
        SetHorizontalOffset(_horizontalOffset - _viewportWidth);
    }

    /// <summary>
    /// Moves right by one viewport width.
    /// </summary>
    public void PageRight()
    {
        SetHorizontalOffset(_horizontalOffset + _viewportWidth);
    }

    /// <summary>
    /// Applies the system mouse-wheel row setting when scrolling upward.
    /// </summary>
    public void MouseWheelUp()
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines < 0)
        {
            PageUp();
        }
        else
        {
            SetVerticalOffset(_verticalOffset - lines);
        }
    }

    /// <summary>
    /// Applies the system mouse-wheel row setting when scrolling downward.
    /// </summary>
    public void MouseWheelDown()
    {
        var lines = SystemParameters.WheelScrollLines;
        if (lines < 0)
        {
            PageDown();
        }
        else
        {
            SetVerticalOffset(_verticalOffset + lines);
        }
    }

    /// <summary>
    /// Moves the horizontal viewport left for a mouse-wheel gesture.
    /// </summary>
    public void MouseWheelLeft()
    {
        LineLeft();
    }

    /// <summary>
    /// Moves the horizontal viewport right for a mouse-wheel gesture.
    /// </summary>
    public void MouseWheelRight()
    {
        LineRight();
    }

    /// <summary>
    /// Bounds horizontal scrolling to the measured row width.
    /// </summary>
    public void SetHorizontalOffset(double offset)
    {
        if (double.IsNaN(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var maximum = Math.Max(0, _extentWidth - _viewportWidth);
        var limited = Math.Min(offset, maximum);

        _horizontalOffset = Math.Max(0, limited);
        InvalidateArrange();
        ScrollOwner?.InvalidateScrollInfo();
    }

    /// <summary>
    /// Bounds logical scrolling to the result sequence without generating skipped rows.
    /// </summary>
    public void SetVerticalOffset(double offset)
    {
        SetVerticalOffsetCore(offset, true);
    }

    /// <summary>
    /// Distinguishes scrollbar end requests from navigation to a particular result row. When
    /// row heights vary, the previous viewport count cannot predict the first row of the final
    /// page; measuring backward from the last result computes that page without a source scan.
    /// </summary>
    private void SetVerticalOffsetCore(double offset, bool alignEnd)
    {
        if (double.IsNaN(offset))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var maximum          = Math.Max(0, ExtentHeight - 1);
        var scrollbarMaximum = Math.Max(0, ExtentHeight - ViewportHeight);
        if (alignEnd && offset > 0 && offset >= scrollbarMaximum)
        {
            offset = maximum;
        }

        var limited = Math.Min(offset, maximum);
        var bounded = Math.Max(0, limited);
        var next    = Math.Floor(bounded);

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (next != _verticalOffset)
        {
            _verticalOffset = next;
            InvalidateMeasure();
            ScrollOwner?.InvalidateScrollInfo();
        }
    }

    /// <summary>
    /// Satisfies native BringIntoView requests for currently generated item descendants.
    /// </summary>
    public Rect MakeVisible(Visual visual, Rect rectangle)
    {
        var element = visual as DependencyObject;
        while (element is not null && VisualTreeHelper.GetParent(element) != this)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is UIElement child)
        {
            var childIndex = InternalChildren.IndexOf(child);
            if (childIndex >= 0 && childIndex < _realized.Count)
            {
                var position = _realized[childIndex];
                BringIndexIntoView(_indices![position]);
                return new Rect(child.RenderSize);
            }
        }

        return Rect.Empty;
    }
}
