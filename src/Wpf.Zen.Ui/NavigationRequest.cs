// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Describes one navigation occurrence and any semantic-path information supplied by its caller.
/// </summary>
/// <remarks>
/// Path hints are occurrence data rather than history operations. An explicit path therefore does
/// not push a hierarchy stack; it only describes where the requested destination lives. The
/// journal independently records the completed occurrence for forward/back behavior.
/// </remarks>
public sealed class NavigationRequest
{
    private NavigationRequest(
        NavigationTarget target,
        object? breadcrumbContent = null,
        NavigationTarget? breadcrumbParent = null,
        IReadOnlyList<BreadcrumbSegment>? breadcrumbs = null)
    {
        Target = target;
        BreadcrumbContent = breadcrumbContent;
        BreadcrumbParent = breadcrumbParent;
        Breadcrumbs = breadcrumbs;
    }

    /// <summary>Gets the semantic destination and occurrence parameter.</summary>
    public NavigationTarget Target { get; }

    /// <summary>Gets an optional leaf label supplied specifically for this occurrence.</summary>
    public object? BreadcrumbContent { get; }

    /// <summary>Gets an optional map target whose path precedes the requested destination.</summary>
    public NavigationTarget? BreadcrumbParent { get; }

    /// <summary>Gets an optional explicit complete breadcrumb path.</summary>
    /// <remarks>An explicit path has the highest resolution priority and is used exactly as supplied.</remarks>
    public IReadOnlyList<BreadcrumbSegment>? Breadcrumbs { get; }

    /// <summary>Creates a request from an already composed semantic target.</summary>
    /// <param name="target">The destination to enter.</param>
    /// <returns>A request with no path overrides.</returns>
    public static NavigationRequest To(NavigationTarget target) =>
        new(target ?? throw new ArgumentNullException(nameof(target)));

    /// <summary>Creates a type-based request.</summary>
    /// <param name="pageType">The page type resolved by the scoped page provider.</param>
    /// <param name="parameter">Optional destination data; this does not replace DataContext.</param>
    /// <returns>A validated type-based request.</returns>
    public static NavigationRequest To(Type pageType, object? parameter = null) =>
        To(NavigationTarget.ForPage(pageType, parameter));

    /// <summary>Creates a type-based request.</summary>
    /// <typeparam name="TPage">The destination page type.</typeparam>
    /// <param name="parameter">Optional destination data; this does not replace DataContext.</param>
    /// <returns>A type-based request.</returns>
    public static NavigationRequest To<TPage>(object? parameter = null) => To(typeof(TPage), parameter);

    /// <summary>Creates a request targeting a registered route key, item id, or target tag.</summary>
    /// <param name="routeKey">The exact semantic route key or host lookup key.</param>
    /// <param name="parameter">Optional destination data; this does not replace DataContext.</param>
    /// <returns>A validated route-based request.</returns>
    public static NavigationRequest To(string routeKey, object? parameter = null) =>
        To(NavigationTarget.ForRoute(routeKey, parameter));

    /// <summary>Creates a copy with a caller-supplied dynamic leaf label.</summary>
    /// <param name="content">The content used by the final breadcrumb segment.</param>
    /// <returns>A request retaining every other path hint.</returns>
    public NavigationRequest WithBreadcrumbContent(object? content) =>
        new(Target, content, BreadcrumbParent, Breadcrumbs);

    /// <summary>Creates a copy whose path is prefixed by the map path of <paramref name="parent"/>.</summary>
    /// <param name="parent">The semantic parent target.</param>
    /// <returns>A request retaining its target and leaf label.</returns>
    public NavigationRequest WithBreadcrumbParent(NavigationTarget parent) =>
        new(Target, BreadcrumbContent, parent ?? throw new ArgumentNullException(nameof(parent)), Breadcrumbs);

    /// <summary>Creates a copy whose path is prefixed by the map path of a page type.</summary>
    /// <typeparam name="TParentPage">The page type registered as the semantic parent.</typeparam>
    /// <returns>A request retaining its target and leaf label.</returns>
    public NavigationRequest WithBreadcrumbParent<TParentPage>() =>
        WithBreadcrumbParent(NavigationTarget.ForPage<TParentPage>());

    /// <summary>Creates a copy with an explicit complete path.</summary>
    /// <param name="breadcrumbs">The complete ordered path, including the current leaf.</param>
    /// <returns>A request whose path will be used without map inference.</returns>
    /// <exception cref="ArgumentException">The path is empty or contains a null segment.</exception>
    public NavigationRequest WithBreadcrumbs(params BreadcrumbSegment[] breadcrumbs)
    {
        if (breadcrumbs is null)
        {
            throw new ArgumentNullException(nameof(breadcrumbs));
        }

        if (breadcrumbs.Length == 0)
        {
            throw new ArgumentException("An explicit breadcrumb path cannot be empty.", nameof(breadcrumbs));
        }

        if (breadcrumbs.Any(static segment => segment is null))
        {
            throw new ArgumentException("An explicit breadcrumb path cannot contain null segments.", nameof(breadcrumbs));
        }

        return new NavigationRequest(Target, BreadcrumbContent, BreadcrumbParent, Array.AsReadOnly(breadcrumbs.ToArray()));
    }

    /// <summary>Creates a copy with an explicit complete path from a dynamic sequence.</summary>
    /// <param name="breadcrumbs">The complete ordered path, including the current leaf.</param>
    /// <returns>A request containing an immutable snapshot of the supplied sequence.</returns>
    /// <exception cref="ArgumentException">The path is empty or contains a null segment.</exception>
    public NavigationRequest WithBreadcrumbs(IEnumerable<BreadcrumbSegment> breadcrumbs)
    {
        if (breadcrumbs is null)
        {
            throw new ArgumentNullException(nameof(breadcrumbs));
        }

        return WithBreadcrumbs(breadcrumbs.ToArray());
    }

    /// <summary>Retains path hints while a host normalizes a type-only target to a map placement.</summary>
    internal NavigationRequest WithTarget(NavigationTarget target) =>
        new(target, BreadcrumbContent, BreadcrumbParent, Breadcrumbs);
}
