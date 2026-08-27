// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides data for a user-requested page change.
/// </summary>
public sealed class PageRequestedEventArgs(RoutedEvent routedEvent, int page)
    : RoutedEventArgs(routedEvent)
{
    /// <summary>Gets the page number requested by the user.</summary>
    public int Page { get; } = page;
}
