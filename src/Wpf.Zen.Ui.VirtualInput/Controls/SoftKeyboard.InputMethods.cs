// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates immediate input-method access on the owning dispatcher.
/// </summary>
public partial class SoftKeyboard
{
#pragma warning disable IDISP006 // WPF releases the owned session when the control unloads.
    private ISoftKeyboardInputMethodSession? _inputMethodSession;
#pragma warning restore IDISP006

    private int  _inputMethodSessionVersion;
    private bool _inputMethodOperationActive;
    private bool _inputMethodStateRefreshQueued;

    #region DP: InputMethodSource

    /// <summary>
    /// Identifies the <see cref="InputMethodSource"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputMethodSourceProperty
        = DependencyProperty.Register(
            nameof(InputMethodSource),
            typeof(ISoftKeyboardInputMethodSource),
            typeof(SoftKeyboard),
            new PropertyMetadata(WindowsSoftKeyboardInputMethodSource.Default, OnInputMethodSourceChanged)
        );

    /// <summary>
    /// Gets or sets the source used to enumerate, activate, and control input methods.
    /// </summary>
    public ISoftKeyboardInputMethodSource InputMethodSource
    {
        get => (ISoftKeyboardInputMethodSource)GetValue(InputMethodSourceProperty);
        set => SetValue(
            InputMethodSourceProperty,
            value ?? throw new ArgumentNullException(nameof(value))
        );
    }

    #endregion

    #region Read-Only DP: InputMethodError

    private static readonly DependencyPropertyKey InputMethodErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(InputMethodError),
            typeof(Exception),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="InputMethodError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputMethodErrorProperty
        = InputMethodErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the most recent input-method source or state-adapter failure, or null after success.
    /// </summary>
    public Exception? InputMethodError => (Exception?)GetValue(InputMethodErrorProperty);

    #endregion

    #region Read-Only DP: InputMethods

    private static readonly DependencyPropertyKey InputMethodsPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(InputMethods),
            typeof(IReadOnlyList<SoftKeyboardInputMethod>),
            typeof(SoftKeyboard),
            new PropertyMetadata(Array.Empty<SoftKeyboardInputMethod>())
        );

    /// <summary>
    /// Identifies the <see cref="InputMethods"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputMethodsProperty
        = InputMethodsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the current immutable catalog, or an empty list before discovery or after source replacement.
    /// </summary>
    /// <remarks>
    /// Each refresh replaces the complete list. Bind to this property to observe catalog replacement.
    /// A previously obtained list remains a stable snapshot and is not updated in place.
    /// </remarks>
    public IReadOnlyList<SoftKeyboardInputMethod> InputMethods
    {
        get => (IReadOnlyList<SoftKeyboardInputMethod>)GetValue(InputMethodsProperty);
    }

    #endregion

    /// <summary>
    /// Reads and publishes the input-method catalog and current mode on the owning dispatcher.
    /// </summary>
    /// <remarks>
    /// Reentrant requests are rejected. Native callbacks cannot start a second operation in the middle
    /// of publication or activation. Source failures are exposed through InputMethodError.
    /// </remarks>
    /// <returns>
    /// True when catalog and state were refreshed; false on failure or reentrancy.
    /// </returns>
    public bool RefreshInputMethods()
    {
        return RunInputMethodOperation((session, isCurrent) =>
            PublishInputMethods(session, isCurrent) && ReadImeState(session, isCurrent));
    }

    /// <summary>
    /// Activates a catalog entry and refreshes the catalog; IME entries also enable native input.
    /// </summary>
    /// <param name="inputMethod">An entry from the current InputMethods collection.</param>
    /// <returns>
    /// True when all native requests were accepted and the source and input target remained current.
    /// </returns>
    public bool TryActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        VerifyAccess();

        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        if (!InputMethods.Any(item => HaveSameInputMethod(item, inputMethod)))
        {
            throw new ArgumentException(
                "The input method does not belong to the current source snapshot.",
                nameof(inputMethod)
            );
        }

        return RunInputMethodOperation(
            (session, isCurrent) => ActivateInputMethodCore(session, isCurrent, inputMethod)
        );
    }

    /// <summary>
    /// Executes the activation stages inside the caller's existing native-state transaction.
    /// </summary>
    private bool ActivateInputMethodCore(
        ISoftKeyboardInputMethodSession session, Func<bool> isCurrent, SoftKeyboardInputMethod inputMethod)
    {
        if (!session.TryActivate(inputMethod) || !isCurrent())
        {
            return false;
        }

        if (!PublishInputMethods(session, isCurrent) || !isCurrent())
        {
            return false;
        }

        if (ActiveInputMethod?.SupportsImeToggle == true
            && !TrySetInputMethodState(session, isCurrent, InputMethodState.On))
        {
            return false;
        }

        if (!ReadImeState(session, isCurrent))
        {
            return false;
        }

        QueueInputMethodStateRefresh();

        return true;
    }

    /// <summary>
    /// Reads focused-document state without introducing a cancellable operation for observation.
    /// </summary>
    private void RefreshImeState()
    {
        if (_inputMethodOperationActive)
        {
            QueueInputMethodStateRefresh();
            return;
        }

        RunInputMethodOperation(ReadImeState);
    }

    /// <summary>
    /// Publishes immediate state inside the caller's native-state transaction.
    /// </summary>
    private bool ReadImeState(ISoftKeyboardInputMethodSession session, Func<bool> isCurrent)
    {
        if ((_inputMethodMenu?.IsOpen == true || SettingsMenu?.IsOpen == true)
            && !ReferenceEquals(Keyboard.FocusedElement, ActiveTarget))
        {
            // A menu's text-service context does not describe the retained editor. Closing the
            // menu queues a fresh observation after WPF has restored keyboard focus.
            return isCurrent();
        }

        var adapter = ResolveInputStateAdapter();

        if (!isCurrent())
        {
            return false;
        }

        var observed = ActiveInputMethod?.SupportsImeToggle != true
            ? InputMethodState.DoNotCare
            : adapter?.GetState(ActiveInputMethod) ?? session.GetState();

        if (!isCurrent())
        {
            return false;
        }

        SetValue(ImeStatePropertyKey, observed is InputMethodState.On or InputMethodState.Off
            ? observed
            : InputMethodState.DoNotCare);

        if (isCurrent() && IsLoaded && !CanPresentCandidates)
        {
            ReleaseCandidatePresentation();
        }

        return isCurrent();
    }

    /// <summary>
    /// Changes focused-document state through exactly one selected state mechanism.
    /// </summary>
    private bool TrySetInputMethodState(
        ISoftKeyboardInputMethodSession session, Func<bool> isCurrent, InputMethodState requestedState)
    {
        var adapter = ResolveInputStateAdapter();

        if (!isCurrent())
        {
            return false;
        }

        if (ActiveInputMethod?.SupportsImeToggle != true)
        {
            return false;
        }

        var succeeded = adapter is null
            ? session.TrySetState(requestedState)
            : adapter.TrySetState(ActiveInputMethod, requestedState);

        return succeeded && isCurrent();
    }

    /// <summary>
    /// Publishes a snapshot while checking ownership after provider and WPF notification callbacks.
    /// </summary>
    private bool PublishInputMethods(ISoftKeyboardInputMethodSession session, Func<bool> isCurrent)
    {
        var snapshot = session.GetSnapshot();

        if (!isCurrent())
        {
            return false;
        }

        if (snapshot is null)
        {
            throw new InvalidOperationException("The input-method source returned a null snapshot.");
        }

        var previous = ActiveInputMethod;

        SetValue(InputMethodsPropertyKey, snapshot.InputMethods);

        if (!isCurrent())
        {
            return false;
        }

        SetValue(ActiveInputMethodPropertyKey, snapshot.ActiveInputMethod);

        if (!isCurrent())
        {
            return false;
        }

        if (!HaveSameInputMethod(previous, snapshot.ActiveInputMethod))
        {
            CancelCandidateOperations();

            if (!isCurrent())
            {
                return false;
            }

            ReleaseCandidatePresentation();

            if (!isCurrent())
            {
                return false;
            }

            RebuildLayout();

            if (!isCurrent())
            {
                return false;
            }

            RequestCandidateRefresh();
        }

        if (!isCurrent())
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Owns one synchronous transaction, including reentrant native callbacks and deferred disposal.
    /// </summary>
    private bool RunInputMethodOperation(
        Func<ISoftKeyboardInputMethodSession, Func<bool>, bool> operation)
    {
        VerifyAccess();

        if (_inputMethodOperationActive)
        {
            return false;
        }

        _inputMethodOperationActive = true;

        var sessionVersion = _inputMethodSessionVersion;
        var inputVersion   = _inputOperationVersion;
        var session        = _inputMethodSession;

        bool IsCurrent()
        {
            return sessionVersion == _inputMethodSessionVersion
                && inputVersion == _inputOperationVersion
                && ReferenceEquals(session, _inputMethodSession);
        }

        try
        {
            if (session is null)
            {
                session = InputMethodSource.CreateSession()
                    ?? throw new InvalidOperationException(
                        "The input-method source returned a null session."
                    );

                if (sessionVersion != _inputMethodSessionVersion || inputVersion != _inputOperationVersion)
                {
                    return false;
                }

                _inputMethodSession = session;
            }

            var succeeded = operation(session, IsCurrent);

            if (_inputMethodLayoutPending)
            {
                _inputMethodLayoutPending = false;
                // Layout providers can invoke application code. Keep the operation gate closed
                // and validate ownership after publishing, just as for dependency properties.
                RebuildLayout(publishInputMethodState: true);
            }

            if (!IsCurrent())
            {
                return false;
            }

            SetValue(InputMethodErrorPropertyKey, null);

            return succeeded && IsCurrent();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (sessionVersion == _inputMethodSessionVersion && inputVersion == _inputOperationVersion)
            {
                SetValue(InputMethodErrorPropertyKey, exception);
                SetValue(ImeStatePropertyKey, InputMethodState.DoNotCare);
            }

            return false;
        }
        finally
        {
            // A source replaced inside its own callback remains alive until that call has unwound.
            // Keep the reentrancy gate closed during Dispose, which can call application code too.
            if (!ReferenceEquals(session, _inputMethodSession))
            {
                DisposeInputMethodSession(session);
            }

            _inputMethodOperationActive = false;

            if (_inputMethodLayoutPending)
            {
                _inputMethodLayoutPending = false;
                RebuildLayout();
            }
        }
    }

    /// <summary>
    /// Invalidates the source before calling its cleanup code.
    /// </summary>
    private void ResetInputMethodSession()
    {
        var version         = ++_inputMethodSessionVersion;
        var session         = _inputMethodSession;
        var operationActive = _inputMethodOperationActive;

        _inputMethodOperationActive = true;
        _inputMethodSession         = null;

        try
        {
            SetValue(InputMethodsPropertyKey, Array.Empty<SoftKeyboardInputMethod>());

            if (version == _inputMethodSessionVersion)
            {
                SetValue(ActiveInputMethodPropertyKey, null);
            }

            if (!operationActive)
            {
                DisposeInputMethodSession(session);
            }
        }
        finally
        {
            _inputMethodOperationActive = operationActive;

            if (!operationActive && version != _inputMethodSessionVersion)
            {
                QueueInputMethodStateRefresh();
            }
        }
    }

    /// <summary>
    /// Releases a detached source while exposing cleanup failure through the control.
    /// </summary>
    private void DisposeInputMethodSession(ISoftKeyboardInputMethodSession? session)
    {
        var version = _inputMethodSessionVersion;

        try
        {
#pragma warning disable IDISP007 // The session was created by this control and detached before disposal.
            session?.Dispose();
#pragma warning restore IDISP007
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (version == _inputMethodSessionVersion)
            {
                SetValue(InputMethodErrorPropertyKey, exception);
            }
        }
    }

    private void ActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        RunAfterActiveCompositionCancellation(() => TryActivateInputMethod(inputMethod));
    }

    /// <summary>
    /// Coalesces native notifications into one observation after the current dispatcher callback.
    /// </summary>
    private void QueueInputMethodStateRefresh()
    {
        if (_inputMethodStateRefreshQueued)
        {
            return;
        }

        _inputMethodStateRefreshQueued = true;

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(
                () =>
                {
                    _inputMethodStateRefreshQueued = false;
                    if (IsLoaded)
                    {
                        RefreshInputMethods();
                        RequestCandidateRefresh();
                        QueueCandidateBridgeRefresh();
                    }
                }
            )
        );
    }

    private static void OnInputMethodSourceChanged(
        DependencyObject dependencyObject, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)dependencyObject;

        keyboard.ResetInputMethodSession();
        keyboard.SetValue(InputMethodErrorPropertyKey, null);

        if (keyboard._inputMethodOperationActive)
        {
            keyboard.QueueInputMethodStateRefresh();
            return;
        }

        keyboard.RefreshInputMethods();
        keyboard.RefreshImeState();
    }

    private static bool HaveSameInputMethod(
        SoftKeyboardInputMethod? left, SoftKeyboardInputMethod? right)
    {
        return left?.ProfileId == right?.ProfileId
            && left?.ClassId == right?.ClassId
            && left?.LanguageId == right?.LanguageId
            && left?.TouchLayoutPreference == right?.TouchLayoutPreference
            && left?.SupportsImeToggle == right?.SupportsImeToggle;
    }
}
