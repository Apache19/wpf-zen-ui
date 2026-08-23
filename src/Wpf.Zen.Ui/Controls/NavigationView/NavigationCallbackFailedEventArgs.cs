// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Provides context for <see cref="NavigationView.NavigationCallbackFailed"/>.</summary>
public sealed class NavigationCallbackFailedEventArgs : EventArgs
{
    internal NavigationCallbackFailedEventArgs(NavigationCallbackException exception)
    {
        Exception = exception;
    }

    /// <summary>Gets the contextual exception produced for the failed callback.</summary>
    public NavigationCallbackException Exception { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the application has handled the failure.
    /// </summary>
    /// <remarks>
    /// The default value is <see langword="false"/>. An unhandled failure is rethrown on the
    /// navigation view's dispatcher to preserve fail-fast behavior. Set this property to
    /// <see langword="true"/> after logging or presenting the failure to suppress that throw.
    /// </remarks>
    public bool Handled { get; set; }
}
