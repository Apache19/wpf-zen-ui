// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Synchronizes TSF candidate snapshots, paging commands, and candidate invocation.
/// </summary>
public partial class SoftKeyboard
{
    internal int CandidateInteractionVersion { get; private set; }

    internal bool ReplaceCandidates(
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
            return false;
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

        CommandManager.InvalidateRequerySuggested();

        CandidateSnapshotVersion++;

        Debug.WriteLine(
            $"[SoftKeyboard.Candidates.Presentation] SNAPSHOT applied; version={CandidateSnapshotVersion}, "
            + $"state={presentationState}, count={snapshot.Length}, page={pageNumber}, "
            + $"pageCount={pageCount}, selection={selectedCandidateIndex}."
        );

        var previous = _candidateSnapshotChanged;
        _candidateSnapshotChanged = CreateCandidateSnapshotSignal();
        previous.TrySetResult(CandidateSnapshotVersion);

        return true;
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
                        RequestCandidateRefresh();
                    }
                }
            )
        );
    }
}
