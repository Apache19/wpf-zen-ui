// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.AutomationPeers;

/// <summary>
/// Automation peer that exposes a <see cref="ContentDialog"/> as a standard modal window
/// for UI Automation clients.
/// </summary>
/// <remarks>
/// This peer maps dialog-specific behavior to the <see cref="IWindowProvider"/> pattern so
/// assistive technologies (screen readers, automation tools) perceive the <see cref="ContentDialog"/>
/// as a modal, non-resizable dialog window.
/// </remarks>
internal sealed class ContentDialogAutomationPeer : UIElementAutomationPeer, IWindowProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogAutomationPeer"/> class.
    /// </summary>
    /// <param name="owner">The associated <see cref="ContentDialog"/>.</param>
    public ContentDialogAutomationPeer(ContentDialog owner)
        : base(owner) { }

    /// <summary>
    /// Gets a value indicating whether the window is modal.
    /// Always <see langword="true"/> for <see cref="ContentDialog"/>.
    /// </summary>
    bool IWindowProvider.IsModal => true;

    /// <summary>
    /// Gets a value indicating whether the window is topmost.
    /// <see cref="ContentDialog"/> are treated as topmost for automation.
    /// </summary>
    bool IWindowProvider.IsTopmost => true;

    /// <summary>
    /// Gets the current interaction state of the dialog window for UI Automation.
    /// </summary>
    public WindowInteractionState InteractionState
    {
        get
        {
            if (Owner is ContentDialog dialog)
            {
                if (!dialog.IsLoaded || dialog.IsClosing || !dialog.IsOpen
                    || dialog.Dispatcher is { HasShutdownFinished: true } or { HasShutdownStarted: true })
                {
                    return WindowInteractionState.Closing;
                }
            }

            return WindowInteractionState.Running;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the window can be maximized.
    /// Always <see langword="false"/> for <see cref="ContentDialog"/>.
    /// </summary>
    public bool Maximizable => false;

    /// <summary>
    /// Gets a value indicating whether the window can be minimized.
    /// Always <see langword="false"/> for <see cref="ContentDialog"/>.
    /// </summary>
    public bool Minimizable => false;

    /// <summary>
    /// Gets the visual state of the window.
    /// <see cref="ContentDialog"/> report <see cref="WindowVisualState.Normal"/>.
    /// </summary>
    public WindowVisualState VisualState => WindowVisualState.Normal;

    /// <inheritdoc/>
    protected override string GetClassNameCore()
    {
        return nameof(ContentDialog);
    }

    /// <inheritdoc/>
    protected override string? GetNameCore()
    {
        if (Owner is ContentDialog dialog)
        {
            string name = AutomationProperties.GetName(dialog);
            return !string.IsNullOrEmpty(name) ? name : dialog.Title?.ToString() ?? string.Empty;
        }

        return base.GetNameCore();
    }

    /// <inheritdoc/>
    protected override AutomationControlType GetAutomationControlTypeCore()
    {
        return AutomationControlType.Window;
    }

#if NET48_OR_GREATER || NET5_0_OR_GREATER
    /// <inheritdoc/>
    protected override bool IsDialogCore()
    {
        return true;
    }
#endif

    /// <inheritdoc/>
    protected override bool IsControlElementCore()
    {
        return true;
    }

    /// <inheritdoc/>
    protected override bool IsContentElementCore()
    {
        return true;
    }

    /// <inheritdoc/>
    protected override bool IsKeyboardFocusableCore()
    {
        return Owner is ContentDialog { Focusable: true, IsEnabled: true };
    }

    /// <summary>
    /// Returns whether the dialog is currently offscreen. A dialog is considered offscreen when not loaded or not visible.
    /// </summary>
    protected override bool IsOffscreenCore()
    {
        return Owner is ContentDialog { IsLoaded: false } or { IsVisible: false };
    }

    /// <summary>
    /// Returns automation pattern implementations supported by this peer. Provides <see cref="IWindowProvider"/>.
    /// </summary>
    /// <param name="pattern">The requested automation pattern.</param>
    /// <returns>An object implementing the requested pattern or <see langword="null"/> when not supported.</returns>
    public override object? GetPattern(PatternInterface pattern)
    {
        // A provider must implement the interface associated with every advertised pattern.
        if (pattern is PatternInterface.Window)
        {
            return this;
        }

        return base.GetPattern(pattern);
    }

    /// <summary>
    /// Closes the associated <see cref="ContentDialog"/>.
    /// This is invoked by UI Automation clients through the <see cref="IWindowProvider"/> pattern.
    /// </summary>
    void IWindowProvider.Close()
    {
        if (Owner is ContentDialog dialog)
        {
            Dispatcher? dispatcher = dialog.Dispatcher;
            if (dispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
            {
                dispatcher.BeginInvoke(
                    () =>
                    {
                        if (dialog.IsDismissible)
                            dialog.Hide();
                    },
                    DispatcherPriority.Normal
                );
            }
        }
    }

    /// <summary>
    /// Sets the visual state of the window. Not supported for <see cref="ContentDialog"/>.
    /// </summary>
    void IWindowProvider.SetVisualState(WindowVisualState state)
    {
        // Not supported for this.
    }

    /// <summary>
    /// Waits for the dialog to become idle.
    /// Always returns <see langword="true"/> for <see cref="ContentDialog"/>.
    /// </summary>
    /// <param name="milliseconds">Maximum time to wait in milliseconds (ignored).</param>
    /// <returns>
    /// <see langword="true"/> if the dialog is idle or the operation completed;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool WaitForInputIdle(int milliseconds)
    {
        return true;
    }
}
