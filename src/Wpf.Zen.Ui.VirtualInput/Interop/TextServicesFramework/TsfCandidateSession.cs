// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Owns dispatcher-wide TSF candidate presentation and scopes asynchronous operations to one UI
/// element.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class TsfCandidateSession : ISoftKeyboardTsfUIElementSink, IDisposable
{
    private static readonly Guid UIElementSinkId = new("EA1EA136-19DF-11D7-A6D2-00065B84435C");

    private readonly Dispatcher          _dispatcher;
    private readonly List<WeakReference> _keyboards = [];
    private readonly DispatcherTimer     _candidatePollTimer;

    private ITfThreadMgr?                      _threadManager;
    private ITfUIElementMgr?                   _elementManager;
    private ITfSource?                         _source;
    private ITfCandidateListUIElementBehavior? _activeBehavior;
    private SoftKeyboard?                      _activeKeyboard;
    private uint                               _cookie;
    private IntPtr                             _sinkPointer;
    private uint?                              _activeElementId;
    private uint[]                             _activePageStarts = [];
    private uint                               _activePageNumber;
    private int                                _activeCandidateVersion;
    private CancellationTokenSource?           _activeCandidateCancellation;
    private bool                               _reflectionUnavailable;

    // Some TIPs keep an element in the manager after its composition has verifiably ended.
    // Remember that identity across ordinary refreshes; a new Begin or WPF composition permits
    // reuse. Keeping only the composition token avoids extending the editor/control lifetime.
    private ITfCandidateListUIElementBehavior? _retiredBehavior;
    private object?                            _retiredCompositionIdentity;

    /// <summary>
    /// Creates the candidate bridge and its bounded polling cadence on the TSF Dispatcher.
    /// </summary>
    /// <param name="dispatcher">The Dispatcher that owns WPF text services and candidates.</param>
    internal TsfCandidateSession(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        // Polling refreshes an existing element and does not wait for injected input. Normal
        // priority lets due snapshots advance while mouse or keyboard input remains pending.
        _candidatePollTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(75),
            DispatcherPriority.Normal,
            OnCandidatePollTimerTick,
            dispatcher
        );

        _candidatePollTimer.Stop();

        dispatcher.ShutdownStarted += OnDispatcherShutdown;
    }

    internal void Register(SoftKeyboard keyboard)
    {
        if (!_keyboards.Any(reference => ReferenceEquals(reference.Target, keyboard)))
        {
            _keyboards.Add(new WeakReference(keyboard));
        }

        EnsureConnected();
    }

    internal void Register(SoftKeyboard keyboard, SoftKeyboardCandidateHost host)
    {
        if (!ReferenceEquals(host.Keyboard, keyboard))
        {
            throw new ArgumentException(
                "The candidate host belongs to another keyboard.",
                nameof(host)
            );
        }

        Register(keyboard);
    }

    internal void Refresh(SoftKeyboard keyboard)
    {
        if (keyboard.IsInputMethodOperationActive)
        {
            return;
        }

        // Refresh the active owner before discovering another candidate element.
        if (ReferenceEquals(_activeKeyboard, keyboard) && _activeBehavior is not null)
        {
            UpdateCandidateSnapshot();

            if (_activeBehavior is not null)
            {
                return;
            }
        }

        if (!CanEmbedCandidates(keyboard))
        {
            ReleasePresentation(keyboard);
            return;
        }

        EnsureConnected();
        TryCaptureExistingCandidate();
    }

    internal void ReleasePresentation(SoftKeyboard keyboard)
    {
        _dispatcher.VerifyAccess();

        if (!ReferenceEquals(_activeKeyboard, keyboard))
        {
            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            return;
        }

        // Show the TIP-owned surface before dropping the behavior interface. An active
        // composition can outlive this control's visual presence, and leaving Show(false)
        // behind would make both the embedded and native candidates inaccessible.
        RestoreNativeCandidate();
        ClearActiveCandidate();
    }

    internal void Unregister(SoftKeyboard keyboard)
    {
        _keyboards.RemoveAll(
            reference => !reference.IsAlive || ReferenceEquals(reference.Target, keyboard)
        );

        if (ReferenceEquals(_activeKeyboard, keyboard))
        {
            RestoreNativeCandidate();
            ClearActiveCandidate();
        }

        if (_keyboards.Count == 0)
        {
            Disconnect();
        }
    }

    internal void EnsureConnected()
    {
        _dispatcher.VerifyAccess();

        if (_source is not null || _reflectionUnavailable)
        {
            return;
        }

        try
        {
            var manager = WpfTextServicesAccessor.TryGetThreadManager();

            // WPF activates TSF when its first editor creates a TextStore. Borrow that manager
            // after initialization; activation flags affect every TIP on the dispatcher, including
            // native-only candidate windows. Focus and input refreshes retry a missing host.
            if (manager is null)
            {
                return;
            }

            _threadManager  = manager as ITfThreadMgr;
            _elementManager = manager as ITfUIElementMgr;
            _source         = manager as ITfSource;

            if (_threadManager is null || _elementManager is null || _source is null)
            {
                _reflectionUnavailable = true;

                SetUnavailableState();
                Disconnect();

                return;
            }

            var sinkId = UIElementSinkId;

            _sinkPointer = Marshal.GetComInterfaceForObject(this, typeof(ISoftKeyboardTsfUIElementSink));

            var result = _source.AdviseSink(ref sinkId, _sinkPointer, out var cookie);
            if (result < 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            _cookie = cookie;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            _reflectionUnavailable = true;

            SetUnavailableState();
            Disconnect();
        }
    }

    /// <summary>
    /// Submits the selected item and retires presentation only after native completion is observed.
    /// Reentrant replacements retain their lifetime.
    /// </summary>
    internal async Task<SoftKeyboardCandidateOperationResult> TryCommitAsync(
        SoftKeyboard keyboard,
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(_activeKeyboard, keyboard)
            || _activeBehavior is not { } nativeBehavior || _activeCandidateCancellation is null)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }

        var version             = _activeCandidateVersion;
        var compositionIdentity = keyboard.CompositionIdentity;
        var target              = keyboard.ActiveTarget;

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _activeCandidateCancellation.Token
        );

        var operationToken = operationCancellation.Token;

        try
        {
            operationToken.ThrowIfCancellationRequested();
            if (!ValidateCandidateOperation(keyboard, nativeBehavior, version)
                || nativeBehavior.GetCount(out var count) < 0
                || !ValidateCandidateOperation(keyboard, nativeBehavior, version)
                || candidate.Index >= count)
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            TsfCandidateDocument composition;
            try
            {
                composition = TsfCandidateDocument.Capture(nativeBehavior);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // No submission has happened yet. Keep presentation available and surface the
                // missing native boundary instead of pretending that a local reset completes it.
                keyboard.SetCandidateError(exception);
                return SoftKeyboardCandidateOperationResult.Failure(exception);
            }

            using var compositionLease = composition;

            var context = CreateBehaviorContext(
                keyboard, nativeBehavior, operationToken, cancellationToken
            );

            if (context is not null)
            {
                foreach (var behavior in keyboard.GetTsfCandidateBehaviors())
                {
                    operationToken.ThrowIfCancellationRequested();

                    if (!ValidateCandidateOperation(keyboard, nativeBehavior, version))
                    {
                        return SoftKeyboardCandidateOperationResult.NotHandled;
                    }

                    SoftKeyboardCandidateOperationResult result;
                    try
                    {
                        result = await behavior
                            .CommitAsync(context, candidate, cancellationToken)
                            .ConfigureAwait(true);
                    }
                    catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!IsCandidateOperationCurrent(keyboard, nativeBehavior, version))
                        {
                            return SoftKeyboardCandidateOperationResult.NotHandled;
                        }

                        keyboard.SetCandidateError(exception);
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (result?.Handled == true)
                    {
                        if (result.Succeeded)
                        {
                            return ConfirmSubmittedCandidate(
                                keyboard, nativeBehavior, version, compositionIdentity, target, composition
                            );
                        }

                        return result;
                    }
                }
            }

            operationToken.ThrowIfCancellationRequested();

            // SetSelection can synchronously end this UI element and begin another in the same
            // document. Only finalize the captured element if its generation survived selection.
            if (!ValidateCandidateOperation(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            var selectionResult = nativeBehavior.SetSelection(candidate.Index);

            Debug.WriteLine(
                $"[SoftKeyboard.Candidates.Tsf] SELECTION applied; version={version}, "
                + $"index={candidate.Index}, result=0x{selectionResult:X8}."
            );

            if (selectionResult < 0 || !ValidateCandidateOperation(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            var finalizeResult = nativeBehavior.FinalizeCandidate();

            Debug.WriteLine(
                $"[SoftKeyboard.Candidates.Tsf] FINALIZE returned; version={version}, "
                + $"index={candidate.Index}, result=0x{finalizeResult:X8}, "
                + $"elementCurrent={IsCandidateOperationCurrent(keyboard, nativeBehavior, version)}."
            );

            // S_OK acknowledges the candidate action, but some TIPs leave their pre-edit active.
            // Observe native composition state before hiding its list; pending edit sessions must run.
            if (finalizeResult == 0)
            {
                return ConfirmSubmittedCandidate(
                    keyboard, nativeBehavior, version, compositionIdentity, target, composition
                );
            }

            return SoftKeyboardCandidateOperationResult.NotHandled;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCandidateOperationCurrent(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            RestoreNativeCandidate();
            ClearActiveCandidate();

            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);

            return SoftKeyboardCandidateOperationResult.Failure(exception);
        }
    }

    internal async Task<SoftKeyboardCandidateOperationResult> TryMovePageAsync(
        SoftKeyboard keyboard,
        int delta,
        CancellationToken cancellationToken)
    {
        if (!ReferenceEquals(_activeKeyboard, keyboard)
            || _activeBehavior is not { } nativeBehavior || _activeCandidateCancellation is null
            || delta == 0 || _activePageNumber == 0)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }

        var targetPage = checked((int)_activePageNumber - 1 + delta);
        if (targetPage < 0 || targetPage >= _activePageStarts.Length)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }

        var version   = _activeCandidateVersion;
        var pageStart = _activePageStarts[targetPage];

        using var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _activeCandidateCancellation.Token
        );

        var operationToken = operationCancellation.Token;

        try
        {
            operationToken.ThrowIfCancellationRequested();

            if (!ValidateCandidateOperation(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            var context = CreateBehaviorContext(keyboard, nativeBehavior, operationToken, cancellationToken);
            if (context is not null)
            {
                foreach (var behavior in keyboard.GetTsfCandidateBehaviors())
                {
                    operationToken.ThrowIfCancellationRequested();

                    if (!ValidateCandidateOperation(keyboard, nativeBehavior, version))
                    {
                        return SoftKeyboardCandidateOperationResult.NotHandled;
                    }

                    SoftKeyboardCandidateOperationResult result;
                    try
                    {
                        result = await behavior
                            .MovePageAsync(context, delta, cancellationToken)
                            .ConfigureAwait(true);
                    }
                    catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is not OutOfMemoryException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!IsCandidateOperationCurrent(keyboard, nativeBehavior, version))
                        {
                            return SoftKeyboardCandidateOperationResult.NotHandled;
                        }

                        keyboard.SetCandidateError(exception);
                        continue;
                    }

                    cancellationToken.ThrowIfCancellationRequested();

                    if (result?.Handled == true)
                    {
                        return result;
                    }
                }
            }

            operationToken.ThrowIfCancellationRequested();

            // TSF owns the page boundaries. Selecting the first item on an adjacent page asks
            // the TIP to move its current page without committing text; FinalizeCandidate is
            // deliberately reserved for an actual candidate-button invocation.
            if (!ValidateCandidateOperation(keyboard, nativeBehavior, version)
                || nativeBehavior.SetSelection(pageStart) < 0
                || !ValidateCandidateOperation(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            UpdateCandidateSnapshot();
            return SoftKeyboardCandidateOperationResult.Success;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (operationToken.IsCancellationRequested)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsCandidateOperationCurrent(keyboard, nativeBehavior, version))
            {
                return SoftKeyboardCandidateOperationResult.NotHandled;
            }

            RestoreNativeCandidate();
            ClearActiveCandidate();

            keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);

            return SoftKeyboardCandidateOperationResult.Failure(exception);
        }
    }

    /// <inheritdoc />
    int ISoftKeyboardTsfUIElementSink.BeginUIElement(uint elementId, ref int show)
    {
        Debug.WriteLine(
            $"[SoftKeyboard.Candidates.Tsf] BEGIN observed; elementId={elementId}."
        );

        ITfCandidateListUIElement? candidate = null;

        var retained = false;

        try
        {
            var keyboard = FindActiveKeyboard();
            if (keyboard is null || keyboard.IsInputMethodOperationActive
                || !TryGetCandidate(elementId, out candidate))
            {
                // pbShow is an in/out value. Explicitly opt into the TIP's native UI whenever
                // no visible keyboard can own this element instead of relying on its input.
                show = 1;
                return 0;
            }

            if (candidate is not ITfCandidateListUIElementBehavior behavior
                || !MatchesFocusedDocument(candidate)
                || !ShouldEmbedCandidate(keyboard, behavior))
            {
                show = 1;
                keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);
                return 0;
            }

            RestoreNativeCandidate();
            ClearActiveCandidate();

            _retiredBehavior            = null;
            _retiredCompositionIdentity = null;

            _activeKeyboard  = keyboard;
            _activeBehavior  = behavior;
            _activeElementId = elementId;

            retained = true;

#pragma warning disable IDISP003 // ClearActiveCandidate disposed the previous element cancellation source.
            _activeCandidateCancellation = new CancellationTokenSource();
#pragma warning restore IDISP003

            show = 0;

            // Begin negotiates ownership before the TIP has finished initializing its UI.
            // Read candidate contents only after its first UpdateUIElement notification.
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            show = 1;
            ClearActiveCandidate();
        }
        finally
        {
            if (!retained)
            {
                ReleaseProbe(candidate);
            }
        }

        return 0;
    }

    /// <inheritdoc />
    int ISoftKeyboardTsfUIElementSink.UpdateUIElement(uint elementId)
    {
        Debug.WriteLine(
            $"[SoftKeyboard.Candidates.Tsf] UPDATE observed; elementId={elementId}, "
            + $"activeElementId={_activeElementId}, version={_activeCandidateVersion}."
        );

        if (_activeBehavior is not null && elementId == _activeElementId)
        {
            _candidatePollTimer.Start();
            UpdateCandidateSnapshot();
        }

        return 0;
    }

    /// <inheritdoc />
    int ISoftKeyboardTsfUIElementSink.EndUIElement(uint elementId)
    {
        Debug.WriteLine(
            $"[SoftKeyboard.Candidates.Tsf] END observed; elementId={elementId}, "
            + $"activeElementId={_activeElementId}, version={_activeCandidateVersion}."
        );

        if (elementId == _activeElementId)
        {
            ClearActiveCandidate();
        }
        else if (_activeBehavior is not null && !_activeElementId.HasValue)
        {
            // End can be delivered before the manager removes the element. Reconcile after
            // the callback unwinds; an unrelated end must not clear the current candidate.
            // Normal priority keeps the displayed element current without waiting for input idle.
            var version = _activeCandidateVersion;

            _ = _dispatcher.BeginInvoke(
                DispatcherPriority.Normal,
                new Action(
                    () =>
                    {
                        if (version == _activeCandidateVersion)
                        {
                            UpdateCandidateSnapshot();
                            if (_activeBehavior is null)
                            {
                                TryCaptureExistingCandidate();
                            }
                        }
                    }
                )
            );
        }

        return 0;
    }

    private bool TryGetCandidate(uint elementId, out ITfCandidateListUIElement? candidate)
    {
        candidate = null;

        if (_elementManager is null)
        {
            return false;
        }

        object? element = null;
        try
        {
            if (_elementManager.GetUIElement(elementId, out element) >= 0)
            {
                candidate = element as ITfCandidateListUIElement;
            }

            return candidate is not null;
        }
        finally
        {
            if (candidate is null)
            {
                ReleaseProbe(element);
            }
        }
    }

    private void TryCaptureExistingCandidate()
    {
        if (_activeBehavior is not null)
        {
            UpdateCandidateSnapshot();
            return;
        }

        var keyboard = FindActiveKeyboard();
        if (keyboard is null || _elementManager is null)
        {
            return;
        }

        object? enumeratorObject = null;
        try
        {
            if (_elementManager.EnumUIElements(out enumeratorObject) < 0
                || enumeratorObject is not IEnumTfUIElements enumerator)
            {
                return;
            }

            while (enumerator.Next(1, out var element, out var fetched) >= 0 && fetched == 1)
            {
                var retained = false;

                try
                {
                    if (element is not ITfCandidateListUIElement candidate)
                    {
                        continue;
                    }

                    if (ReferenceEquals(_retiredCompositionIdentity, keyboard.CompositionIdentity)
                        && HaveSameComIdentity(_retiredBehavior, element))
                    {
                        // Neither polling nor an unrelated refresh can revive a finalized list.
                        // IsShown is unsuitable here because embedding itself calls Show(false).
                        continue;
                    }

                    if (!MatchesFocusedDocument(candidate)
                        || element is not ITfCandidateListUIElementBehavior behavior
                        // Enumeration must use the same extension policy as BeginUIElement;
                        // otherwise callback timing could change the presentation decision.
                        || !ShouldEmbedCandidate(keyboard, behavior))
                    {
                        keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);
                        continue;
                    }

                    // A normally activated WPF TSF thread is not guaranteed to receive the
                    // BeginUIElement callback. UIElement-capable TIPs, including Microsoft
                    // Pinyin, still publish their active element through the manager. Discovering
                    // it after the injected key lets the application transition into embedded
                    // presentation without deactivating WPF's ThreadManager or invalidating its
                    // TextStore client id.
                    ClearActiveCandidate();

                    _activeKeyboard  = keyboard;
                    _activeBehavior  = behavior;
                    // Enumeration exposes COM identity but no UI-element id. Such elements are
                    // validated against a fresh manager enumeration before every read or operation.
                    _activeElementId = null;

                    _retiredBehavior            = null;
                    _retiredCompositionIdentity = null;

                    retained = true;

#pragma warning disable IDISP003 // ClearActiveCandidate disposed the previous element cancellation source.
                    _activeCandidateCancellation = new CancellationTokenSource();
#pragma warning restore IDISP003

                    if (behavior.Show(0) < 0)
                    {
                        RestoreNativeCandidate();
                        ClearActiveCandidate();

                        keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);

                        return;
                    }

                    _candidatePollTimer.Start();

                    UpdateCandidateSnapshot();

                    return;
                }
                finally
                {
                    if (!retained)
                    {
                        ReleaseProbe(element);
                    }
                }
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // Enumeration is an enhancement for a TIP running in normal WPF TSF mode. Failure
            // leaves its native candidate surface untouched and never affects text composition.
        }
        finally
        {
            // The enumerator retains native TIP elements. Release this private snapshot on its
            // owning STA before later input can end a composition or unload the TIP. Deferring
            // cleanup to the RCW finalizer can retain stale third-party element pointers.
            if (enumeratorObject is not null && Marshal.IsComObject(enumeratorObject))
            {
                Marshal.ReleaseComObject(enumeratorObject);
            }
        }
    }

    /// <summary>
    /// Checks whether the candidate belongs to WPF's currently focused TSF document.
    /// </summary>
    private bool MatchesFocusedDocument(ITfCandidateListUIElement candidate)
    {
        object? candidateDocument = null;
        object? focusedDocument   = null;
        try
        {
            return _threadManager is not null
                && candidate.GetDocumentMgr(out candidateDocument) >= 0
                && _threadManager.GetFocus(out focusedDocument) >= 0
                && HaveSameComIdentity(candidateDocument, focusedDocument);
        }
        finally
        {
            // Each COM out parameter acquired a reference, even when both project the same RCW.
            ReleaseProbe(focusedDocument);
            ReleaseProbe(candidateDocument);
        }
    }

    /// <summary>
    /// Balances one temporary UI-element acquisition on the TSF dispatcher, including rejected
    /// interfaces and failed capability checks. These probes must not remain rooted by an RCW
    /// until a later composition or profile change. Shared WPF managers are not probe acquisitions.
    /// </summary>
    private static void ReleaseProbe(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.ReleaseComObject(value);
        }
    }

    private void UpdateCandidateSnapshot()
    {
        if (_activeBehavior is not { } behavior || _activeKeyboard is not { } keyboard)
        {
            return;
        }

        var version = _activeCandidateVersion;
        try
        {
            if (!ValidateCandidateOperation(keyboard, behavior, version))
            {
                return;
            }

            var countResult = behavior.GetCount(out var count);
            if (!IsCandidateOperationCurrent(keyboard, behavior, version))
            {
                return;
            }

            if (countResult < 0)
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
                return;
            }

            var selectionResult = behavior.GetSelection(out var selection);
            if (!IsCandidateOperationCurrent(keyboard, behavior, version))
            {
                return;
            }

            if (selectionResult < 0)
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
                return;
            }

            GetCandidatePageRange(
                behavior,
                count,
                out var firstIndex,
                out var endIndex,
                out var pageNumber,
                out var pageCount,
                out var pageStarts
            );

            if (!IsCandidateOperationCurrent(keyboard, behavior, version))
            {
                return;
            }

            var visibleEnd = firstIndex + Math.Min(endIndex - firstIndex, 256U);
            var candidates = new List<SoftKeyboardCandidate>(checked((int)(visibleEnd - firstIndex)));

            for (var index = firstIndex; index < visibleEnd; index++)
            {
                var stringResult = behavior.GetString(index, out var text);
                if (!IsCandidateOperationCurrent(keyboard, behavior, version))
                {
                    return;
                }

                if (stringResult >= 0 && text is not null)
                {
                    candidates.Add(new SoftKeyboardCandidate(index, text));
                }
            }

            _activePageNumber = pageNumber;
            _activePageStarts = pageStarts;

            var host = keyboard.ActiveCandidateHost;
            host?.Publish(
                new SoftKeyboardCandidateSnapshot(
                    candidates,
                    SoftKeyboardCandidatePresentationState.Embedded,
                    pageNumber,
                    pageCount,
                    selectionResult == 0 && selection < count ? selection : null
                )
            );
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            if (IsCandidateOperationCurrent(keyboard, behavior, version))
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
            }
        }
    }

    private static void GetCandidatePageRange(
        ITfCandidateListUIElementBehavior behavior,
        uint candidateCount,
        out uint firstIndex,
        out uint endIndex,
        out uint pageNumber,
        out uint pageCount,
        out uint[] pageStarts)
    {
        firstIndex = 0;
        endIndex   = candidateCount;
        pageNumber = candidateCount == 0 ? 0U : 1U;
        pageCount  = candidateCount == 0 ? 0U : 1U;
        pageStarts = candidateCount == 0 ? [] : [0U];

        if (candidateCount == 0)
        {
            return;
        }

        // A page cannot begin more often than there are candidates. The cap protects the UI
        // thread from a malformed third-party TIP reporting an unbounded candidate count; the
        // fallback still exposes candidates even when page metadata cannot be trusted.
        var capacity = Math.Min(candidateCount, 4096U);
        var indices  = Marshal.AllocCoTaskMem(checked((int)capacity * sizeof(uint)));

        try
        {
            if (behavior.GetPageIndex(indices, capacity, out var nativePageCount) < 0
                || nativePageCount == 0
                || nativePageCount > capacity
                || behavior.GetCurrentPage(out var currentPage) < 0
                || currentPage >= nativePageCount
                || currentPage >= capacity)
            {
                return;
            }

            var  nativePageStarts = new uint[nativePageCount];
            uint previousStart    = 0;

            for (uint page = 0; page < nativePageCount; page++)
            {
                var pageStart = unchecked(
                    (uint)Marshal.ReadInt32(indices, checked((int)page * sizeof(uint)))
                );

                if (pageStart >= candidateCount || (page > 0 && pageStart <= previousStart))
                {
                    return;
                }

                nativePageStarts[page] = pageStart;
                previousStart          = pageStart;
            }

            var start = nativePageStarts[currentPage];

            var end = currentPage + 1U < nativePageCount
                ? nativePageStarts[currentPage + 1U]
                : candidateCount;

            firstIndex = start;
            endIndex   = end;
            pageNumber = currentPage + 1U;
            pageCount  = nativePageCount;
            pageStarts = nativePageStarts;
        }
        finally
        {
            Marshal.FreeCoTaskMem(indices);
        }
    }

    private SoftKeyboard? FindActiveKeyboard()
    {
        _keyboards.RemoveAll(reference => !reference.IsAlive);

        var available = _keyboards
            .Select(reference => reference.Target as SoftKeyboard)
            .Where(CanEmbedCandidates)
            .ToArray();

        return available.LastOrDefault();
    }

    private static bool CanEmbedCandidates(SoftKeyboard? keyboard)
    {
        return keyboard?.CanPresentCandidates == true;
    }

    /// <summary>
    /// Requires an explicit extension decision before taking ownership of candidate presentation.
    /// Undecided or missing extensions leave the input method's native surface visible.
    /// </summary>
    private static bool ShouldEmbedCandidate(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior)
    {
        if (behavior.GetGuid(out var elementType) < 0)
        {
            return false;
        }

        var context = CreateBehaviorContext(keyboard, elementType);
        if (context is not null)
        {
            foreach (var candidateBehavior in keyboard.GetTsfCandidateBehaviors())
            {
                try
                {
                    var presentation = candidateBehavior.GetPresentation(context);
                    if (presentation != SoftKeyboardTsfCandidatePresentation.Default)
                    {
                        return presentation == SoftKeyboardTsfCandidatePresentation.Embedded;
                    }
                }
                catch (Exception exception) when (exception is not OutOfMemoryException)
                {
                    keyboard.SetCandidateError(exception);
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Captures extension metadata with the cancellation boundary of the element being operated on.
    /// </summary>
    private static SoftKeyboardTsfCandidateContext? CreateBehaviorContext(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior,
        CancellationToken cancellationToken,
        CancellationToken callerCancellationToken)
    {
        if (behavior.GetGuid(out var elementType) < 0)
        {
            return null;
        }

        return CreateBehaviorContext(keyboard, elementType, cancellationToken, callerCancellationToken);
    }

    /// <summary>
    /// Creates an extension context from an element type that the caller has already
    /// obtained from TSF.
    /// </summary>
    private static SoftKeyboardTsfCandidateContext? CreateBehaviorContext(
        SoftKeyboard keyboard,
        Guid elementType,
        CancellationToken cancellationToken = default,
        CancellationToken callerCancellationToken = default)
    {
        var host = keyboard.ActiveCandidateHost;
        if (host is null)
        {
            return null;
        }

        return new SoftKeyboardTsfCandidateContext(
            host,
            elementType,
            keyboard.Candidates.ToArray(),
            keyboard.CandidatePageNumber,
            keyboard.CandidatePageCount,
            keyboard.SelectedCandidateIndex,
            cancellationToken,
            callerCancellationToken
        );
    }

    /// <summary>
    /// Revalidates both managed target ownership and the TSF document before reading or committing.
    /// Focus and visibility can change without ending a TIP-owned composition.
    /// </summary>
    private bool ValidateActiveCandidate()
    {
        var version = _activeCandidateVersion;

        if (!_activeElementId.HasValue && _activeBehavior is { } enumerated)
        {
            var membership = IsEnumeratedElementActive(enumerated);

            if (version != _activeCandidateVersion)
            {
                return false;
            }

            if (membership != true)
            {
                // An ended element must not receive Show or any other native operation. If the
                // inventory query failed, restore presentation as a best-effort fallback instead.
                if (membership is null)
                {
                    RestoreNativeCandidate();
                }

                ClearActiveCandidate();
                return false;
            }
        }

        var valid = _activeKeyboard?.CanPresentCandidates == true
            && _activeBehavior is ITfCandidateListUIElement candidate
            && MatchesFocusedDocument(candidate);

        // A document query can synchronously replace the element. Its result only describes the
        // captured generation and must not restore or clear the later element's presentation.
        if (version != _activeCandidateVersion)
        {
            return false;
        }

        if (valid)
        {
            return true;
        }

        RestoreNativeCandidate();
        ClearActiveCandidate();

        return false;
    }

    private void RestoreNativeCandidate()
    {
        try
        {
            _activeBehavior?.Show(1);
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // Restoration is best effort: TSF also destroys its UI element at composition end.
        }
    }

    /// <summary>
    /// Tests identity without touching COM or the presentation of a later element.
    /// </summary>
    private bool IsCandidateOperationCurrent(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior,
        int version)
    {
        return version == _activeCandidateVersion
            && ReferenceEquals(keyboard, _activeKeyboard)
            && ReferenceEquals(behavior, _activeBehavior);
    }

    /// <summary>
    /// Revalidates both element generation and focused document before native input operations.
    /// COM document queries may reenter the sink, so identity is checked on both sides of
    /// validation.
    /// </summary>
    private bool ValidateCandidateOperation(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior,
        int version)
    {
        return IsCandidateOperationCurrent(keyboard, behavior, version)
            && ValidateActiveCandidate()
            && IsCandidateOperationCurrent(keyboard, behavior, version);
    }

    /// <summary>
    /// Confirms completion only while the same editor and document still own input.
    /// EndUIElement may arrive before composition end, so an ended UI element alone is not proof
    /// that the TIP reset. Failures retain presentation and never replay the candidate submission.
    /// </summary>
    private SoftKeyboardCandidateOperationResult ConfirmSubmittedCandidate(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior,
        int version,
        object compositionIdentity,
        IInputElement? target,
        TsfCandidateDocument composition)
    {
        bool IsCurrent()
        {
            return ReferenceEquals(compositionIdentity, keyboard.CompositionIdentity)
                && ReferenceEquals(target, keyboard.ActiveTarget)
                && keyboard.CanPresentCandidates
                && _elementManager is not null
                && (_activeBehavior is null || IsCandidateOperationCurrent(keyboard, behavior, version));
        }

        bool CanConfirm()
        {
            return IsCurrent()
                && _threadManager is not null
                && composition.MatchesFocus(_threadManager)
                && IsCurrent();
        }

        try
        {
            if (CanConfirm() && composition.IsCompositionCompleted() && CanConfirm())
            {
                RetireCommittedCandidate(keyboard, behavior, version, compositionIdentity);
            }

            return SoftKeyboardCandidateOperationResult.Success;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            Debug.WriteLine(
                $"[SoftKeyboard.Candidates.Tsf] COMPOSITION verification failed; version={version}, "
                + $"result=0x{exception.HResult:X8}."
            );

            keyboard.SetCandidateError(exception);
            return SoftKeyboardCandidateOperationResult.Failure(exception);
        }
    }

    /// <summary>
    /// Closes presentation after native completion and excludes the retained native element
    /// from discovery until a fresh composition or Begin establishes another candidate lifetime.
    /// </summary>
    private void RetireCommittedCandidate(
        SoftKeyboard keyboard,
        ITfCandidateListUIElementBehavior behavior,
        int version,
        object compositionIdentity)
    {
        if (!ReferenceEquals(compositionIdentity, keyboard.CompositionIdentity))
        {
            return;
        }

        var current = IsCandidateOperationCurrent(keyboard, behavior, version);
        if (!current && (_activeBehavior is not null || _elementManager is null))
        {
            return;
        }

        // EndUIElement can arrive inside Finalize before the manager removes the element.
        // Remember that completed identity too, while leaving any replacement generation alone.
        _retiredBehavior            = behavior;
        _retiredCompositionIdentity = compositionIdentity;

        if (current)
        {
            ClearActiveCandidate("CompositionCompleted");
        }
    }

    /// <summary>
    /// Detaches the current element and cancels its pending extension work before
    /// a later element takes ownership.
    /// </summary>
    private void ClearActiveCandidate(string reason = "ElementReleased")
    {
        var keyboard     = _activeKeyboard;
        var cancellation = _activeCandidateCancellation;

        if (_activeBehavior is not null)
        {
            Debug.WriteLine(
                $"[SoftKeyboard.Candidates.Tsf] Candidate RESET applied; reason={reason}, "
                + $"elementId={_activeElementId}, version={_activeCandidateVersion}."
            );
        }

        _activeCandidateVersion++;
        _activeCandidateCancellation = null;

        _candidatePollTimer.Stop();
        _candidatePollTimer.Interval = TimeSpan.FromMilliseconds(75);

        _activeBehavior   = null;
        _activeKeyboard   = null;
        _activeElementId  = null;
        _activePageStarts = [];
        _activePageNumber = 0;

        keyboard?.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);

        try
        {
            cancellation?.Cancel();
        }
        catch (AggregateException exception)
        {
            keyboard?.SetCandidateError(exception);
        }
        finally
        {
            cancellation?.Dispose();
        }
    }

    private void SetUnavailableState()
    {
        foreach (var keyboard in _keyboards.Select(reference => reference.Target as SoftKeyboard))
        {
            keyboard?.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.Unavailable);
        }
    }

    private void Disconnect()
    {
        RestoreNativeCandidate();
        ClearActiveCandidate();

        if (_source is not null && _cookie != 0)
        {
            try
            {
                _ = _source.UnadviseSink(_cookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // The Dispatcher or WPF's TSF host can already be shutting down.
            }
        }

        _cookie = 0;

        if (_sinkPointer != IntPtr.Zero)
        {
            _            = Marshal.Release(_sinkPointer);
            _sinkPointer = IntPtr.Zero;
        }

        _source                     = null;
        _elementManager             = null;
        _threadManager              = null;
        _retiredBehavior            = null;
        _retiredCompositionIdentity = null;

        // The thread manager is borrowed from WPF. Its activation and COM lifetime remain
        // owned by WPF's TextServicesHost.
    }

    /// <summary>
    /// Refreshes the embedded element and reconciles manager membership if that element ended.
    /// </summary>
    private void OnCandidatePollTimerTick(object? sender, EventArgs eventArgs)
    {
        // A TIP that began its UI before our sink was advised need not send UpdateUIElement.
        // Poll only while such an element is embedded; the timer stops as soon as the element
        // becomes invalid, the composition is committed, or the keyboard session is released.
        UpdateCandidateSnapshot();
        if (_activeBehavior is null)
        {
            TryCaptureExistingCandidate();
        }
    }

    /// <summary>
    /// Checks manager membership for an element discovered without a BeginUIElement id. A COM
    /// object can remain callable after EndUIElement, so document identity alone is insufficient.
    /// Each temporary acquisition is balanced even when it shares the active element's RCW.
    /// </summary>
    /// <returns>
    /// True if active, false if removed, or null when the inventory is unavailable.
    /// </returns>
    private bool? IsEnumeratedElementActive(object candidate)
    {
        object? enumeratorObject = null;
        try
        {
            if (_elementManager is null
                || _elementManager.EnumUIElements(out enumeratorObject) < 0
                || enumeratorObject is not IEnumTfUIElements enumerator)
            {
                return null;
            }

            while (true)
            {
                var result = enumerator.Next(1, out var element, out var fetched);
                try
                {
                    if (result < 0)
                    {
                        return null;
                    }

                    if (fetched != 1)
                    {
                        return false;
                    }

                    if (HaveSameComIdentity(candidate, element))
                    {
                        return true;
                    }
                }
                finally
                {
                    ReleaseProbe(element);
                }
            }
        }
        finally
        {
            ReleaseProbe(enumeratorObject);
        }
    }

    private void OnDispatcherShutdown(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    /// <summary>
    /// Releases native subscriptions and pending candidate work on the owning dispatcher.
    /// The dispatcher calls this at shutdown; repeated disposal is safe.
    /// </summary>
    public void Dispose()
    {
        _dispatcher.VerifyAccess();
        Disconnect();
        _dispatcher.ShutdownStarted -= OnDispatcherShutdown;
    }

    /// <summary>
    /// Compares TSF object identity across interface projections without retaining native pointers.
    /// </summary>
    internal static bool HaveSameComIdentity(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return false;
        }

        var leftIdentity  = IntPtr.Zero;
        var rightIdentity = IntPtr.Zero;

        try
        {
            leftIdentity  = Marshal.GetIUnknownForObject(left);
            rightIdentity = Marshal.GetIUnknownForObject(right);

            return leftIdentity == rightIdentity;
        }
        finally
        {
            if (leftIdentity != IntPtr.Zero)
            {
                _ = Marshal.Release(leftIdentity);
            }

            if (rightIdentity != IntPtr.Zero)
            {
                _ = Marshal.Release(rightIdentity);
            }
        }
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is ArgumentException
            or COMException
            or InvalidCastException
            or InvalidOperationException
            or MemberAccessException
            or PlatformNotSupportedException
            or TargetInvocationException
            or TypeLoadException;
    }
}
