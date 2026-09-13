// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Owns dispatcher-wide TSF candidate presentation and scopes asynchronous operations to one UI element.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class TsfCandidateSession : ISoftKeyboardTsfUIElementSink,
    ISoftKeyboardTsfActiveLanguageProfileNotifySink, IDisposable
{
    private const uint UiElementEnabledOnly = 0x00000004;

    private static readonly Guid UIElementSinkId             = new("EA1EA136-19DF-11D7-A6D2-00065B84435C");
    private static readonly Guid ActiveLanguageProfileSinkId = new("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57");
    private static readonly Guid ThreadManagerClassId        = new("529A9E6B-6587-4F23-AB9E-9C7D683E3C50");

    private readonly Dispatcher          _dispatcher;
    private readonly List<WeakReference> _keyboards = [];
    private readonly DispatcherTimer     _candidatePollTimer;

    private object?                            _threadManagerObject;
    private ITfThreadMgr?                      _threadManager;
    private ITfUIElementMgr?                   _elementManager;
    private ITfSource?                         _source;
    private ITfCandidateListUIElementBehavior? _activeBehavior;
    private SoftKeyboard?                      _activeKeyboard;
    private uint                               _cookie;
    private uint                               _profileCookie;
    private IntPtr                             _sinkPointer;
    private IntPtr                             _profileSinkPointer;
    private uint                               _activeElementId;
    private uint[]                             _activePageStarts = [];
    private uint                               _activePageNumber;
    private int                                _activeCandidateVersion;
    private CancellationTokenSource?           _activeCandidateCancellation;
    private bool                               _reflectionUnavailable;
    private bool                               _ownsUiLessActivation;

    internal TsfCandidateSession(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        _candidatePollTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(75),
            DispatcherPriority.Input,
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

            if (manager is null)
            {
                manager = TryCreateUiLessThreadManager();
                if (manager is null)
                {
                    return;
                }
            }
            else
            {
                TryActivateUiLessMode(manager);
            }

            _threadManagerObject = manager;
            _threadManager       = manager as ITfThreadMgr;
            _elementManager      = manager as ITfUIElementMgr;
            _source              = manager as ITfSource;

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

            // Profile changes within the same language need not raise WPF's InputLanguageChanged
            // event. A second sink keeps the embedded picker and key labels synchronized.
            var profileSinkId = ActiveLanguageProfileSinkId;

            _profileSinkPointer = Marshal.GetComInterfaceForObject(
                this,
                typeof(ISoftKeyboardTsfActiveLanguageProfileNotifySink)
            );

            if (_source.AdviseSink(ref profileSinkId, _profileSinkPointer, out var profileCookie) < 0)
            {
                _ = Marshal.Release(_profileSinkPointer);
                _profileSinkPointer = IntPtr.Zero;
            }
            else
            {
                _profileCookie = profileCookie;
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            _reflectionUnavailable = true;

            SetUnavailableState();
            Disconnect();
        }
    }

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

        var version = _activeCandidateVersion;

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
                        return result;
                    }
                }
            }

            operationToken.ThrowIfCancellationRequested();

            // SetSelection can synchronously end this UI element and begin another in the same
            // document. Only finalize the captured element if its generation survived selection.
            var succeeded = ValidateCandidateOperation(keyboard, nativeBehavior, version)
                && nativeBehavior.SetSelection(candidate.Index) >= 0
                && ValidateCandidateOperation(keyboard, nativeBehavior, version)
                && nativeBehavior.FinalizeCandidate() >= 0;

            return succeeded
                ? SoftKeyboardCandidateOperationResult.Success
                : SoftKeyboardCandidateOperationResult.NotHandled;
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

    int ISoftKeyboardTsfUIElementSink.BeginUIElement(uint elementId, ref int show)
    {
        try
        {
            var keyboard = FindActiveKeyboard();
            if (keyboard is null || !TryGetCandidate(elementId, out var candidate))
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

            ClearActiveCandidate();

            _activeKeyboard  = keyboard;
            _activeBehavior  = behavior;
            _activeElementId = elementId;

#pragma warning disable IDISP003 // ClearActiveCandidate disposed the previous element cancellation source.
            _activeCandidateCancellation = new CancellationTokenSource();
#pragma warning restore IDISP003

            show = 0;

            _candidatePollTimer.Start();

            UpdateCandidateSnapshot();
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            show = 1;
            ClearActiveCandidate();
        }

        return 0;
    }

    int ISoftKeyboardTsfUIElementSink.UpdateUIElement(uint elementId)
    {
        if (elementId == _activeElementId)
        {
            UpdateCandidateSnapshot();
        }

        return 0;
    }

    int ISoftKeyboardTsfUIElementSink.EndUIElement(uint elementId)
    {
        if (elementId == _activeElementId)
        {
            ClearActiveCandidate();
        }

        return 0;
    }

    int ISoftKeyboardTsfActiveLanguageProfileNotifySink.OnActivated(ref Guid classId, ref Guid profileId, int activated)
    {
        foreach (var keyboard in _keyboards.Select(reference => reference.Target as SoftKeyboard))
        {
            if (keyboard is { IsLoaded: true })
            {
                keyboard.OnSessionInputLanguageChanged();
            }
        }

        return 0;
    }

    private bool TryGetCandidate(uint elementId, out ITfCandidateListUIElement? candidate)
    {
        candidate = null;

        if (_elementManager is null
            || _elementManager.GetUIElement(elementId, out var element) < 0)
        {
            return false;
        }

        candidate = element as ITfCandidateListUIElement;
        return candidate is not null;
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

        try
        {
            if (_elementManager.EnumUIElements(out var enumeratorObject) < 0
                || enumeratorObject is not IEnumTfUIElements enumerator)
            {
                return;
            }

            while (enumerator.Next(1, out var element, out var fetched) >= 0 && fetched == 1)
            {
                if (element is not ITfCandidateListUIElement candidate
                    || !MatchesFocusedDocument(candidate))
                {
                    continue;
                }

                if (element is not ITfCandidateListUIElementBehavior behavior
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
                _activeElementId = uint.MaxValue;

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
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // Enumeration is an enhancement for a TIP running in normal WPF TSF mode. Failure
            // leaves its native candidate surface untouched and never affects text composition.
        }
    }

    /// <summary>
    /// Checks whether the candidate belongs to WPF's currently focused TSF document.
    /// </summary>
    private bool MatchesFocusedDocument(ITfCandidateListUIElement candidate)
    {
        return _threadManager is not null
            && candidate.GetDocumentMgr(out var candidateDocument) >= 0
            && _threadManager.GetFocus(out var focusedDocument) >= 0
            && HaveSameComIdentity(candidateDocument, focusedDocument);
    }

    private void UpdateCandidateSnapshot()
    {
        if (_activeBehavior is null || _activeKeyboard is null)
        {
            return;
        }

        try
        {
            if (!ValidateActiveCandidate())
            {
                return;
            }

            if (_activeBehavior!.GetCount(out var count) < 0
                || _activeBehavior.GetSelection(out var selection) < 0)
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
                return;
            }

            GetCandidatePageRange(
                _activeBehavior,
                count,
                out var firstIndex,
                out var endIndex,
                out var pageNumber,
                out var pageCount,
                out var pageStarts
            );

            _activePageNumber = pageNumber;
            _activePageStarts = pageStarts;

            var visibleEnd = firstIndex + Math.Min(endIndex - firstIndex, 256U);
            var candidates = new List<SoftKeyboardCandidate>(checked((int)(visibleEnd - firstIndex)));

            for (var index = firstIndex; index < visibleEnd; index++)
            {
                if (_activeBehavior.GetString(index, out var text) >= 0 && text is not null)
                {
                    candidates.Add(new SoftKeyboardCandidate(index, text));
                }
            }

            var host = _activeKeyboard.ActiveCandidateHost;
            if (host is not null)
            {
                host.Publish(
                    new SoftKeyboardCandidateSnapshot(
                        candidates,
                        SoftKeyboardCandidatePresentationState.Embedded,
                        pageNumber,
                        pageCount,
                        selection
                    )
                );
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            RestoreNativeCandidate();
            ClearActiveCandidate();
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

    private static bool ShouldEmbedCandidate(SoftKeyboard keyboard, ITfCandidateListUIElementBehavior behavior)
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

        return ShouldEmbedCandidate(keyboard.CandidateMode);
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

    internal static bool ShouldEmbedCandidate(SoftKeyboardCandidateMode mode)
    {
        return mode != SoftKeyboardCandidateMode.Native;
    }

    /// <summary>
    /// Revalidates both managed target ownership and the TSF document before reading or committing.
    /// Focus and visibility can change without ending a TIP-owned composition.
    /// </summary>
    private bool ValidateActiveCandidate()
    {
        var version = _activeCandidateVersion;
        var valid = CanEmbedCandidates(_activeKeyboard)
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
    /// COM document queries may reenter the sink, so identity is checked on both sides of validation.
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
    /// Detaches the current element and cancels its pending extension work before
    /// a later element takes ownership.
    /// </summary>
    private void ClearActiveCandidate()
    {
        var keyboard     = _activeKeyboard;
        var cancellation = _activeCandidateCancellation;

        _activeCandidateVersion++;
        _activeCandidateCancellation = null;

        _candidatePollTimer.Stop();
        _candidatePollTimer.Interval = TimeSpan.FromMilliseconds(75);

        _activeBehavior   = null;
        _activeKeyboard   = null;
        _activeElementId  = 0;
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

        if (_source is not null && _profileCookie != 0)
        {
            try
            {
                _ = _source.UnadviseSink(_profileCookie);
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // The profile sink has the same Dispatcher-owned lifetime as the candidate sink.
            }
        }

        _cookie        = 0;
        _profileCookie = 0;

        if (_sinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_sinkPointer);
            _sinkPointer = IntPtr.Zero;
        }

        if (_profileSinkPointer != IntPtr.Zero)
        {
            _ = Marshal.Release(_profileSinkPointer);
            _profileSinkPointer = IntPtr.Zero;
        }

        _source         = null;
        _elementManager = null;
        _threadManager  = null;

        if (_ownsUiLessActivation && _threadManagerObject is ITfThreadMgr ownedActivation)
        {
            try
            {
                _ = ownedActivation.Deactivate();
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                // Dispatcher shutdown can already be tearing down TSF. The matching call is
                // still attempted because ActivateEx explicitly requires balanced activation.
            }
        }

        _ownsUiLessActivation = false;

        // TSF returns one COM identity for the calling thread. WPF can therefore hold the same
        // RCW after its TextStore initializes. Never call ReleaseComObject on that shared RCW;
        // dropping our managed reference lets the runtime release it only after WPF is done.
        _threadManagerObject = null;
    }

    private object? TryCreateUiLessThreadManager()
    {
        try
        {
            var managerType = Type.GetTypeFromCLSID(ThreadManagerClassId, throwOnError: false);
            var manager     = managerType is null ? null : Activator.CreateInstance(managerType);

            if (manager is null)
            {
                return null;
            }

            if (!TryActivateUiLessMode(manager))
            {
                return null;
            }

            return manager;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return null;
        }
    }

    private bool TryActivateUiLessMode(object manager)
    {
        if (_ownsUiLessActivation)
        {
            return true;
        }

        if (manager is not ITfThreadMgrEx managerEx
            || managerEx.ActivateEx(out _, UiElementEnabledOnly) < 0)
        {
            return false;
        }

        // WPF later calls ITfThreadMgr.Activate for its own client id. TSF explicitly permits
        // multiple activation calls on a thread as long as every caller balances its own call.
        // Taking the first activation here lets UIElement-aware TIPs publish controllable UI
        // without creating a second WPF TextStore or replacing WPF's document manager.
        _ownsUiLessActivation = true;
        return true;
    }

    private void OnCandidatePollTimerTick(object? sender, EventArgs eventArgs)
    {
        // A TIP that began its UI before our sink was advised need not send UpdateUIElement.
        // Poll only while such an element is embedded; the timer stops as soon as the element
        // becomes invalid, the composition is committed, or the keyboard session is released.
        UpdateCandidateSnapshot();
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

    private static bool HaveSameComIdentity(object? left, object? right)
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
