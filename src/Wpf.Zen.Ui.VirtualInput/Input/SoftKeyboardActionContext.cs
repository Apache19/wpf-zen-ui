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
/// Every input call revalidates the target. Literal text cancels active composition while virtual
/// keys preserve the IME pipeline. Expired contexts throw instead of typing into a later editor.
/// </remarks>
public sealed class SoftKeyboardActionContext
{
    private readonly int               _operationVersion;
    private readonly CancellationToken _cancellationToken;

    private SoftKeyboard? _keyboard;

    internal SoftKeyboardActionContext(
        SoftKeyboard keyboard,
        int operationVersion,
        CancellationToken cancellationToken)
    {
        _keyboard          = keyboard;
        _operationVersion  = operationVersion;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Sends literal Unicode using the keyboard's composition-cancellation boundary.
    /// </summary>
    /// <param name="text">Nonempty literal text to send.</param>
    /// <returns>
    /// Whether Windows accepted the input; failure leaves one-shot Shift unchanged. During
    /// composition this requests cancellation and returns false without sending text. Use
    /// <see cref="TrySendTextAsync"/> to wait for cancellation before sending.
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
    /// Waits for composition cancellation before sending literal Unicode to the original target.
    /// </summary>
    /// <param name="text">Nonempty literal text to send.</param>
    /// <returns>
    /// Whether Windows accepted the text. Ignored cancellation or changed input ownership returns
    /// false; cancellation of this action throws <see cref="OperationCanceledException"/>.
    /// </returns>
    /// <remarks>
    /// Await this method from an asynchronous key action so its context remains valid. Cancellation
    /// is checked before text injection; it cannot retract input already accepted by Windows.
    /// </remarks>
    public Task<bool> TrySendTextAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Input text is required.", nameof(text));
        }

        var keyboard = GetKeyboard();
        return keyboard.SendActionTextAsync(
            text,
            _cancellationToken,
            () => ReferenceEquals(_keyboard, keyboard) && keyboard.IsActionContextCurrent(_operationVersion)
        );
    }

    /// <summary>
    /// Sends a virtual key through the active Windows layout and IME.
    /// </summary>
    /// <param name="key">
    /// The virtual key to send.
    /// </param>
    /// <param name="modifiers">
    /// The Shift, Control, Alt, or Windows modifiers held for this chord.
    /// </param>
    /// <returns>
    /// Whether Windows accepted the input.
    /// </returns>
    public bool TrySendKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        return GetKeyboard().SendActionKey(key, modifiers);
    }

    /// <summary>
    /// Gets the token cancelled when a newer action or input session takes ownership.
    /// </summary>
    public CancellationToken CancellationToken => _cancellationToken;

    /// <summary>
    /// Gets the active input-method profile captured through the owning keyboard.
    /// </summary>
    public SoftKeyboardInputMethod? InputMethod => GetKeyboard().ActiveInputMethod;

    /// <summary>
    /// Gets the current Windows keyboard-layout handle.
    /// </summary>
    public IntPtr KeyboardLayoutHandle => GetKeyboard().CurrentKeyboardLayoutHandle;

    /// <summary>
    /// Gets the containing window handle, or zero when the keyboard has no WPF window.
    /// </summary>
    public IntPtr HostWindowHandle => GetKeyboard().HostWindowHandle;

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

        _cancellationToken.ThrowIfCancellationRequested();

        if (!keyboard.IsActionContextCurrent(_operationVersion))
        {
            throw new InvalidOperationException("The key action context no longer owns the input session.");
        }

        return keyboard;
    }
}
