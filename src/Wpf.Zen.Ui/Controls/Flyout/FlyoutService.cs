// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Attaches a reusable <see cref="Flyout"/> to a framework element and optionally manages its
/// focus-and-click interaction lifetime.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="AttachedFlyoutProperty"/> supplies the panel without inserting it into the owner's layout
/// or logical tree. Call <see cref="ShowAttachedFlyout"/> for an explicit trigger, or enable
/// <see cref="ShowOnFocusOrClickProperty"/> to open when keyboard focus enters the owner's subtree or the
/// owner completes a primary-button click. Focus leaving both the owner and panel closes the flyout.
/// </para>
/// <para>
/// An automatically triggered editor flyout normally sets <see cref="Flyout.FocusOnOpen"/> to
/// <see langword="false"/> so typing remains in the editor. Leave it enabled for a picker whose commands
/// should receive keyboard focus. One flyout may be shared by multiple owners; delayed focus callbacks
/// verify the current placement target so an owner losing focus cannot close a panel already retargeted
/// by another owner.
/// </para>
/// </remarks>
public static class FlyoutService
{
    static FlyoutService()
    {
        // Once opening transfers focus into a detached Popup, the owner has already raised its only
        // LostKeyboardFocus event. Observe Flyout instances process-wide so a later transition from
        // panel content to an unrelated element can complete the attached focus lifetime as well.
        EventManager.RegisterClassHandler(
            typeof(Flyout),
            Keyboard.LostKeyboardFocusEvent,
            new KeyboardFocusChangedEventHandler(OnFlyoutLostKeyboardFocus),
            handledEventsToo: true
        );
    }

    /// <summary>
    /// Identifies the attached flyout associated with a framework element.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="null"/>. The property does not inherit and does not establish WPF
    /// logical ownership, allowing the flyout to create its own popup host when shown. Replacing or clearing
    /// the value closes the old flyout only when it is currently anchored to this owner.
    /// </remarks>
    public static readonly DependencyProperty AttachedFlyoutProperty =
        DependencyProperty.RegisterAttached(
            "AttachedFlyout",
            typeof(Flyout),
            typeof(FlyoutService),
            new FrameworkPropertyMetadata(null, OnAttachedFlyoutChanged)
        );

    /// <summary>
    /// Gets the flyout associated with <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element from which to read the association.</param>
    /// <returns>The associated flyout, or <see langword="null"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static Flyout? GetAttachedFlyout(FrameworkElement element)
    {
        return (Flyout?)element.GetValue(AttachedFlyoutProperty);
    }

    /// <summary>
    /// Associates a flyout with <paramref name="element"/>.
    /// </summary>
    /// <param name="element">The element that will anchor the flyout.</param>
    /// <param name="value">The flyout to associate, or <see langword="null"/> to remove it.</param>
    public static void SetAttachedFlyout(FrameworkElement element, Flyout? value)
    {
        element.SetValue(AttachedFlyoutProperty, value);
    }

    /// <summary>
    /// Identifies the attached property that enables focus-or-click opening and focus-scope closing.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="false"/>. Enabling it subscribes only to the annotated element's
    /// keyboard-focus and primary mouse-button events. Pointer opening occurs after mouse-up so an editor
    /// has released the temporary capture it uses to position the caret or update selection. Opening is then
    /// deferred to input priority so the initiating input can finish before <see cref="Flyout.FocusOnOpen"/>
    /// optionally transfers focus into the popup. A secondary-button click is deliberately ignored so WPF can open
    /// the owner's context menu. Disabling this behavior removes its subscriptions and closes a flyout
    /// currently anchored to the element.
    /// </remarks>
    public static readonly DependencyProperty ShowOnFocusOrClickProperty =
        DependencyProperty.RegisterAttached(
            "ShowOnFocusOrClick",
            typeof(bool),
            typeof(FlyoutService),
            new FrameworkPropertyMetadata(false, OnShowOnFocusOrClickChanged)
        );

    /// <summary>
    /// Gets whether keyboard focus or a primary-button click automatically opens the attached flyout.
    /// </summary>
    /// <param name="element">The element from which to read the behavior.</param>
    /// <returns><see langword="true"/> when focus-or-click opening is enabled.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static bool GetShowOnFocusOrClick(FrameworkElement element)
    {
        return (bool)element.GetValue(ShowOnFocusOrClickProperty);
    }

    /// <summary>
    /// Enables or disables focus-or-click opening and focus-scope closing for the attached flyout.
    /// </summary>
    /// <param name="element">The element whose input and focus lifetime should be observed.</param>
    /// <param name="value"><see langword="true"/> to enable automatic opening and closing.</param>
    public static void SetShowOnFocusOrClick(FrameworkElement element, bool value)
    {
        element.SetValue(ShowOnFocusOrClickProperty, value);
    }

    /// <summary>
    /// Opens the flyout associated with <paramref name="element"/> and anchors it to that element.
    /// </summary>
    /// <param name="element">The owner and placement target.</param>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="element"/> has no <see cref="AttachedFlyoutProperty"/> value.
    /// </exception>
    public static void ShowAttachedFlyout(FrameworkElement element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        var flyout = GetAttachedFlyout(element)
                     ?? throw new InvalidOperationException("The element has no attached Flyout.");

        flyout.ShowAt(element);
    }

    /// <summary>
    /// Closes the flyout associated with <paramref name="element"/> when it is anchored to that element.
    /// </summary>
    /// <param name="element">The owner whose attached flyout should close.</param>
    /// <remarks>
    /// The placement-target check preserves a shared flyout that another owner has opened since this owner
    /// last used it. Calling this method when no flyout is attached has no effect.
    /// </remarks>
    public static void HideAttachedFlyout(FrameworkElement element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (GetAttachedFlyout(element) is { } flyout
            && ReferenceEquals(flyout.PlacementTarget, element))
        {
            flyout.Hide();
        }
    }

    private static void OnAttachedFlyoutChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if (e.OldValue is Flyout oldFlyout
            && ReferenceEquals(oldFlyout.PlacementTarget, element))
        {
            oldFlyout.Hide();
        }

        if (GetShowOnFocusOrClick(element) && element.IsKeyboardFocusWithin)
        {
            QueueOpen(element, (Flyout?)e.NewValue, requireKeyboardFocusWithin: true);
        }
    }

    private static void OnShowOnFocusOrClickChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not FrameworkElement element)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            element.GotKeyboardFocus  += OnOwnerGotKeyboardFocus;
            element.LostKeyboardFocus += OnOwnerLostKeyboardFocus;
            element.AddHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnOwnerMouseUp),
                handledEventsToo: true
            );

            if (element.IsKeyboardFocusWithin)
            {
                QueueOpen(element, GetAttachedFlyout(element), requireKeyboardFocusWithin: true);
            }
        }
        else
        {
            element.GotKeyboardFocus  -= OnOwnerGotKeyboardFocus;
            element.LostKeyboardFocus -= OnOwnerLostKeyboardFocus;
            element.RemoveHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnOwnerMouseUp)
            );
            HideAttachedFlyout(element);
        }
    }

    private static void OnOwnerGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var flyout = GetAttachedFlyout(element);

        // Escape and explicit dismissal may restore focus from the popup. Reopening during that same
        // focus transition would make an automatically triggered flyout impossible to dismiss from the keyboard.
        if (flyout is null
            || IsFocusWithin(flyout, e.OldFocus)
            || IsAnyMouseButtonPressed())
        {
            // Text editors acquire focus during mouse-down while retaining capture for caret and
            // selection work. WPF Popup.EstablishPopupCapture deliberately refuses to replace a
            // non-popup capture. The matching MouseUp handler opens after that temporary capture has
            // been released, allowing StaysOpen=false to observe and dismiss later outside clicks.
            return;
        }

        QueueOpen(element, flyout, requireKeyboardFocusWithin: true);
    }

    private static void OnOwnerMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        var element = (FrameworkElement)sender;
        var flyout = GetAttachedFlyout(element);
        if (flyout is null || flyout.IsOpen)
        {
            // Opening is idempotent. In particular, do not retarget or rebuild an open popup when its
            // placement owner is clicked; PopupDismissal keeps that owner gesture from becoming a
            // close-and-reopen race for StaysOpen=false popups.
            return;
        }

        // MouseUp bubbles after TextBox and ButtonBase have completed their control-specific gesture
        // and released capture. The dispatcher hop also lets remaining application handlers finish
        // before FocusOnOpen=true moves keyboard focus into the detached popup source.
        QueueOpen(element, flyout, requireKeyboardFocusWithin: false);
    }

    private static void OnOwnerLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var flyout = GetAttachedFlyout(element);
        if (flyout is null)
        {
            return;
        }

        QueueCloseWhenFocusLeaves(element, flyout);
    }

    private static void OnFlyoutLostKeyboardFocus(
        object sender,
        KeyboardFocusChangedEventArgs e
    )
    {
        var flyout = (Flyout)sender;
        if (flyout.PlacementTarget is FrameworkElement element
            && GetShowOnFocusOrClick(element)
            && ReferenceEquals(GetAttachedFlyout(element), flyout))
        {
            QueueCloseWhenFocusLeaves(element, flyout);
        }
    }

    private static void QueueCloseWhenFocusLeaves(FrameworkElement element, Flyout flyout)
    {
        // WPF reports the owner's loss before Flyout.OnPopupOpened completes its optional focus move.
        // It likewise reports the panel's loss before a nested ContextMenu finishes taking focus.
        // Evaluate at input priority so both transitions count as staying in the attached scope.
        _ = element.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (GetShowOnFocusOrClick(element)
                    && ReferenceEquals(GetAttachedFlyout(element), flyout)
                    && ReferenceEquals(flyout.PlacementTarget, element)
                    && flyout.IsOpen
                    && !element.IsKeyboardFocusWithin
                    && !flyout.IsKeyboardFocusWithin
                    && !IsFocusWithinOwnedContextMenu(element, flyout))
                {
                    flyout.Hide();
                }
            })
        );
    }

    private static bool IsFocusWithinOwnedContextMenu(
        FrameworkElement element,
        Flyout flyout
    )
    {
        if (Keyboard.FocusedElement is not DependencyObject focusedElement)
        {
            return false;
        }

        var contextMenu = FindVisualOrLogicalAncestor<ContextMenu>(focusedElement);
        if (contextMenu?.PlacementTarget is not UIElement contextTarget)
        {
            return false;
        }

        return ReferenceEquals(contextTarget, element)
               || element.IsAncestorOf(contextTarget)
               || ReferenceEquals(contextTarget, flyout)
               || flyout.IsAncestorOf(contextTarget);
    }

    private static void QueueOpen(
        FrameworkElement element,
        Flyout? flyout,
        bool requireKeyboardFocusWithin
    )
    {
        if (flyout is null)
        {
            return;
        }

        _ = element.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (GetShowOnFocusOrClick(element)
                    && ReferenceEquals(GetAttachedFlyout(element), flyout)
                    && (!requireKeyboardFocusWithin || element.IsKeyboardFocusWithin)
                    && !flyout.IsOpen)
                {
                    flyout.ShowAt(element);
                }
            })
        );
    }

    private static bool IsFocusWithin(Flyout flyout, IInputElement? focus)
    {
        return focus is UIElement element
               && (ReferenceEquals(element, flyout) || flyout.IsAncestorOf(element));
    }

    private static bool IsAnyMouseButtonPressed()
    {
        return Mouse.LeftButton == MouseButtonState.Pressed
               || Mouse.RightButton == MouseButtonState.Pressed
               || Mouse.MiddleButton == MouseButtonState.Pressed
               || Mouse.XButton1 == MouseButtonState.Pressed
               || Mouse.XButton2 == MouseButtonState.Pressed;
    }

    private static T? FindVisualOrLogicalAncestor<T>(DependencyObject? current)
        where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T result)
            {
                return result;
            }

            current = current is Visual or Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
