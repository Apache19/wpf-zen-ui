// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Candidates;

/// <summary>
/// Applies the Windows text-service candidate presentation policy used by the default extensions.
/// </summary>
internal sealed class WindowsCandidatePresentationBehavior : ISoftKeyboardTsfCandidateBehavior
{
    // Embed only services whose page metadata and selection lifecycle fit the TSF bridge.
    private static readonly HashSet<Guid> EmbeddedInputMethodIds =
    [
        KnownTextServices.MicrosoftJapanese,  // Microsoft Japanese IME
        KnownTextServices.MicrosoftCangjie,   // Microsoft Cangjie
        KnownTextServices.MicrosoftPinyin,    // Microsoft Pinyin and Wubi
        KnownTextServices.MicrosoftKorean,    // Microsoft Korean IME
        KnownTextServices.MicrosoftOldHangul, // Microsoft Old Hangul IME
        KnownTextServices.MicrosoftBopomofo,  // Microsoft Bopomofo
        KnownTextServices.WindowsTable,       // Windows table text service
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
    /// Determines whether a TSF service is explicitly allowed to use embedded candidates.
    /// </summary>
    /// <param name="classId">The active TSF text service class identifier.</param>
    /// <returns>
    /// True when the soft keyboard should present the service's candidates; otherwise false.
    /// </returns>
    internal static bool IsEmbeddedInputMethod(Guid? classId)
    {
        return classId is { } value && EmbeddedInputMethodIds.Contains(value);
    }
}
