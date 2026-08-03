// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Abstractions.Controls;

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
    /// <remarks>
    /// The navigation host invokes this callback only after it has successfully committed the
    /// destination. Failed, stopped, or superseded requests do not enter the navigation lifecycle.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnNavigationEnteredAsync();

    /// <summary>
    /// Asynchronously handles the component leaving the current navigation state.
    /// </summary>
    /// <remarks>
    /// During a successful transition, the navigation host awaits this callback before invoking
    /// <see cref="OnNavigationEnteredAsync"/> on the new component. The host also invokes it when
    /// the current navigation surface is genuinely detached from the visual tree.
    /// </remarks>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task OnNavigationExitedAsync();
}
