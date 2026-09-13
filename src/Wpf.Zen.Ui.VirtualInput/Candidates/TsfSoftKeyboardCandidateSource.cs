// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Candidates;

/// <summary>
/// Uses the dispatcher-scoped Text Services Framework session as the default candidate source.
/// </summary>
public sealed class TsfSoftKeyboardCandidateSource : ISoftKeyboardCandidateSource
{
    /// <summary>
    /// Gets the shared stateless TSF source.
    /// </summary>
    public static TsfSoftKeyboardCandidateSource Default { get; } = new();

    /// <inheritdoc />
    public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
    {
        return new Session(host ?? throw new ArgumentNullException(nameof(host)));
    }

    private sealed class Session : ISoftKeyboardCandidateSession
    {
        private readonly SoftKeyboardCandidateHost _host;
        private bool _disposed;

        internal Session(SoftKeyboardCandidateHost host)
        {
            _host = host;
            SoftKeyboardTsfCandidateBridge.Register(host.Keyboard, host);
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            SoftKeyboardTsfCandidateBridge.Refresh(_host.Keyboard);

            return Task.CompletedTask;
        }

        public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
            SoftKeyboardCandidate candidate,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return SoftKeyboardTsfCandidateBridge.TryCommitAsync(
                _host.Keyboard,
                candidate,
                cancellationToken
            );
        }

        public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
            int delta,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            return SoftKeyboardTsfCandidateBridge.TryMovePageAsync(
                _host.Keyboard,
                delta,
                cancellationToken
            );
        }

        public void ReleasePresentation()
        {
            if (!_disposed)
            {
                SoftKeyboardTsfCandidateBridge.ReleasePresentation(_host.Keyboard);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            SoftKeyboardTsfCandidateBridge.Unregister(_host.Keyboard);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(Session));
            }
        }
    }
}
