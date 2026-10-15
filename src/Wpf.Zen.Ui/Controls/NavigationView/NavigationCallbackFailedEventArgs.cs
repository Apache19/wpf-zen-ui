// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides context for <see cref="NavigationView.NavigationCallbackFailed"/>.
/// </summary>
public sealed class NavigationCallbackFailedEventArgs : EventArgs
{
    internal NavigationCallbackFailedEventArgs(NavigationCallbackException exception)
    {
        Exception = exception;
    }

    /// <summary>
    /// Gets the contextual exception produced for the failed callback.
    /// </summary>
    public NavigationCallbackException Exception { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the application has handled the failure.
    /// </summary>
    /// <remarks>
    /// Defaults to false; unhandled errors remain in the owning scope's Completion task.
    /// Notifications run on the Dispatcher before dependencies are released; handler failures
    /// accompany the original error.
    /// Notifications do not recurse. Setting true indicates that the application has logged or
    /// displayed the error.
    /// </remarks>
    public bool Handled { get; set; }
}
