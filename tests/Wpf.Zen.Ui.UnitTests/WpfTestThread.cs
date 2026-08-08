// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.UnitTests;

/// <summary>
/// Runs isolated WPF test work on an STA thread and owns the complete lifetime of that thread's dispatcher.
/// </summary>
/// <remarks>
/// Constructing many WPF objects creates thread-affine infrastructure even when a test never starts a message loop.
/// In particular, weak-event managers register process-exit listeners that synchronously dispatch their cleanup back
/// to the creating thread. A test thread must therefore shut down its dispatcher before terminating; otherwise WPF's
/// process-exit handler waits for a dispatcher whose thread no longer exists, adding hundreds of milliseconds for
/// every leaked test dispatcher after the test run itself has already completed.
/// </remarks>
internal static class WpfTestThread
{
    /// <summary>
    /// Executes <paramref name="action"/> on a fresh STA thread and rethrows failures on the calling test thread.
    /// </summary>
    /// <param name="action">The WPF test work to execute.</param>
    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
            finally
            {
                try
                {
                    ShutdownCurrentDispatcher();
                }
                catch (Exception shutdownException)
                {
                    // Preserve the test failure when both the action and dispatcher teardown fail. The original
                    // failure describes the behavior under test; a teardown failure is otherwise surfaced normally.
                    exception ??= shutdownException;
                }
            }
        })
        {
            IsBackground = true,
            Name = "Wpf.Zen.Ui isolated unit-test thread",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }

    /// <summary>
    /// Completes dispatcher-owned WPF cleanup while the owning thread is still available to process it.
    /// </summary>
    private static void ShutdownCurrentDispatcher()
    {
        // FromThread deliberately avoids creating a Dispatcher for tests that performed no dispatcher-affine work.
        // InvokeShutdown runs synchronously on this owning thread and raises ShutdownFinished, allowing WPF weak-event,
        // input, layout, media and HWND infrastructure to unregister its process-level shutdown listeners immediately.
        Dispatcher? dispatcher = Dispatcher.FromThread(Thread.CurrentThread);
        if (dispatcher is not null && !dispatcher.HasShutdownFinished)
        {
            dispatcher.InvokeShutdown();
        }
    }
}
