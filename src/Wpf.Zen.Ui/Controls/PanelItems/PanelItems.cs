// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents a collection of elements as direct children of an application-defined panel.
/// </summary>
/// <remarks>
/// Static children remain in the panel, followed by source elements in collection order. Attached
/// layout properties on each element therefore apply directly to the host, including Grid rows
/// and columns. Presentation starts on Loaded and releases elements and collection subscriptions
/// on Unloaded. Observable sources update automatically; other sources are read on source changes
/// and reload. Each element must be distinct and available to this one host. Items-host panels
/// managed by an ItemsControl cannot use this behavior. Collection changes must use the UI thread.
/// </remarks>
public static class PanelItems
{
    /// <summary>
    /// Identifies the source of elements appended to a loaded panel. The default is null.
    /// </summary>
    public static readonly DependencyProperty SourceProperty
        = DependencyProperty.RegisterAttached(
            "Source",
            typeof(IEnumerable<UIElement>),
            typeof(PanelItems),
            new PropertyMetadata(null, OnSourceChanged)
        );

    /// <summary>
    /// Gets the elements presented as direct children of a panel.
    /// </summary>
    /// <param name="panel">The panel that presents the elements.</param>
    /// <returns>The source collection, or null when no source is assigned.</returns>
    [AttachedPropertyBrowsableForType(typeof(Panel))]
    public static IEnumerable<UIElement>? GetSource(Panel panel)
    {
        if (panel is null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        return (IEnumerable<UIElement>?)panel.GetValue(SourceProperty);
    }

    /// <summary>
    /// Sets the elements presented by a loaded panel, preserving its static children.
    /// </summary>
    /// <param name="panel">The panel that presents the elements.</param>
    /// <param name="source">The source collection, or null to release contributed elements.</param>
    public static void SetSource(Panel panel, IEnumerable<UIElement>? source)
    {
        if (panel is null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        panel.SetValue(SourceProperty, source);
    }

    /// <summary>
    /// Stores the host-owned lifecycle controller without adding an element to either WPF tree.
    /// </summary>
    private static readonly DependencyProperty ProjectionProperty
        = DependencyProperty.RegisterAttached(
            "Projection",
            typeof(Projection),
            typeof(PanelItems),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Connects or releases the controller when a binding replaces the panel's source.
    /// </summary>
    private static void OnSourceChanged(DependencyObject target, DependencyPropertyChangedEventArgs args)
    {
        if (target is not Panel panel)
        {
            throw new InvalidOperationException("PanelItems.Source can only be applied to a Panel.");
        }

        var projection = (Projection?)panel.GetValue(ProjectionProperty);
        if (args.NewValue is null)
        {
            projection?.Dispose();
            panel.ClearValue(ProjectionProperty);
            return;
        }

        if (projection is null)
        {
            projection = new Projection(panel);
            panel.SetValue(ProjectionProperty, projection);
        }

        projection.SetSource((IEnumerable<UIElement>)args.NewValue);
    }

    /// <summary>
    /// Owns only contributed children and observes a source while its panel is loaded.
    /// </summary>
    private sealed class Projection : IDisposable
    {
        private readonly Panel           _panel;
        private readonly List<UIElement> _mounted = [];

        private IEnumerable<UIElement>?   _source;
        private INotifyCollectionChanged? _observedCollection;

        private bool _synchronizing;
        private bool _synchronizeAgain;
        private bool _disposed;

        /// <summary>
        /// Connects one panel's load and unload notifications to its contribution lifecycle.
        /// </summary>
        /// <param name="panel">The panel whose direct children are managed.</param>
        public Projection(Panel panel)
        {
            _panel = panel;
            _panel.Loaded   += OnLoaded;
            _panel.Unloaded += OnUnloaded;
        }

        /// <summary>
        /// Replaces the observed source; unloaded hosts defer presentation until their next load.
        /// </summary>
        /// <param name="source">The collection of elements contributed to this panel.</param>
        public void SetSource(IEnumerable<UIElement> source)
        {
            StopObserving();

            _source = source;

            if (_panel.IsLoaded)
            {
                Observe();
                Synchronize();
            }
        }

        /// <summary>
        /// Releases the host's contributed elements and all lifecycle subscriptions.
        /// </summary>
        public void Dispose()
        {
            _disposed = true;

            _panel.Loaded  -= OnLoaded;
            _panel.Unloaded -= OnUnloaded;

            StopObserving();
            ReleaseElements();

            _source = null;
        }

        private void OnLoaded(object sender, RoutedEventArgs args)
        {
            Observe();
            Synchronize();
        }

        private void OnUnloaded(object sender, RoutedEventArgs args)
        {
            StopObserving();
            ReleaseElements();
        }

        /// <summary>
        /// Uses a weak subscription so an external collection cannot retain its visual host.
        /// </summary>
        private void Observe()
        {
            StopObserving();

            _observedCollection = _source as INotifyCollectionChanged;

            if (_observedCollection is not null)
            {
                CollectionChangedEventManager.AddHandler(_observedCollection, OnCollectionChanged);
            }
        }

        private void StopObserving()
        {
            if (_observedCollection is not null)
            {
                CollectionChangedEventManager.RemoveHandler(_observedCollection, OnCollectionChanged);
                _observedCollection = null;
            }
        }

        private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
        {
            _panel.Dispatcher.VerifyAccess();
            Synchronize();
        }

        /// <summary>
        /// Reconciles a validated snapshot, retaining unchanged elements and their WPF state.
        /// </summary>
        private void Synchronize()
        {
            if (_synchronizing)
            {
                _synchronizeAgain = true;
                return;
            }

            _synchronizing = true;

            try
            {
                do
                {
                    _synchronizeAgain = false;

                    if (_disposed || !_panel.IsLoaded || _source is null)
                    {
                        return;
                    }

                    var items = _source.ToList();

                    Validate(items);

                    foreach (var previous in _mounted.ToArray())
                    {
                        if (!items.Contains(previous))
                        {
                            if (_panel.Children.Contains(previous))
                            {
                                _panel.Children.Remove(previous);
                            }

                            _mounted.Remove(previous);
                        }
                    }

                    // Working backwards places the source after static children without moving
                    // any static child or detaching contributions already in the right position.
                    UIElement? next = null;

                    for (var index = items.Count - 1; index >= 0 && !_disposed; index--)
                    {
                        var item         = items[index];
                        var currentIndex = _panel.Children.IndexOf(item);

                        var nextIndex = next is null ? _panel.Children.Count : _panel.Children.IndexOf(next);

                        if (currentIndex < 0 || currentIndex != nextIndex - 1)
                        {
                            if (currentIndex >= 0)
                            {
                                _panel.Children.Remove(item);
                            }

                            if (!_mounted.Contains(item))
                            {
                                _mounted.Add(item);
                            }

                            nextIndex = next is null ? _panel.Children.Count : _panel.Children.IndexOf(next);
                            _panel.Children.Insert(nextIndex, item);
                        }

                        next = item;
                    }

                    Debug.WriteLine(
                        $"[PanelItems.Host] SOURCE applied; panelType={_panel.GetType().Name}, itemCount={items.Count}."
                    );
                }
                while (_synchronizeAgain);
            }
            finally
            {
                _synchronizing = false;
            }
        }

        /// <summary>
        /// Checks the complete source before mutation to avoid partial transfers or parent theft.
        /// </summary>
        private void Validate(List<UIElement> items)
        {
            if (_panel.IsItemsHost)
            {
                throw new InvalidOperationException("PanelItems.Source cannot manage an ItemsControl items host.");
            }

            var seen = new HashSet<UIElement>();
            foreach (var item in items)
            {
                if (item is null || !seen.Add(item))
                {
                    throw new InvalidOperationException("PanelItems.Source requires distinct, non-null elements.");
                }

                item.Dispatcher.VerifyAccess();

                var visualParent  = VisualTreeHelper.GetParent(item);
                var logicalParent = LogicalTreeHelper.GetParent(item);

                if ((visualParent is not null && !ReferenceEquals(visualParent, _panel))
                    || (logicalParent is not null && !ReferenceEquals(logicalParent, _panel))
                    || (_panel.Children.Contains(item) && !_mounted.Contains(item)))
                {
                    throw new InvalidOperationException("A source element already belongs to another owner.");
                }

                if (ReferenceEquals(item, _panel) || item.IsAncestorOf(_panel))
                {
                    throw new InvalidOperationException("A source element cannot contain its own host panel.");
                }
            }
        }

        /// <summary>
        /// Removes exactly the elements owned by this controller, preserving static children.
        /// </summary>
        private void ReleaseElements()
        {
            foreach (var item in _mounted.ToArray())
            {
                if (_panel.Children.Contains(item))
                {
                    _panel.Children.Remove(item);
                }
            }

            _mounted.Clear();
        }
    }
}
