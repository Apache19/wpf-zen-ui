// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Provides runtime data to map label resolvers and page location declarations.</summary>
public sealed class NavigationLocationContext
{
    internal NavigationLocationContext(NavigationTarget target, object page)
    {
        Target = target;
        Page = page;
    }

    /// <summary>Gets the destination being resolved, including its occurrence parameter.</summary>
    public NavigationTarget Target { get; }

    /// <summary>Gets the page instance entered by the navigation occurrence.</summary>
    public object Page { get; }

    /// <summary>Gets the page's data context when the destination is a WPF framework element.</summary>
    public object? DataContext => (Page as FrameworkElement)?.DataContext;
}
