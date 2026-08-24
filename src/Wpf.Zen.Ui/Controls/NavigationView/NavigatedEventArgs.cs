// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="NavigationView.Navigated"/> event, which is raised
/// after a navigation request has completed and the destination page has been selected.
/// </summary>
public class NavigatedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigatedEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The NavigationView that raised the event.</param>
    public NavigatedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source)
    {
    }

    /// <summary>Gets the page instance selected by the navigation request.</summary>
    public required object Page { get; init; }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        ((TypedEventHandler<NavigationView, NavigatedEventArgs>)genericHandler)(
            (NavigationView)genericTarget,
            this
        );
    }
}
