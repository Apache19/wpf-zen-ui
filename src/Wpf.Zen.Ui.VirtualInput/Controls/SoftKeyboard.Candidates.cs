// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Synchronizes TSF candidate snapshots, paging commands, and candidate invocation.
/// </summary>
public partial class SoftKeyboard
{
    #region DP: CandidateMode

    /// <summary>
    /// Identifies the <see cref="CandidateMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidateModeProperty
        = DependencyProperty.Register(
            nameof(CandidateMode),
            typeof(SoftKeyboardCandidateMode),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardCandidateMode.Automatic,
                (d, _) => ((SoftKeyboard)d).RefreshSettings()
            ),
            static value => value is SoftKeyboardCandidateMode.Automatic
                or SoftKeyboardCandidateMode.Native
                or SoftKeyboardCandidateMode.Embedded
        );

    /// <summary>
    /// Gets or sets the presentation policy for the next candidate UI element.
    /// </summary>
    /// <remarks>
    /// Does not inherit or animate. An already embedded element retains its owner until it ends:
    /// some IMEs only create their native window when BeginUIElement returns show=true, so changing
    /// ownership midway cannot reliably restore the preview. Finish or cancel the current input
    /// normally for a policy change to take full effect. Automatic is a compatibility policy,
    /// not a claim that TSF offers a universal complete-integration capability flag.
    /// </remarks>
    public SoftKeyboardCandidateMode CandidateMode
    {
        get => (SoftKeyboardCandidateMode)GetValue(CandidateModeProperty);
        set => SetValue(CandidateModeProperty, value);
    }

    #endregion

    #region Read-Only DP: CandidateCommitError

    private static readonly DependencyPropertyKey CandidateCommitErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CandidateCommitError),
            typeof(string),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="CandidateCommitError"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidateCommitErrorProperty
        = CandidateCommitErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the last failed candidate request's reason, or null when no failure has been reported.
    /// </summary>
    /// <remarks>
    /// Does not inherit or affect layout. A new click clears the diagnostic; null is not proof of committed text.
    /// </remarks>
    public string? CandidateCommitError => (string?)GetValue(CandidateCommitErrorProperty);

    #endregion

    internal void SetCandidateCommitError(string? reason)
    {
        SetValue(CandidateCommitErrorPropertyKey, reason);
    }

    internal int CandidateInteractionVersion { get; private set; }

    internal bool SendCandidateNavigationKey(System.Windows.Input.Key key)
    {
        // Both paging and number selection must retain foreground, editor and input ownership.
        return TrySendVirtualKey(key, false);
    }

    internal void ReplaceCandidates(
        IEnumerable<SoftKeyboardCandidate>     candidates,
        SoftKeyboardCandidatePresentationState presentationState,
        uint  pageNumber = 0,
        uint  pageCount  = 0,
        uint? selectedCandidateIndex = null)
    {
        var snapshot       = candidates.ToArray();
        var sameCandidates = snapshot.Length == _candidates.Count;

        for (var index = 0; sameCandidates && index < snapshot.Length; index++)
        {
            var current     = _candidates[index];
            var replacement = snapshot[index];

            sameCandidates = current.Index == replacement.Index && current.Text == replacement.Text;
        }

        // Some TIPs do not raise UpdateUIElement reliably, so the bridge polls while composing.
        // Keeping equal immutable items in place is essential: clearing the collection every tick
        // destroys the Button below the pointer between MouseDown and MouseUp, which both flashes
        // its hover state and prevents Click from being raised.
        if (sameCandidates
            && CandidatePresentationState == presentationState
            && CandidatePageNumber == pageNumber
            && CandidatePageCount == pageCount
            && SelectedCandidateIndex == selectedCandidateIndex)
        {
            return;
        }

        if (!sameCandidates)
        {
            _candidates.Clear();
            foreach (var candidate in snapshot)
            {
                _candidates.Add(candidate);
            }
        }

        SetValue(SelectedCandidateIndexPropertyKey, selectedCandidateIndex);
        SetValue(HasCandidatesPropertyKey, _candidates.Count > 0);
        SetValue(CandidatePageNumberPropertyKey, pageNumber);
        SetValue(CandidatePageCountPropertyKey, pageCount);
        SetValue(CanGoToPreviousCandidatePagePropertyKey, pageNumber > 1);
        SetValue(CanGoToNextCandidatePagePropertyKey, pageNumber > 0 && pageNumber < pageCount);
        SetValue(CandidatePresentationStatePropertyKey, presentationState);
    }

    private void QueueCandidateBridgeRefresh()
    {
        // WPF initializes TextStore at DispatcherPriority.Background after the editor's first text
        // view layout. ContextIdle is intentionally lower, so the bridge sees the ThreadManager even
        // when focus and keyboard loading happen during the same layout pass. The same deferred path
        // runs after SendInput so a normal-mode TIP candidate element can be discovered and embedded.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(
                () =>
                {
                    if (IsLoaded && IsVisible && IsOpen && ActiveTarget is not null)
                    {
                        _platform.Candidates.Refresh(this);
                    }
                }
            )
        );
    }

    private void OnCandidateInvoked(object? sender, SoftKeyboardCandidateInvokedEventArgs args)
    {
        // Ownership is the current template part, independent of how its candidate data is bound.
        if (args.OriginalSource is SoftKeyboardCandidateList list
            && ReferenceEquals(list, GetTemplateChild(CandidatesPartName)))
        {
            CommitCandidate(args.Candidate);
            args.Handled = true;
        }
    }

    private void CommitCandidate(SoftKeyboardCandidate candidate)
    {
        // Reject containers retained from an obsolete page before sending a native index.
        if (_session is not null && CanPresentCandidates && _candidates.Contains(candidate)
            && PrepareTargetForInput() && _candidates.Contains(candidate))
        {
            SetCandidateCommitError(null);

            // A successful HRESULT or queued key is not a composition-end notification. Keep the
            // snapshot until TSF actually changes it, including multi-segment candidate conversion.
            _ = _platform.Candidates.TryCommit(this, candidate.Index);
        }
    }
}
