// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Provides short-lived input and navigation services for a custom key action.
/// </summary>
/// <remarks>
/// Every input call revalidates the target. Unicode input cancels active composition; virtual input
/// follows the Windows IME pipeline. Expired contexts throw instead of typing into a later editor.
/// This object is dispatcher-affine and is not an asynchronous input channel.
/// </remarks>
public sealed class SoftKeyboardActionContext
{
    private SoftKeyboard? _keyboard;

    internal SoftKeyboardActionContext(SoftKeyboard keyboard)
    {
        _keyboard = keyboard;
    }

    /// <summary>
    /// Sends literal Unicode using the keyboard's composition-cancellation boundary.
    /// </summary>
    /// <param name="text">Nonempty literal text to send.</param>
    /// <returns>
    /// Whether Windows accepted the input; failure leaves one-shot Shift unchanged.
    /// </returns>
    public bool TrySendText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Input text is required.", nameof(text));
        }

        return GetKeyboard().SendActionText(text);
    }

    /// <summary>
    /// Sends a virtual key through the active Windows layout and IME.
    /// </summary>
    /// <param name="key">
    /// The virtual key to send.
    /// </param>
    /// <param name="shift">
    /// Whether this specific keystroke uses Shift; defaults to false.
    /// </param>
    /// <returns>
    /// Whether Windows accepted the input.
    /// </returns>
    public bool TrySendKey(Key key, bool shift = false)
    {
        return GetKeyboard().SendActionKey(key, shift);
    }

    /// <summary>
    /// Requests a built-in or custom page, preserving the normal composition guard.
    /// </summary>
    /// <param name="page">
    /// A nonempty, case-sensitive page identifier.
    /// </param>
    public void ShowPage(string page)
    {
        GetKeyboard().ShowPage(page);
    }

    /// <summary>
    /// Rebuilds the current page after application-owned layout data changes.
    /// </summary>
    public void RefreshLayout()
    {
        GetKeyboard().RefreshLayout();
    }

    internal void Expire()
    {
        _keyboard = null;
    }

    private SoftKeyboard GetKeyboard()
    {
        var keyboard = _keyboard ?? throw new InvalidOperationException(
            "The key action context has expired."
        );

        keyboard.VerifyAccess();

        return keyboard;
    }
}
