// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Specifies how a navigation target is entered.</summary>
public enum NavigationMode
{
    /// <summary>Replaces the current semantic navigation-stack entry.</summary>
    Standard,

    /// <summary>Adds the destination to the current semantic hierarchy.</summary>
    Hierarchical,
}

/// <summary>Describes a navigation target and the data passed to it.</summary>
public sealed class NavigationRequest
{
    private NavigationRequest(
        Type? pageType,
        string? pageIdOrTargetTag,
        object? parameter,
        NavigationMode mode
    )
    {
        PageType = pageType;
        PageIdOrTargetTag = pageIdOrTargetTag;
        Parameter = parameter;
        Mode = mode;
    }

    /// <summary>Gets the requested page type, or <see langword="null"/> for an id/tag request.</summary>
    public Type? PageType { get; }

    /// <summary>Gets the requested item id or target tag, or <see langword="null"/> for a type request.</summary>
    public string? PageIdOrTargetTag { get; }

    /// <summary>
    /// Gets application data published through <see cref="Navigation.GetParameter"/> on the
    /// destination tree. It does not replace the page's dependency-injected DataContext.
    /// </summary>
    public object? Parameter { get; }

    /// <summary>Gets the navigation-stack behavior requested by the caller.</summary>
    public NavigationMode Mode { get; }

    /// <summary>Creates a request targeting a page type.</summary>
    /// <param name="pageType">The page type that the scoped page provider will resolve.</param>
    /// <param name="parameter">Optional navigation data; this does not replace DataContext.</param>
    /// <param name="mode">The semantic stack behavior for the destination.</param>
    /// <returns>A validated type-based navigation request.</returns>
    public static NavigationRequest To(
        Type pageType,
        object? parameter = null,
        NavigationMode mode = NavigationMode.Standard
    )
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        return new NavigationRequest(pageType, null, parameter, mode);
    }

    /// <summary>Creates a request targeting a page type.</summary>
    /// <typeparam name="TPage">The destination page type.</typeparam>
    /// <param name="parameter">Optional navigation data; this does not replace DataContext.</param>
    /// <param name="mode">The semantic stack behavior for the destination.</param>
    /// <returns>A type-based navigation request for <typeparamref name="TPage"/>.</returns>
    public static NavigationRequest To<TPage>(
        object? parameter = null,
        NavigationMode mode = NavigationMode.Standard
    ) => To(typeof(TPage), parameter, mode);

    /// <summary>Creates a request targeting a registered navigation item id or tag.</summary>
    /// <param name="pageIdOrTargetTag">The exact item id or target tag registered by the host.</param>
    /// <param name="parameter">Optional navigation data; this does not replace DataContext.</param>
    /// <param name="mode">The semantic stack behavior for the destination.</param>
    /// <returns>A validated key-based navigation request.</returns>
    public static NavigationRequest To(
        string pageIdOrTargetTag,
        object? parameter = null,
        NavigationMode mode = NavigationMode.Standard
    )
    {
        if (string.IsNullOrWhiteSpace(pageIdOrTargetTag))
        {
            throw new ArgumentException("A navigation target cannot be empty.", nameof(pageIdOrTargetTag));
        }

        return new NavigationRequest(null, pageIdOrTargetTag, parameter, mode);
    }
}
