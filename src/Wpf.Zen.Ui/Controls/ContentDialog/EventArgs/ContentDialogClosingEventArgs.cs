// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary
/// >A synchronous close request.
/// Forced cleanup and token cancellation bypass this event.
/// </summary>
public sealed class ContentDialogClosingEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogClosingEventArgs"/> class.
    /// Creates a close notification for the given dialog and routed event.
    /// </summary>
    public ContentDialogClosingEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source)
    {
    }

    /// <summary>
    /// Gets the proposed business result; null represents dismissal without a result.
    /// </summary>
    public object? Result { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to reject this request.
    /// Default false; another request may follow a veto.
    /// </summary>
    public bool Cancel { get; set; }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        // Direct dispatch preserves the application exception instead of wrapping DynamicInvoke.
        if (genericHandler is EventHandler<ContentDialogClosingEventArgs> handler)
        {
            handler(genericTarget, this);
        }
        else
        {
            base.InvokeEventHandler(genericHandler, genericTarget);
        }
    }
}