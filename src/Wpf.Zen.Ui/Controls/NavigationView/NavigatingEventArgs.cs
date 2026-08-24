// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="NavigationView.Navigating"/> event, which is raised
/// before a navigation request is committed and allows the request to be cancelled.
/// </summary>
public class NavigatingEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigatingEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The NavigationView that raised the event.</param>
    public NavigatingEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source)
    {
    }

    /// <summary>Gets the page instance proposed as the destination.</summary>
    public required object Page { get; init; }

    /// <summary>Gets or sets a value indicating whether the request is cancelled.</summary>
    public bool Cancel { get; set; }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        ((TypedEventHandler<NavigationView, NavigatingEventArgs>)genericHandler)(
            (NavigationView)genericTarget,
            this
        );
    }
}
