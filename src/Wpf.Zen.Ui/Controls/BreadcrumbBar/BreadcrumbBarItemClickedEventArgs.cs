// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides the source data item, realized container, and position for a
/// <see cref="BreadcrumbBar.ItemClicked"/> event.
/// </summary>
/// <remarks>
/// <see cref="Item"/> is the application-owned value from the items source. Consumers that need
/// presentation state can opt into <see cref="Container"/> without confusing it with data identity.
/// </remarks>
public sealed class BreadcrumbBarItemClickedEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="BreadcrumbBarItemClickedEventArgs"/> class.</summary>
    public BreadcrumbBarItemClickedEventArgs(
        RoutedEvent       routedEvent,
        object            source,
        object            item,
        BreadcrumbBarItem container,
        int               index)
        : base(routedEvent, source)
    {
        Item      = item;
        Container = container;
        Index     = index;
    }

    /// <summary>
    /// Gets the original item from <see cref="System.Windows.Controls.ItemsControl.ItemsSource"/>.
    /// </summary>
    public object Item { get; }

    /// <summary>Gets the item container that received the click.</summary>
    public BreadcrumbBarItem Container { get; }

    /// <summary>
    /// Gets the index of the item that was clicked.
    /// </summary>
    public int Index { get; }
}