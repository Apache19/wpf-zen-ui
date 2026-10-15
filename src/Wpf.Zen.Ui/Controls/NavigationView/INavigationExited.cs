// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Receives notification when a component leaves the current navigation state.
/// </summary>
/// <remarks>
/// Delivers exit only to participants whose entry was invoked, in reverse entry order and
/// deduplicated by reference.
/// Implementing only exit creates no exit obligation. Exit uses the entry snapshot rather than
/// rereading DataContext.
/// </remarks>
public interface INavigationExited
{
    /// <summary>
    /// Handles the component leaving the current navigation state.
    /// </summary>
    /// <remarks>
    /// Page departure, real visual unload, and scope stop end activation; one participant failure
    /// does not skip other exits.
    /// Exit requests stopping while asynchronous cleanup may continue. It may request stop but must
    /// not await its scope completion.
    /// </remarks>
    void OnNavigationExited();
}
