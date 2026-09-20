// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Owns input observation and native integrations exactly once for each WPF Dispatcher.
/// </summary>
/// <remarks>
/// Multiple Pages or dialogs can keep keyboard instances loaded at once. Centralizing observation
/// prevents each instance from installing competing TSF sinks and makes focus routing
/// deterministic.
/// Instances are retained weakly so a templated page cannot be kept alive by the dispatcher.
/// </remarks>
internal sealed class SoftKeyboardDispatcherSession : IDisposable
{
    private static readonly ConditionalWeakTable<Dispatcher, SoftKeyboardDispatcherSession> Sessions = new();

    private readonly Dispatcher          _dispatcher;
    private readonly List<WeakReference> _keyboards = [];
    private readonly InputMethodMonitor  _inputMethodMonitor;

    private int _lastTouchOrPenTick;

    /// <summary>
    /// Attaches one input pipeline observer and native monitor for the dispatcher lifetime.
    /// </summary>
    private SoftKeyboardDispatcherSession(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;

        _inputMethodMonitor = new InputMethodMonitor(dispatcher, OnInputMethodStateInvalidated);

        InputManager.Current.PreProcessInput += OnPreProcessInput;

        dispatcher.ShutdownStarted += OnDispatcherShutdownStarted;
    }

    /// <summary>
    /// Returns the shared session for the calling UI dispatcher, creating its observers once.
    /// </summary>
    internal static SoftKeyboardDispatcherSession GetForCurrentDispatcher()
    {
        return Sessions.GetValue(
            Dispatcher.CurrentDispatcher,
            static dispatcher => new SoftKeyboardDispatcherSession(dispatcher)
        );
    }

    /// <summary>
    /// Adds a weak control registration and retries native monitoring after WPF initialization.
    /// </summary>
    internal void Register(SoftKeyboard keyboard)
    {
        RemoveDeadReferences();

        if (_keyboards.Any(reference => ReferenceEquals(reference.Target, keyboard)))
        {
            return;
        }

        _keyboards.Add(new WeakReference(keyboard));
        _inputMethodMonitor.EnsureNotifications();
    }

    /// <summary>
    /// Retries profile monitoring after WPF creates a text-services host for a focused editor.
    /// </summary>
    internal void EnsureInputMethodMonitoring()
    {
        _inputMethodMonitor.EnsureNotifications();
    }

    /// <summary>
    /// Removes a control while retaining shared observation for later loads on this dispatcher.
    /// </summary>
    internal void Unregister(SoftKeyboard keyboard)
    {
        _keyboards.RemoveAll(reference => !reference.IsAlive || ReferenceEquals(reference.Target, keyboard));
    }

    /// <summary>
    /// Determines whether another keyboard still owns the embedded input surface.
    /// </summary>
    /// <remarks>
    /// InputPane is HWND-scoped rather than control-scoped. Without this arbitration, closing one
    /// of two loaded keyboards could issue TryShow while the other keyboard still needs the native
    /// pane hidden, causing the controls to fight over the same Windows surface.
    /// </remarks>
    internal bool IsSystemKeyboardSuppressionRequiredByOther(SoftKeyboard owner)
    {
        RemoveDeadReferences();

        return _keyboards.Any(reference =>
            reference.Target is SoftKeyboard keyboard
            && !ReferenceEquals(keyboard, owner)
            && keyboard.IsSystemKeyboardSuppressionRequired
        );
    }

    /// <summary>
    /// Routes editor composition and focus events, retaining recent touch or pen intent for
    /// opening.
    /// </summary>
    private void OnPreProcessInput(object sender, PreProcessInputEventArgs args)
    {
        var input = args.StagingItem.Input;
        if (input is TextCompositionEventArgs compositionArgs)
        {
            // Composition events are observed at the Dispatcher boundary because the keyboard is
            // deliberately not an ancestor of its editor. Tracking the editor's composition events lets
            // a surface transition send Escape only while a TIP-owned pre-edit transaction exists;
            // blindly sending Escape would leak a cancellation command into the host application.
            ForEachKeyboard(keyboard => keyboard.OnSessionTextComposition(compositionArgs));
        }

        if (input is TouchEventArgs or StylusEventArgs)
        {
            _lastTouchOrPenTick = Environment.TickCount;
            return;
        }

        if (input.RoutedEvent != Keyboard.GotKeyboardFocusEvent
            || input is not KeyboardFocusChangedEventArgs focusArgs)
        {
            return;
        }

        var elapsed             = unchecked(Environment.TickCount - _lastTouchOrPenTick);
        var touchOrPenInitiated = elapsed is >= 0 and <= 1000;

        _inputMethodMonitor.EnsureNotifications();

        ForEachKeyboard(
            keyboard => keyboard.OnSessionFocusChanged(
                focusArgs.NewFocus, touchOrPenInitiated
            )
        );
    }

    /// <summary>
    /// Delivers native evidence only to sources that opt into the Windows notification pipeline.
    /// </summary>
    private void OnInputMethodStateInvalidated(InputMethodChange change)
    {
        ForEachKeyboard(keyboard =>
        {
            if (keyboard.InputMethodSource is WindowsSoftKeyboardInputMethodSource
                { UsesNativeNotifications: true })
            {
                keyboard.OnSessionInputMethodChanged(change);
            }
        });
    }

    /// <summary>
    /// Releases shared observers when their owning dispatcher begins shutdown.
    /// </summary>
    private void OnDispatcherShutdownStarted(object? sender, EventArgs eventArgs)
    {
        Dispose();
    }

    /// <summary>
    /// Releases dispatcher-wide managed and native input observation.
    /// </summary>
    public void Dispose()
    {
        InputManager.Current.PreProcessInput -= OnPreProcessInput;

        _inputMethodMonitor.Dispose();

        _dispatcher.ShutdownStarted -= OnDispatcherShutdownStarted;

        _keyboards.Clear();
    }

    /// <summary>
    /// Visits a registration snapshot so callbacks can load or unload controls during delivery.
    /// </summary>
    private void ForEachKeyboard(Action<SoftKeyboard> action)
    {
        foreach (var reference in _keyboards.ToArray())
        {
            if (reference.Target is SoftKeyboard keyboard)
            {
                action(keyboard);
            }
        }

        RemoveDeadReferences();
    }

    /// <summary>
    /// Prunes collected controls without extending page or template lifetimes.
    /// </summary>
    private void RemoveDeadReferences()
    {
        _keyboards.RemoveAll(static reference => !reference.IsAlive);
    }
}
