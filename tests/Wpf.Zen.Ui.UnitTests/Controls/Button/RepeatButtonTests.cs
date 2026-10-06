// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.Input;

namespace Wpf.Zen.Ui.UnitTests.Controls.ButtonControl;

/// <summary>
/// Verifies repeat scheduling, UI delivery, and cancellation across the control's lifetime.
/// </summary>
public class RepeatButtonTests
{
    /// <summary>
    /// Checks native timing defaults, validation, and the inherited Fluent style key.
    /// </summary>
    [Fact]
    public void DefaultsAndValidation_MatchWpfRepeatTiming()
    {
        WpfTestThread.Run(() =>
        {
            var button = new Probe();
            var native = new System.Windows.Controls.Primitives.RepeatButton();
            Assert.Equal(native.Delay, button.Delay);
            Assert.Equal(native.Interval, button.Interval);
            Assert.Equal(ClickMode.Press, button.ClickMode);
            Assert.Equal(typeof(Wpf.Zen.Ui.Controls.Button), button.StyleKey);
            Assert.Throws<ArgumentException>(() => button.Delay = -1);
            Assert.Throws<ArgumentException>(() => button.Interval = 0);
        });
    }

    /// <summary>
    /// Checks dispatcher priority and the thread used for routed clicks and commands.
    /// </summary>
    [Fact]
    public void Repeat_RunsClickAndCommandOnUiThreadBeforeBackgroundWork()
    {
        WithButton((button, window) =>
        {
            int uiThread = Environment.CurrentManagedThreadId;
            var order = new List<string>();
            button.Command = new RelayCommand(() =>
            {
                Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                order.Add("command");
            });
            button.Click += (_, _) =>
            {
                Assert.Equal(uiThread, Environment.CurrentManagedThreadId);
                order.Add("click");
                button.Press(false);
            };
            button.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                order.Add("background");
            }));

            WaitForRepeatPost(button, () => button.Press(true));
            DrainDispatcher();
            Assert.Equal(new[] { "click", "command", "background" }, order);
        });
    }

    /// <summary>
    /// Ensures a queued notification cannot outlive its press or timing configuration.
    /// </summary>
    [Theory]
    [InlineData("release")]
    [InlineData("disable")]
    [InlineData("hide")]
    [InlineData("unload")]
    [InlineData("timing")]
    [InlineData("newPress")]
    public void PendingRepeat_IsInvalidatedWhenSessionEnds(string reason)
    {
        WithButton((button, window) =>
        {
            int clicks = 0;
            button.Click += (_, _) => clicks++;
            WaitForRepeatPost(button, () => button.Press(true));

            switch (reason)
            {
                case "release":
                    button.Press(false);
                    break;
                case "disable":
                    button.IsEnabled = false;
                    break;
                case "hide":
                    button.Visibility = Visibility.Collapsed;
                    break;
                case "unload":
                    window.Content = null;
                    break;
                case "timing":
                    button.Delay = int.MaxValue;
                    break;
                case "newPress":
                    button.Press(false);
                    button.Delay = int.MaxValue;
                    button.Press(true);
                    break;
            }

            DrainDispatcher();
            Assert.Equal(0, clicks);
        });
    }

    /// <summary>
    /// Ensures slow handlers and nested message loops do not accumulate or nest repeat clicks.
    /// </summary>
    [Fact]
    public void BlockedUi_CoalescesTicksAndDoesNotReenterClick()
    {
        WithButton((button, window) =>
        {
            int posts = 0;
            int clicks = 0;
            void OnPosted(object? sender, DispatcherHookEventArgs e)
            {
                if (e.Operation.Priority == DispatcherPriority.Input)
                {
                    Interlocked.Increment(ref posts);
                }
            }

            button.Dispatcher.Hooks.OperationPosted += OnPosted;
            try
            {
                button.Interval = 10;
                button.Click += (_, _) =>
                {
                    clicks++;
                    // Let several timer periods pass, then simulate a modal nested message loop.
                    Thread.Sleep(100);
                    DrainDispatcher();
                    button.Press(false);
                };
                WaitForRepeatPost(button, () => button.Press(true));
                Thread.Sleep(100);
                Assert.Equal(1, Volatile.Read(ref posts));
                DrainDispatcher();
                Assert.Equal(1, clicks);
                Assert.Equal(1, Volatile.Read(ref posts));
            }
            finally
            {
                button.Dispatcher.Hooks.OperationPosted -= OnPosted;
            }
        });
    }

    /// <summary>
    /// Checks that completing a delivered click permits later periodic notifications.
    /// </summary>
    [Fact]
    public void HeldPress_ContinuesRepeatingAfterDelivery()
    {
        WithButton((button, window) =>
        {
            button.Interval = 40;
            int clicks = 0;
            button.Click += (_, _) => clicks++;

            WaitForRepeatPost(button, () => button.Press(true));
            DrainDispatcher();
            int firstCount = clicks;
            Assert.True(firstCount > 0);

            WaitForRepeatPost(button, () => { });
            DrainDispatcher();
            Assert.True(clicks > firstCount);
            button.Press(false);
        });
    }

    /// <summary>
    /// Waits for a real worker timer to enqueue a repeat while deliberately not pumping the UI.
    /// </summary>
    private static void WaitForRepeatPost(Probe button, Action start)
    {
        using var posted = new ManualResetEventSlim();
        void OnPosted(object? sender, DispatcherHookEventArgs e)
        {
            if (e.Operation.Priority == DispatcherPriority.Input)
            {
                posted.Set();
            }
        }

        button.Dispatcher.Hooks.OperationPosted += OnPosted;
        try
        {
            start();
            Assert.True(posted.Wait(TimeSpan.FromSeconds(5)), "No repeat was queued.");
        }
        finally
        {
            button.Dispatcher.Hooks.OperationPosted -= OnPosted;
        }
    }

    /// <summary>
    /// Pumps pending input and background operations without depending on short timing thresholds.
    /// </summary>
    private static void DrainDispatcher()
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    /// <summary>
    /// Hosts each button in a live visual tree on an isolated STA dispatcher.
    /// </summary>
    private static void WithButton(Action<Probe, Window> action)
    {
        WpfTestThread.Run(() =>
        {
            var button = new Probe { Delay = 0, Interval = 1000, OverridesDefaultStyle = true };
            var window = new Window
            {
                Content = button,
                Width = 100,
                Height = 100,
                ShowActivated = false,
                ShowInTaskbar = false,
                Left = -10000,
                Top = -10000,
            };
            try
            {
                window.Show();
                DrainDispatcher();
                Assert.True(button.IsLoaded);
                Assert.True(button.IsVisible);
                action(button, window);
            }
            finally
            {
                button.Press(false);
                window.Close();
            }
        });
    }

    private sealed class Probe : Wpf.Zen.Ui.Controls.RepeatButton
    {
        /// <summary>
        /// Exposes the inherited style lookup key for verification.
        /// </summary>
        public object StyleKey => DefaultStyleKey;

        /// <summary>
        /// Simulates the pressed transitions normally owned by WPF input handling.
        /// </summary>
        public void Press(bool value)
        {
#pragma warning disable WPF0041
            IsPressed = value;
#pragma warning restore WPF0041
        }
    }
}
