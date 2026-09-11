// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Describes a key's semantic request without performing input or retaining an editor reference.
/// </summary>
public sealed class SoftKeyboardInputRequestedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardInputRequestedEventArgs"/> class
    /// for a primary or alternative action.
    /// </summary>
    /// <param name="routedEvent">The input request event.</param>
    /// <param name="source">The key control producing the request.</param>
    /// <param name="key">The immutable description used for this interaction.</param>
    /// <param name="alternateText">
    /// The selected alternative, or null for the primary action.
    /// </param>
    public SoftKeyboardInputRequestedEventArgs(
        RoutedEvent routedEvent,
        object source,
        SoftKeyboardKey key,
        string? alternateText = null
        )
        : base(routedEvent, source)
    {
        Key = key ?? throw new ArgumentNullException(nameof(key));
        AlternateText = alternateText;
    }

    /// <summary>
    /// Gets the key whose action was requested.
    /// </summary>
    public SoftKeyboardKey Key { get; }

    /// <summary>
    /// Gets the selected Unicode alternative; null requests the primary action.
    /// </summary>
    public string? AlternateText { get; }

    /// <summary>
    /// Dispatches the typed request to handlers along the routed event path.
    /// </summary>
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        ((EventHandler<SoftKeyboardInputRequestedEventArgs>)genericHandler)(genericTarget, this);
    }
}
