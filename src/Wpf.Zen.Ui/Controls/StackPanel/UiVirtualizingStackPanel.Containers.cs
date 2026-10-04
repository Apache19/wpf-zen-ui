// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Owns container generation, cleanup notifications and collection-change handling.
/// </summary>
public partial class UiVirtualizingStackPanel
{
    // Maps measured visuals to local data indices. Arrange consumes this snapshot rather
    // than assuming recycled visuals have the same index as their position in InternalChildren.
    private readonly Dictionary<UIElement, int> _measuredIndices = [];

    /// <summary>
    /// Identifies the attached cleanup event shared with WPF's VirtualizingStackPanel. The owning
    /// ItemsControl receives the event before a container is removed or recycled by virtualization.
    /// </summary>
    public static readonly RoutedEvent CleanUpVirtualizedItemEvent =
        VirtualizingStackPanel.CleanUpVirtualizedItemEvent.AddOwner(
            typeof(UiVirtualizingStackPanel)
        );

    /// <summary>
    /// Adds a cleanup handler to an owning ItemsControl. Set the event's Cancel property to retain
    /// a container until a later cleanup attempt; handlers registered through WPF also receive it.
    /// </summary>
    /// <param name="element">The element receiving the direct cleanup event.</param>
    /// <param name="handler">The handler to register.</param>
    public static void AddCleanUpVirtualizedItemHandler(
        DependencyObject element,
        CleanUpVirtualizedItemEventHandler handler)
    {
        VirtualizingStackPanel.AddCleanUpVirtualizedItemHandler(element, handler);
    }

    /// <summary>
    /// Removes a previously registered handler from the shared cleanup event.
    /// </summary>
    /// <param name="element">The element on which the handler was registered.</param>
    /// <param name="handler">The handler to remove.</param>
    public static void RemoveCleanUpVirtualizedItemHandler(
        DependencyObject element,
        CleanUpVirtualizedItemEventHandler handler)
    {
        VirtualizingStackPanel.RemoveCleanUpVirtualizedItemHandler(element, handler);
    }

    /// <summary>
    /// Notifies the owning ItemsControl before virtualizing a container. Overrides can customize
    /// notification; the Cancel flag is checked before changing the generator or visual tree.
    /// </summary>
    /// <param name="e">The data item, container and cancellation decision.</param>
    protected virtual void OnCleanUpVirtualizedItem(CleanUpVirtualizedItemEventArgs e)
    {
        var owner = ItemsControl.GetItemsOwner(this);
        owner?.RaiseEvent(e);
    }

    /// <summary>
    /// Attaches and prepares the entire generator sequence in visual order for complete layout.
    /// The local sequence can contain groups rather than the owning control's flattened leaf items.
    /// </summary>
    private List<UIElement> GenerateAllContainers()
    {
        var containers    = new List<UIElement>();
        var generator     = ItemContainerGenerator;
        var startPosition = new GeneratorPosition(-1, 0);

        using (generator.StartAt(startPosition, GeneratorDirection.Forward))
        {
            var index = 0;
            while (generator.GenerateNext(out var newlyRealized) is UIElement child)
            {
                var attached = AttachContainer(child, index);
                index++;
                if (newlyRealized || attached)
                {
                    generator.PrepareItemContainer(child);
                }

                containers.Add(child);
            }
        }

        return containers;
    }

    /// <summary>
    /// Obtains a container at a data index and maintains the generator's required visual order.
    /// Recycled containers must be attached before preparation, even if newlyRealized is false.
    /// </summary>
    private UIElement GetOrCreateContainer(int index)
    {
        var generator = ItemContainerGenerator;
        var position  = generator.GeneratorPositionFromIndex(index);

        using (generator.StartAt(position, GeneratorDirection.Forward, true))
        {
            var child       = (UIElement)generator.GenerateNext(out var newlyRealized);
            var visualIndex = position.Offset == 0 ? position.Index : position.Index + 1;
            var attached    = AttachContainer(child, visualIndex);

            if (newlyRealized || attached)
            {
                generator.PrepareItemContainer(child);
            }

            return child;
        }
    }

    /// <summary>
    /// Inserts a newly generated or detached recycled container into generator-compatible order.
    /// Returns whether preparation is needed because the visual has just been attached.
    /// </summary>
    private bool AttachContainer(UIElement child, int position)
    {
        if (InternalChildren.Contains(child))
        {
            return false;
        }

        InsertInternalChild(position, child);
        return true;
    }

    /// <summary>
    /// Releases containers outside the cache in reverse generator order. Focused and explicitly
    /// nonvirtualizable containers remain connected so scrolling does not discard their state.
    /// </summary>
    private void RemoveContainersOutsideRange(ItemsControl owner, int first, int last)
    {
        var generator = ItemContainerGenerator;

        for (var position = InternalChildren.Count - 1; position >= 0; position--)
        {
            var child             = InternalChildren[position];
            var generatorPosition = new GeneratorPosition(position, 0);
            var index             = generator.IndexFromGeneratorPosition(generatorPosition);

            if ((index >= first && index <= last) || child.IsKeyboardFocusWithin
                || !GetIsContainerVirtualizable(child))
            {
                continue;
            }

            var itemGenerator = (ItemContainerGenerator)generator;
            var item          = itemGenerator.ItemFromContainer(child);

            var cleanup = new CleanUpVirtualizedItemEventArgs(item, child)
            {
                Source = this
            };

            OnCleanUpVirtualizedItem(cleanup);

            if (cleanup.Cancel)
            {
                continue;
            }

            _measuredIndices.Remove(child);

            if (GetVirtualizationMode(owner) == VirtualizationMode.Recycling
                && generator is IRecyclingItemContainerGenerator recycling)
            {
                recycling.Recycle(generatorPosition, 1);
            }
            else
            {
                generator.Remove(generatorPosition, 1);
            }

            RemoveInternalChildRange(position, 1);
        }
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
    {
        base.OnItemsChanged(sender, args);

        // WPF has already updated generator positions. Remove only the affected visuals so an
        // unrelated edit preserves container state and keyboard focus in surviving items.
        if (args.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Move)
        {
            var position = args.Action == NotifyCollectionChangedAction.Move
                ? args.OldPosition
                : args.Position;

            var start = position.Index + (position.Offset > 0 ? 1 : 0);

            if (args.ItemUICount > 0)
            {
                RemoveInternalChildRange(start, args.ItemUICount);
            }
        }
        else if (args.Action == NotifyCollectionChangedAction.Replace && args.ItemUICount > 0)
        {
            RemoveInternalChildRange(args.Position.Index, args.ItemUICount);

            using (ItemContainerGenerator.StartAt(args.Position, GeneratorDirection.Forward, true))
            {
                for (var index = 0; index < args.ItemUICount; index++)
                {
                    var child = (UIElement)ItemContainerGenerator.GenerateNext(out _);
                    AttachContainer(child, args.Position.Index + index);
                    ItemContainerGenerator.PrepareItemContainer(child);
                }
            }
        }

        if (_anchorContainer is not null && ItemContainerGenerator is ItemContainerGenerator generator)
        {
            var index = generator.IndexFromContainer(_anchorContainer, true);
            _pendingLayoutAnchor = index >= 0
                ? new LayoutAnchor(index, _anchorOffsetWithinItem)
                : null;
        }

        ResetItemMetrics(0);
        _crossExtent = 0;
        _measuredIndices.Clear();
        InvalidateMeasure();
    }

    /// <inheritdoc />
    protected override void OnClearChildren()
    {
        base.OnClearChildren();
        // Detach visuals when their generator is cleared so a subsequent layout cannot reuse
        // containers that no longer belong to the generator's current sequence.
        if (InternalChildren.Count > 0)
        {
            RemoveInternalChildRange(0, InternalChildren.Count);
        }

        ResetItemMetrics(0);
        _measuredIndices.Clear();
        _gaps.Clear();
        _anchorContainer     = null;
        _pendingLayoutAnchor = null;
        _stackOffset         = 0;
        _crossOffset         = 0;
        _crossExtent         = 0;
    }
}