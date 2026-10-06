// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Threading;
using NativeRepeatButton = System.Windows.Controls.Primitives.RepeatButton;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A Fluent button that repeats clicks while pressed, using an independent periodic timer.
/// </summary>
/// <remarks>
/// <para>
/// The default ClickMode is Press. Mouse, Space, and Hover mode use WPF's pressed state.
/// Leaving the pressed state stops repetition; pressing again starts a fresh Delay.
/// The inherited button style, icons, commands, and automation behavior remain available.
/// </para>
/// <para>
/// A thread-pool timer schedules repeats independently of click-handler duration. At most
/// one repeat is queued or executing per press. Missed ticks are merged, not replayed.
/// Click and Command execute on the UI dispatcher at Input priority, so UI blocking,
/// higher-priority work, thread-pool contention, and OS scheduling can still delay them.
/// This control is not a real-time timer; keep click handlers short.
/// </para>
/// </remarks>
public class RepeatButton : Button
{
    private RepeatSession? _session;
    private bool           _clickInProgress;

    /// <summary>
    /// Identifies the Delay dependency property, measured in milliseconds.
    /// </summary>
    public static readonly DependencyProperty DelayProperty
        = NativeRepeatButton.DelayProperty.AddOwner(
            typeof(RepeatButton),
            new FrameworkPropertyMetadata(OnTimingChanged)
        );

    /// <summary>
    /// Gets or sets the nonnegative delay before the first repeat, in milliseconds.
    /// The default follows WPF's system keyboard-delay setting. Changing this value
    /// during a press restarts the delay.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public int Delay
    {
        get => (int)GetValue(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    /// <summary>
    /// Identifies the Interval dependency property, measured in milliseconds.
    /// </summary>
    public static readonly DependencyProperty IntervalProperty
        = NativeRepeatButton.IntervalProperty.AddOwner(
            typeof(RepeatButton),
            new FrameworkPropertyMetadata(OnTimingChanged)
        );

    /// <summary>
    /// Gets or sets the positive period between scheduled repeats, in milliseconds.
    /// The default follows WPF's system keyboard-speed setting. Changing this value
    /// during a press starts a new schedule, beginning with Delay.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public int Interval
    {
        get => (int)GetValue(IntervalProperty);
        set => SetValue(IntervalProperty, value);
    }

    static RepeatButton()
    {
        ClickModeProperty.OverrideMetadata(
            typeof(RepeatButton),
            new FrameworkPropertyMetadata(ClickMode.Press)
        );
    }

    /// <summary>
    /// Creates a button whose repeat session ends when the control is unloaded.
    /// </summary>
    public RepeatButton()
    {
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Starts a fresh delay on press and invalidates queued repeats on release.
    /// </summary>
    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        RestartRepeating();
    }

    /// <summary>
    /// Ends repetition when the control becomes disabled or invisible.
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if ((e.Property == IsEnabledProperty && !IsEnabled)
            || (e.Property == IsVisibleProperty && !IsVisible)
            || e.Property == ClickModeProperty)
        {
            StopRepeating();
        }
    }

    /// <summary>
    /// Raises Click and executes the command while preventing nested repeat delivery.
    /// </summary>
    protected override void OnClick()
    {
        if (_clickInProgress)
        {
            return;
        }

        _clickInProgress = true;

        try
        {
            base.OnClick();
        }
        finally
        {
            _clickInProgress = false;
        }
    }

    /// <summary>
    /// Replaces the active schedule so queued callbacks cannot use obsolete timing values.
    /// </summary>
    private static void OnTimingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (RepeatButton)d;
        button.RestartRepeating();
    }

    /// <summary>
    /// Creates a session only for an enabled, visible press in a live visual tree.
    /// </summary>
    private void RestartRepeating()
    {
        StopRepeating();
        if (!IsPressed || !IsEnabled || !IsLoaded || !IsVisible || Dispatcher.HasShutdownStarted)
        {
            return;
        }

        var session = new RepeatSession(this);
        _session = session;
        session.Start(Delay, Interval);
    }

    /// <summary>
    /// Invalidates the session before disposing its timer; an in-flight callback may still arrive.
    /// </summary>
    private void StopRepeating()
    {
        var session = _session;
        _session = null;
        session?.Dispose();
    }

    /// <summary>
    /// Releases timer resources when the control leaves its presentation tree.
    /// </summary>
    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopRepeating();
    }

    /// <summary>
    /// Owns one press's timer and coalesces notifications across the worker/UI boundary.
    /// The weak owner prevents a timer from keeping a discarded visual tree alive.
    /// </summary>
    private sealed class RepeatSession : IDisposable
    {
        private readonly WeakReference<RepeatButton> _owner;
        private readonly Dispatcher                  _dispatcher;
        private readonly Timer                       _timer;

        private int _stopped;
        private int _pending;

        /// <summary>
        /// Captures the dispatcher and weak owner, leaving the timer initially disarmed.
        /// </summary>
        public RepeatSession(RepeatButton owner)
        {
            _owner      = new WeakReference<RepeatButton>(owner);
            _dispatcher = owner.Dispatcher;
            _timer      = new Timer(OnTimer, null, Timeout.Infinite, Timeout.Infinite);

            _dispatcher.ShutdownStarted += OnDispatcherShutdown;
        }

        /// <summary>
        /// Arms a periodic timer after the owner has published this session on its UI thread.
        /// </summary>
        public void Start(int delay, int interval)
        {
            _timer.Change(delay, interval);
        }

        /// <summary>
        /// Stops future ticks and makes already queued callbacks harmless.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _stopped, 1) == 0)
            {
                _timer.Dispose();
                _dispatcher.ShutdownStarted -= OnDispatcherShutdown;
            }
        }

        /// <summary>
        /// Releases even long-delay timers immediately when their UI dispatcher shuts down.
        /// </summary>
        private void OnDispatcherShutdown(object? sender, EventArgs e)
        {
            Dispose();
        }

        /// <summary>
        /// Posts at most one UI operation without reading thread-affine control properties.
        /// </summary>
        private void OnTimer(object? state)
        {
            if (Volatile.Read(ref _stopped) != 0)
            {
                return;
            }

            if (_dispatcher.HasShutdownStarted || !_owner.TryGetTarget(out _))
            {
                Dispose();
                return;
            }

            if (Interlocked.CompareExchange(ref _pending, 1, 0) != 0)
            {
                return;
            }

            // Input priority shares scheduling with input and outranks Background timers.
            // BeginInvoke preserves WPF's dispatcher exception handling for user Click code
            // and returns an aborted operation if shutdown wins this race.
            _dispatcher.BeginInvoke(DispatcherPriority.Input, (Action)DeliverRepeat);
        }

        /// <summary>
        /// Rechecks lifetime and press state on the UI thread before invoking user code.
        /// </summary>
        private void DeliverRepeat()
        {
            try
            {
                if (Volatile.Read(ref _stopped) != 0 || !_owner.TryGetTarget(out var owner))
                {
                    return;
                }

                if (owner._session != this || !owner.IsPressed || !owner.IsEnabled
                    || !owner.IsLoaded || !owner.IsVisible)
                {
                    Dispose();
                    return;
                }

                owner.OnClick();
            }
            finally
            {
                // Hold the gate through user code, including any nested dispatcher frames.
                Volatile.Write(ref _pending, 0);
            }
        }
    }
}