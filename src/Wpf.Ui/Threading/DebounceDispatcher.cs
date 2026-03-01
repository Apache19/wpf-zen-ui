// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Threading;

/// <summary>
/// Executes an action only after a period of inactivity.
/// Multiple calls within the delay window will result in a single execution.
/// Thread-safe.
/// </summary>
internal sealed class DebounceDispatcher : IDisposable
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif

    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <summary>
    /// Schedules an asynchronous action using debounce semantics.
    /// </summary>
    /// <remarks>
    /// Debounce means the action is executed only after no calls have been made
    /// for the specified <paramref name="delay"/> period. Every new call resets
    /// the timer and cancels any previously scheduled execution.
    ///
    /// Thread safety:
    /// This method is fully thread-safe. Multiple concurrent callers will always
    /// result in at most one pending execution.
    ///
    /// Cancellation model:
    /// Each invocation replaces the previous <see cref="CancellationTokenSource"/>.
    /// The provided <see cref="CancellationToken"/> is used to cancel the internal
    /// delay and prevent the action from running if a newer invocation occurs.
    ///
    /// Important implementation detail:
    /// The cancellation token is intentionally NOT passed to <see cref="Task.Run(Action)"/>.
    /// Passing a token to Task.Run only cancels task scheduling, not the internal
    /// delay or the action itself. Debounce requires the task to start reliably
    /// and cancel cooperatively during the delay phase instead. This guarantees
    /// stable and predictable task completion behavior for callers.
    ///
    /// Exception behavior:
    /// - <see cref="OperationCanceledException"/> from the internal delay is expected
    ///   and swallowed.
    /// - Exceptions thrown by <paramref name="action"/> will propagate to the
    ///   returned task.
    ///
    /// Synchronization context:
    /// The action is executed on a thread pool thread. Callers must marshal back
    /// to a UI thread if required (e.g., via Dispatcher).
    /// </remarks>
    /// <param name="delay">
    /// The inactivity period that must elapse before the action is executed.
    /// </param>
    /// <param name="action">
    /// The asynchronous action to execute after the debounce delay.
    /// The provided token is cancelled if a newer invocation supersedes this one.
    /// </param>
    /// <returns>
    /// A task representing the scheduled debounce operation.
    /// </returns>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public Task DebounceAsync(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        CancellationTokenSource currentCts;

        lock (_lock)
        {
#if NET8_0_OR_GREATER
            ObjectDisposedException.ThrowIf(_disposed, this);
#else
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(DebounceDispatcher));
            }
#endif

            _cts?.Cancel();
            _cts?.Dispose();

            _cts       = new CancellationTokenSource();
            currentCts = _cts;
        }

        var token = currentCts.Token;

        // ReSharper disable once MethodSupportsCancellation
        return Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(delay, token).ConfigureAwait(false);

                    if (!token.IsCancellationRequested)
                    {
                        await action(token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException)
                {
                    // expected, swallow
                }
            }
        );
    }

    /// <summary>
    /// Schedules a synchronous action using debounce semantics.
    /// </summary>
    /// <remarks>
    /// Convenience overload wrapping the asynchronous version.
    /// The action is executed on a thread pool thread.
    /// </remarks>
    /// <param name="delay">The inactivity period before execution.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A task representing the scheduled debounce operation.</returns>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public Task DebounceAsync(TimeSpan delay, Action action)
    {
        return DebounceAsync(delay, _ =>
        {
            action();
            return Task.CompletedTask;
        });
    }

    /// <summary>
    /// Schedules an asynchronous action using debounce semantics (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DebounceAsync(TimeSpan, Func{CancellationToken, Task})"/>,
    /// this method does not return a <see cref="Task"/>. Unhandled exceptions from
    /// the action will surface as unobserved task exceptions.
    /// </remarks>
    /// <param name="delay">The inactivity period before execution.</param>
    /// <param name="action">The asynchronous action to execute.</param>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Debounce(TimeSpan delay, Func<CancellationToken, Task> action)
    {
        _ = DebounceAsync(delay, action);
    }

    /// <summary>
    /// Schedules a synchronous action using debounce semantics (fire-and-forget).
    /// </summary>
    /// <remarks>
    /// Unlike <see cref="DebounceAsync(TimeSpan, Action)"/>,
    /// this method does not return a <see cref="Task"/>. Unhandled exceptions from
    /// the action will surface as unobserved task exceptions.
    /// </remarks>
    /// <param name="delay">The inactivity period before execution.</param>
    /// <param name="action">The action to execute.</param>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Debounce(TimeSpan delay, Action action)
    {
        _ = DebounceAsync(delay, action);
    }

    /// <summary>
    /// Cancels any pending execution.
    /// </summary>
    public void Cancel()
    {
        lock (_lock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}