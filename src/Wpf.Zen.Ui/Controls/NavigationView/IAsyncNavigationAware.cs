// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Asynchronously participates in the successful entry lifecycle of a navigation host.
/// </summary>
/// <remarks>
/// This callback describes navigation state, not WPF visual-tree state. It is independent of
/// <c>Loaded</c> and cannot cancel a navigation request. Implement
/// <see cref="ICancellableAsyncNavigationAware"/> instead when the asynchronous work can stop
/// after a later navigation supersedes it.
/// </remarks>
public interface IAsyncNavigationAware : INavigationExited
{
    /// <summary>
    /// Asynchronously handles the component entering the current navigation state.
    /// </summary>
    /// <remarks>
    /// The navigation host invokes this callback only after it has successfully committed the
    /// destination and attached its visual tree. Failed or stopped requests do not enter the
    /// navigation lifecycle. The callback is not awaited by the host: a slow page cannot delay
    /// lifecycle delivery to a later destination.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnNavigationEnteredAsync();
}
