// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Carries one immutable candidate page and its presentation metadata.
/// </summary>
public sealed class SoftKeyboardCandidateSnapshot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardCandidateSnapshot"/> class.
    /// </summary>
    /// <param name="candidates">Candidates in visible order.</param>
    /// <param name="presentationState">How the candidate UI is currently presented.</param>
    /// <param name="pageNumber">The one-based current page, or zero when unavailable.</param>
    /// <param name="pageCount">The total page count, or zero when unavailable.</param>
    /// <param name="selectedCandidateIndex">The source-owned selected index, or null.</param>
    public SoftKeyboardCandidateSnapshot(
        IEnumerable<SoftKeyboardCandidate> candidates,
        SoftKeyboardCandidatePresentationState presentationState,
        uint pageNumber = 0,
        uint pageCount = 0,
        uint? selectedCandidateIndex = null)
    {
        if (!Enum.IsDefined(typeof(SoftKeyboardCandidatePresentationState), presentationState))
        {
            throw new ArgumentOutOfRangeException(nameof(presentationState));
        }

        var items = candidates?.ToArray() ?? throw new ArgumentNullException(nameof(candidates));

        if (items.Any(static candidate => candidate is null))
        {
            throw new ArgumentException("A candidate cannot be null.", nameof(candidates));
        }

        if (items.Select(static candidate => candidate.Index).Distinct().Count() != items.Length)
        {
            throw new ArgumentException("Candidate indices must be unique within a snapshot.", nameof(candidates));
        }

        Candidates             = new ReadOnlyCollection<SoftKeyboardCandidate>(items);
        PresentationState      = presentationState;
        PageNumber             = pageNumber;
        PageCount              = pageCount;
        SelectedCandidateIndex = selectedCandidateIndex;
    }

    /// <summary>
    /// Gets an empty inactive candidate snapshot.
    /// </summary>
    public static SoftKeyboardCandidateSnapshot Empty { get; } = new(
        [],
        SoftKeyboardCandidatePresentationState.None
    );

    /// <summary>
    /// Gets the candidates in visible order.
    /// </summary>
    public IReadOnlyList<SoftKeyboardCandidate> Candidates { get; }

    /// <summary>
    /// Gets the current presentation state.
    /// </summary>
    public SoftKeyboardCandidatePresentationState PresentationState { get; }

    /// <summary>
    /// Gets the one-based current page, or zero when unavailable.
    /// </summary>
    public uint PageNumber { get; }

    /// <summary>
    /// Gets the total page count, or zero when unavailable.
    /// </summary>
    public uint PageCount { get; }

    /// <summary>
    /// Gets the source-owned selected candidate index, or null.
    /// </summary>
    public uint? SelectedCandidateIndex { get; }
}