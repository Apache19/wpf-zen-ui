// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Identifies a semantic navigation destination independently from any menu item or visual
/// container.
/// </summary>
/// <remarks>
/// <para>
/// A target may carry a route key, a page type, or both. Route keys distinguish multiple semantic
/// placements of the same page type, while the page type gives a host enough information to open a
/// page that has no menu entry. <see cref="Parameter"/> belongs to the destination occurrence so a
/// dynamic route such as an article can preserve its identifier when it is placed in a breadcrumb.
/// </para>
/// <para>
/// This type deliberately contains no reference to <c>NavigationViewItem</c>. Menus are optional
/// launch surfaces and are not the authority for application hierarchy.
/// </para>
/// </remarks>
public sealed class NavigationTarget
{
    /// <summary>Initializes a new instance of the <see cref="NavigationTarget"/> class.</summary>
    /// <param name="pageType">The page type to activate, or <see langword="null"/> for a route-only target.</param>
    /// <param name="routeKey">The stable route key, or <see langword="null"/> for a type-only target.</param>
    /// <param name="parameter">Optional data published to the destination visual tree.</param>
    /// <exception cref="ArgumentException">Neither a page type nor a non-empty route key was supplied.</exception>
    public NavigationTarget(Type? pageType, string? routeKey = null, object? parameter = null)
    {
        if (pageType is null && string.IsNullOrWhiteSpace(routeKey))
        {
            throw new ArgumentException("A navigation target requires a page type or a route key.");
        }

        if (routeKey is not null && string.IsNullOrWhiteSpace(routeKey))
        {
            throw new ArgumentException("A route key cannot be empty.", nameof(routeKey));
        }

        PageType = pageType;
        RouteKey = routeKey;
        Parameter = parameter;
    }

    /// <summary>Gets the page type activated by this target, when one is known.</summary>
    public Type? PageType { get; }

    /// <summary>
    /// Gets the stable semantic route key used to distinguish placements and aliases.
    /// </summary>
    public string? RouteKey { get; }

    /// <summary>Gets the optional data published to the destination visual tree.</summary>
    public object? Parameter { get; }

    /// <summary>Creates a type-based target.</summary>
    /// <param name="pageType">The page type to activate.</param>
    /// <param name="parameter">Optional destination data.</param>
    /// <returns>A validated type-based target.</returns>
    public static NavigationTarget ForPage(Type pageType, object? parameter = null) =>
        new(pageType ?? throw new ArgumentNullException(nameof(pageType)), parameter: parameter);

    /// <summary>Creates a type-based target.</summary>
    /// <typeparam name="TPage">The page type to activate.</typeparam>
    /// <param name="parameter">Optional destination data.</param>
    /// <returns>A type-based target.</returns>
    public static NavigationTarget ForPage<TPage>(object? parameter = null) =>
        ForPage(typeof(TPage), parameter);

    /// <summary>Creates a route-based target.</summary>
    /// <param name="routeKey">The stable route key.</param>
    /// <param name="parameter">Optional destination data.</param>
    /// <returns>A validated route-based target.</returns>
    public static NavigationTarget ForRoute(string routeKey, object? parameter = null) =>
        new(null, routeKey, parameter);

    /// <summary>Creates a copy that carries a different destination parameter.</summary>
    /// <param name="parameter">The new destination data.</param>
    /// <returns>A target retaining this instance's route and page identity.</returns>
    public NavigationTarget WithParameter(object? parameter) => new(PageType, RouteKey, parameter);

    /// <inheritdoc />
    public override string ToString() => RouteKey ?? PageType?.FullName ?? string.Empty;
}
