// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Participates in the successful navigation lifecycle of a navigation host.
/// </summary>
/// <remarks>
/// These callbacks describe navigation state, not WPF visual-tree state. They are independent of
/// <c>Loaded</c> and <c>Unloaded</c>, and cannot cancel a navigation request.
/// </remarks>
public interface INavigationAware
{
    /// <summary>
    /// Asynchronously handles the component entering the current navigation state.
    /// </summary>
    /// <param name="cancellationToken">
    /// A token that is cancelled when a later navigation supersedes this occurrence or when the
    /// navigation host is detached. Implementations should pass it to cancellable asynchronous
    /// work and avoid publishing stale results after cancellation.
    /// </param>
    /// <remarks>
    /// The navigation host invokes this callback only after it has successfully committed the
    /// destination and attached its visual tree. Failed or stopped requests do not enter the
    /// navigation lifecycle. The callback is not awaited by the host: a slow page cannot delay
    /// lifecycle delivery to a later destination.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnNavigationEnteredAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Handles the component leaving the current navigation state.
    /// </summary>
    /// <remarks>
    /// During a successful transition, the navigation host invokes this callback before starting
    /// <see cref="OnNavigationEnteredAsync(CancellationToken)"/> on the new component. The host
    /// also invokes it when the current navigation surface is genuinely detached from the visual
    /// tree. Exit is deliberately synchronous because navigation has already committed and cannot
    /// be rolled back or delayed by cleanup work at this boundary.
    /// </remarks>
    void OnNavigationExited();
}
