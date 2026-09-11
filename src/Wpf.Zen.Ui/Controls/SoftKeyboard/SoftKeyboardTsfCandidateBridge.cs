// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable SA1134 // Compact COM declarations preserve native vtable order for auditability.

/// <summary>
/// Bridges WPF's existing TSF thread manager to the embedded candidate surface.
/// </summary>
/// <remarks>
/// WPF intentionally keeps TextServicesHost internal. Creating another thread manager would split
/// composition ownership from WPF's TextStore, so this bridge uses cached, failure-tolerant
/// reflection to read the manager already owned by the current Dispatcher. If a framework update
/// changes that private shape, candidates remain native and normal typing continues unchanged.
/// </remarks>
internal static class SoftKeyboardTsfCandidateBridge
{
    private static readonly ConditionalWeakTable<Dispatcher, CandidateSession> Sessions = new();

    internal static void Register(SoftKeyboard keyboard)
    {
        var session = Sessions.GetValue(
            keyboard.Dispatcher,
            static dispatcher => new CandidateSession(dispatcher)
        );

        session.Register(keyboard);
    }

    internal static void Unregister(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Unregister(keyboard);
        }
    }

    internal static void Refresh(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.Refresh(keyboard);
        }
    }

    internal static void ReleasePresentation(SoftKeyboard keyboard)
    {
        if (Sessions.TryGetValue(keyboard.Dispatcher, out var session))
        {
            session.ReleasePresentation(keyboard);
        }
    }

    internal static bool TryCommit(SoftKeyboard keyboard, uint index)
    {
        return Sessions.TryGetValue(keyboard.Dispatcher, out var session)
               && session.TryCommit(keyboard, index);
    }

    internal static bool TryMovePage(SoftKeyboard keyboard, int delta)
    {
        return Sessions.TryGetValue(keyboard.Dispatcher, out var session)
               && session.TryMovePage(keyboard, delta);
    }

    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class CandidateSession : ISoftKeyboardTsfUIElementSink, ISoftKeyboardTsfActiveLanguageProfileNotifySink
    {
        private const uint UiElementEnabledOnly = 0x00000004;
        private static readonly Guid UIElementSinkId = new("EA1EA136-19DF-11D7-A6D2-00065B84435C");
        private static readonly Guid ActiveLanguageProfileSinkId = new("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57");
        private static readonly Guid ThreadManagerClassId = new("529A9E6B-6587-4F23-AB9E-9C7D683E3C50");
        private readonly Dispatcher _dispatcher;
        private readonly List<WeakReference> _keyboards = [];
        private readonly DispatcherTimer _candidatePollTimer;
        private object? _threadManagerObject;
        private ITfThreadMgr? _threadManager;
        private ITfUIElementMgr? _elementManager;
        private ITfSource? _source;
        private ITfCandidateListUIElementBehavior? _activeBehavior;
        private SoftKeyboard? _activeKeyboard;
        private uint _cookie;
        private uint _profileCookie;
        private IntPtr _sinkPointer;
        private IntPtr _profileSinkPointer;
        private uint _activeElementId;
        private uint[] _activePageStarts = [];
        private uint _activePageNumber;
        private bool _reflectionUnavailable;
        private bool _ownsUiLessActivation;

        internal CandidateSession(Dispatcher dispatcher)
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
                _threadManager = manager as ITfThreadMgr;
                _elementManager = manager as ITfUIElementMgr;
                _source = manager as ITfSource;

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

        internal bool TryCommit(SoftKeyboard keyboard, uint index)
        {
            if (!ReferenceEquals(_activeKeyboard, keyboard) || _activeBehavior is null)
            {
                return false;
            }

            try
            {
                if (!ValidateActiveCandidate())
                {
                    return false;
                }

                // SetSelection can synchronously dispatch TSF callbacks and invalidate ownership.
                return _activeBehavior!.SetSelection(index) >= 0
                       && ValidateActiveCandidate()
                       && _activeBehavior!.FinalizeCandidate() >= 0;
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
                keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);
                return false;
            }
        }

        internal bool TryMovePage(SoftKeyboard keyboard, int delta)
        {
            if (!ReferenceEquals(_activeKeyboard, keyboard)
                || _activeBehavior is null
                || delta == 0
                || _activePageNumber == 0)
            {
                return false;
            }

            var targetPage = checked((int)_activePageNumber - 1 + delta);
            if (targetPage < 0 || targetPage >= _activePageStarts.Length)
            {
                return false;
            }

            try
            {
                if (!ValidateActiveCandidate())
                {
                    return false;
                }

                // TSF owns the page boundaries. Selecting the first item on an adjacent page asks
                // the TIP to move its current page without committing text; FinalizeCandidate is
                // deliberately reserved for an actual candidate-button invocation.
                if (_activeBehavior.SetSelection(_activePageStarts[targetPage]) < 0)
                {
                    return false;
                }

                UpdateCandidateSnapshot();
                return true;
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                RestoreNativeCandidate();
                ClearActiveCandidate();
                keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);
                return false;
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
                    || !MatchesFocusedDocument(candidate))
                {
                    show = 1;
                    keyboard.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.NativeFallback);
                    ReleaseComReference(candidate);
                    return 0;
                }

                ClearActiveCandidate();

                _activeKeyboard = keyboard;
                _activeBehavior = behavior;
                _activeElementId = elementId;

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
            if (candidate is null)
            {
                ReleaseComReference(element);
            }

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
                        if (element is not ITfCandidateListUIElement candidate
                            || !MatchesFocusedDocument(candidate))
                        {
                            continue;
                        }

                        if (element is not ITfCandidateListUIElementBehavior behavior)
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

                        _activeKeyboard = keyboard;
                        _activeBehavior = behavior;
                        _activeElementId = uint.MaxValue;

                        retained = true;

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
                            ReleaseComReference(element);
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
                ReleaseComReference(enumeratorObject);
            }
        }

        private bool MatchesFocusedDocument(ITfCandidateListUIElement candidate)
        {
            object? candidateDocument = null;
            object? focusedDocument = null;

            try
            {
                return _threadManager is not null
                       && candidate.GetDocumentMgr(out candidateDocument) >= 0
                       && _threadManager.GetFocus(out focusedDocument) >= 0
                       && HaveSameComIdentity(candidateDocument, focusedDocument);
            }
            finally
            {
                ReleaseComReference(candidateDocument);
                ReleaseComReference(focusedDocument);
            }
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

                _activeKeyboard.ReplaceCandidates(
                    candidates,
                    SoftKeyboardCandidatePresentationState.Embedded,
                    pageNumber,
                    pageCount,
                    selection
                );
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
            endIndex = candidateCount;
            pageNumber = candidateCount == 0 ? 0U : 1U;
            pageCount = candidateCount == 0 ? 0U : 1U;
            pageStarts = candidateCount == 0 ? [] : [0U];

            if (candidateCount == 0)
            {
                return;
            }

            // A page cannot begin more often than there are candidates. The cap protects the UI
            // thread from a malformed third-party TIP reporting an unbounded candidate count; the
            // fallback still exposes candidates even when page metadata cannot be trusted.
            var capacity = Math.Min(candidateCount, 4096U);
            var indices = Marshal.AllocCoTaskMem(checked((int)capacity * sizeof(uint)));
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

                var nativePageStarts = new uint[nativePageCount];
                uint previousStart = 0;

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
                    previousStart = pageStart;
                }

                var start = nativePageStarts[currentPage];

                var end = currentPage + 1U < nativePageCount
                    ? nativePageStarts[currentPage + 1U]
                    : candidateCount;

                firstIndex = start;
                endIndex = end;
                pageNumber = currentPage + 1U;
                pageCount = nativePageCount;
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
        /// Revalidates both managed target ownership and the TSF document before reading or committing.
        /// Focus and visibility can change without ending a TIP-owned composition.
        /// </summary>
        private bool ValidateActiveCandidate()
        {
            if (CanEmbedCandidates(_activeKeyboard)
                && _activeBehavior is ITfCandidateListUIElement candidate
                && MatchesFocusedDocument(candidate))
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

        private void ClearActiveCandidate()
        {
            var keyboard = _activeKeyboard;

            _candidatePollTimer.Stop();

            ReleaseComReference(_activeBehavior);

            _activeBehavior = null;
            _activeKeyboard = null;
            _activeElementId = 0;
            _activePageStarts = [];
            _activePageNumber = 0;

            keyboard?.ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
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

            _cookie = 0;
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

            _source = null;
            _elementManager = null;
            _threadManager = null;

            if (_ownsUiLessActivation
                && _threadManagerObject is ITfThreadMgr ownedActivation)
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
                var manager = managerType is null ? null : Activator.CreateInstance(managerType);

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
            Disconnect();
            _dispatcher.ShutdownStarted -= OnDispatcherShutdown;
        }

        private static bool HaveSameComIdentity(object? left, object? right)
        {
            if (left is null || right is null)
            {
                return false;
            }

            var leftIdentity = IntPtr.Zero;
            var rightIdentity = IntPtr.Zero;
            try
            {
                leftIdentity = Marshal.GetIUnknownForObject(left);
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

        private static void ReleaseComReference(object? value)
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                try
                {
                    _ = Marshal.ReleaseComObject(value);
                }
                catch (InvalidComObjectException)
                {
                    // TSF may invalidate a transient UI element before EndUIElement is dispatched.
                    // Cleanup is deliberately idempotent so that shutdown cannot crash the UI thread.
                }
            }
        }
    }

    private static class WpfTextServicesAccessor
    {
        private static readonly Lazy<Func<object?>?> Accessor = new(CreateAccessor);

        internal static object? TryGetThreadManager()
        {
            try
            {
                return Accessor.Value?.Invoke();
            }
            catch (Exception exception) when (
                exception is TargetInvocationException
                    or TypeLoadException
                    or MemberAccessException
                    or InvalidOperationException)
            {
                return null;
            }
        }

        private static Func<object?>? CreateAccessor()
        {
            // These members are cached once because candidate updates can be frequent. Merely reading
            // the thread-local store does not instantiate TextServicesHost; a null host means WPF has
            // no active TSF text store and the bridge must retain native candidate presentation.
            var editorType = typeof(TextBox).Assembly.GetType("System.Windows.Documents.TextEditor");

            var storeProperty = editorType?.GetProperty(
                "_ThreadLocalStore",
                BindingFlags.Static | BindingFlags.NonPublic
            );

            var storeType = typeof(TextBox).Assembly.GetType("System.Windows.Documents.TextEditorThreadLocalStore");

            var hostProperty = storeType?.GetProperty(
                "TextServicesHost",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            var hostType = typeof(TextBox).Assembly.GetType("System.Windows.Documents.TextServicesHost");

            var managerProperty = hostType?.GetProperty(
                "ThreadManager",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (storeProperty is null || hostProperty is null || managerProperty is null)
            {
                return null;
            }

            return () =>
            {
                var store = storeProperty.GetValue(null, null);
                var host = store is null ? null : hostProperty.GetValue(store, null);

                return host is null ? null : managerProperty.GetValue(host, null);
            };
        }
    }

    [ComImport]
    [Guid("4EA48A35-60AE-446F-8FD6-E6A8D82459F7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfSource
    {
        [PreserveSig]
        int AdviseSink(ref Guid interfaceId, IntPtr sink, out uint cookie);

        [PreserveSig]
        int UnadviseSink(uint cookie);
    }

    [ComImport]
    [Guid("EA1EA135-19DF-11D7-A6D2-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfUIElementMgr
    {
        [PreserveSig]
        int BeginUIElement([MarshalAs(UnmanagedType.IUnknown)] object element, ref int show, out uint elementId);

        [PreserveSig]
        int UpdateUIElement(uint elementId);

        [PreserveSig]
        int EndUIElement(uint elementId);

        [PreserveSig]
        int GetUIElement(uint elementId, [MarshalAs(UnmanagedType.IUnknown)] out object? element);

        [PreserveSig]
        int EnumUIElements([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);
    }

    [ComImport]
    [Guid("887AA91E-ACBA-4931-84DA-3C5208CF543F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumTfUIElements
    {
        [PreserveSig]
        int Clone(out IEnumTfUIElements enumerator);

        [PreserveSig]
        int Next(uint count, [MarshalAs(UnmanagedType.IUnknown)] out object? element, out uint fetched);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Skip(uint count);
    }

    [ComImport]
    [Guid("AA80E801-2021-11D2-93E0-0060B067B86E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfThreadMgr
    {
        [PreserveSig]
        int Activate(out uint clientId);

        [PreserveSig]
        int Deactivate();

        [PreserveSig]
        int CreateDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

        [PreserveSig]
        int EnumDocumentMgrs([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);

        [PreserveSig]
        int GetFocus([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);
    }

    // ITfThreadMgrEx inherits the full ITfThreadMgr vtable. Unused slots are intentionally retained
    // in SDK order so ActivateEx and GetActiveFlags cannot be dispatched to the wrong native method.
    [ComImport]
    [Guid("3E90ADE3-7594-4CB0-BB58-69628F5F458C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfThreadMgrEx
    {
        [PreserveSig]
        int Activate(out uint clientId);

        [PreserveSig]
        int Deactivate();

        [PreserveSig]
        int CreateDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

        [PreserveSig]
        int EnumDocumentMgrs([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);

        [PreserveSig]
        int GetFocus([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

        [PreserveSig]
        int SetFocus([MarshalAs(UnmanagedType.IUnknown)] object documentManager);

        [PreserveSig]
        int AssociateFocus(
            IntPtr window,
            [MarshalAs(UnmanagedType.IUnknown)] object? newDocumentManager,
            [MarshalAs(UnmanagedType.IUnknown)] out object? previousDocumentManager
        );

        [PreserveSig]
        int IsThreadFocus(out int isFocused);

        [PreserveSig]
        int GetFunctionProvider(in Guid classId, [MarshalAs(UnmanagedType.IUnknown)] out object? provider);

        [PreserveSig]
        int EnumFunctionProviders([MarshalAs(UnmanagedType.IUnknown)] out object? enumerator);

        [PreserveSig]
        int GetGlobalCompartment([MarshalAs(UnmanagedType.IUnknown)] out object? compartmentManager);

        [PreserveSig]
        int ActivateEx(out uint clientId, uint flags);

        [PreserveSig]
        int GetActiveFlags(out uint flags);
    }

    [ComImport]
    [Guid("EA1EA138-19DF-11D7-A6D2-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfCandidateListUIElement
    {
        [PreserveSig]
        int GetDescription([MarshalAs(UnmanagedType.BStr)] out string? description);

        [PreserveSig]
        int GetGuid(out Guid guid);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int IsShown(out int show);

        [PreserveSig]
        int GetUpdatedFlags(out uint flags);

        [PreserveSig]
        int GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetSelection(out uint index);

        [PreserveSig]
        int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string? text);

        [PreserveSig]
        int GetPageIndex(IntPtr indices, uint size, out uint pageCount);

        [PreserveSig]
        int SetPageIndex(IntPtr indices, uint pageCount);

        [PreserveSig]
        int GetCurrentPage(out uint page);
    }

    [ComImport]
    [Guid("85FAD185-58CE-497A-9460-355366B64B9A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ITfCandidateListUIElementBehavior
    {
        [PreserveSig]
        int GetDescription([MarshalAs(UnmanagedType.BStr)] out string? description);

        [PreserveSig]
        int GetGuid(out Guid guid);

        [PreserveSig]
        int Show(int show);

        [PreserveSig]
        int IsShown(out int show);

        [PreserveSig]
        int GetUpdatedFlags(out uint flags);

        [PreserveSig]
        int GetDocumentMgr([MarshalAs(UnmanagedType.IUnknown)] out object? documentManager);

        [PreserveSig]
        int GetCount(out uint count);

        [PreserveSig]
        int GetSelection(out uint index);

        [PreserveSig]
        int GetString(uint index, [MarshalAs(UnmanagedType.BStr)] out string? text);

        [PreserveSig]
        int GetPageIndex(IntPtr indices, uint size, out uint pageCount);

        [PreserveSig]
        int SetPageIndex(IntPtr indices, uint pageCount);

        [PreserveSig]
        int GetCurrentPage(out uint page);

        [PreserveSig]
        int SetSelection(uint index);

        [PreserveSig]
        int FinalizeCandidate();

        [PreserveSig]
        int Abort();
    }
}

/// <summary>
/// Defines the COM-callable TSF UI-element callback implemented by the soft-keyboard session.
/// </summary>
/// <remarks>
/// This interop contract is public only because the CLR requires a COM-callable interface type to
/// be externally visible. It is hidden from IntelliSense and is not intended for application code.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("EA1EA136-19DF-11D7-A6D2-00065B84435C")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfUIElementSink
{
    /// <summary>Receives notification that a TSF UI element is beginning.</summary>
    /// <param name="elementId">The TSF UI-element identifier.</param>
    /// <param name="show">Receives whether the input method may show its native surface.</param>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int BeginUIElement(uint elementId, ref int show);

    /// <summary>Receives notification that a TSF UI element changed.</summary>
    /// <param name="elementId">The TSF UI-element identifier.</param>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int UpdateUIElement(uint elementId);

    /// <summary>Receives notification that a TSF UI element ended.</summary>
    /// <param name="elementId">The TSF UI-element identifier.</param>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int EndUIElement(uint elementId);
}

/// <summary>
/// Defines the COM-callable active-language-profile callback implemented by the keyboard session.
/// </summary>
/// <remarks>
/// This interop contract is public only to satisfy CLR COM visibility requirements and is hidden
/// from IntelliSense. Applications should observe <see cref="SoftKeyboard.ActiveInputMethod"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[ComVisible(true)]
[Guid("B246CB75-A93E-4652-BF8C-B3FE0CFD7E57")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface ISoftKeyboardTsfActiveLanguageProfileNotifySink
{
    /// <summary>Receives notification that a TSF language profile changed activation state.</summary>
    /// <param name="classId">The text service class identifier.</param>
    /// <param name="profileId">The language profile identifier.</param>
    /// <param name="activated">A nonzero value when the profile became active.</param>
    /// <returns>An HRESULT value.</returns>
    [PreserveSig]
    int OnActivated(ref Guid classId, ref Guid profileId, int activated);
}

#pragma warning restore SA1134