// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Asynchronously participates in the successful entry lifecycle of a navigation host.
/// </summary>
/// <remarks>
/// Enters at the Loaded scheduling boundary after Frame commit; real unload ends activation and
/// reattachment creates a new one.
/// The same instance waits for its previous activation to drain. Use INavigationActivationAware to
/// register subsequent commands.
/// </remarks>
public interface IAsyncNavigationAware : INavigationExited
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
    /// Registers exit responsibility before entry, including synchronous callback failures. Request
    /// results wait only for Frame commit;
    /// scope stop waits for this task and associated error delivery. Slow initialization of another
    /// page does not block requests.
    /// Callbacks may call RequestStop but must not await scope completion that includes themselves.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnNavigationEnteredAsync(CancellationToken cancellationToken);
}
