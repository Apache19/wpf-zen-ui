// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides event data for the <see cref="AutoSuggestBox.QuerySubmitted"/> event.
/// </summary>
public sealed class AutoSuggestBoxQuerySubmittedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSuggestBoxQuerySubmittedEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The source that raised the event.</param>
    public AutoSuggestBoxQuerySubmittedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>
    /// Gets the query text submitted by the user.
    /// </summary>
    public required string QueryText { get; init; }
}
