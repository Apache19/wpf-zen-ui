// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using Wpf.Zen.Ui.VirtualInput.Infrastructure;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Executes one-shot callbacks only when a test explicitly advances monotonic time.
/// </summary>
internal sealed class ManualInputObservationScheduler : IInputObservationScheduler
{
    private readonly List<Work> _pending = [];
    /// <inheritdoc />
    public long Now { get; private set; }

    /// <inheritdoc />
    public IDisposable Schedule(int milliseconds, Action callback)
    {
        var work = new Work(Now + milliseconds, callback);
        _pending.Add(work);
        return work;
    }

    /// <summary>
    /// Runs due callbacks in deadline order, including newly queued work, until the chosen time.
    /// </summary>
    internal void Advance(int milliseconds)
    {
        var end = Now + milliseconds;
        var count = 0;
        while (_pending.Where(work => work.Callback is not null && work.Due <= end)
            .OrderBy(work => work.Due).FirstOrDefault() is { } work)
        {
            Assert.True(++count < 1000, "Observation scheduling did not settle.");
            Now = work.Due;
            var callback = work.Callback;
            work.Dispose();
            callback!();
        }

        Now = end;
        _pending.RemoveAll(work => work.Callback is null);
    }

    private sealed class Work(long due, Action callback) : IDisposable
    {
        internal long Due { get; } = due;
        internal Action? Callback { get; private set; } = callback;
        /// <inheritdoc />
        public void Dispose()
        {
            Callback = null;
        }
    }
}
