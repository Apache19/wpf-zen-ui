// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Threading.Tasks;
using Wpf.Zen.Ui.VirtualInput.Candidates;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Input;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Adapts recording test backends to the production candidate-source contract.
/// </summary>
internal sealed class BackendSoftKeyboardCandidateSource(
    ISoftKeyboardCandidateBackend backend) : ISoftKeyboardCandidateSource
{
    public ISoftKeyboardCandidateSession CreateSession(SoftKeyboardCandidateHost host)
    {
        return new Session(
            backend ?? throw new ArgumentNullException(nameof(backend)),
            host ?? throw new ArgumentNullException(nameof(host))
        );
    }

    private sealed class Session : ISoftKeyboardCandidateSession
    {
        private readonly ISoftKeyboardCandidateBackend _backend;
        private readonly SoftKeyboardCandidateHost     _host;

        private bool _disposed;

        internal Session(ISoftKeyboardCandidateBackend backend, SoftKeyboardCandidateHost host)
        {
            _backend = backend;
            _host    = host;

            _backend.Register(_host.Keyboard);
        }

        public Task RefreshAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            _backend.Refresh(_host.Keyboard);

            return Task.CompletedTask;
        }

        public Task<SoftKeyboardCandidateOperationResult> CommitAsync(
            SoftKeyboardCandidate candidate,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            var handled = _backend.TryCommit(_host.Keyboard, candidate.Index);

            return Task.FromResult(
                handled
                    ? SoftKeyboardCandidateOperationResult.Success
                    : SoftKeyboardCandidateOperationResult.NotHandled
            );
        }

        public Task<SoftKeyboardCandidateOperationResult> MovePageAsync(
            int delta,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            cancellationToken.ThrowIfCancellationRequested();

            var handled = _backend.TryMovePage(_host.Keyboard, delta);

            return Task.FromResult(
                handled
                    ? SoftKeyboardCandidateOperationResult.Success
                    : SoftKeyboardCandidateOperationResult.NotHandled
            );
        }

        public void ReleasePresentation()
        {
            if (!_disposed)
            {
                _backend.ReleasePresentation(_host.Keyboard);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _backend.Unregister(_host.Keyboard);
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