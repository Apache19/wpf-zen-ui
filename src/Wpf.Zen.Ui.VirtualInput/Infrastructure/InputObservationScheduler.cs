// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Provides a monotonic clock and cancellable one-shot scheduling on the owning Dispatcher.
/// </summary>
internal interface IInputObservationScheduler
{
    /// <summary>
    /// Gets monotonic elapsed milliseconds for quiet-window and maximum-deadline calculations.
    /// </summary>
    long Now { get; }

    /// <summary>
    /// Queues one callback on the owning Dispatcher and returns its cancellation handle.
    /// </summary>
    IDisposable Schedule(int milliseconds, Action callback);
}

/// <summary>
/// Schedules observation work outside native callbacks. Shutdown releases outstanding timers.
/// </summary>
internal sealed class DispatcherInputObservationScheduler(Dispatcher dispatcher) : IInputObservationScheduler
{
    /// <inheritdoc />
    public long Now => (long)(Stopwatch.GetTimestamp() * (1000.0 / Stopwatch.Frequency));

    /// <inheritdoc />
    public IDisposable Schedule(int milliseconds, Action callback)
    {
        return new ScheduledCallback(dispatcher, milliseconds, callback);
    }

    private sealed class ScheduledCallback : IDisposable
    {
        private readonly Dispatcher      _dispatcher;
        private readonly DispatcherTimer _timer;

        private Action? _callback;

        internal ScheduledCallback(Dispatcher dispatcher, int milliseconds, Action callback)
        {
            _dispatcher = dispatcher;
            _callback   = callback;

            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(milliseconds),
            };

            _timer.Tick                += OnTick;
            dispatcher.ShutdownStarted += OnShutdown;

            if (!dispatcher.HasShutdownStarted)
            {
                _timer.Start();
            }
            else
            {
                Dispose();
            }
        }

        private void OnTick(object? sender, EventArgs args)
        {
            var callback = _callback;
            Dispose();
            callback?.Invoke();
        }

        private void OnShutdown(object? sender, EventArgs args)
        {
            Dispose();
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _callback = null;
            _timer.Stop();
            _timer.Tick                 -= OnTick;
            _dispatcher.ShutdownStarted -= OnShutdown;
        }
    }
}