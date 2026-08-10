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
    /// <summary>Gets the dispatcher that owns this navigation surface.</summary>
    Dispatcher Dispatcher { get; }

    /// <summary>Gets a value indicating whether this host has an entry to which it can navigate back.</summary>
    bool CanGoBack { get; }

    /// <summary>Performs a navigation request on the host's dispatcher.</summary>
    /// <param name="request">The request to perform.</param>
    /// <returns>The result of the request.</returns>
    NavigationResult Navigate(NavigationRequest request);

    /// <summary>Performs back navigation on the host's dispatcher.</summary>
    /// <returns>The result of the request.</returns>
    NavigationResult RequestGoBack();

    /// <summary>Clears history owned by this host.</summary>
    void ClearNavigationHistory();

    /// <summary>
    /// Attaches the immutable service and page provider belonging to one navigation scope.
    /// </summary>
    /// <param name="navigationService">The contextual navigation service.</param>
    /// <param name="pageProvider">The provider resolved from the same dependency-injection scope.</param>
    /// <exception cref="InvalidOperationException">The host is already attached.</exception>
    void AttachNavigationScope(
        INavigationService navigationService,
        INavigationViewPageProvider pageProvider
    );

    /// <summary>Detaches a previously attached scope and clears context published to its page.</summary>
    /// <param name="navigationService">The service used when the scope was attached.</param>
    /// <returns>
    /// A task that completes after the current page has received its asynchronous navigation-exit
    /// notification and scoped services are safe to dispose.
    /// </returns>
    Task DetachNavigationScopeAsync(INavigationService navigationService);
}
