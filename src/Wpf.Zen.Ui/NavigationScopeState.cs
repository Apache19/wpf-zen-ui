// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>
/// The lifecycle state of one navigation host session; stopped sessions never accept navigation
/// again.
/// </summary>
public enum NavigationScopeState
{
    /// <summary>
    /// Establishing host context and routing, rolling back acquired resources on failure.
    /// </summary>
    Attaching,

    /// <summary>
    /// Attachment completed; the session can accept navigation requests.
    /// </summary>
    Attached,

    /// <summary>
    /// Admission is closed; cancellation, exit, and UI unbinding are in progress.
    /// </summary>
    Stopping,

    /// <summary>
    /// The host is detached; session work and resource disposal are still draining.
    /// </summary>
    Draining,

    /// <summary>
    /// Cleanup has ended; Completion preserves any cleanup failures.
    /// </summary>
    Disposed
}