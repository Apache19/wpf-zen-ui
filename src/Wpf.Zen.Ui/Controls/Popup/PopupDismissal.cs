// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Coordinates owner-click dismissal for <see cref="Popup"/> and <see cref="ContextMenu"/>
/// instances.
/// </summary>
/// <remarks>
/// <para>
/// WPF closes a non-sticky popup on the mouse-down that occurs outside its private presentation
/// source. An owner control normally opens the popup from a mouse-up or <see cref="ButtonBase.Click"/>,
/// so the same physical gesture can close the popup and immediately open it again. WPF's native
/// <see cref="DatePicker"/> avoids that race with a control-local flag; this behavior applies the
/// equivalent input policy once for every popup surface in the library.
/// </para>
/// <para>
/// The effective owner is <see cref="ExcludedElementProperty"/> when explicitly supplied, otherwise
/// the popup or context menu placement target. Ordinary popups temporarily suspend auto-close so the
/// owner receives its complete click and performs the final close itself. Context menus own menu-mode
/// capture and cannot release it without closing, so a one-shot input shield consumes the remainder
/// of the owner gesture after MenuBase performs its normal mouse-down dismissal. Both paths prevent
/// the release phase from observing a closed surface and treating the same gesture as a new opening.
/// </para>
/// </remarks>
public static class PopupDismissal
{
    /// <summary>
    /// Identifies the attached property that overrides the owner element used for coordinated popup
    /// dismissal.
    /// </summary>
    /// <remarks>
    /// When this property is <see langword="null"/>, <see cref="Popup.PlacementTarget"/> or
    /// <see cref="ContextMenu.PlacementTarget"/> is used automatically. Supply a value when the
    /// placement anchor is smaller than, or otherwise differs from, the control that owns the opening
    /// interaction. Screen coordinates are used because the owner and popup have different
    /// presentation sources.
    /// </remarks>
    public static readonly DependencyProperty ExcludedElementProperty = DependencyProperty.RegisterAttached(
        "ExcludedElement",
        typeof(UIElement),
        typeof(PopupDismissal),
        new PropertyMetadata(null)
    );

    private static readonly DependencyProperty PopupGestureControllerProperty =
        DependencyProperty.RegisterAttached(
            "PopupGestureController",
            typeof(PopupGestureController),
            typeof(PopupDismissal),
            new PropertyMetadata(null)
        );

    private static readonly DependencyProperty ContextMenuGestureControllerProperty =
        DependencyProperty.RegisterAttached(
            "ContextMenuGestureController",
            typeof(ContextMenuGestureController),
            typeof(PopupDismissal),
            new PropertyMetadata(null)
        );

    private static readonly DependencyProperty ContextMenuOwnerSnapshotProperty =
        DependencyProperty.RegisterAttached(
            "ContextMenuOwnerSnapshot",
            typeof(WeakReference<UIElement>),
            typeof(PopupDismissal),
            new PropertyMetadata(null)
        );

    static PopupDismissal()
    {
        // These class handlers run for detached popup presentation sources as well as popups declared
        // in library templates. Registering once avoids requiring every popup-owning control to mirror
        // DatePicker's private _disablePopupReopen state machine.
        EventManager.RegisterClassHandler(
            typeof(Popup),
            Mouse.PreviewMouseDownOutsideCapturedElementEvent,
            new MouseButtonEventHandler(OnPopupPreviewMouseDownOutsideCapturedElement),
            handledEventsToo: true
        );

        // MenuBase closes a ContextMenu during this outside-capture notification. Arm a one-shot owner
        // shield in the same event route: MouseDevice has not yet routed the original raw mouse-down to
        // its real hit target, regardless of the ordering of individual framework class handlers.
        EventManager.RegisterClassHandler(
            typeof(ContextMenu),
            ContextMenu.OpenedEvent,
            new RoutedEventHandler(OnContextMenuOpened),
            handledEventsToo: true
        );
        EventManager.RegisterClassHandler(
            typeof(ContextMenu),
            Mouse.PreviewMouseDownOutsideCapturedElementEvent,
            new MouseButtonEventHandler(OnContextMenuPreviewMouseDownOutsideCapturedElement),
            handledEventsToo: true
        );
    }

    /// <summary>
    /// Ensures that the process-wide class handlers have been registered.
    /// </summary>
    /// <remarks>
    /// The library initializer calls this method before controls are used. It is intentionally empty;
    /// invoking it forces the CLR to run this type's static constructor exactly once.
    /// </remarks>
    internal static void Initialize() { }

    /// <summary>
    /// Gets the explicit owner element used to coordinate dismissal of the specified popup surface.
    /// </summary>
    /// <param name="element">The popup or context menu from which to read the property.</param>
    /// <returns>
    /// The explicit owner, or <see langword="null"/> when the surface should use its placement target.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    [AttachedPropertyBrowsableForType(typeof(ContextMenu))]
    public static UIElement? GetExcludedElement(DependencyObject element)
    {
        return (UIElement?)element.GetValue(ExcludedElementProperty);
    }

    /// <summary>
    /// Sets an explicit owner element used to coordinate dismissal of the specified popup surface.
    /// </summary>
    /// <param name="element">The popup or context menu on which to set the property.</param>
    /// <param name="value">
    /// The explicit owner, or <see langword="null"/> to use the surface's placement target.
    /// </param>
    public static void SetExcludedElement(DependencyObject element, UIElement? value)
    {
        element.SetValue(ExcludedElementProperty, value);
    }

    private static void OnPopupPreviewMouseDownOutsideCapturedElement(
        object sender,
        MouseButtonEventArgs e
    )
    {
        if (
            sender is not Popup { IsOpen: true, StaysOpen: false } popup
            || popup.GetValue(PopupGestureControllerProperty) is PopupGestureController
            || !TryGetOwnerUnderPointer(popup, popup.PlacementTarget, out UIElement owner)
        )
        {
            return;
        }

        var controller = new PopupGestureController(popup, owner);
        popup.SetValue(PopupGestureControllerProperty, controller);
        controller.Begin();
    }

    private static void OnContextMenuPreviewMouseDownOutsideCapturedElement(
        object sender,
        MouseButtonEventArgs e
    )
    {
        if (
            sender is not ContextMenu contextMenu
            || (e.ChangedButton != MouseButton.Left && e.ChangedButton != MouseButton.Right)
            || contextMenu.GetValue(ContextMenuGestureControllerProperty)
                is ContextMenuGestureController
            || !TryGetContextMenuOwnerUnderPointer(contextMenu, out UIElement owner)
        )
        {
            return;
        }

        var controller = new ContextMenuGestureController(contextMenu, owner, e.ChangedButton);
        contextMenu.SetValue(ContextMenuGestureControllerProperty, controller);
        controller.Begin();
    }

    private static void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (sender is not ContextMenu contextMenu)
        {
            return;
        }

        UIElement? owner = GetExcludedElement(contextMenu) ?? contextMenu.PlacementTarget;
        contextMenu.SetValue(
            ContextMenuOwnerSnapshotProperty,
            owner is null ? null : new WeakReference<UIElement>(owner)
        );
    }

    private static bool TryGetContextMenuOwnerUnderPointer(
        ContextMenu contextMenu,
        out UIElement owner
    )
    {
        UIElement? candidate = GetExcludedElement(contextMenu);

        // PopupControlService can clear or re-coerce PlacementTarget while MenuBase is processing
        // the outside-capture notification. Snapshot it at Opened so the later class handler still
        // knows which owner is about to receive the retargeted raw input. A weak reference avoids
        // extending the lifetime of a service-owned placement target when a ContextMenu is shared.
        if (
            candidate is null
            && contextMenu.GetValue(ContextMenuOwnerSnapshotProperty)
                is WeakReference<UIElement> ownerReference
        )
        {
            _ = ownerReference.TryGetTarget(out candidate);
        }

        candidate ??= contextMenu.PlacementTarget;
        candidate ??=
            contextMenu.ReadLocalValue(ContextMenu.PlacementTargetProperty) as UIElement;
        if (candidate is null || !IsPointerWithin(candidate))
        {
            owner = null!;
            return false;
        }

        owner = candidate;
        return true;
    }

    private static bool TryGetOwnerUnderPointer(
        DependencyObject popupSurface,
        UIElement? placementTarget,
        out UIElement owner
    )
    {
        UIElement? candidate = GetExcludedElement(popupSurface) ?? placementTarget;
        if (candidate is null || !IsPointerWithin(candidate))
        {
            owner = null!;
            return false;
        }

        owner = candidate;
        return true;
    }

    private static bool IsPointerWithin(UIElement element)
    {
        if (
            !element.IsVisible
            || element.RenderSize.Width <= 0D
            || element.RenderSize.Height <= 0D
            || PresentationSource.FromVisual(element) == null
            || !PInvoke.GetCursorPos(out var cursorPosition)
        )
        {
            return false;
        }

        try
        {
            // MouseEventArgs.GetPosition remains relative to the captured PopupRoot/MenuBase. Native
            // cursor coordinates bridge that HWND boundary and are transformed through the owner's
            // own presentation source.
            Point localPosition = element.PointFromScreen(
                new Point(cursorPosition.X, cursorPosition.Y)
            );
            return new Rect(element.RenderSize).Contains(localPosition);
        }
        catch (InvalidOperationException)
        {
            // The owner can leave its presentation source between the source check and conversion.
            // Treat the race as an ordinary outside click and preserve WPF's default dismissal path.
            return false;
        }
    }

    private sealed class PopupGestureController
    {
        private readonly Popup _popup;
        private readonly UIElement _owner;
        private bool _isActive;

        public PopupGestureController(Popup popup, UIElement owner)
        {
            _popup = popup;
            _owner = owner;
        }

        public void Begin()
        {
            _isActive = true;
            _popup.Closed += OnPopupClosed;
            _owner.AddHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnOwnerMouseUp),
                handledEventsToo: true
            );
            InputManager.Current.PostProcessInput += OnPostProcessInput;

            // Popup.OnStaysOpenChanged releases PopupRoot capture before WPF routes the original raw
            // mouse-down. SetCurrentValue retains any binding or template expression.
            _popup.SetCurrentValue(Popup.StaysOpenProperty, true);
        }

        private void OnOwnerMouseUp(object sender, MouseButtonEventArgs e)
        {
            Complete();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            // The owner click commonly closes the popup before this controller sees MouseUp. Restore
            // the declared non-sticky mode now so a later opening can establish ordinary capture.
            Complete();
        }

        private void OnPostProcessInput(object sender, ProcessInputEventArgs e)
        {
            // The routed owner MouseUp is the normal path. This fallback also handles a press that is
            // dragged and released outside the owner's event route.
            if (
                e.StagingItem.Input is MouseButtonEventArgs { RoutedEvent: var routedEvent }
                && routedEvent == Mouse.MouseUpEvent
            )
            {
                Complete();
            }
        }

        private void Complete()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _popup.Closed -= OnPopupClosed;
            _owner.RemoveHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnOwnerMouseUp)
            );
            InputManager.Current.PostProcessInput -= OnPostProcessInput;

            _popup.ClearValue(PopupGestureControllerProperty);

            // Suppression begins only when the effective value is false; restoring false returns
            // ownership of outside-click capture without replacing a binding expression.
            _popup.SetCurrentValue(Popup.StaysOpenProperty, false);
        }
    }

    private sealed class ContextMenuGestureController
    {
        private readonly ContextMenu _contextMenu;
        private readonly UIElement _owner;
        private readonly MouseButton _button;
        private bool _isActive;

        public ContextMenuGestureController(
            ContextMenu contextMenu,
            UIElement owner,
            MouseButton button
        )
        {
            _contextMenu = contextMenu;
            _owner = owner;
            _button = button;
        }

        public void Begin()
        {
            _isActive = true;
            _owner.AddHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(OnOwnerPreviewMouseDown),
                handledEventsToo: true
            );
            _owner.AddHandler(
                Mouse.PreviewMouseUpEvent,
                new MouseButtonEventHandler(OnOwnerPreviewMouseUp),
                handledEventsToo: true
            );
            InputManager.Current.PostProcessInput += OnPostProcessInput;
        }

        private void OnOwnerPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == _button)
            {
                // MouseDevice raises the outside-capture notification before it routes the original
                // raw mouse-down. MenuBase has closed and released capture by now, so consume that
                // retargeted event before ButtonBase can enter a pressed state.
                e.Handled = true;
            }
        }

        private void OnOwnerPreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != _button)
            {
                return;
            }

            // The matching release must also be consumed: PopupControlService opens native context
            // menus from right-button release, while ButtonBase completes left-button activation there.
            e.Handled = true;
            Complete();
        }

        private void OnPostProcessInput(object sender, ProcessInputEventArgs e)
        {
            if (
                e.StagingItem.Input is MouseButtonEventArgs mouseEvent
                && mouseEvent.RoutedEvent == Mouse.MouseUpEvent
                && mouseEvent.ChangedButton == _button
            )
            {
                // A drag can release outside the owner's routed-event subtree. Do not retain the
                // context menu or owner through a gesture that has already ended.
                Complete();
            }
        }

        private void Complete()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _owner.RemoveHandler(
                Mouse.PreviewMouseDownEvent,
                new MouseButtonEventHandler(OnOwnerPreviewMouseDown)
            );
            _owner.RemoveHandler(
                Mouse.PreviewMouseUpEvent,
                new MouseButtonEventHandler(OnOwnerPreviewMouseUp)
            );
            InputManager.Current.PostProcessInput -= OnPostProcessInput;
            _contextMenu.ClearValue(ContextMenuGestureControllerProperty);
        }
    }
}
