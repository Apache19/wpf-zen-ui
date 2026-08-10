// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Routes navigation to an explicitly named navigation scope.</summary>
/// <remarks>
/// The router never guesses a target from the active or most recently registered window. Callers
/// must name the destination, which keeps routing deterministic for multiple windows, WPF islands,
/// and applications with more than one navigation surface per window.
/// </remarks>
public interface INavigationRouter
{
    /// <summary>Routes a request to a currently registered host id.</summary>
    /// <param name="hostId">The id supplied when the destination scope was attached.</param>
    /// <param name="request">The navigation request.</param>
    /// <returns><see cref="NavigationResultStatus.HostNotFound"/> if no live scope has the id.</returns>
    NavigationResult Navigate(string hostId, NavigationRequest request);

    /// <summary>Asynchronously routes a request to a currently registered host id.</summary>
    /// <param name="hostId">The id supplied when the destination scope was attached.</param>
    /// <param name="request">The navigation request.</param>
    /// <param name="cancellationToken">Cancels a request that has not begun on the target UI thread.</param>
    /// <returns><see cref="NavigationResultStatus.HostNotFound"/> if no live scope has the id.</returns>
    Task<NavigationResult> NavigateAsync(
        string hostId,
        NavigationRequest request,
        CancellationToken cancellationToken = default
    );
}
