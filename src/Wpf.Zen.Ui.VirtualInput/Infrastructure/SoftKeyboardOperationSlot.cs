// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Owns one dispatcher-bound asynchronous operation and its right to publish results.
/// </summary>
/// <remarks>
/// Replacement and cancellation detach ownership before invoking cancellation callbacks.
/// Only the executing operation disposes its token source, after its final continuation finishes.
/// Native candidate-element lifetime is separate: its end may acknowledge successful input.
/// </remarks>
internal sealed class SoftKeyboardOperationSlot
{
#pragma warning disable IDISP006 // This slot owns publication rights; the executing caller owns disposal.
    private Operation? _current;
#pragma warning restore IDISP006

    /// <summary>
    /// Starts an operation, either replacing the current operation or rejecting concurrent input.
    /// </summary>
    internal Operation? Start(
        CancellationToken cancellationToken, bool replace, Action<Exception> reportError)
    {
        if (cancellationToken.IsCancellationRequested || (!replace && _current is { IsCurrent: true }))
        {
            return null;
        }

        var previous  = _current;
        var operation = new Operation(this, cancellationToken);

#pragma warning disable IDISP003 // Replaced operations release their own resources after execution finishes.
        _current = operation;
#pragma warning restore IDISP003

        previous?.Cancel(reportError);
        return operation;
    }

    /// <summary>
    /// Revokes result publication immediately, then requests cooperative cancellation.
    /// </summary>
    internal void Cancel(Action<Exception> reportError)
    {
        var previous = _current;

#pragma warning disable IDISP003 // Cancellation revokes publication without disposing a running operation.
        _current = null;
#pragma warning restore IDISP003

        previous?.Cancel(reportError);
    }

    /// <summary>
    /// Revokes related operations together before any cancellation callback can start replacement work.
    /// </summary>
    internal static void CancelTogether(
        SoftKeyboardOperationSlot first, SoftKeyboardOperationSlot second, Action<Exception> reportError)
    {
        var firstOperation  = first._current;
        var secondOperation = second._current;

#pragma warning disable IDISP003 // Running callers dispose the detached operations after execution.
        first._current  = null;
        second._current = null;
#pragma warning restore IDISP003

        firstOperation?.Cancel(reportError);
        secondOperation?.Cancel(reportError);
    }

    /// <summary>
    /// Holds the execution lifetime independently from the slot's current ownership.
    /// </summary>
    internal sealed class Operation : IDisposable
    {
        private readonly SoftKeyboardOperationSlot _owner;
        private readonly CancellationTokenSource   _cancellation;

        private bool _disposed;

        internal Operation(SoftKeyboardOperationSlot owner, CancellationToken cancellationToken)
        {
            _owner        = owner;
            _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Token         = _cancellation.Token;
        }

        /// <summary>
        /// Gets a stable token usable until execution has finished, even after replacement.
        /// </summary>
        internal CancellationToken Token { get; }

        /// <summary>
        /// Gets a value indicating whether this operation still owns result and error publication.
        /// </summary>
        internal bool IsCurrent => !_disposed
            && !Token.IsCancellationRequested && ReferenceEquals(_owner._current, this);

        /// <summary>
        /// Releases execution resources without disturbing a replacement operation.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (ReferenceEquals(_owner._current, this))
            {
                _owner._current = null;
            }

            _cancellation.Dispose();
        }

        /// <summary>
        /// Notifies all cancellation callbacks; callback failure cannot prevent ownership revocation.
        /// </summary>
        internal void Cancel(Action<Exception> reportError)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                _cancellation.Cancel();
            }
            catch (AggregateException exception)
            {
                reportError(exception);
            }
        }
    }
}