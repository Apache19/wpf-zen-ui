// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Applies the Windows inbox-IME candidate presentation policy used by the default extensions.
/// </summary>
internal sealed class WindowsCandidatePresentationBehavior : ISoftKeyboardTsfCandidateBehavior
{
    private static readonly HashSet<Guid> EmbeddedInputMethodIds =
    [
        new("03B5835F-F03C-411B-9CE2-AA23E1171E36"), // Microsoft Japanese IME
        new("531FDEBF-9B4C-4A43-A2AA-960E8FCDC732"), // Microsoft Cangjie
        new("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E"), // Microsoft Pinyin and Wubi
        new("86598FB9-66A2-463E-B9C2-AEB906D477AD"), // WeChat Input Method (WeType)
        new("A028AE76-01B1-46C2-99C4-ACD9858AE02F"), // Microsoft Korean IME
        new("A1E2B86B-924A-4D43-80F6-8A820DF7190F"), // Microsoft Old Hangul IME
        new("B115690A-EA02-48D5-A231-E3578D2FDF80"), // Microsoft Bopomofo
        new("E429B25A-E5D3-4D1F-9BE3-0C608477E3A1"), // Windows table text service
    ];

    private WindowsCandidatePresentationBehavior()
    {
    }

    /// <summary>
    /// Gets the shared Windows candidate policy.
    /// </summary>
    internal static WindowsCandidatePresentationBehavior Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardTsfCandidatePresentation GetPresentation(
        SoftKeyboardTsfCandidateContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return IsEmbeddedInputMethod(context.InputMethod?.ClassId)
            ? SoftKeyboardTsfCandidatePresentation.Embedded
            : SoftKeyboardTsfCandidatePresentation.Native;
    }

    /// <inheritdoc />
    public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
        SoftKeyboardTsfCandidateContext context,
        SoftKeyboardCandidate candidate,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
    }

    /// <inheritdoc />
    public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
        SoftKeyboardTsfCandidateContext context,
        int delta,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(SoftKeyboardCandidateOperationResult.NotHandled);
    }

    /// <summary>
    /// Determines whether the candidate UI of a TSF service should be embedded in the soft keyboard.
    /// </summary>
    /// <param name="classId">The active TSF text service class identifier.</param>
    /// <returns>True when the soft keyboard should present the service's candidates; otherwise false.</returns>
    internal static bool IsEmbeddedInputMethod(Guid? classId)
    {
        return classId is { } value && EmbeddedInputMethodIds.Contains(value);
    }
}
