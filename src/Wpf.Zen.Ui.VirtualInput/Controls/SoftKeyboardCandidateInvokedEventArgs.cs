// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Describes a candidate invocation without executing input-method operations.
/// </summary>
public sealed class SoftKeyboardCandidateInvokedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardCandidateInvokedEventArgs"/> class.
    /// </summary>
    /// <param name="source">
    /// The list raising the request.
    /// </param>
    /// <param name="candidate">
    /// The candidate to submit; hosts must reject obsolete snapshots.
    /// </param>
    public SoftKeyboardCandidateInvokedEventArgs(
        SoftKeyboardCandidateList source, SoftKeyboardCandidate candidate
    )
        : base(SoftKeyboardCandidateList.CandidateInvokedEvent, source)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
    }

    /// <summary>
    /// Gets the candidate requested by the user, including its backend index.
    /// </summary>
    public SoftKeyboardCandidate Candidate { get; }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        ((EventHandler<SoftKeyboardCandidateInvokedEventArgs>)genericHandler)(genericTarget, this);
    }
}
