// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// Provides navigation operations for exactly one attached navigation host.
/// </summary>
/// <remarks>
/// A navigation service is scoped to a navigation surface rather than to an application or a
/// window. Its target is immutable after attachment, so resolving this service from a page or view
/// model can never redirect a request to a different window, tab, or hosted WPF island.
/// </remarks>
public interface INavigationService
{
    /// <summary>Gets a value indicating whether the attached host can navigate back.</summary>
    bool CanGoBack { get; }

    /// <summary>Requests navigation using an explicit target, parameter, and navigation mode.</summary>
    /// <param name="request">The complete navigation request.</param>
    /// <returns>A result describing whether the host performed the request.</returns>
    NavigationResult Navigate(NavigationRequest request);

    /// <summary>Requests navigation to a page type.</summary>
    /// <param name="pageType">The page type to resolve inside this navigation scope.</param>
    /// <param name="parameter">An optional parameter published to the destination visual tree.</param>
    /// <returns>A result describing whether the host performed the request.</returns>
    NavigationResult Navigate(Type pageType, object? parameter = null);

    /// <summary>Requests navigation to a target registered under an id or tag.</summary>
    /// <param name="pageIdOrTargetTag">The navigation item id or target tag.</param>
    /// <param name="parameter">An optional parameter published to the destination visual tree.</param>
    /// <returns>A result describing whether the host performed the request.</returns>
    NavigationResult Navigate(string pageIdOrTargetTag, object? parameter = null);

    /// <summary>Requests hierarchical navigation to a page type.</summary>
    /// <param name="pageType">The page type to add to the active hierarchy.</param>
    /// <param name="parameter">An optional parameter published to the destination visual tree.</param>
    /// <returns>A result describing whether the host performed the request.</returns>
    NavigationResult NavigateWithHierarchy(Type pageType, object? parameter = null);

    /// <summary>Requests navigation to the preceding history entry.</summary>
    /// <returns>A result describing whether the host performed the request.</returns>
    NavigationResult GoBack();

    /// <summary>Clears the history owned by this navigation scope.</summary>
    void ClearHistory();

    /// <summary>
    /// Asynchronously requests navigation, dispatching to the host's UI thread when necessary.
    /// </summary>
    /// <param name="request">The complete navigation request.</param>
    /// <param name="cancellationToken">Cancels a request that has not begun on the UI thread.</param>
    /// <returns>A task containing the navigation result.</returns>
    Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously requests back navigation, dispatching to the host's UI thread when necessary.
    /// </summary>
    /// <param name="cancellationToken">Cancels a request that has not begun on the UI thread.</param>
    /// <returns>A task containing the navigation result.</returns>
    Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default);
}
