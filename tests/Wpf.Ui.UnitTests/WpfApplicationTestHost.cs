// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Threading;
using System.Windows.Threading;

namespace Wpf.Ui.UnitTests;

/// <summary>
/// Owns the single long-lived STA dispatcher used by tests that touch <see cref="Application.Current"/>.
/// </summary>
/// <remarks>
/// ThemeManager intentionally creates one process-wide WPF Application when the host did not provide one.
/// Creating that Application on a short-lived test thread leaves its dispatcher owned by a terminated thread and
/// makes later resource mutations nondeterministic. Keeping this dispatcher alive mirrors a real WPF host and also
/// gives every test a stable thread on which to inspect and mutate application resources.
/// </remarks>
internal static class WpfApplicationTestHost
{
    private static readonly Lazy<Dispatcher> ApplicationDispatcher = new(CreateDispatcher);

    /// <summary>
    /// Ensures that the test assembly's single WPF application belongs to the long-lived dispatcher thread.
    /// </summary>
    public static void EnsureApplication()
    {
        ApplicationDispatcher.Value.Invoke(() =>
        {
            if (Application.Current is null)
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            }
        });
    }

    public static void Run(Action action)
    {
        ApplicationDispatcher.Value.Invoke(action);
    }

    /// <summary>
    /// Shuts down the application and its dispatcher after the assembly fixture has released all tests.
    /// </summary>
    public static void Shutdown()
    {
        if (!ApplicationDispatcher.IsValueCreated)
        {
            return;
        }

        var dispatcher = ApplicationDispatcher.Value;
        dispatcher.Invoke(() => Application.Current?.Shutdown());

        // Application.Shutdown schedules DoShutdown at Normal priority. The ContextIdle barrier is required
        // before stopping the dispatcher; otherwise WPF can leave its process-wide package cache half-cleaned.
        dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);
        dispatcher.InvokeShutdown();
    }

    private static Dispatcher CreateDispatcher()
    {
        Dispatcher? dispatcher = null;
        using var dispatcherReady = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            dispatcher = Dispatcher.CurrentDispatcher;
            dispatcherReady.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "Wpf.Ui unit-test application dispatcher",
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        dispatcherReady.Wait();

        return dispatcher!;
    }
}
