// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Exposes safe metadata and guarded operations for one active TSF candidate element.
/// </summary>
public sealed class SoftKeyboardTsfCandidateContext
{
    private readonly SoftKeyboardCandidateHost _host;
    private readonly CancellationToken _cancellationToken;
    private readonly CancellationToken _callerCancellationToken;

    internal SoftKeyboardTsfCandidateContext(
        SoftKeyboardCandidateHost host,
        Guid elementType,
        IReadOnlyList<SoftKeyboardCandidate> candidates,
        uint pageNumber,
        uint pageCount,
        uint? selectedCandidateIndex,
        CancellationToken cancellationToken = default,
        CancellationToken callerCancellationToken = default)
    {
        _host                    = host;
        _cancellationToken       = cancellationToken;
        _callerCancellationToken = callerCancellationToken;
        ElementType              = elementType;
        Candidates               = candidates;
        PageNumber               = pageNumber;
        PageCount                = pageCount;
        SelectedCandidateIndex   = selectedCandidateIndex;
        SnapshotVersion          = host.SnapshotVersion;
    }

    /// <summary>
    /// Gets the TSF UI element type identifier.
    /// </summary>
    public Guid ElementType { get; }

    /// <summary>
    /// Gets the active input-method profile, or null.
    /// </summary>
    public SoftKeyboardInputMethod? InputMethod
    {
        get => _host.InputMethod;
    }

    /// <summary>
    /// Gets the keyboard's candidate-presentation policy.
    /// </summary>
    public SoftKeyboardCandidateMode CandidateMode
    {
        get => _host.CandidateMode;
    }

    /// <summary>
    /// Gets the currently displayed candidates.
    /// </summary>
    public IReadOnlyList<SoftKeyboardCandidate> Candidates { get; }

    /// <summary>
    /// Gets the one-based current page, or zero when unavailable.
    /// </summary>
    public uint PageNumber { get; }

    /// <summary>
    /// Gets the total page count, or zero when unavailable.
    /// </summary>
    public uint PageCount { get; }

    /// <summary>
    /// Gets the current source-owned candidate index, or null.
    /// </summary>
    public uint? SelectedCandidateIndex { get; }

    /// <summary>
    /// Gets the candidate snapshot version captured for this operation.
    /// </summary>
    public int SnapshotVersion { get; }

    /// <summary>
    /// Sends one guarded virtual-key chord while the original candidate operation remains active.
    /// </summary>
    /// <param name="key">The main virtual key.</param>
    /// <param name="modifiers">Modifier keys held for the chord.</param>
    /// <returns>Whether Windows accepted the complete input.</returns>
    public bool TrySendKey(Key key, ModifierKeys modifiers = ModifierKeys.None)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        return _host.TrySendKey(key, modifiers);
    }

    /// <summary>
    /// Waits for the candidate source to publish a newer snapshot.
    /// </summary>
    /// <remarks>
    /// Ending the original element can acknowledge a successful injected commit. This wait observes caller
    /// cancellation and the supplied token independently of the element's input lifetime.
    /// Input through this context still expires as soon as the original element ends.
    /// </remarks>
    /// <param name="timeout">The maximum acknowledgement interval.</param>
    /// <param name="cancellationToken">Cancels an obsolete operation.</param>
    /// <returns>True when the snapshot changed; otherwise false.</returns>
    public async Task<bool> WaitForSnapshotChangeAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var acknowledgementCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _callerCancellationToken, cancellationToken
        );

        var token = acknowledgementCancellation.Token;

        token.ThrowIfCancellationRequested();

        var changed = await _host
            .WaitForSnapshotChangeAsync(SnapshotVersion, timeout, token)
            .ConfigureAwait(false);

        token.ThrowIfCancellationRequested();

        return changed;
    }
}
