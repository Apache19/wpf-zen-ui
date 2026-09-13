// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Owns one keyboard's candidate-source lifecycle and cancellable operations.
/// </summary>
public interface ISoftKeyboardCandidateSession : IDisposable
{
    /// <summary>
    /// Refreshes candidate discovery or the current source snapshot.
    /// </summary>
    /// <param name="cancellationToken">Cancels work that no longer belongs to the input session.</param>
    /// <returns>A task that completes after the refresh request has been processed.</returns>
    Task RefreshAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Commits a candidate from the current snapshot.
    /// </summary>
    /// <param name="candidate">The candidate selected by the user.</param>
    /// <param name="cancellationToken">Cancels work that no longer belongs to the input session.</param>
    /// <returns>The operation result.</returns>
    Task<SoftKeyboardCandidateOperationResult> CommitAsync(
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Moves the current candidate page by a relative delta.
    /// </summary>
    /// <param name="delta">A negative value for a previous page or a positive value for a next page.</param>
    /// <param name="cancellationToken">Cancels work that no longer belongs to the input session.</param>
    /// <returns>The operation result.</returns>
    Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
        int delta,
        CancellationToken cancellationToken);

    /// <summary>
    /// Stops embedded presentation synchronously and restores source-owned UI when possible.
    /// </summary>
    void ReleasePresentation();
}
