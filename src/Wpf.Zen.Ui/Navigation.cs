// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Publishes the navigation service, host, and parameter to the page visual tree as one immutable
/// snapshot.
/// </summary>
/// <remarks>
/// Frame forms a property inheritance boundary, so the snapshot is written directly to the page
/// root. Bindings may use
/// (Navigation.Context).Parameter; all three values observed in a property notification belong to
/// the same commit.
/// </remarks>
public static class Navigation
{
    private static readonly DependencyPropertyKey ContextPropertyKey
        = DependencyProperty.RegisterAttachedReadOnly(
            "Context", typeof(NavigationContext), typeof(Navigation),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>
    /// Identifies the read-only inheritable navigation context attached property, cleared on page
    /// exit and scope detachment.
    /// </summary>
    public static readonly DependencyProperty ContextProperty
        = ContextPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the immutable navigation context from the nearest page root, or null outside a scope.
    /// </summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The complete current context, or null.</returns>
    public static NavigationContext? GetContext(DependencyObject element)
    {
        return (NavigationContext?)element.GetValue(ContextProperty);
    }

    /// <summary>
    /// Gets the owning session's service, which becomes permanently invalid when that session
    /// stops.
    /// </summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The service from the nearest context, or null.</returns>
    public static INavigationService? GetService(DependencyObject element)
    {
        var context = GetContext(element);
        return context?.Service;
    }

    /// <summary>
    /// Gets the navigation host owning the current page.
    /// </summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The host from the nearest context, or null.</returns>
    public static INavigationHost? GetHost(DependencyObject element)
    {
        var context = GetContext(element);
        return context?.Host;
    }

    /// <summary>
    /// Gets this request's parameter; navigation preserves the page DataContext.
    /// </summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The parameter from the nearest context, or null.</returns>
    public static object? GetParameter(DependencyObject element)
    {
        var context = GetContext(element);
        return context?.Parameter;
    }

    /// <summary>
    /// Publishes context atomically so property notifications observe service and parameter values
    /// from the same request.
    /// </summary>
    internal static void SetContext(
        DependencyObject   element,
        INavigationService service,
        INavigationHost    host,
        object?            parameter)
    {
        var context = new NavigationContext(service, host, parameter);
        element.SetValue(ContextPropertyKey, context);
    }

    /// <summary>
    /// Clears the inherited snapshot at once; callers must transfer owned references before
    /// property callbacks can reenter.
    /// </summary>
    internal static void ClearContext(DependencyObject element)
    {
        element.ClearValue(ContextPropertyKey);
    }
}

/// <summary>
/// Stores the visual context of one committed navigation without owning session or page resources.
/// </summary>
public sealed class NavigationContext
{
    internal NavigationContext(INavigationService service, INavigationHost host, object? parameter)
    {
        Service   = service;
        Host      = host;
        Parameter = parameter;
    }

    /// <summary>
    /// Gets the page's session service, which cannot control subsequent sessions after stopping.
    /// </summary>
    public INavigationService Service { get; }

    /// <summary>
    /// Gets the navigation host that published this snapshot.
    /// </summary>
    public INavigationHost Host { get; }

    /// <summary>
    /// Gets this navigation's parameter, or null if none was supplied.
    /// </summary>
    public object? Parameter { get; }
}