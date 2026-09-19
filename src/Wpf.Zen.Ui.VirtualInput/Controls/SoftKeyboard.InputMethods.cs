// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates input-method commands and read-only state observation on the owning Dispatcher.
/// </summary>
public partial class SoftKeyboard
{
    /// <summary>
    /// Gets whether a profile command or state publication is currently in progress.
    /// Candidate discovery resumes after the operation and its layout publication finish.
    /// </summary>
    internal bool IsInputMethodOperationActive => _inputMethodOperationDepth != 0;

#pragma warning disable IDISP006 // WPF releases the owned session when the control unloads.
    private ISoftKeyboardInputMethodSession? _inputMethodSession;
#pragma warning restore IDISP006

    private int _inputMethodSessionVersion;
    private int _inputMethodOperationDepth;
    private int _inputMethodObservationVersion;

    private readonly InputMethodObservationCoordinator _inputObservation;
    private CultureInfo? _observedInputCulture;
    private SoftKeyboardInputMethod? _observedModeProfile;
    private IInputElement? _observationTarget;
    private bool _observationInitialized;
    private int _observationInputVersion;
    private ISoftKeyboardLayoutProvider? _observedLayoutProvider;

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
    /// Requests profile discovery and refreshes presentation when its observation window is
    /// complete.
    /// </summary>
    /// <remarks>
    /// This method is an explicit refresh request. System notifications are processed separately
    /// according to their notification type. Failures preserve the last successfully published
    /// state.
    /// </remarks>
    /// <returns>
    /// True when presentation was published or confirmed unchanged. False also indicates a pending
    /// activation observation; it does not necessarily indicate a platform failure.
    /// </returns>
    public bool RefreshInputMethods()
    {
        VerifyAccess();

        return IsInputMethodOperationActive ? false : RefreshInputMethodStateNow();
    }

    /// <summary>
    /// Reads and publishes one current input-method observation.
    /// </summary>
    private bool RefreshInputMethodsCore()
    {
        _inputMethodOperationDepth++;

        var sessionVersion     = _inputMethodSessionVersion;
        var inputVersion       = _inputOperationVersion;
        var observationVersion = _inputMethodObservationVersion;
        var target             = ActiveTarget;
        var session            = _inputMethodSession;

        bool IsCurrent()
        {
            return sessionVersion == _inputMethodSessionVersion
                && inputVersion == _inputOperationVersion
                && observationVersion == _inputMethodObservationVersion
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
            return IsCurrent();
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (sessionVersion == _inputMethodSessionVersion && inputVersion == _inputOperationVersion)
            {
                SetValue(InputMethodErrorPropertyKey, exception);
            }

            return false;
        }
        finally
        {
            if (!ReferenceEquals(session, _inputMethodSession))
            {
                DisposeInputMethodSession(session);
            }

            var sourceChanged = sessionVersion != _inputMethodSessionVersion;
            _inputMethodOperationDepth--;
            PublishPendingInputMethodLayout();

            if (sourceChanged && !IsInputMethodOperationActive)
            {
                RefreshInputMethodState();
            }
        }
    }

    /// <summary>
    /// Requests native activation of a catalog entry; later observations describe its presentation.
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

        return RunInputMethodCommand(
            (session, isCurrent) => session.TryActivate(inputMethod) && isCurrent()
        );
    }

    /// <summary>
    /// Reinterprets observations after an adapter, focus, or extension policy change.
    /// </summary>
    private void RefreshInputMode()
    {
        RefreshInputMethodState();
    }

    /// <summary>
    /// Writes one native input-method command without reading or publishing observable state.
    /// </summary>
    private bool RunInputMethodCommand(
        Func<ISoftKeyboardInputMethodSession, Func<bool>, bool> command)
    {
        VerifyAccess();

        if (IsInputMethodOperationActive)
        {
            return false;
        }

        _inputMethodOperationDepth++;

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
            }

            return false;
        }
        finally
        {
            if (!ReferenceEquals(session, _inputMethodSession))
            {
                DisposeInputMethodSession(session);
            }

            var sourceChanged = sessionVersion != _inputMethodSessionVersion;
            _inputMethodOperationDepth--;
            PublishPendingInputMethodLayout();

            if (sourceChanged && !IsInputMethodOperationActive)
            {
                RefreshInputMethodState();
            }
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

        return session;
    }

    /// <summary>
    /// Reads the catalog and active-profile state requested by a full refresh.
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

        if (!_observationInitialized
            || !HaveSameInputMethod(_observedModeProfile, snapshot.ActiveInputMethod)
            || !ReferenceEquals(_observationTarget, target)
            || _observationInputVersion != _inputOperationVersion)
        {
            BeginInputObservation(snapshot.ActiveInputMethod);
            return null;
        }

        if (_inputObservation.IsObserving)
        {
            return null;
        }

        var culture = CultureInfo.ReadOnly(
            (CultureInfo)InputLanguageManager.Current.CurrentInputLanguage.Clone()
        );

        var mode = ReadInputModeObservation(
            session,
            snapshot.ActiveInputMethod,
            culture,
            target,
            isCurrent
        );

        return isCurrent()
            ? new InputMethodObservation(
                snapshot,
                mode.Button,
                mode.LayoutProvider,
                culture
            )
            : null;
    }

    /// <summary>
    /// Resolves platform and extension presentation from the active generation.
    /// </summary>
    private InputModeObservation ReadInputModeObservation(
        ISoftKeyboardInputMethodSession session,
        SoftKeyboardInputMethod? active,
        CultureInfo culture,
        IInputElement? target,
        Func<bool> isCurrent)
    {
        if (active is null)
        {
            return new InputModeObservation(null, null);
        }

        var platformButton = session is ISoftKeyboardInputModeButtonSession buttonSession
            ? buttonSession.GetInputModeButton(active)
            : null;
        var adapter = ResolveObservedInputModeAdapter(active, culture, target);
        if (adapter is not null && isCurrent()
            && InputMethodSource is WindowsSoftKeyboardInputMethodSource { StateBackend: { } stateBackend }
            && target is DependencyObject editor && ReferenceEquals(target, Keyboard.FocusedElement)
            && PresentationSource.FromDependencyObject(editor) is HwndSource window)
        {
            // Load, profile changes and window reactivation all settle through this path.
            // Sample only the current editor; interpretation is the extension's existing contract.
            _inputObservation.CompleteObservation(
                useImm => stateBackend.ReadState(useImm, window.Handle),
                state =>
                {
                    var result = adapter.GetContribution(active, state);
                    return result.Button is not null || result.LayoutProvider is not null
                        || result.Transition is not null;
                },
                () => isCurrent() && ReferenceEquals(target, Keyboard.FocusedElement)
                    && !window.IsDisposed
                    && ReferenceEquals(PresentationSource.FromDependencyObject(editor), window));
        }

        if (!isCurrent())
        {
            return new InputModeObservation(null, null);
        }

        var contribution = adapter?.GetContribution(active, _inputObservation.Observation)
            ?? SoftKeyboardInputModeContribution.Empty;
        var fallbackButton = contribution.Transition is not null ? contribution.Button : null;
        var button = contribution.OverridePlatformButton && contribution.Button is not null
                ? contribution.Button
                : platformButton ?? fallbackButton;

        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Presentation] BUTTON resolved; "
            + $"profileId={active.ProfileId}, classId={active.ClassId}, "
            + $"adapter={adapter?.GetType().Name ?? "<none>"}, "
            + $"openObserved={_inputObservation.Observation.OpenObserved}, "
            + $"open={_inputObservation.Observation.OpenValue?.ToString() ?? "<unavailable>"}, "
            + $"conversionObserved={_inputObservation.Observation.ConversionObserved}, "
            + $"conversion={_inputObservation.Observation.ConversionValue?.ToString() ?? "<unavailable>"}, "
            + $"platform={platformButton is not null}, fallback={fallbackButton is not null}, "
            + $"override={contribution.OverridePlatformButton}, button={button?.Id ?? "<none>"}.");
        return new InputModeObservation(button, contribution.LayoutProvider);
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
            culture, scope, Page, ShiftState, inputMethod,
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

        var layoutChanged  = !ReferenceEquals(_observedLayoutProvider, observation.LayoutProvider);
        var buttonChanged  = profileChanged || !Equals(InputModeButton, observation.Button);
        var cultureChanged = _observedInputCulture?.Name != observation.Culture.Name;

        _observedLayoutProvider = observation.LayoutProvider;
        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Presentation] Observation APPLIED; "
            + $"profileId={snapshot.ActiveInputMethod?.ProfileId}, "
            + $"button={observation.Button?.Id ?? "<none>"}, layoutChanged={layoutChanged}.");

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

        if (buttonChanged)
        {
            SetValue(InputModeButtonPropertyKey, observation.Button);
        }

        _observedInputCulture     =  observation.Culture;
        _inputMethodLayoutPending |= profileChanged || layoutChanged || buttonChanged
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

        if (!isCurrent())
        {
            return false;
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
        var operationActive = IsInputMethodOperationActive;

        _inputMethodOperationDepth++;
        _inputMethodSession = null;

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

            if (version == _inputMethodSessionVersion && InputModeButton is not null)
            {
                SetValue(InputModeButtonPropertyKey, null);
            }

            _observedLayoutProvider = null;

            _observedInputCulture     = null;
            _inputMethodLayoutPending = true;
            ResetObservedPlatformState();

            if (!operationActive)
            {
                DisposeInputMethodSession(session);
            }
        }
        finally
        {
            _inputMethodOperationDepth--;

            if (!operationActive && version != _inputMethodSessionVersion)
            {
                RefreshInputMethodState();
            }
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
    /// Performs one explicit full refresh immediately.
    /// </summary>
    private bool RefreshInputMethodState()
    {
        return RefreshInputMethodStateNow();
    }

    /// <summary>
    /// Performs one full read for the request currently being processed.
    /// </summary>
    private bool RefreshInputMethodStateNow()
    {
        var refreshed = RefreshInputMethodsCore();
        RefreshCandidatesAfterInputMethodChange();

        return refreshed;
    }

    /// <summary>
    /// Lets candidate presentation observe the same completed input-method change.
    /// </summary>
    private void RefreshCandidatesAfterInputMethodChange()
    {
        if (!IsLoaded)
        {
            return;
        }

        RequestCandidateRefresh();
        QueueCandidateBridgeRefresh();
    }

    private void OnInputMethodSourceStateInvalidated(object? sender, EventArgs eventArgs)
    {
        VerifyAccess();
        OnSessionInputMethodStateInvalidated();
    }

    private static void OnInputMethodSourceChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        var operationActive = keyboard.IsInputMethodOperationActive;

        keyboard.ResetInputMethodSession();
        keyboard.SetValue(InputMethodErrorPropertyKey, null);

        if (!operationActive)
        {
            keyboard.RefreshInputMethodState();
        }
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
            && left?.KeyboardLayout == right?.KeyboardLayout;
    }

    /// <summary>
    /// Starts an empty profile/editor observation while retaining the last published surface.
    /// </summary>
    private void BeginInputObservation(SoftKeyboardInputMethod? profile)
    {
        _observedModeProfile = profile;
        _observationTarget = ActiveTarget;
        _observationInputVersion = _inputOperationVersion;
        _observationInitialized = true;
        // Invalidate pending snapshots immediately; extension cleanup runs on the Dispatcher.
        _candidateRefreshVersion++;
        _inputObservation.Begin();
    }

    /// <summary>
    /// Publishes scheduled work only for its original editor and input session.
    /// </summary>
    private void PublishScheduledInputObservation()
    {
        if (_observationInitialized && (!ReferenceEquals(_observationTarget, ActiveTarget)
            || _observationInputVersion != _inputOperationVersion))
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode.Observation] Publication REJECTED; reason=ExpiredTarget.");
            ResetObservedPlatformState();
            return;
        }

        if (IsInputMethodOperationActive)
        {
            _inputObservation.RequestRefresh();
            return;
        }

        _session?.EnsureInputMethodMonitoring();
        RefreshInputMethodStateNow();
    }

    /// <summary>
    /// Cancels pending work and clears data belonging to an expired profile/editor generation.
    /// </summary>
    private void ResetObservedPlatformState()
    {
        _inputObservation.Cancel();
        _observedModeProfile = null;
        _observationTarget = null;
        _observationInitialized = false;
    }

    /// <summary>
    /// Carries a complete surface publication, without asserting an interpreted native mode.
    /// </summary>
    private sealed record InputMethodObservation(
        SoftKeyboardInputMethodSnapshot Snapshot,
        SoftKeyboardInputModeButton? Button,
        ISoftKeyboardLayoutProvider? LayoutProvider,
        CultureInfo Culture
    );

    private readonly record struct InputModeObservation(
        SoftKeyboardInputModeButton? Button,
        ISoftKeyboardLayoutProvider? LayoutProvider
    );
}
