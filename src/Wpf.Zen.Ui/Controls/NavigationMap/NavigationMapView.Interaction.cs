// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains pointer, keyboard, panning, zooming, marquee, and node-drag interaction for
/// <see cref="NavigationMapView"/>.
/// </summary>
public partial class NavigationMapView
{
    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);

        if (!CanEditSelection() || _isMiddleButtonPanning)
        {
            return;
        }

        // Keyboard commands belong to the editor, not to whichever card happened to be clicked
        // last. Selection and its primary node already provide the visual/item focus semantics.
        // Keeping keyboard focus here also makes a blank-surface click immediately command-ready.
        _ = Focus();

        var source = e.OriginalSource as DependencyObject;
        var panel = FindVisualAncestor<NavigationMapPanel>(source);

        if (panel is null || !ReferenceEquals(FindVisualAncestor<NavigationMapView>(panel), this))
        {
            // Scroll bars and template chrome are deliberately excluded. Previously every
            // non-card source cleared selection, so clicking a thumb looked like a blank click.
            return;
        }

        var container = FindVisualAncestor<NavigationMapItem>(source);
        if (
            container is null
            || !ReferenceEquals(ItemsControlFromItemContainer(container), this)
            || container.NodeKey is not { Length: > 0 } key
        )
        {
            BeginMarqueeGesture(panel, e);
            e.Handled = true;
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var additive  = (modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0;
        var selection = new HashSet<string>(_selectedNodeKeys, StringComparer.Ordinal);

        if ((modifiers & ModifierKeys.Control) != 0 && selection.Contains(key))
        {
            selection.Remove(key);
            ApplySelection(selection, selection.FirstOrDefault());
            e.Handled = true;
            return;
        }

        if (!additive && !selection.Contains(key))
        {
            selection.Clear();
        }

        selection.Add(key);

        ApplySelection(selection, key);
        BeginNodeDragGesture(panel, e);

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (
            e.ChangedButton != MouseButton.Middle
            || !CanEditSelection()
            || _scrollViewer is null
        )
        {
            return;
        }

        var source = e.OriginalSource as DependencyObject;
        var panel = FindVisualAncestor<NavigationMapPanel>(source);

        if (panel is null || !ReferenceEquals(FindVisualAncestor<NavigationMapView>(panel), this))
        {
            // Only the map canvas and its node descendants initiate panning. In particular, a
            // middle click on a ScrollBar must retain the standard ScrollViewer interaction.
            return;
        }

        EndEditingGesture();

        _panPointerOrigin          = e.GetPosition(_scrollViewer);
        _panHorizontalOffsetOrigin = _scrollViewer.HorizontalOffset;
        _panVerticalOffsetOrigin   = _scrollViewer.VerticalOffset;
        _isMiddleButtonPanning     = CaptureMouse();

        if (_isMiddleButtonPanning)
        {
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);

        if (_isMiddleButtonPanning)
        {
            if (e.MiddleButton != MouseButtonState.Pressed)
            {
                // Mouse capture can survive when a release is consumed outside this visual tree.
                // The physical button state is therefore the authoritative fallback termination.
                EndMiddleButtonPanning();
                return;
            }

            if (_scrollViewer is not null)
            {
                var panPointer = e.GetPosition(_scrollViewer);

                _scrollViewer.ScrollToHorizontalOffset(
                    _panHorizontalOffsetOrigin - (panPointer.X - _panPointerOrigin.X)
                );

                _scrollViewer.ScrollToVerticalOffset(
                    _panVerticalOffsetOrigin - (panPointer.Y - _panPointerOrigin.Y)
                );
            }

            e.Handled = true;
            return;
        }

        if (
            _editedPanel is null
            || _editorGesture == EditorGesture.None
            || e.LeftButton != MouseButtonState.Pressed
        )
        {
            return;
        }

        if (_scrollViewer is not null)
        {
            _lastPointerInScrollViewer = e.GetPosition(_scrollViewer);
        }

        var pointer = e.GetPosition(_editedPanel);

        if (
            _editorGesture is EditorGesture.NodeDragPending or EditorGesture.MarqueePending
            && !IsDragThresholdExceeded(e.GetPosition(this))
        )
        {
            e.Handled = true;
            return;
        }

        if (_editorGesture == EditorGesture.NodeDragPending)
        {
            _editorGesture = EditorGesture.NodeDrag;
            _pendingDragBefore = CapturePlacementStates(_dragOriginCenters.Keys);
            _editedPanel.BeginExtentHold();

            StartAutoScroll();
        }
        else if (_editorGesture == EditorGesture.MarqueePending)
        {
            _editorGesture = EditorGesture.Marquee;
        }

        if (_editorGesture == EditorGesture.NodeDrag)
        {
            UpdateDraggedPositions(pointer);
        }
        else if (_editorGesture == EditorGesture.Marquee)
        {
            UpdateMarqueeSelection(pointer);
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseUp(e);

        if (e.ChangedButton != MouseButton.Middle || !_isMiddleButtonPanning)
        {
            return;
        }

        EndMiddleButtonPanning();

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);

        if (_editedPanel is null || _editorGesture == EditorGesture.None)
        {
            return;
        }

        if (_editorGesture == EditorGesture.Marquee)
        {
            UpdateMarqueeSelection(e.GetPosition(_editedPanel));
        }
        else if (
            _editorGesture == EditorGesture.MarqueePending
         && (_gestureModifiers & (ModifierKeys.Control | ModifierKeys.Shift)) == 0
        )
        {
            // A click, rather than a drag, on the actual map surface is the only blank-space
            // gesture that clears selection. Template chrome never reaches this branch.
            ClearSelection();
        }

        EndEditingGesture();

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        EndMiddleButtonPanning(releaseCapture: false);
        EndEditingGesture(releaseCapture: false);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        ChangeZoomAtPointer(e.Delta > 0 ? ZoomFactor + ZoomStep : ZoomFactor - ZoomStep, e);

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (
            CanEditSelection()
            && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && e.Key == Key.Z
        )
        {
            _ = (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? Redo() : Undo();
            e.Handled = true;

            return;
        }

        if (
            CanEditSelection()
            && (Keyboard.Modifiers & ModifierKeys.Control) != 0
            && e.Key == Key.Y
        )
        {
            _ = Redo();
            e.Handled = true;

            return;
        }

        if (CanEditSelection() && e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            SelectAll();
            e.Handled = true;

            return;
        }

        if (CanEditSelection() && e.Key == Key.Escape)
        {
            ClearSelection();
            e.Handled = true;

            return;
        }

        if (!CanEditSelection() || _selectedNodeKeys.Count == 0)
        {
            return;
        }

        var step = SnapToGrid ? GridSize : (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10D : 1D;
        var delta = e.Key switch
        {
            Key.Left  => new Vector(-step, 0D),
            Key.Right => new Vector(step,  0D),
            Key.Up    => new Vector(0D,    -step),
            Key.Down  => new Vector(0D,    step),
            _         => default,
        };

        if (delta == default)
        {
            return;
        }

        MoveSelection(delta);

        e.Handled = true;
    }

    private void OnItemContainerClick(object sender, RoutedEventArgs e)
    {
        if (CanEditSelection())
        {
            e.Handled = true;
            return;
        }

        if (e.OriginalSource is not NavigationMapItem container)
        {
            return;
        }

        var item = ItemContainerGenerator.ItemFromContainer(container);
        if (!ReferenceEquals(item, DependencyProperty.UnsetValue))
        {
            OnNodeInvoked(item, container);
        }
    }

    private void BeginNodeDragGesture(NavigationMapPanel panel, MouseButtonEventArgs e)
    {
        _dragOriginCenters.Clear();
        foreach (var selected in GetSelectedContainers())
        {
            if (selected.NodeKey is { Length: > 0 } key)
            {
                _dragOriginCenters[key] = Center(GetContainerBounds(selected));
            }
        }

        BeginEditorGesture(panel, e, EditorGesture.NodeDragPending);
    }

    private void BeginMarqueeGesture(NavigationMapPanel panel, MouseButtonEventArgs e)
    {
        _dragOriginCenters.Clear();
        BeginEditorGesture(panel, e, EditorGesture.MarqueePending);
    }

    private void BeginEditorGesture(
        NavigationMapPanel panel,
        MouseButtonEventArgs e,
        EditorGesture gesture)
    {
        EndEditingGesture();

        _editedPanel                = panel;
        _editorGesture              = gesture;
        _dragPointerOrigin          = e.GetPosition(panel);
        _gesturePointerOriginInView = e.GetPosition(this);
        _gestureModifiers           = Keyboard.Modifiers;

        _selectionAtGestureStart.Clear();
        _selectionAtGestureStart.UnionWith(_selectedNodeKeys);

        _pendingDragBefore = null;

        _scrollViewer ??= FindVisualDescendant<ScrollViewer>(this);

        if (_scrollViewer is not null)
        {
            _lastPointerInScrollViewer = e.GetPosition(_scrollViewer);
        }

        CaptureMouse();
    }

    private bool IsDragThresholdExceeded(Point pointerInView)
    {
        return
            Math.Abs(pointerInView.X - _gesturePointerOriginInView.X) >= SystemParameters.MinimumHorizontalDragDistance
         || Math.Abs(pointerInView.Y - _gesturePointerOriginInView.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void UpdateMarqueeSelection(Point pointer)
    {
        if (_editedPanel is null)
        {
            return;
        }

        var rectangle = new Rect(_dragPointerOrigin, pointer);

        _editedPanel.SetSelectionRectangle(rectangle);

        var hits = GetGeneratedContainers()
            .Where(container => container.IsVisible && rectangle.IntersectsWith(GetContainerBounds(container)))
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .ToArray();

        var selection = new HashSet<string>(_selectionAtGestureStart, StringComparer.Ordinal);

        if ((_gestureModifiers & ModifierKeys.Control) != 0)
        {
            foreach (var key in hits)
            {
                if (!selection.Add(key))
                {
                    selection.Remove(key);
                }
            }
        }
        else
        {
            if ((_gestureModifiers & ModifierKeys.Shift) == 0)
            {
                selection.Clear();
            }

            selection.UnionWith(hits);
        }

        var primary = PrimarySelectedNodeKey;
        if (primary is null || !selection.Contains(primary))
        {
            primary = hits.FirstOrDefault(selection.Contains) ?? selection.FirstOrDefault();
        }

        ApplySelection(selection, primary);
    }

    private void UpdateDraggedPositions(Point pointer)
    {
        if (_editedPanel is null || _dragOriginCenters.Count == 0)
        {
            return;
        }

        var delta = pointer - _dragPointerOrigin;

        if (
            SnapToGrid
            && PrimarySelectedNodeKey is { } primaryKey
            && _dragOriginCenters.TryGetValue(primaryKey, out var primaryOrigin)
        )
        {
            var desired = primaryOrigin + delta;
            delta = new Vector(Snap(desired.X) - primaryOrigin.X, Snap(desired.Y) - primaryOrigin.Y);
        }

        delta = ClampGroupDelta(delta, _dragOriginCenters);

        var layout = EnsureLayout();

        foreach (var origin in _dragOriginCenters)
        {
            layout.SetPosition(origin.Key, origin.Value + delta);
        }
    }

    private void MoveSelection(Vector requestedDelta)
    {
        var origins = new Dictionary<string, Point>(StringComparer.Ordinal);

        foreach (var container in GetSelectedContainers())
        {
            if (container.NodeKey is { Length: > 0 } key)
            {
                origins[key] = Center(GetContainerBounds(container));
            }
        }

        var before = CapturePlacementStates(origins.Keys);
        var delta  = ClampGroupDelta(requestedDelta, origins);
        var layout = EnsureLayout();

        foreach (var origin in origins)
        {
            layout.SetPosition(origin.Key, origin.Value + delta);
        }

        RecordLayoutEdit(before, CapturePlacementStates(origins.Keys));
    }

    private bool AlignContainersToGrid(
        IReadOnlyList<NavigationMapItem> containers,
        bool recordHistory)
    {
        var keys = containers
            .Select(static container => container.NodeKey)
            .Where(static key => key is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (keys.Length == 0)
        {
            return false;
        }

        var before = CapturePlacementStates(keys);
        var layout = EnsureLayout();

        foreach (var container in containers)
        {
            if (container.NodeKey is not { Length: > 0 } key)
            {
                continue;
            }

            var center = Center(GetContainerBounds(container));
            layout.SetPosition(key, ClampCenter(container, new Point(Snap(center.X), Snap(center.Y))));
        }

        if (recordHistory)
        {
            RecordLayoutEdit(before, CapturePlacementStates(keys));
        }

        return true;
    }

    private void ScheduleFullGridAlignment()
    {
        var requestVersion = ++_gridAlignmentRequestVersion;

        Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (
                    requestVersion != _gridAlignmentRequestVersion
                    || !CanEditSelection()
                    || !SnapToGrid
                )
                {
                    return;
                }

                // Automatic fallback geometry can change after reset, direction, spacing, item, or
                // node-size changes. Always wait for that layout pass, then materialize every center
                // together so a later group drag cannot mix old and newly snapped coordinates.
                UpdateLayout();

                _ = AlignContainersToGrid(GetGeneratedContainers().ToList(), recordHistory: false);
            })
        );
    }

    private void OnLayoutPlacementsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs args)
    {
        // Clear() raises Reset before an application repopulates a loaded document. Context-idle
        // coalescing lets all synchronous additions finish and then normalizes the final layout,
        // while ordinary SetPosition additions during drag do not recursively schedule alignment.
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            ScheduleFullGridAlignment();
        }
    }

    private Vector ClampGroupDelta(Vector delta, IReadOnlyDictionary<string, Point> origins)
    {
        var minimumX = double.PositiveInfinity;
        var minimumY = double.PositiveInfinity;

        foreach (var origin in origins)
        {
            if (FindContainer(origin.Key) is not { } container)
            {
                continue;
            }

            minimumX = Math.Min(minimumX, origin.Value.X - (container.RenderSize.Width / 2D));
            minimumY = Math.Min(minimumY, origin.Value.Y - (container.RenderSize.Height / 2D));
        }

        if (!double.IsPositiveInfinity(minimumX))
        {
            delta.X = Math.Max(delta.X, -minimumX);
            delta.Y = Math.Max(delta.Y, -minimumY);
        }

        return delta;
    }

    private void EndEditingGesture(bool releaseCapture = true)
    {
        CompletePendingDrag();

        var panel = _editedPanel;

        _editedPanel   = null;
        _editorGesture = EditorGesture.None;
        _selectionAtGestureStart.Clear();

        panel?.SetSelectionRectangle(null);
        panel?.EndExtentHold();

        StopAutoScroll();

        if (releaseCapture && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void EndMiddleButtonPanning(bool releaseCapture = true)
    {
        if (!_isMiddleButtonPanning)
        {
            return;
        }

        _isMiddleButtonPanning = false;

        if (releaseCapture && IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
    }

    private void CompletePendingDrag()
    {
        if (_pendingDragBefore is not { } before)
        {
            return;
        }

        _pendingDragBefore = null;

        RecordLayoutEdit(before, CapturePlacementStates(before.Keys));
    }

    private void StartAutoScroll()
    {
        if (_isAutoScrollRendering)
        {
            return;
        }

        _lastAutoScrollFrame   = DateTime.UtcNow;
        _isAutoScrollRendering = true;

        CompositionTarget.Rendering += OnAutoScrollFrame;
    }

    private void StopAutoScroll()
    {
        if (_isAutoScrollRendering)
        {
            CompositionTarget.Rendering -= OnAutoScrollFrame;
            _isAutoScrollRendering = false;
        }

        _autoScrollVelocity = default;
    }

    private void OnAutoScrollFrame(object? sender, EventArgs e)
    {
        if (
            _editedPanel is null
            || _editorGesture != EditorGesture.NodeDrag
            || _scrollViewer is null
            || Mouse.LeftButton != MouseButtonState.Pressed
        )
        {
            StopAutoScroll();
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = Math.Min(0.05D, Math.Max(0D, (now - _lastAutoScrollFrame).TotalSeconds));

        _lastAutoScrollFrame = now;

        var target = new Vector(
            GetAutoScrollSpeed(_lastPointerInScrollViewer.X, _scrollViewer.ViewportWidth),
            GetAutoScrollSpeed(_lastPointerInScrollViewer.Y, _scrollViewer.ViewportHeight)
        );

        var smoothing = Math.Min(1D, elapsed * 10D);

        _autoScrollVelocity += (target - _autoScrollVelocity) * smoothing;

        if (_autoScrollVelocity.Length < 0.5D)
        {
            return;
        }

        var oldHorizontal = _scrollViewer.HorizontalOffset;
        var oldVertical   = _scrollViewer.VerticalOffset;

        _scrollViewer.ScrollToHorizontalOffset(oldHorizontal + (_autoScrollVelocity.X * elapsed));
        _scrollViewer.ScrollToVerticalOffset(oldVertical + (_autoScrollVelocity.Y * elapsed));

        _scrollViewer.UpdateLayout();

        if (
            // ReSharper disable CompareOfFloatsByEqualityOperator
            oldHorizontal != _scrollViewer.HorizontalOffset || oldVertical != _scrollViewer.VerticalOffset
            // ReSharper restore CompareOfFloatsByEqualityOperator
        )
        {
            // Scrolling changes the pointer's map coordinate even while the physical mouse remains
            // still, so update the drag on every animation frame instead of waiting for MouseMove.
            UpdateDraggedPositions(Mouse.GetPosition(_editedPanel));
        }
    }

    private double GetAutoScrollSpeed(double coordinate, double viewportLength)
    {
        if (coordinate < AutoScrollEdgeSize)
        {
            return -MaximumAutoScrollSpeed * Math.Min(
                1D,
                (AutoScrollEdgeSize - coordinate) / AutoScrollEdgeSize
            );
        }

        if (coordinate > viewportLength - AutoScrollEdgeSize)
        {
            return MaximumAutoScrollSpeed * Math.Min(
                1D,
                (coordinate - viewportLength + AutoScrollEdgeSize) / AutoScrollEdgeSize
            );
        }

        return 0D;
    }

    private void ChangeZoomAtPointer(double requestedZoom, MouseWheelEventArgs e)
    {
        if (_scrollViewer is null || FindVisualDescendant<NavigationMapPanel>(this) is not { } panel)
        {
            SetCurrentValue(ZoomFactorProperty, requestedZoom);
            return;
        }

        var mapPoint      = e.GetPosition(panel);
        var viewportPoint = e.GetPosition(_scrollViewer);

        SetCurrentValue(ZoomFactorProperty, requestedZoom);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                if (_scrollViewer is null || !panel.IsDescendantOf(this))
                {
                    return;
                }

                UpdateLayout();

                var transformed = panel.TransformToAncestor(_scrollViewer).Transform(mapPoint);

                _scrollViewer.ScrollToHorizontalOffset(
                    _scrollViewer.HorizontalOffset + transformed.X - viewportPoint.X
                );

                _scrollViewer.ScrollToVerticalOffset(
                    _scrollViewer.VerticalOffset + transformed.Y - viewportPoint.Y
                );
            })
        );
    }

    private enum EditorGesture
    {
        None,
        NodeDragPending,
        NodeDrag,
        MarqueePending,
        Marquee,
    }
}
