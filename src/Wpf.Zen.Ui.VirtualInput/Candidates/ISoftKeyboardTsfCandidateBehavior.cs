// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Overrides selected behaviors of the core TSF candidate source without exposing COM objects.
/// </summary>
public interface ISoftKeyboardTsfCandidateBehavior
{
    /// <summary>
    /// Chooses whether a matching TSF element should use embedded or native presentation.
    /// </summary>
    /// <param name="context">Safe metadata for the active TSF candidate element.</param>
    /// <returns>A decision, or Default to delegate to the next behavior and core policy.</returns>
    SoftKeyboardTsfCandidatePresentation GetPresentation(SoftKeyboardTsfCandidateContext context);

    /// <summary>
    /// Attempts to commit a candidate using input-method-specific behavior.
    /// </summary>
    /// <param name="context">Safe metadata and input operations for the active candidate element.</param>
    /// <param name="candidate">The currently published candidate to commit.</param>
    /// <param name="cancellationToken">
    /// Cancels the caller's operation. Element end expires context input but permits pending commit acknowledgement.
    /// </param>
    /// <returns>An operation result; NotHandled delegates to the next behavior and standard TSF.</returns>
    Task<SoftKeyboardCandidateOperationResult> CommitAsync(
        SoftKeyboardTsfCandidateContext context,
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to move the candidate page using input-method-specific behavior.
    /// </summary>
    /// <param name="context">Safe metadata and input operations for the active candidate element.</param>
    /// <param name="delta">A negative value for the previous page or a positive value for the next page.</param>
    /// <param name="cancellationToken">
    /// Cancels the caller's operation. Element end expires context input but permits pending commit acknowledgement.
    /// </param>
    /// <returns>An operation result; NotHandled delegates to the next behavior and standard TSF.</returns>
    Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
        SoftKeyboardTsfCandidateContext context,
        int delta,
        CancellationToken cancellationToken);
}
