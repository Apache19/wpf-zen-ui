// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

namespace Wpf.Zen.Ui.UnitTests;

/// <summary>
/// Verifies the dispatcher-lifetime contract shared by isolated WPF unit tests.
/// </summary>
public class WpfTestThreadTests
{
    /// <summary>
    /// Ensures a dispatcher created by test work has completed shutdown before its STA thread terminates.
    /// </summary>
    [Fact]
    public void Run_ShutsDownDispatcherBeforeReturning()
    {
        Dispatcher? dispatcher = null;

        WpfTestThread.Run(() => dispatcher = Dispatcher.CurrentDispatcher);

        Assert.NotNull(dispatcher);
        Assert.True(dispatcher.HasShutdownFinished);
    }

    /// <summary>
    /// Ensures asynchronous test delegates can suspend for dispatcher work without leaving their STA thread.
    /// </summary>
    [Fact]
    public void Run_PumpsDispatcherForAsyncDelegate()
    {
        WpfTestThread.Run(async () =>
        {
            int threadId = Environment.CurrentManagedThreadId;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;

            await dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);

            Assert.Equal(threadId, Environment.CurrentManagedThreadId);
        });
    }
}
