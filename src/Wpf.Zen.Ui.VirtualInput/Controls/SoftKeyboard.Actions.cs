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

    internal IntPtr CurrentKeyboardLayoutHandle => KeyboardLayoutLabels.GetCurrentLayout();

    internal IntPtr HostWindowHandle
    {
        get
        {
            var window = Window.GetWindow(this);
            return window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        }
    }

    private void ExecuteCustomAction(ISoftKeyboardAction action, string? alternative = null)
    {
        _ = ExecuteCustomActionAsync(action, alternative);
    }

    private async Task ExecuteCustomActionAsync(ISoftKeyboardAction action, string? alternative)
    {
        if (!PrepareTargetForInput())
        {
            return;
        }

        using var operation = _actionOperation.Start(
            CancellationToken.None, replace: true, ReportExtensionError
        );

        if (operation is null || !operation.IsCurrent)
        {
            return;
        }

        var operationVersion = _inputOperationVersion;

        var context = new SoftKeyboardActionContext(this, operationVersion, operation.Token);

        try
        {
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
        catch (OperationCanceledException) when (!operation.IsCurrent)
        {
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (operation.IsCurrent)
            {
                ReportExtensionError(exception);
            }
        }
        finally
        {
            context.Expire();
        }
    }

    internal bool SendActionText(string text)
    {
        var sent = TrySendText(text);
        if (sent && ShiftState == SoftKeyboardShiftState.Once)
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

        if (string.IsNullOrEmpty(text) || !PrepareTargetForInput())
        {
            return false;
        }

        if (_activeTextComposition is not null)
        {
            var version            = _inputOperationVersion;
            var compositionVersion = _compositionStartVersion;
            var target             = ActiveTarget;

            if (!TryCancelActiveComposition())
            {
                return false;
            }

            await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle);

            cancellationToken.ThrowIfCancellationRequested();

            if (version != _inputOperationVersion
                || compositionVersion != _compositionStartVersion
                || !ReferenceEquals(target, ActiveTarget)
                || !ReferenceEquals(target, Keyboard.FocusedElement)
                || _activeTextComposition is not null)
            {
                return false;
            }
        }

        return (isContextCurrent is null || isContextCurrent()) && SendActionText(text);
    }

    internal bool SendActionKey(Key key, ModifierKeys modifiers)
    {
        if (key == Key.None || !Enum.IsDefined(typeof(Key), key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var sent = TrySendVirtualKey(key, modifiers);
        if (sent && IsCharacterKey(key) && ShiftState == SoftKeyboardShiftState.Once)
        {
            SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
        }

        return sent;
    }

    internal bool IsActionContextCurrent(int operationVersion)
    {
        return operationVersion == _inputOperationVersion
            && IsOpen
            && IsVisible
            && IsEnabled
            && ActiveTarget is not null;
    }

    private void CancelAsyncAction()
    {
        _actionOperation.Cancel(ReportExtensionError);
    }
}

#pragma warning restore CA1001