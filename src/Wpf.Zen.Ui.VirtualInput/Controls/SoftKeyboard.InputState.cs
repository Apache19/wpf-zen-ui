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
    /// Identifies the <see cref="InputStateAdapter"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InputStateAdapterProperty
        = DependencyProperty.Register(
            nameof(InputStateAdapter),
            typeof(ISoftKeyboardInputStateAdapter),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                null,
                static (owner, _) => ((SoftKeyboard)owner).RefreshImeState()
            )
        );

    /// <summary>
    /// Gets or sets the adapter for nonstandard text-service mode APIs. Defaults to null, using WPF state.
    /// </summary>
    /// <remarks>
    /// This property is not inherited or animated. Changing it re-reads state and may regenerate
    /// the layout. Adapter exceptions degrade to unknown state or a rejected write; a failed
    /// adapter is never followed by a second write through a different state mechanism.
    /// </remarks>
    public ISoftKeyboardInputStateAdapter? InputStateAdapter
    {
        get => (ISoftKeyboardInputStateAdapter?)GetValue(InputStateAdapterProperty);
        set => SetValue(InputStateAdapterProperty, value);
    }

    private InputMethodState ReadInputState()
    {
        try
        {
            var state = InputStateAdapter?.GetState(ActiveInputMethod)
                ?? _platform.InputMethods.GetImeState();

            return state is InputMethodState.On or InputMethodState.Off
                ? state
                : InputMethodState.DoNotCare;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return InputMethodState.DoNotCare;
        }
    }

    private bool TryChangeInputState(InputMethodState state)
    {
        try
        {
            return InputStateAdapter?.TrySetState(ActiveInputMethod, state)
                ?? _platform.InputMethods.TrySetImeState(state);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return false;
        }
    }

    // WPF StateChanged covers TSF compartments, but a same-language profile switch need not
    // raise InputLanguageChanged. Observe profiles at a bounded cadence while this keyboard owns
    // foreground input. Never restore focus or write system state from this read-only fallback.
    // The timer is stopped on Unloaded, so it cannot retain a discarded keyboard through Dispatcher.
    private void StartInputStateObservation()
    {
        _inputStateTimer ??= new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) =>
            {
                if (IsOpen && IsVisible && IsEnabled && IsWindowForeground()
                    && ActiveTarget is not null && ReferenceEquals(Keyboard.FocusedElement, ActiveTarget))
                {
                    RefreshInputMethods();
                    RefreshImeState();
                }
            },
            Dispatcher
        );

        _inputStateTimer.Start();
    }
}