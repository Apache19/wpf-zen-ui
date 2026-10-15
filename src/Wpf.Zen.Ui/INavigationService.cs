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
    /// <summary>
    /// Gets a value indicating whether the attached host can navigate back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Gets a value indicating whether the attached host can navigate forward.
    /// </summary>
    bool CanGoForward { get; }

    /// <summary>
    /// Gets the read-only semantic state owned by the attached navigation surface.
    /// </summary>
    /// <remarks>
    /// This is the binding source for independent breadcrumb presenters. The state is scoped to
    /// the same host as this service and never follows application-global window focus.
    /// </remarks>
    INavigationState State { get; }

    /// <summary>
    /// Requests navigation using an explicit target and semantic-path hints.
    /// </summary>
    /// <param name="request">The complete navigation request.</param>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult Navigate(NavigationRequest request);

    /// <summary>
    /// Requests navigation to a composed semantic target.
    /// </summary>
    /// <param name="target">
    /// The route, page type, parameter, and placement identity to enter.
    /// </param>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult Navigate(NavigationTarget target);

    /// <summary>
    /// Navigates to the target represented by a resolved breadcrumb segment.
    /// </summary>
    /// <param name="segment">The clicked or otherwise invoked breadcrumb segment.</param>
    /// <returns>
    /// <see cref="NavigationResultStatus.Rejected"/> for a structural segment without a target;
    /// otherwise, the host's navigation result.
    /// </returns>
    NavigationResult Navigate(BreadcrumbSegment segment);

    /// <summary>
    /// Requests navigation to a page type.
    /// </summary>
    /// <param name="pageType">The page type to resolve inside this navigation scope.</param>
    /// <param name="parameter">
    /// An optional parameter published to the destination visual tree.
    /// </param>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult Navigate(Type pageType, object? parameter = null);

    /// <summary>
    /// Requests navigation to a target registered under an id or tag.
    /// </summary>
    /// <param name="pageIdOrTargetTag">The navigation item id or target tag.</param>
    /// <param name="parameter">
    /// An optional parameter published to the destination visual tree.
    /// </param>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult Navigate(string pageIdOrTargetTag, object? parameter = null);

    /// <summary>
    /// Requests navigation to the preceding history entry.
    /// </summary>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult GoBack();

    /// <summary>
    /// Requests navigation to the next history entry.
    /// </summary>
    /// <returns>An accepted or final result; await Completion for actual Frame commit.</returns>
    NavigationResult GoForward();

    /// <summary>
    /// Clears the history owned by this navigation scope.
    /// </summary>
    void ClearHistory();

    /// <summary>
    /// Asynchronously requests navigation, dispatching to the host's UI thread when necessary.
    /// </summary>
    /// <param name="request">The complete navigation request.</param>
    /// <param name="cancellationToken">
    /// Cancels before Frame commit without undoing a committed page.
    /// </param>
    /// <returns>
    /// The final commit, cancellation, or failure result, without waiting for page initialization.
    /// </returns>
    Task<NavigationResult> NavigateAsync(
        NavigationRequest request,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Asynchronously requests back navigation, dispatching to the host's UI thread when necessary.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels before Frame commit without undoing a committed page.
    /// </param>
    /// <returns>
    /// The final commit, cancellation, or failure result, without waiting for page initialization.
    /// </returns>
    Task<NavigationResult> GoBackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously requests forward navigation, dispatching to the host's UI thread when
    /// necessary.
    /// </summary>
    /// <param name="cancellationToken">
    /// Cancels before Frame commit without undoing a committed page.
    /// </param>
    /// <returns>
    /// The final commit, cancellation, or failure result, without waiting for page initialization.
    /// </returns>
    Task<NavigationResult> GoForwardAsync(CancellationToken cancellationToken = default);
}
