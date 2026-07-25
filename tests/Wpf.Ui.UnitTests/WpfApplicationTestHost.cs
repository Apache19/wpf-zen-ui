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

    public static void Run(Action action)
    {
        ApplicationDispatcher.Value.Invoke(action);
    }

    public static void ShutdownApplication()
    {
        var dispatcher = ApplicationDispatcher.Value;
        dispatcher.Invoke(() => Application.Current?.Shutdown());

        // Application.Shutdown queues its cleanup at Normal priority. A lower-priority barrier guarantees that
        // Application.Current has been cleared before xUnit releases this non-parallel collection and starts tests
        // which intentionally verify operation without a WPF Application.
        dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);
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