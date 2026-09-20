// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

#pragma warning disable CA1001 // WPF owns control lifetime; Unloaded releases operation-owned resources.

/// <summary>
/// Coordinates synchronous and asynchronous custom actions with the target-validation boundary.
/// </summary>
public partial class SoftKeyboard
{
    private readonly SoftKeyboardOperationSlot _actionOperation = new();

    /// <summary>
    /// Gets the current presentation source's HWND for host integration diagnostics.
    /// </summary>
    internal IntPtr HostWindowHandle
    {
        get => GetHostWindowHandle();
    }

    private void ExecuteCustomAction(ISoftKeyboardAction action, string? alternative = null)
    {
        _ = ExecuteCustomActionAsync(action, alternative);
    }

    /// <summary>
    /// Owns the action context until completion and suppresses failures from expired lifetimes.
    /// </summary>
    private async Task ExecuteCustomActionAsync(ISoftKeyboardAction action, string? alternative)
    {
        if (!PrepareTargetForInput())
        {
            return;
        }

        var isInputCurrent = CaptureInputLifetime();

        using var operation = _actionOperation.Start(
            CancellationToken.None, replace: true, ReportExtensionError
        );

        if (operation is null || !operation.IsCurrent)
        {
            return;
        }

        var lifetime = new SoftKeyboardOperationContext(operation, isInputCurrent);
        var context  = new SoftKeyboardActionContext(this, lifetime, operation.Token);

        try
        {
            if (!lifetime.IsCurrent)
            {
                return;
            }

            if (alternative is not null
                && action is IAsyncSoftKeyboardAlternateKeyAction asyncAlternate)
            {
                await asyncAlternate.ExecuteAlternateAsync(
                    context, alternative, operation.Token).ConfigureAwait(true);
            }
            else if (alternative is not null
                && action is ISoftKeyboardAlternateKeyAction alternate)
            {
                alternate.ExecuteAlternate(context, alternative);
            }
            else if (action is IAsyncSoftKeyboardKeyAction asyncAction)
            {
                await asyncAction.ExecuteAsync(context, operation.Token).ConfigureAwait(true);
            }
            else if (action is ISoftKeyboardKeyAction synchronousAction)
            {
                synchronousAction.Execute(context);
            }
            else
            {
                throw new InvalidOperationException("The custom key action has no executable capability.");
            }
        }
        catch (OperationCanceledException) when (!lifetime.IsCurrent)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (lifetime.IsCurrent)
            {
                ReportExtensionError(exception);
            }
        }
        finally
        {
            context.Expire();
        }
    }

    /// <summary>
    /// Sends action text and consumes one-shot Shift while the initiating owner remains current.
    /// </summary>
    internal bool SendActionText(string text, Func<bool>? isCurrent = null)
    {
        var sent = TrySendText(text, isCurrent);
        if (sent && (isCurrent is null || isCurrent()) && ShiftState == SoftKeyboardShiftState.Once)
        {
            SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
        }

        return sent;
    }

    /// <summary>
    /// Sends literal text after WPF acknowledges composition cancellation. An idle boundary
    /// bounds the request: ignored Escape, a new composition, or changed ownership rejects it.
    /// </summary>
    internal async Task<bool> SendActionTextAsync(
        string text,
        CancellationToken cancellationToken = default,
        Func<bool>? isContextCurrent = null)
    {
        VerifyAccess();

        cancellationToken.ThrowIfCancellationRequested();

        var isInputCurrent = CaptureInputLifetime();

        if (string.IsNullOrEmpty(text) || !IsCurrent() || !PrepareTargetForInput() || !IsCurrent())
        {
            return false;
        }

        if (_activeTextComposition is not null)
        {
            var compositionVersion = _compositionStartVersion;
            var target             = ActiveTarget;

            if (!TryCancelActiveComposition())
            {
                return false;
            }

            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);

            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCurrent()
                || compositionVersion != _compositionStartVersion
                || !ReferenceEquals(target, Keyboard.FocusedElement)
                || _activeTextComposition is not null)
            {
                return false;
            }
        }

        return SendActionText(text, IsCurrent);

        bool IsCurrent()
        {
            return !cancellationToken.IsCancellationRequested
                && isInputCurrent() && (isContextCurrent is null || isContextCurrent());
        }
    }

    /// <summary>
    /// Sends an action chord using the same ownership guard before and after focus restoration.
    /// </summary>
    internal bool SendActionKey(Key key, ModifierKeys modifiers, Func<bool>? isCurrent = null)
    {
        if (key == Key.None || !Enum.IsDefined(typeof(Key), key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var sent = TrySendVirtualKey(key, modifiers, isCurrent);
        if (sent && (isCurrent is null || isCurrent())
            && IsCharacterKey(key) && ShiftState == SoftKeyboardShiftState.Once)
        {
            SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
        }

        return sent;
    }

    private void CancelAsyncAction()
    {
        _actionOperation.Cancel(ReportExtensionError);
    }
}

#pragma warning restore CA1001
