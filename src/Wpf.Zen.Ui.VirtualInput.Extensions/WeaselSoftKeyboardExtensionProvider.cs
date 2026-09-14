// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

#pragma warning disable SA1402 // The public provider and behavior form one small adapter.

/// <summary>
/// Contributes safe presentation, selection, paging, and acknowledgement policy for Weasel TSF.
/// </summary>
public sealed class WeaselSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Gets the shared Weasel extension provider.
    /// </summary>
    public static WeaselSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return [new SoftKeyboardExtension(candidateBehavior: WeaselCandidateBehavior.Default)];
    }
}

/// <summary>
/// Adapts the Weasel candidate element without exposing or retaining its COM implementation.
/// </summary>
public sealed class WeaselCandidateBehavior : ISoftKeyboardTsfCandidateBehavior
{
    /// <summary>
    /// Gets the candidate-element identifier used by Weasel TSF.
    /// </summary>
    public static Guid CandidateElementId { get; } = new("36C3C795-7159-45AA-AB12-30229A51DBD3");

    /// <summary>
    /// Gets the shared stateless behavior.
    /// </summary>
    public static WeaselCandidateBehavior Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardTsfCandidatePresentation GetPresentation(SoftKeyboardTsfCandidateContext context)
    {
        if (context.ElementType != CandidateElementId)
        {
            return SoftKeyboardTsfCandidatePresentation.Default;
        }

        return SoftKeyboardTsfCandidatePresentation.Native;
    }

    /// <inheritdoc />
    public async Task<SoftKeyboardCandidateOperationResult> CommitAsync(
        SoftKeyboardTsfCandidateContext context,
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (context.ElementType != CandidateElementId)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }

        var position = context.Candidates.IndexOf(candidate);
        if (!TryGetCandidateNumberKey(position, out var key))
        {
            return SoftKeyboardCandidateOperationResult.Failure(
                new InvalidOperationException("Weasel exposes no number key for this candidate.")
            );
        }

        if (!context.TrySendKey(key))
        {
            return SoftKeyboardCandidateOperationResult.Failure(
                new InvalidOperationException("Windows rejected the Weasel candidate number key.")
            );
        }

        return await context
            .WaitForSnapshotChangeAsync(TimeSpan.FromMilliseconds(750), cancellationToken)
            .ConfigureAwait(false)
            ? SoftKeyboardCandidateOperationResult.Success
            : SoftKeyboardCandidateOperationResult.Failure(
                new TimeoutException("Weasel did not acknowledge the candidate number key within 750 ms.")
            );
    }

    /// <inheritdoc />
    public async Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
        SoftKeyboardTsfCandidateContext context,
        int delta,
        CancellationToken cancellationToken)
    {
        if (context.ElementType != CandidateElementId)
        {
            return SoftKeyboardCandidateOperationResult.NotHandled;
        }

        if (!context.TrySendKey(delta > 0 ? Key.PageDown : Key.PageUp))
        {
            return SoftKeyboardCandidateOperationResult.Failure(
                new InvalidOperationException("Windows rejected the Weasel candidate paging key.")
            );
        }

        return await context
            .WaitForSnapshotChangeAsync(TimeSpan.FromMilliseconds(750), cancellationToken)
            .ConfigureAwait(false)
            ? SoftKeyboardCandidateOperationResult.Success
            : SoftKeyboardCandidateOperationResult.Failure(
                new TimeoutException("Weasel did not acknowledge the paging key within 750 ms.")
            );
    }

    internal static bool TryGetCandidateNumberKey(int position, out Key key)
    {
        if (position is >= 0 and < 9)
        {
            key = Key.D1 + position;
            return true;
        }

        key = Key.None;
        return false;
    }
}

internal static class CandidateListExtensions
{
    internal static int IndexOf(
        this IReadOnlyList<SoftKeyboardCandidate> candidates,
        SoftKeyboardCandidate candidate)
    {
        for (var index = 0; index < candidates.Count; index++)
        {
            if (ReferenceEquals(candidates[index], candidate)
                || (candidates[index].Index == candidate.Index
                    && candidates[index].Text == candidate.Text))
            {
                return index;
            }
        }

        return -1;
    }
}
#pragma warning restore SA1402
