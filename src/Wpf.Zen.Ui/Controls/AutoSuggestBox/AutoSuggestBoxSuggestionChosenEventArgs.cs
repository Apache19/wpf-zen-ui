// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides data for the <see cref="AutoSuggestBox.SuggestionChosen"/> event.
/// </summary>
public sealed class AutoSuggestBoxSuggestionChosenEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSuggestBoxSuggestionChosenEventArgs"/> class.
    /// </summary>
    /// <param name="routedEvent">The routed event associated with this instance.</param>
    /// <param name="source">The source that raised the event.</param>
    public AutoSuggestBoxSuggestionChosenEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>
    /// Gets the suggestion committed by the user.
    /// </summary>
    public required object SelectedItem { get; init; }
}
