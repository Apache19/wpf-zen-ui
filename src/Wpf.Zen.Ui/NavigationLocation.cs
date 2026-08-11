// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Describes the current semantic location produced by one navigation occurrence.</summary>
public sealed class NavigationLocation
{
    internal NavigationLocation(
        NavigationTarget target,
        object page,
        IReadOnlyList<BreadcrumbSegment> breadcrumbs)
    {
        Target = target;
        Page = page;
        Breadcrumbs = breadcrumbs;
    }

    /// <summary>Gets the destination and occurrence parameter represented by this location.</summary>
    public NavigationTarget Target { get; }

    /// <summary>Gets the page instance currently displayed by the navigation host.</summary>
    public object Page { get; }

    /// <summary>Gets the immutable semantic path resolved for this occurrence.</summary>
    public IReadOnlyList<BreadcrumbSegment> Breadcrumbs { get; }
}
