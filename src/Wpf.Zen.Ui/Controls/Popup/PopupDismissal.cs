// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides attached behavior for refining the outside-click dismissal area of a non-sticky
/// <see cref="Popup"/>.
/// </summary>
/// <remarks>
/// <para>
/// WPF implements <see cref="Popup.StaysOpen"/> set to <see langword="false"/> by capturing the mouse to the
/// Popup's private root. Because a Popup uses a separate presentation source, its placement target and other owner
/// elements are outside that captured subtree and are therefore treated like unrelated outside content.
/// </para>
/// <para>
/// Set <see cref="ExcludedElementProperty"/> on a Popup to an owner element whose rendered bounds should not dismiss
/// it. When a press occurs in those bounds, this behavior temporarily releases WPF's auto-close capture so the real
/// owner child receives its normal mouse input, then restores non-sticky capture after the matching release. Presses
/// elsewhere retain WPF's standard dismissal behavior. Sticky Popups are not modified.
/// </para>
/// </remarks>
public static class PopupDismissal
{
    /// <summary>
    /// Identifies the attached property that specifies an owner element excluded from outside-click dismissal.
    /// </summary>
    /// <remarks>
    /// The property is intended for <see cref="Popup"/> instances whose <see cref="Popup.StaysOpen"/> value is
    /// <see langword="false"/>. A <see langword="null"/> value disables the behavior. The element may belong to a
    /// different presentation source from the Popup; screen coordinates are used to bridge the two HWNDs.
    /// </remarks>
    public static readonly DependencyProperty ExcludedElementProperty = DependencyProperty.RegisterAttached(
        "ExcludedElement",
        typeof(UIElement),
        typeof(PopupDismissal),
        new PropertyMetadata(null, OnExcludedElementChanged)
    );

    private static readonly DependencyProperty ControllerProperty = DependencyProperty.RegisterAttached(
        "Controller",
        typeof(DismissalController),
        typeof(PopupDismissal),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets the owner element that should not dismiss the specified Popup when clicked.
    /// </summary>
    /// <param name="element">The Popup from which to read the attached property.</param>
    /// <returns>The excluded owner element, or <see langword="null"/> when the behavior is disabled.</returns>
    [AttachedPropertyBrowsableForType(typeof(Popup))]
    public static UIElement? GetExcludedElement(DependencyObject element)
    {
        return (UIElement?)element.GetValue(ExcludedElementProperty);
    }

    /// <summary>
    /// Sets the owner element that should not dismiss the specified Popup when clicked.
    /// </summary>
    /// <param name="element">The Popup on which to set the attached property.</param>
    /// <param name="value">The excluded owner element, or <see langword="null"/> to disable the behavior.</param>
    public static void SetExcludedElement(DependencyObject element, UIElement? value)
    {
        element.SetValue(ExcludedElementProperty, value);
    }

    private static void OnExcludedElementChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Popup popup)
        {
            return;
        }

        if (popup.GetValue(ControllerProperty) is DismissalController previousController)
        {
            previousController.Detach();
            popup.ClearValue(ControllerProperty);
        }

        if (e.NewValue is UIElement excludedElement)
        {
            var controller = new DismissalController(popup, excludedElement);
            popup.SetValue(ControllerProperty, controller);
            controller.Attach();
        }
    }

    private sealed class DismissalController
    {
        private readonly Popup _popup;
        private readonly UIElement _excludedElement;
        private bool _isAutoCloseSuppressed;

        public DismissalController(Popup popup, UIElement excludedElement)
        {
            _popup = popup;
            _excludedElement = excludedElement;
        }

        public void Attach()
        {
            _popup.AddHandler(
                Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OnPreviewMouseDownOutsideCapturedElement),
                handledEventsToo: true
            );
            _popup.Closed += OnPopupClosed;
            _excludedElement.AddHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnExcludedElementMouseUp),
                handledEventsToo: true
            );
        }

        public void Detach()
        {
            _popup.RemoveHandler(
                Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OnPreviewMouseDownOutsideCapturedElement)
            );
            _popup.Closed -= OnPopupClosed;
            _excludedElement.RemoveHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(OnExcludedElementMouseUp)
            );
            RestoreAutoClose();
        }

        private void OnPreviewMouseDownOutsideCapturedElement(object sender, MouseButtonEventArgs e)
        {
            if (!_popup.IsOpen || _popup.StaysOpen || !IsPointerWithinExcludedElement())
            {
                return;
            }

            // Popup.OnStaysOpenChanged releases PopupRoot capture when this value becomes true. This notification is
            // raised before WPF routes the original raw mouse-down, so that event can then target the real owner child
            // and preserve native TextBox caret, selection, and ButtonBase behavior. The value is restored on MouseUp.
            _isAutoCloseSuppressed = true;
            InputManager.Current.PostProcessInput += OnPostProcessInput;
            _popup.SetCurrentValue(Popup.StaysOpenProperty, true);
        }

        private void OnExcludedElementMouseUp(object sender, MouseButtonEventArgs e)
        {
            RestoreAutoClose();
        }

        private void OnPopupClosed(object? sender, EventArgs e)
        {
            // Application code may close the Popup during the owner press. Restore the declared mode immediately so
            // its next opening can establish WPF's ordinary outside-click capture.
            RestoreAutoClose();
        }

        private void OnPostProcessInput(object sender, ProcessInputEventArgs e)
        {
            // The routed owner MouseUp is the ordinary restoration path. This input-manager fallback covers a press
            // that starts in the excluded element but is dragged and released outside its routed-event subtree.
            if (
                e.StagingItem.Input is MouseButtonEventArgs { RoutedEvent: var routedEvent }
                && routedEvent == Mouse.MouseUpEvent
            )
            {
                RestoreAutoClose();
            }
        }

        private bool IsPointerWithinExcludedElement()
        {
            if (
                !_excludedElement.IsVisible
                || _excludedElement.RenderSize.Width <= 0D
                || _excludedElement.RenderSize.Height <= 0D
                || PresentationSource.FromVisual(_excludedElement) == null
                || !PInvoke.GetCursorPos(out var cursorPosition)
            )
            {
                return false;
            }

            try
            {
                // MouseEventArgs.GetPosition cannot cross from PopupRoot's HWND back to the owner reliably while the
                // Popup owns capture. PointFromScreen first converts the physical cursor position through the owner's
                // presentation source and then accounts for transforms between that source and the excluded element.
                Point localPosition = _excludedElement.PointFromScreen(
                    new Point(cursorPosition.X, cursorPosition.Y)
                );
                return new Rect(_excludedElement.RenderSize).Contains(localPosition);
            }
            catch (InvalidOperationException)
            {
                // The element can leave its presentation source between the source check and coordinate conversion.
                // Treat that race as an ordinary outside click and let WPF dismiss the Popup.
                return false;
            }
        }

        private void RestoreAutoClose()
        {
            if (!_isAutoCloseSuppressed)
            {
                return;
            }

            _isAutoCloseSuppressed = false;
            InputManager.Current.PostProcessInput -= OnPostProcessInput;

            // SetCurrentValue preserves a template binding or other expression. Suppression is entered only while the
            // effective value is false, so restoring false returns ownership of outside-click capture to WPF.
            _popup.SetCurrentValue(Popup.StaysOpenProperty, false);
        }
    }
}
