// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Synchronizes native input mode without transferring ownership of the editor document.
/// </summary>
public partial class SoftKeyboard
{
    private DispatcherTimer? _inputStateTimer;

    /// <summary>
    /// Identifies the <see cref="InputModeAdapter"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputModeAdapterProperty
        = DependencyProperty.Register(
            nameof(InputModeAdapter),
            typeof(ISoftKeyboardInputModeAdapter),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                null,
                static (owner, _) => ((SoftKeyboard)owner).RefreshInputMode()
            )
        );

    /// <summary>
    /// Gets or sets the adapter that interprets the active text service's input modes.
    /// </summary>
    /// <remarks>
    /// The default is null, which uses the input-method source's standard TSF interpretation.
    /// This property is not inherited or animated.
    /// Changing it schedules a complete observation and may regenerate the layout. Adapter read
    /// failures preserve the last valid mode. A failed adapter transition is never followed by a
    /// second transition through another mechanism.
    /// </remarks>
    public ISoftKeyboardInputModeAdapter? InputModeAdapter
    {
        get => (ISoftKeyboardInputModeAdapter?)GetValue(InputModeAdapterProperty);
        set => SetValue(InputModeAdapterProperty, value);
    }

    /// <summary>
    /// Starts a recovery-only observation timer whose cadence reflects notification health.
    /// </summary>
    private void StartInputStateObservation()
    {
        _inputStateTimer ??= new DispatcherTimer(
            TimeSpan.FromSeconds(30),
            DispatcherPriority.Background,
            (_, _) =>
            {
                _session?.EnsureInputMethodMonitoring();
                UpdateInputStateRecoveryInterval();

                if (IsOpen && IsVisible && IsEnabled && IsWindowForeground()
                    && ActiveTarget is not null && ReferenceEquals(Keyboard.FocusedElement, ActiveTarget))
                {
                    QueueInputMethodStateRefresh();
                }
            },
            Dispatcher
        );

        UpdateInputStateRecoveryInterval();
        _inputStateTimer.Start();
    }

    /// <summary>
    /// Uses one-second recovery for custom mode adapters, failed reads, or incomplete notifications.
    /// </summary>
    private void UpdateInputStateRecoveryInterval()
    {
        if (_inputStateTimer is null)
        {
            return;
        }

        var sourceHasNotifications = _inputMethodSession
            is ISoftKeyboardInputMethodObservableSession;

        var systemNotificationsHealthy = InputMethodSource
            is WindowsSoftKeyboardInputMethodSource
            && _session?.HasReliableInputMethodNotifications == true;

        // WPF compartment subscriptions do not describe a vendor adapter's notification contract.
        // Such adapters can observe a language-bar mode whose changes bypass WPF StateChanged.
        var interval = ResolveInputModeAdapter() is not null
            || InputMethodError is not null
            || (!sourceHasNotifications && !systemNotificationsHealthy)
                ? TimeSpan.FromSeconds(1)
                : TimeSpan.FromSeconds(30);

        if (_inputStateTimer.Interval != interval)
        {
            _inputStateTimer.Interval = interval;
        }
    }
}
