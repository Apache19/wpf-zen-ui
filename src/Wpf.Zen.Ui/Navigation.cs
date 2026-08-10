// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Provides contextual navigation information to a navigated WPF visual tree.</summary>
/// <remarks>
/// WPF Frame deliberately creates an inheritance boundary. NavigationView therefore publishes
/// these inherited, read-only values directly on the navigated page root. Descendant controls can
/// discover their nearest navigation scope without consulting an application singleton, while
/// non-visual view models should continue to receive <see cref="INavigationService"/> from the
/// scope's dependency-injection provider.
/// </remarks>
public static class Navigation
{
    private static readonly DependencyPropertyKey ServicePropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "Service",
            typeof(INavigationService),
            typeof(Navigation),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    private static readonly DependencyPropertyKey HostPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "Host",
            typeof(INavigationHost),
            typeof(Navigation),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    private static readonly DependencyPropertyKey ParameterPropertyKey =
        DependencyProperty.RegisterAttachedReadOnly(
            "Parameter",
            typeof(object),
            typeof(Navigation),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>Identifies the read-only attached Service dependency property.</summary>
    public static readonly DependencyProperty ServiceProperty = ServicePropertyKey.DependencyProperty;

    /// <summary>Identifies the read-only attached Host dependency property.</summary>
    public static readonly DependencyProperty HostProperty = HostPropertyKey.DependencyProperty;

    /// <summary>Identifies the read-only attached Parameter dependency property.</summary>
    public static readonly DependencyProperty ParameterProperty = ParameterPropertyKey.DependencyProperty;

    /// <summary>Gets the navigation service belonging to an element's nearest navigated page.</summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The nearest contextual service, or <see langword="null"/> outside a scoped page.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0042:Avoid side effects in CLR accessors",
        Justification = "GetValue is the standard read-only attached-property getter and does not mutate navigation state."
    )]
    public static INavigationService? GetService(DependencyObject element)
    {
        return element.GetValue(ServiceProperty) as INavigationService;
    }

    /// <summary>Gets the navigation host belonging to an element's nearest navigated page.</summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The nearest host, or <see langword="null"/> outside a scoped page.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0042:Avoid side effects in CLR accessors",
        Justification = "GetValue is the standard read-only attached-property getter and does not mutate navigation state."
    )]
    public static INavigationHost? GetHost(DependencyObject element)
    {
        return element.GetValue(HostProperty) as INavigationHost;
    }

    /// <summary>Gets the parameter supplied by the request that entered the current page.</summary>
    /// <param name="element">The page root or one of its logical descendants.</param>
    /// <returns>The request parameter, or <see langword="null"/> when none was supplied.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "WpfAnalyzers.DependencyProperty",
        "WPF0042:Avoid side effects in CLR accessors",
        Justification = "GetValue is the standard read-only attached-property getter and does not mutate navigation state."
    )]
    public static object? GetParameter(DependencyObject element)
    {
        return element.GetValue(ParameterProperty);
    }

    internal static void SetContext(
        DependencyObject element,
        INavigationService service,
        INavigationHost host,
        object? parameter
    )
    {
        element.SetValue(ServicePropertyKey, service);
        element.SetValue(HostPropertyKey, host);
        element.SetValue(ParameterPropertyKey, parameter);
    }

    internal static void ClearContext(DependencyObject element)
    {
        element.ClearValue(ServicePropertyKey);
        element.ClearValue(HostPropertyKey);
        element.ClearValue(ParameterPropertyKey);
    }
}
