// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Receives notification when a component leaves the current navigation state.
/// </summary>
/// <remarks>
/// This callback describes navigation state, not WPF visual-tree state. It is independent of
/// <c>Unloaded</c> and cannot cancel or delay a navigation request.
/// </remarks>
public interface INavigationExited
{
    /// <summary>
    /// Handles the component leaving the current navigation state.
    /// </summary>
    /// <remarks>
    /// During a successful transition, the navigation host invokes this callback before entering
    /// the new component. The host also invokes it when the current navigation surface is genuinely
    /// detached from the visual tree.
    /// </remarks>
    void OnNavigationExited();
}
