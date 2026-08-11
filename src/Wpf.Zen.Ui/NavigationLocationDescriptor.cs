// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Supplies page-specific breadcrumb information that overrides or augments a navigation map.
/// </summary>
/// <remarks>
/// <see cref="Breadcrumbs"/> is an explicit complete path. When it is absent,
/// <see cref="Parent"/> selects a map path prefix and <see cref="Content"/> overrides only the leaf
/// label. This distinction supports both fixed exceptional paths and dynamic pages such as article
/// detail views without forcing either concern into menu data.
/// </remarks>
public sealed class NavigationLocationDescriptor
{
    /// <summary>Initializes a new instance of the <see cref="NavigationLocationDescriptor"/> class.</summary>
    /// <param name="content">An optional dynamic leaf label.</param>
    /// <param name="parent">An optional map target whose path precedes the current page.</param>
    /// <param name="breadcrumbs">An optional explicit complete path.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="breadcrumbs"/> is empty or contains a null segment.
    /// </exception>
    public NavigationLocationDescriptor(
        object? content = null,
        NavigationTarget? parent = null,
        IReadOnlyList<BreadcrumbSegment>? breadcrumbs = null)
    {
        Content = content;
        Parent = parent;
        Breadcrumbs = breadcrumbs is null ? null : SnapshotBreadcrumbs(breadcrumbs);
    }

    /// <summary>Gets the optional leaf label supplied by the current page or view model.</summary>
    public object? Content { get; }

    /// <summary>Gets the optional map target used as this page's semantic parent.</summary>
    public NavigationTarget? Parent { get; }

    /// <summary>Gets an optional explicit complete path, including the current leaf.</summary>
    public IReadOnlyList<BreadcrumbSegment>? Breadcrumbs { get; }

    private static IReadOnlyList<BreadcrumbSegment> SnapshotBreadcrumbs(
        IReadOnlyList<BreadcrumbSegment> breadcrumbs)
    {
        if (breadcrumbs.Count == 0)
        {
            throw new ArgumentException(
                "An explicit breadcrumb path cannot be empty.",
                nameof(breadcrumbs)
            );
        }

        var snapshot = new BreadcrumbSegment[breadcrumbs.Count];
        for (int index = 0; index < breadcrumbs.Count; index++)
        {
            snapshot[index] = breadcrumbs[index]
                ?? throw new ArgumentException(
                    "An explicit breadcrumb path cannot contain null segments.",
                    nameof(breadcrumbs)
                );
        }

        return Array.AsReadOnly(snapshot);
    }
}

/// <summary>Lets a page or its data context describe a dynamic semantic navigation location.</summary>
/// <remarks>
/// The navigation host checks the page first and then its data context. Raise
/// <see cref="LocationChanged"/> when a dynamic label or parent changes; the host will resolve the
/// current breadcrumb path again without adding a journal entry or recreating the page.
/// </remarks>
public interface INavigationLocationSource
{
    /// <summary>Occurs when the location description for the current page must be resolved again.</summary>
    event EventHandler? LocationChanged;

    /// <summary>Describes the current page occurrence.</summary>
    /// <param name="context">The target, parameter, page, and data context being resolved.</param>
    /// <returns>A dynamic location description.</returns>
    NavigationLocationDescriptor GetNavigationLocation(NavigationLocationContext context);
}
