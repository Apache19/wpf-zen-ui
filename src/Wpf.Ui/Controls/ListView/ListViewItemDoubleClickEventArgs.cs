// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides the source data item, realized container, position, and mouse button for a
/// <see cref="ListView.ItemDoubleClick"/> event.
/// </summary>
/// <remarks>
/// <see cref="Item"/> is the application-owned value from the items source. Consumers that need
/// presentation state can opt into <see cref="Container"/> without confusing it with data identity.
/// </remarks>
public sealed class ListViewItemDoubleClickEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ListViewItemDoubleClickEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event identifier assigned to this event data.</param>
    /// <param name="source">The <see cref="ListView"/> that reports the item activation.</param>
    /// <param name="item">The application-owned item from the items collection or source.</param>
    /// <param name="container">The realized container that received the double-click.</param>
    /// <param name="index">The current zero-based index of <paramref name="item"/>.</param>
    /// <param name="changedButton">The mouse button that produced the double-click.</param>
    public ListViewItemDoubleClickEventArgs(
        RoutedEvent routedEvent,
        object source,
        object item,
        System.Windows.Controls.ListViewItem container,
        int index,
        MouseButton changedButton
    )
        : base(routedEvent, source)
    {
        Item = item;
        Container = container;
        Index = index;
        ChangedButton = changedButton;
    }

    /// <summary>Gets the original item from the list's items collection or source.</summary>
    public object Item { get; }

    /// <summary>Gets the realized item container that received the double-click.</summary>
    public System.Windows.Controls.ListViewItem Container { get; }

    /// <summary>Gets the current zero-based index of <see cref="Item"/>.</summary>
    public int Index { get; }

    /// <summary>Gets the mouse button that produced the double-click.</summary>
    public MouseButton ChangedButton { get; }
}
