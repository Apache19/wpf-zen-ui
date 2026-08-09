// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

public class NavigatedEventArgs : RoutedEventArgs
{
    public NavigatedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    public required object Page { get; init; }
}
