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
    // Profile identity and thread-compartment callbacks are not atomic. This quiet window groups
    // one activation burst without adding latency to ordinary mode changes.
    private static readonly TimeSpan InputMethodProfileQuietPeriod = TimeSpan.FromMilliseconds(75);

    /// <summary>
    /// Gets whether a profile command or state publication is currently in progress.
    /// Candidate discovery resumes after the operation and its layout publication finish.
    /// </summary>
    internal bool IsInputMethodOperationActive => _inputMethodOperationActive;

#pragma warning disable IDISP006 // WPF releases the owned session when the control unloads.
    private ISoftKeyboardInputMethodSession? _inputMethodSession;
#pragma warning restore IDISP006

    private int  _inputMethodSessionVersion;
    private int  _inputMethodInvalidationVersion;
    private bool _inputMethodOperationActive;
    private bool _inputMethodStateRefreshQueued;

    private DispatcherTimer?         _inputMethodProfileTimer;
    private SoftKeyboardInputMethod? _settlingInputMethod;
    private bool                     _inputMethodProfileReady;

    private InputModeTransitionRoute? _inputModeTransitionRoute;

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
    /// Gets the most recent input-method source or mode-adapter failure, or null after success.
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
    private void RefreshInputMode()
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

        var mode   = InputMode;
        var button = InputModeButton;
        var transitionRoute = _inputModeTransitionRoute;

        var menuLacksFocusedTarget = (_inputMethodMenuCatalogRefreshDepth > 0
                || _inputMethodMenu?.IsOpen == true
                || SettingsMenu?.IsOpen == true)
            && !ReferenceEquals(Keyboard.FocusedElement, target);

        var profileSettling = !menuLacksFocusedTarget
            && ShouldDeferInputMethodProfile(snapshot.ActiveInputMethod);

        if (profileSettling)
        {
            // Publish the verified profile identity without attaching a command read from a
            // thread compartment that the newly activated TIP may still be initializing.
            return isCurrent()
                ? new InputMethodObservation(snapshot, null, null, null, culture, true)
                : null;
        }

        // A disabled focusless menu may still refresh the catalog while focus is outside the
        // remembered editor. Preserve that editor's last mode instead of reading another TSF
        // document and publishing the result against the remembered target.
        if (!menuLacksFocusedTarget)
        {
            var active = snapshot.ActiveInputMethod;
            if (active is null)
            {
                mode            = null;
                button          = null;
                transitionRoute = null;
            }
            else
            {
                var adapter = ResolveObservedInputModeAdapter(active, culture, target);
                if (!isCurrent())
                {
                    return null;
                }

                var contribution = adapter?.GetContribution(active)
                    ?? SoftKeyboardInputModeContribution.Empty;

                // Mode interpretation and button discovery are independent. The platform command
                // supplies defaults only for facets that the extension left unhandled.
                var defaultMode = contribution.Mode.IsHandled
                    ? null
                    : session.GetInputMode(active);

                mode = contribution.Mode.IsHandled
                    ? contribution.Mode.Value
                    : defaultMode;

                var buttonSession = session as ISoftKeyboardInputModeButtonSession;
                var needsPlatformButton = !contribution.Presentation.IsHandled
                    || !contribution.Transition.IsHandled;
                var platformButton = needsPlatformButton
                    ? buttonSession?.GetInputModeButton(active)
                    : null;

                button = contribution.Presentation.IsHandled
                    ? contribution.Presentation.Value
                    : platformButton;

                if (contribution.Transition.IsHandled)
                {
                    transitionRoute = adapter is not null
                        && contribution.Transition.Value is { } adapterTransition
                        ? InputModeTransitionRoute.FromAdapter(adapter, adapterTransition)
                        : null;
                }
                else if (buttonSession is not null && platformButton is not null)
                {
                    transitionRoute = InputModeTransitionRoute.FromPlatformButton();
                }
                else if (defaultMode is not null)
                {
                    transitionRoute = InputModeTransitionRoute.FromPlatformMode(defaultMode);
                }
                else
                {
                    transitionRoute = null;
                }

                // A mode key is actionable UI. Never publish presentation without the command
                // selected by the same resolution pass, or a hidden command without presentation.
                if (button is null || transitionRoute is null)
                {
                    button          = null;
                    transitionRoute = null;
                }

                // Profile activation and compartment publication are independent TSF callbacks.
                // Verify the identity again so a mode read from the old service is never published
                // beside the newly selected profile.
                var verification = session.GetSnapshot();
                if (!HaveSameInputMethod(active, verification.ActiveInputMethod))
                {
                    QueueInputMethodStateRefresh();
                    return null;
                }

                return isCurrent()
                    ? new InputMethodObservation(
                        snapshot,
                        mode,
                        button,
                        transitionRoute,
                        culture,
                        false
                    )
                    : null;
            }
        }

        return isCurrent()
            ? new InputMethodObservation(snapshot, mode, button, transitionRoute, culture, false)
            : null;
    }

    /// <summary>
    /// Selects a mode adapter for the newly observed profile before that profile is published.
    /// </summary>
    private ISoftKeyboardInputModeAdapter? ResolveObservedInputModeAdapter(
        SoftKeyboardInputMethod? inputMethod,
        CultureInfo culture,
        IInputElement? target)
    {
        if (InputModeAdapter is not null)
        {
            return InputModeAdapter;
        }

        // Layout extensions still describe the last published profile at this point. Reusing them
        // would read a newly selected TIP through the previous TIP's mode adapter, and a failed
        // read would prevent publication of the very profile needed to select the correct adapter.
        var targetAdapter = target is null ? null : GetAdapter(target);

        var scope = target is not null && targetAdapter is not null
            ? targetAdapter.GetInputScope(target)
            : InputScopeNameValue.Default;

        var context = new SoftKeyboardLayoutContext(
            culture, scope, Page, ShiftState, InputMode, inputMethod,
            supplementalLayout: SelectedSupplementalLayout,
            inputModeButton: InputModeButton
        );

        var extensions = ExtensionProvider.GetExtensions(new SoftKeyboardExtensionContext(context))
            ?? throw new InvalidOperationException("The extension provider returned a null sequence.");

        return extensions.Select(extension => extension.InputModeAdapter)
            .FirstOrDefault(adapter => adapter is not null);
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

        var modeChanged    = !Equals(InputMode, observation.Mode);
        var buttonChanged  = !Equals(InputModeButton, observation.Button);
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

        if (modeChanged)
        {
            SetValue(InputModePropertyKey, observation.Mode);
        }

        if (buttonChanged)
        {
            SetValue(InputModeButtonPropertyKey, observation.Button);
        }

        _inputModeTransitionRoute = observation.TransitionRoute;

        _observedInputCulture     =  observation.Culture;
        _inputMethodLayoutPending |= profileChanged || modeChanged || buttonChanged
            || cultureChanged;

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
        else if (modeChanged && IsLoaded && !CanPresentCandidates)
        {
            ReleaseCandidatePresentation();
        }

        if (!isCurrent())
        {
            return false;
        }

        if (!observation.IsProfileSettling
            && HaveSameInputMethod(_settlingInputMethod, snapshot.ActiveInputMethod))
        {
            CompleteInputMethodProfileSettlement();
        }

        return true;
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

            if (version == _inputMethodSessionVersion && InputMode is not null)
            {
                SetValue(InputModePropertyKey, null);
            }

            if (version == _inputMethodSessionVersion && InputModeButton is not null)
            {
                SetValue(InputModeButtonPropertyKey, null);
            }

            _inputModeTransitionRoute = null;

            _observedInputCulture     = null;
            _inputMethodLayoutPending = true;

            CompleteInputMethodProfileSettlement();

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

    /// <summary>
    /// Closes the focusless selector and activates a profile only for its still-focused editor.
    /// </summary>
    private void ActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        if (!IsInputMethodMenuAvailable)
        {
            return;
        }

        CloseFlyouts();

        var operationVersion = _inputOperationVersion;
        var sessionVersion   = _inputMethodSessionVersion;
        var requestVersion   = _inputMethodMenuRequestVersion;
        var target           = ActiveTarget;

        // Let the popup click and teardown finish before entering native profile code. The menu
        // never changes keyboard focus, so a different focus owner means the request has expired;
        // it must not be corrected by focusing the remembered target.
        _ = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            if (operationVersion != _inputOperationVersion
                || sessionVersion != _inputMethodSessionVersion
                || requestVersion != _inputMethodMenuRequestVersion
                || !ReferenceEquals(target, ActiveTarget)
                || !IsInputMethodMenuAvailable)
            {
                return;
            }

            RunAfterActiveCompositionCancellation(() => TryActivateInputMethod(inputMethod));
        }));
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

        RestartInputMethodProfileSettlement();
        QueueInputMethodStateRefresh();
    }

    /// <summary>
    /// Withholds profile-dependent mode state until native activation callbacks become quiet.
    /// </summary>
    /// <remarks>
    /// TSF publishes profile identity and thread-compartment values independently. During profile
    /// activation, the new identity can therefore be observable before the TIP has restored its
    /// remembered conversion mode. The transition publishes no command until the callback stream
    /// has been quiet for one bounded interval.
    /// </remarks>
    private bool ShouldDeferInputMethodProfile(SoftKeyboardInputMethod? inputMethod)
    {
        if (InputMethodSource is not WindowsSoftKeyboardInputMethodSource
            || inputMethod is null)
        {
            CompleteInputMethodProfileSettlement();
            return false;
        }

        if (HaveSameInputMethod(_settlingInputMethod, inputMethod))
        {
            return !_inputMethodProfileReady;
        }

        // The first profile observed after the control is loaded already belongs to the focused
        // editor; there is no previously published profile transition to stabilize.
        if (ActiveInputMethod is null && _observedInputCulture is null)
        {
            return false;
        }

        if (HaveSameInputMethod(ActiveInputMethod, inputMethod))
        {
            CompleteInputMethodProfileSettlement();
            return false;
        }

        _settlingInputMethod     = inputMethod;
        _inputMethodProfileReady = false;

        _inputMethodProfileTimer ??= new DispatcherTimer(
            InputMethodProfileQuietPeriod,
            DispatcherPriority.Background,
            OnInputMethodProfileTimerTick,
            Dispatcher
        );

        _inputMethodProfileTimer.Stop();
        _inputMethodProfileTimer.Start();

        return true;
    }

    /// <summary>
    /// Restarts the quiet period when another native notification arrives during activation.
    /// </summary>
    private void RestartInputMethodProfileSettlement()
    {
        if (_settlingInputMethod is null || _inputMethodProfileTimer is null)
        {
            return;
        }

        _inputMethodProfileReady = false;
        _inputMethodProfileTimer.Stop();
        _inputMethodProfileTimer.Start();
    }

    /// <summary>
    /// Allows one final observation after the activation callback stream becomes quiet.
    /// </summary>
    private void OnInputMethodProfileTimerTick(object? sender, EventArgs eventArgs)
    {
        _inputMethodProfileTimer?.Stop();
        _inputMethodProfileReady = true;

        QueueInputMethodStateRefresh();
    }

    /// <summary>
    /// Ends the current profile settlement and releases its timer state.
    /// </summary>
    private void CompleteInputMethodProfileSettlement()
    {
        _inputMethodProfileTimer?.Stop();
        _settlingInputMethod     = null;
        _inputMethodProfileReady = false;
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
            && left?.KeyboardLayout == right?.KeyboardLayout
            && left?.TouchLayoutPreference == right?.TouchLayoutPreference;
    }

    /// <summary>
    /// Carries values read under one focus target and notification version.
    /// </summary>
    private sealed record InputMethodObservation(
        SoftKeyboardInputMethodSnapshot Snapshot,
        SoftKeyboardInputMode? Mode,
        SoftKeyboardInputModeButton? Button,
        InputModeTransitionRoute? TransitionRoute,
        CultureInfo Culture,
        bool IsProfileSettling
    );

    /// <summary>
    /// Retains the command owner selected by the same pass that resolved visible mode state.
    /// </summary>
    private sealed class InputModeTransitionRoute
    {
        private readonly ISoftKeyboardInputModeAdapter? _adapter;
        private readonly SoftKeyboardInputModeTransition? _adapterTransition;
        private readonly SoftKeyboardInputMode? _platformMode;
        private readonly bool _usesPlatformButton;

        private InputModeTransitionRoute(
            ISoftKeyboardInputModeAdapter? adapter,
            SoftKeyboardInputModeTransition? adapterTransition,
            SoftKeyboardInputMode? platformMode,
            bool usesPlatformButton)
        {
            _adapter            = adapter;
            _adapterTransition  = adapterTransition;
            _platformMode       = platformMode;
            _usesPlatformButton = usesPlatformButton;
        }

        internal static InputModeTransitionRoute FromAdapter(
            ISoftKeyboardInputModeAdapter adapter,
            SoftKeyboardInputModeTransition transition)
        {
            return new InputModeTransitionRoute(adapter, transition, null, false);
        }

        internal static InputModeTransitionRoute FromPlatformButton()
        {
            return new InputModeTransitionRoute(null, null, null, true);
        }

        internal static InputModeTransitionRoute FromPlatformMode(SoftKeyboardInputMode mode)
        {
            return new InputModeTransitionRoute(null, null, mode, false);
        }

        internal bool TryInvoke(
            ISoftKeyboardInputMethodSession session,
            SoftKeyboardInputMethod inputMethod)
        {
            if (_adapter is not null && _adapterTransition is not null)
            {
                return _adapter.TrySwitchMode(inputMethod, _adapterTransition);
            }

            if (_usesPlatformButton)
            {
                return session is ISoftKeyboardInputModeButtonSession buttonSession
                    && buttonSession.TryInvokeInputModeButton(inputMethod);
            }

            return _platformMode is not null
                && session.TrySwitchInputMode(inputMethod, _platformMode);
        }
    }
}
