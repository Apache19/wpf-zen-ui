// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui;

/// <summary>
/// Represents one navigation surface that owns its history, selection, and page cache.
/// </summary>
/// <remarks>
/// The contract intentionally has no dependency on <see cref="System.Windows.Window"/>. A host can
/// live in a window, a tab, an adorner, or a WPF control embedded in WinForms through ElementHost.
/// Infrastructure methods are part of the contract so an external scope factory can attach the
/// page provider and contextual service without depending on a concrete control type.
/// </remarks>
public interface INavigationHost
{
    /// <summary>
    /// Gets the dispatcher that owns this navigation surface.
    /// </summary>
    Dispatcher Dispatcher { get; }

    /// <summary>
    /// Gets the semantic navigation state owned by this host.
    /// </summary>
    INavigationState NavigationState { get; }

    /// <summary>
    /// Gets a value indicating whether this host has an entry to which it can navigate back.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Gets a value indicating whether this host has a forward history entry.
    /// </summary>
    bool CanGoForward { get; }

    /// <summary>
    /// Performs a navigation request on the host's dispatcher.
    /// </summary>
    /// <param name="request">The request to perform.</param>
    /// <param name="cancellationToken">
    /// Cancels before Frame commit without undoing a committed page.
    /// </param>
    /// <returns>An accepted or final result; await Completion for actual commit.</returns>
    NavigationResult Navigate(NavigationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs back navigation on the host's dispatcher.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request before Frame commit.</param>
    /// <returns>The result of the request.</returns>
    NavigationResult RequestGoBack(CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs forward navigation on the host's dispatcher.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request before Frame commit.</param>
    /// <returns>The result of the request.</returns>
    NavigationResult RequestGoForward(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears history owned by this host.
    /// </summary>
    void ClearNavigationHistory();

    /// <summary>
    /// Attaches the immutable service and page provider belonging to one navigation scope.
    /// </summary>
    /// <param name="navigationService">The contextual navigation service.</param>
    /// <param name="pageProvider">
    /// The page activator bound to this navigation scope. In DI mode it uses the same scoped
    /// service
    /// provider as <paramref name="navigationService"/>, but is not itself an application service.
    /// </param>
    /// <exception cref="InvalidOperationException">The host is already attached.</exception>
    void AttachNavigationScope(
        INavigationService navigationService,
        INavigationViewPageProvider pageProvider
    );

    /// <summary>
    /// Detaches a previously attached scope and clears context published to its page.
    /// </summary>
    /// <param name="navigationService">The service used when the scope was attached.</param>
    /// <returns>
    /// The host detachment result; the scope separately drains registered work and disposes its
    /// resources.
    /// </returns>
    /// <remarks>
    /// Hosts should transfer cleanup fields first, attempt all unbinding steps independently, and
    /// detach only the supplied service's session.
    /// This is an infrastructure entry point; application code should await
    /// INavigationScope.DisposeAsync.
    /// </remarks>
    Task DetachNavigationScope(INavigationService navigationService);
}
