// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates input-method commands and read-only state observation on the owning Dispatcher.
/// </summary>
public partial class SoftKeyboard
{
#pragma warning disable IDISP006 // WPF releases the owned session when the control unloads.
    private ISoftKeyboardInputMethodSession? _inputMethodSession;
#pragma warning restore IDISP006

    private int  _inputMethodSessionVersion;
    private int  _inputMethodInvalidationVersion;
    private bool _inputMethodOperationActive;
    private bool _inputMethodStateRefreshQueued;

    private CultureInfo? _observedInputCulture;

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
    /// Gets the last successfully observed immutable input-method catalog.
    /// </summary>
    /// <remarks>
    /// A refresh replaces this value only when catalog content changes. A previously obtained list
    /// remains immutable, and transient read failures preserve the last successful value.
    /// </remarks>
    public IReadOnlyList<SoftKeyboardInputMethod> InputMethods
    {
        get => (IReadOnlyList<SoftKeyboardInputMethod>)GetValue(InputMethodsProperty);
    }

    #endregion

    /// <summary>
    /// Reads one complete input-method observation and publishes only semantic changes.
    /// </summary>
    /// <remarks>
    /// The catalog, active profile, input mode, culture, focus target, and notification version are
    /// treated as one snapshot. A notification arriving during the read discards the observation
    /// and schedules another one. Failures preserve the last successfully published state.
    /// </remarks>
    /// <returns>
    /// True when a current observation was published or confirmed unchanged; otherwise false.
    /// </returns>
    public bool RefreshInputMethods()
    {
        VerifyAccess();

        if (_inputMethodOperationActive)
        {
            QueueInputMethodStateRefresh();
            return false;
        }

        _inputMethodOperationActive = true;

        var sessionVersion      = _inputMethodSessionVersion;
        var inputVersion        = _inputOperationVersion;
        var invalidationVersion = _inputMethodInvalidationVersion;
        var monitorVersion      = _session?.InputMethodVersion ?? 0;
        var target              = ActiveTarget;
        var session             = _inputMethodSession;

        bool IsCurrent()
        {
            return sessionVersion == _inputMethodSessionVersion
                && inputVersion == _inputOperationVersion
                && invalidationVersion == _inputMethodInvalidationVersion
                && monitorVersion == (_session?.InputMethodVersion ?? 0)
                && ReferenceEquals(target, ActiveTarget)
                && ReferenceEquals(session, _inputMethodSession);
        }

        try
        {
            session = GetOrCreateInputMethodSession(
                session,
                sessionVersion,
                inputVersion
            );

            if (session is null || !IsCurrent())
            {
                return false;
            }

            var observation = ReadInputMethodObservation(session, IsCurrent, target);
            if (observation is null || !IsCurrent())
            {
                return false;
            }

            if (!PublishInputMethodObservation(observation, IsCurrent))
            {
                return false;
            }

            PublishPendingInputMethodLayout(publishInputMethodState: true);

            if (!IsCurrent())
            {
                return false;
            }

            SetValue(InputMethodErrorPropertyKey, null);
            UpdateInputStateRecoveryInterval();

            return IsCurrent();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (sessionVersion == _inputMethodSessionVersion && inputVersion == _inputOperationVersion)
            {
                SetValue(InputMethodErrorPropertyKey, exception);
                UpdateInputStateRecoveryInterval();
            }

            return false;
        }
        finally
        {
            if (!ReferenceEquals(session, _inputMethodSession))
            {
                DisposeInputMethodSession(session);
            }

            _inputMethodOperationActive = false;
            PublishPendingInputMethodLayout();
        }
    }

    /// <summary>
    /// Requests activation of a catalog entry without changing its native/direct input mode.
    /// </summary>
    /// <param name="inputMethod">
    /// An entry from the current <see cref="InputMethods"/> snapshot.
    /// </param>
    /// <returns>
    /// True when the profile activation request was accepted in the current context.
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

        var accepted = RunInputMethodCommand(
            (session, isCurrent) => session.TryActivate(inputMethod) && isCurrent()
        );

        QueueInputMethodStateRefresh();
        return accepted;
    }

    /// <summary>
    /// Queues a complete observation after an adapter, focus, or extension policy change.
    /// </summary>
    private void RefreshImeState()
    {
        QueueInputMethodStateRefresh();
    }

    /// <summary>
    /// Writes one native input-method command without reading or publishing observable state.
    /// </summary>
    private bool RunInputMethodCommand(
        Func<ISoftKeyboardInputMethodSession, Func<bool>, bool> command)
    {
        VerifyAccess();

        if (_inputMethodOperationActive)
        {
            return false;
        }

        _inputMethodOperationActive = true;

        var sessionVersion = _inputMethodSessionVersion;
        var inputVersion   = _inputOperationVersion;
        var target         = ActiveTarget;
        var session        = _inputMethodSession;

        bool IsCurrent()
        {
            return sessionVersion == _inputMethodSessionVersion
                && inputVersion == _inputOperationVersion
                && ReferenceEquals(target, ActiveTarget)
                && ReferenceEquals(session, _inputMethodSession);
        }

        try
        {
            session = GetOrCreateInputMethodSession(
                session,
                sessionVersion,
                inputVersion
            );

            return session is not null && IsCurrent() && command(session, IsCurrent) && IsCurrent();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (sessionVersion == _inputMethodSessionVersion && inputVersion == _inputOperationVersion)
            {
                SetValue(InputMethodErrorPropertyKey, exception);
                UpdateInputStateRecoveryInterval();
            }

            return false;
        }
        finally
        {
            if (!ReferenceEquals(session, _inputMethodSession))
            {
                DisposeInputMethodSession(session);
            }

            _inputMethodOperationActive = false;
            PublishPendingInputMethodLayout();
        }
    }

    /// <summary>
    /// Creates and attaches a session only while its source and target context remain current.
    /// </summary>
    private ISoftKeyboardInputMethodSession? GetOrCreateInputMethodSession(
        ISoftKeyboardInputMethodSession? session,
        int sessionVersion,
        int inputVersion)
    {
        if (session is not null)
        {
            return session;
        }

        session = InputMethodSource.CreateSession()
            ?? throw new InvalidOperationException(
                "The input-method source returned a null session."
            );

        if (sessionVersion != _inputMethodSessionVersion || inputVersion != _inputOperationVersion)
        {
            return session;
        }

        _inputMethodSession = session;

        if (session is ISoftKeyboardInputMethodObservableSession observable)
        {
            observable.StateInvalidated += OnInputMethodSourceStateInvalidated;
        }

        UpdateInputStateRecoveryInterval();

        return session;
    }

    /// <summary>
    /// Reads all state used by public properties and layout from one notification version.
    /// </summary>
    private InputMethodObservation? ReadInputMethodObservation(
        ISoftKeyboardInputMethodSession session,
        Func<bool> isCurrent,
        IInputElement? target)
    {
        var snapshot = session.GetSnapshot()
            ?? throw new InvalidOperationException(
                "The input-method source returned a null snapshot."
            );

        if (!isCurrent())
        {
            return null;
        }

        var culture = CultureInfo.ReadOnly(
            (CultureInfo)InputLanguageManager.Current.CurrentInputLanguage.Clone()
        );

        var state = ImeState;
        var menuOwnsFocus = (_inputMethodMenu?.IsOpen == true || SettingsMenu?.IsOpen == true)
            && !ReferenceEquals(Keyboard.FocusedElement, target);

        if (!menuOwnsFocus)
        {
            var active = snapshot.ActiveInputMethod;
            if (active?.SupportsImeToggle != true)
            {
                state = InputMethodState.DoNotCare;
            }
            else
            {
                var adapter  = ResolveInputStateAdapter();
                var observed = adapter?.GetState(active) ?? session.GetState();
                if (observed is not (InputMethodState.On or InputMethodState.Off))
                {
                    throw new InvalidOperationException(
                        "The active input method did not return a readable input state."
                    );
                }

                state = observed;
            }
        }

        return isCurrent()
            ? new InputMethodObservation(snapshot, state, culture)
            : null;
    }

    /// <summary>
    /// Publishes one validated observation and records at most one pending layout rebuild.
    /// </summary>
    private bool PublishInputMethodObservation(
        InputMethodObservation observation,
        Func<bool> isCurrent)
    {
        var snapshot       = observation.Snapshot;
        var catalogChanged = !HaveSameCatalog(InputMethods, snapshot.InputMethods);

        var profileChanged = !HaveSamePublishedInputMethod(
            ActiveInputMethod,
            snapshot.ActiveInputMethod
        );

        var stateChanged   = ImeState != observation.State;
        var cultureChanged = _observedInputCulture?.Name != observation.Culture.Name;

        if (catalogChanged)
        {
            SetValue(InputMethodsPropertyKey, snapshot.InputMethods);
        }

        if (!isCurrent())
        {
            return false;
        }

        if (profileChanged)
        {
            SetValue(ActiveInputMethodPropertyKey, snapshot.ActiveInputMethod);
        }

        if (!isCurrent())
        {
            return false;
        }

        if (stateChanged)
        {
            SetValue(ImeStatePropertyKey, observation.State);
        }

        _observedInputCulture     =  observation.Culture;
        _inputMethodLayoutPending |= profileChanged || stateChanged || cultureChanged;

        if (!isCurrent())
        {
            return false;
        }

        if (profileChanged)
        {
            CancelCandidateOperations();
            ReleaseCandidatePresentation();
            RequestCandidateRefresh();
        }
        else if (stateChanged && IsLoaded && !CanPresentCandidates)
        {
            ReleaseCandidatePresentation();
        }

        return isCurrent();
    }

    /// <summary>
    /// Rebuilds the layout once after all fields from an observation have been published.
    /// </summary>
    private void PublishPendingInputMethodLayout(bool publishInputMethodState = false)
    {
        if (!_inputMethodLayoutPending)
        {
            return;
        }

        _inputMethodLayoutPending = false;
        RebuildLayout(publishInputMethodState);
    }

    /// <summary>
    /// Invalidates a source before invoking its application-defined cleanup code.
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
            if (!HaveSameCatalog(InputMethods, []))
            {
                SetValue(InputMethodsPropertyKey, Array.Empty<SoftKeyboardInputMethod>());
            }

            if (version == _inputMethodSessionVersion && ActiveInputMethod is not null)
            {
                SetValue(ActiveInputMethodPropertyKey, null);
            }

            if (version == _inputMethodSessionVersion && ImeState != InputMethodState.DoNotCare)
            {
                SetValue(ImeStatePropertyKey, InputMethodState.DoNotCare);
            }

            _observedInputCulture     = null;
            _inputMethodLayoutPending = true;

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

            UpdateInputStateRecoveryInterval();
        }
    }

    /// <summary>
    /// Releases a detached source while exposing cleanup failure through the control.
    /// </summary>
    private void DisposeInputMethodSession(ISoftKeyboardInputMethodSession? session)
    {
        if (session is null)
        {
            return;
        }

        var version = _inputMethodSessionVersion;

        try
        {
            if (session is ISoftKeyboardInputMethodObservableSession observable)
            {
                observable.StateInvalidated -= OnInputMethodSourceStateInvalidated;
            }

#pragma warning disable IDISP007 // The session was created by this control and detached before disposal.
            session.Dispose();
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
    /// Coalesces invalidations into one input-priority observation after native callbacks unwind.
    /// </summary>
    private void QueueInputMethodStateRefresh()
    {
        unchecked
        {
            _inputMethodInvalidationVersion++;
        }

        if (_inputMethodStateRefreshQueued)
        {
            return;
        }

        _inputMethodStateRefreshQueued = true;

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
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

    private void OnInputMethodSourceStateInvalidated(object? sender, EventArgs eventArgs)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() => OnInputMethodSourceStateInvalidated(sender, eventArgs))
            );

            return;
        }

        QueueInputMethodStateRefresh();
    }

    private static void OnInputMethodSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
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
    }

    private static bool HaveSameCatalog(
        IReadOnlyList<SoftKeyboardInputMethod> left,
        IReadOnlyList<SoftKeyboardInputMethod> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var index = 0; index < left.Count; index++)
        {
            if (!HaveSamePublishedInputMethod(left[index], right[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HaveSamePublishedInputMethod(
        SoftKeyboardInputMethod? left,
        SoftKeyboardInputMethod? right)
    {
        return HaveSameInputMethod(left, right)
            && left?.DisplayName == right?.DisplayName
            && left?.IsActive == right?.IsActive;
    }

    private static bool HaveSameInputMethod(
        SoftKeyboardInputMethod? left,
        SoftKeyboardInputMethod? right)
    {
        return left?.ProfileId == right?.ProfileId
            && left?.ClassId == right?.ClassId
            && left?.LanguageId == right?.LanguageId
            && left?.TouchLayoutPreference == right?.TouchLayoutPreference
            && left?.SupportsImeToggle == right?.SupportsImeToggle;
    }

    /// <summary>
    /// Carries values read under one focus target and notification version.
    /// </summary>
    private sealed record InputMethodObservation(
        SoftKeyboardInputMethodSnapshot Snapshot,
        InputMethodState State,
        CultureInfo Culture
    );
}