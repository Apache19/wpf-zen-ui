// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="AutoSuggestBox.TextChanged"/> event.
/// </summary>
public sealed class AutoSuggestBoxTextChangedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSuggestBoxTextChangedEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The source that raised the event.</param>
    public AutoSuggestBoxTextChangedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>
    /// Gets the new text displayed by the <see cref="AutoSuggestBox"/>.
    /// </summary>
    public required string Text { get; init; }
}
