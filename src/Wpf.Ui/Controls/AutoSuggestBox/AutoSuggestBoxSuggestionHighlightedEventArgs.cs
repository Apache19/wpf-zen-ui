// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides event data for the <see cref="AutoSuggestBox.SuggestionHighlighted"/> event.
/// </summary>
/// <remarks>
/// Highlighting is a preview operation produced by keyboard or pointer navigation. It is deliberately
/// distinct from <see cref="AutoSuggestBox.SuggestionChosen"/>, which represents committing an item.
/// </remarks>
public sealed class AutoSuggestBoxSuggestionHighlightedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSuggestBoxSuggestionHighlightedEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The source that raised the event.</param>
    public AutoSuggestBoxSuggestionHighlightedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>
    /// Gets the suggestion currently highlighted in the suggestions list.
    /// </summary>
    public required object HighlightedItem { get; init; }
}
