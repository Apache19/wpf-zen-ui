// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Threading;
using NativeGridViewColumn = System.Windows.Controls.GridViewColumn;
using NativeGridViewColumnCollection = System.Windows.Controls.GridViewColumnCollection;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Coordinates <see cref="GridViewColumn"/> star widths with a GridView ScrollViewer viewport.
/// </summary>
/// <remarks>
/// <para>
/// This attached behavior is infrastructure for the Fluent GridView template. It intentionally
/// lives on the single scrolling owner instead of deriving every header and row presenter: WPF's
/// native presenters already provide Auto measurement, shared column state, header reordering,
/// scrolling synchronization, virtualization participation, and automation support.
/// </para>
/// <para>
/// Applications normally enable the behavior by using <see cref="GridView"/>. It is public so a
/// replacement ListView template can retain star sizing by binding <see cref="ColumnsProperty"/>
/// on its main <see cref="ScrollViewer"/>.
/// </para>
/// </remarks>
public static class GridViewColumnSizing
{
    /// <summary>
    /// Identifies the <c>Columns</c> attached dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
        "Columns",
        typeof(NativeGridViewColumnCollection),
        typeof(GridViewColumnSizing),
        new PropertyMetadata(null, OnColumnsChanged)
    );

    /// <summary>
    /// Identifies the <c>LeadingContentWidth</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// This value reserves viewport space used by template content that precedes the first column,
    /// such as the Fluent row-selection indicator gutter. The default is <c>0</c> so replacement
    /// templates without leading content continue to allocate the complete viewport to columns.
    /// </remarks>
    public static readonly DependencyProperty LeadingContentWidthProperty =
        DependencyProperty.RegisterAttached(
            "LeadingContentWidth",
            typeof(double),
            typeof(GridViewColumnSizing),
            new FrameworkPropertyMetadata(0.0, OnLeadingContentWidthChanged),
            static value =>
                value is double width
                && !double.IsNaN(width)
                && !double.IsInfinity(width)
                && width >= 0.0
        );

    private static readonly DependencyProperty CoordinatorProperty = DependencyProperty.RegisterAttached(
        "Coordinator",
        typeof(Coordinator),
        typeof(GridViewColumnSizing),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets the columns whose star definitions are coordinated with <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The ScrollViewer that owns the GridView viewport.</param>
    /// <returns>The coordinated column collection, or <see langword="null"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
    public static NativeGridViewColumnCollection? GetColumns(DependencyObject element)
    {
        return (NativeGridViewColumnCollection?)element.GetValue(ColumnsProperty);
    }

    /// <summary>
    /// Sets the columns whose star definitions are coordinated with <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The ScrollViewer that owns the GridView viewport.</param>
    /// <param name="value">The GridView column collection, or <see langword="null"/> to detach.</param>
    public static void SetColumns(DependencyObject element, NativeGridViewColumnCollection? value)
    {
        element.SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Gets the width reserved before the first coordinated column.
    /// </summary>
    /// <param name="element">The ScrollViewer that owns the GridView viewport.</param>
    /// <returns>The reserved device-independent width. The default is <c>0</c>.</returns>
    [AttachedPropertyBrowsableForType(typeof(ScrollViewer))]
    public static double GetLeadingContentWidth(DependencyObject element)
    {
        return (double)element.GetValue(LeadingContentWidthProperty);
    }

    /// <summary>
    /// Sets the width occupied by template content before the first coordinated column.
    /// </summary>
    /// <param name="element">The ScrollViewer that owns the GridView viewport.</param>
    /// <param name="value">A finite, non-negative device-independent width.</param>
    public static void SetLeadingContentWidth(DependencyObject element, double value)
    {
        element.SetValue(LeadingContentWidthProperty, value);
    }

    private static void OnLeadingContentWidthChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d.GetValue(CoordinatorProperty) is Coordinator coordinator)
        {
            coordinator.OnLeadingContentWidthChanged();
        }
    }

    private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer scrollViewer)
        {
            throw new InvalidOperationException(
                $"{nameof(GridViewColumnSizing)}.{nameof(ColumnsProperty)} can only be set on a ScrollViewer."
            );
        }

        if (scrollViewer.GetValue(CoordinatorProperty) is Coordinator previousCoordinator)
        {
            previousCoordinator.Detach();
            scrollViewer.ClearValue(CoordinatorProperty);
        }

        if (e.NewValue is NativeGridViewColumnCollection columns)
        {
            // The private attached property owns this coordinator for exactly the lifetime of the
            // public Columns value; OnColumnsChanged detaches it before replacement or removal.
            var coordinator = new Coordinator(scrollViewer, columns);
            scrollViewer.SetValue(CoordinatorProperty, coordinator);
        }
    }

    /// <summary>
    /// Owns the event lifetime and ensures that one viewport, rather than every realized row,
    /// performs star redistribution.
    /// </summary>
    private sealed class Coordinator
    {
        private readonly ScrollViewer _scrollViewer;
        private readonly NativeGridViewColumnCollection _columns;
        private readonly List<NativeGridViewColumn> _subscribedColumns = [];
        private bool _isActive;
        private bool _isUpdating;
        private bool _updatePending;

        internal Coordinator(ScrollViewer scrollViewer, NativeGridViewColumnCollection columns)
        {
            _scrollViewer = scrollViewer;
            _columns = columns;
            _scrollViewer.Loaded += OnLoaded;
            _scrollViewer.Unloaded += OnUnloaded;

            if (_scrollViewer.IsLoaded)
            {
                Activate();
            }
        }

        internal void Detach()
        {
            _scrollViewer.Loaded -= OnLoaded;
            _scrollViewer.Unloaded -= OnUnloaded;
            Deactivate();
        }

        internal void OnLeadingContentWidthChanged()
        {
            UpdateWidths();
            ScheduleUpdate();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Activate();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Deactivate();
        }

        private void Activate()
        {
            if (_isActive)
            {
                return;
            }

            _isActive = true;
            _scrollViewer.ScrollChanged += OnScrollChanged;
            _scrollViewer.SizeChanged += OnSizeChanged;
            ((INotifyCollectionChanged)_columns).CollectionChanged += OnCollectionChanged;
            SubscribeColumns();

            UpdateWidths();
            ScheduleUpdate();
        }

        private void Deactivate()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer.SizeChanged -= OnSizeChanged;
            ((INotifyCollectionChanged)_columns).CollectionChanged -= OnCollectionChanged;
            UnsubscribeColumns();
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (!AreClose(e.ViewportWidthChange, 0.0))
            {
                // ScrollViewer raises this after publishing the new viewport. Updating immediately
                // prevents an obsolete star total from participating in another scrollbar decision.
                UpdateWidths();
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!AreClose(e.PreviousSize.Width, e.NewSize.Width))
            {
                UpdateWidths();
                ScheduleUpdate();
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            // Rebuild subscriptions because GridView collections are small and Reset does not expose
            // old items. This also makes Move and Replace follow exactly the same reliable path.
            UnsubscribeColumns();
            SubscribeColumns();
            UpdateWidths();
            ScheduleUpdate();
        }

        private void OnColumnLayoutPropertyChanged(object? sender, EventArgs e)
        {
            // Header dragging and programmatic fixed-width changes must contract the remaining stars
            // synchronously; delaying this update is what lets a transient horizontal extent escape.
            UpdateWidths();
            ScheduleUpdate();
        }

        private void OnColumnResizeRequested(
            object? sender,
            GridViewColumnResizeRequestedEventArgs e
        )
        {
            if (
                !_isActive
                || _isUpdating
                || sender is not GridViewColumn column
                || !TryGetViewportWidth(out double viewportWidth)
            )
            {
                return;
            }

            _isUpdating = true;
            try
            {
                // This callback runs inside the native Width dependency-property change. Handling
                // it synchronously prevents that temporary finite width from becoming a published
                // horizontal extent before the remaining stars absorb the boundary delta.
                e.Handled = GridViewColumnLayout.TryResizeStarColumn(
                    _columns,
                    column,
                    e.PreviousWidth,
                    e.RequestedWidth,
                    viewportWidth,
                    GetLeadingContentWidth(_scrollViewer)
                );
            }
            finally
            {
                _isUpdating = false;
            }

            ScheduleUpdate();
        }

        private void OnNativeColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is "ActualWidth" or "Width")
            {
                // Auto desired widths are finalized during WPF layout. Defer their dependent star
                // allocation until the current presenter has completed its state transition.
                ScheduleUpdate();
            }
        }

        private void SubscribeColumns()
        {
            foreach (NativeGridViewColumn column in _columns)
            {
                _subscribedColumns.Add(column);
                ((INotifyPropertyChanged)column).PropertyChanged += OnNativeColumnPropertyChanged;
                if (column is GridViewColumn gridLengthColumn)
                {
                    gridLengthColumn.LayoutPropertyChanged += OnColumnLayoutPropertyChanged;
                    gridLengthColumn.ResizeRequested += OnColumnResizeRequested;
                }
            }
        }

        private void UnsubscribeColumns()
        {
            foreach (NativeGridViewColumn column in _subscribedColumns)
            {
                ((INotifyPropertyChanged)column).PropertyChanged -= OnNativeColumnPropertyChanged;
                if (column is GridViewColumn gridLengthColumn)
                {
                    gridLengthColumn.LayoutPropertyChanged -= OnColumnLayoutPropertyChanged;
                    gridLengthColumn.ResizeRequested -= OnColumnResizeRequested;
                }
            }

            _subscribedColumns.Clear();
        }

        private void ScheduleUpdate()
        {
            if (!_isActive || _updatePending)
            {
                return;
            }

            _updatePending = true;
            _ = _scrollViewer.Dispatcher.BeginInvoke(
                DispatcherPriority.Loaded,
                new Action(() =>
                {
                    _updatePending = false;
                    UpdateWidths();
                })
            );
        }

        private void UpdateWidths()
        {
            if (!_isActive || _isUpdating || !TryGetViewportWidth(out double viewportWidth))
            {
                return;
            }

            _isUpdating = true;
            try
            {
                GridViewColumnLayout.CoerceAutoColumns(_columns);
                _ = GridViewColumnLayout.ResolveStarColumns(
                    _columns,
                    viewportWidth,
                    GetLeadingContentWidth(_scrollViewer)
                );
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private bool TryGetViewportWidth(out double viewportWidth)
        {
            viewportWidth = _scrollViewer.ViewportWidth;
            if (IsFinitePositive(viewportWidth))
            {
                return true;
            }

            // Before the first ScrollInfo publication, ActualWidth is the only finite public value.
            // Star columns use MinWidth placeholders during this short phase, so this fallback can
            // only improve the first layout and is replaced by the authoritative viewport event.
            viewportWidth =
                _scrollViewer.ActualWidth
                - _scrollViewer.Padding.Left
                - _scrollViewer.Padding.Right;
            return IsFinitePositive(viewportWidth);
        }

        private static bool IsFinitePositive(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
        }

        private static bool AreClose(double first, double second)
        {
            double delta = Math.Abs(first - second);
            return delta < 0.000001 * (Math.Abs(first) + Math.Abs(second) + 1.0);
        }
    }
}
