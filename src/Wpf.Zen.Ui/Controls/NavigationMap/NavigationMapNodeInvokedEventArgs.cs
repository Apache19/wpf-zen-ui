// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Provides data for <see cref="NavigationMapView.NodeInvoked"/>.</summary>
public sealed class NavigationMapNodeInvokedEventArgs : RoutedEventArgs
{
    internal NavigationMapNodeInvokedEventArgs(
        RoutedEvent routedEvent,
        NavigationMapView source,
        object item,
        NavigationMapItem container)
        : base(routedEvent, source)
    {
        Item = item;
        Container = container;
        Node = item as NavigationMapNode;
        Target = Node?.Target ?? container.Target;
    }

    /// <summary>
    /// Gets the original item supplied through
    /// <see cref="System.Windows.Controls.ItemsControl.ItemsSource"/> or the item collection.
    /// </summary>
    public object Item { get; }

    /// <summary>Gets the generated or directly supplied visual container.</summary>
    public NavigationMapItem Container { get; }

    /// <summary>Gets the navigation-map node when the source item uses the built-in data model.</summary>
    public NavigationMapNode? Node { get; }

    /// <summary>Gets the optional navigation destination represented by the invoked node.</summary>
    public NavigationTarget? Target { get; }
}
