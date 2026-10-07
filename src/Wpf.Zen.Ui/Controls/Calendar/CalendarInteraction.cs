// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Calendar = System.Windows.Controls.Calendar;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Supplies mouse press feedback for native Calendar day buttons during date selection.
/// </summary>
/// <remarks>
/// CalendarItem captures the mouse for dragging, which clears ButtonBase.IsPressed on the day.
/// This behavior tracks the gesture separately. Templates combine IsPointerPressed with the
/// native keyboard press state. Capture and selection remain owned by WPF.
/// </remarks>
public static class CalendarInteraction
{
    /// <summary>
    /// Identifies the IsEnabled attached property. The default is false.
    /// </summary>
    public static readonly DependencyProperty IsEnabledProperty
        = DependencyProperty.RegisterAttached(
            "IsEnabled", typeof(bool), typeof(CalendarInteraction),
            new PropertyMetadata(false, OnIsEnabledChanged)
        );

    /// <summary>
    /// Gets whether mouse feedback tracking is enabled on a Calendar.
    /// </summary>
    /// <param name="element">The Calendar that owns the date buttons.</param>
    /// <returns>True when the behavior is enabled.</returns>
    public static bool GetIsEnabled(DependencyObject element)
    {
        return (bool)element.GetValue(IsEnabledProperty);
    }

    /// <summary>
    /// Enables or detaches mouse feedback tracking on a Calendar.
    /// </summary>
    /// <param name="element">The Calendar that owns the date buttons.</param>
    /// <param name="value">True to track gestures; false to clear feedback and detach.</param>
    public static void SetIsEnabled(DependencyObject element, bool value)
    {
        element.SetValue(IsEnabledProperty, value);
    }

    private static readonly DependencyPropertyKey IsPointerPressedPropertyKey
        = DependencyProperty.RegisterAttachedReadOnly(
            "IsPointerPressed", typeof(bool), typeof(CalendarInteraction),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only mouse press flag on a date button. The default is false.
    /// </summary>
    public static readonly DependencyProperty IsPointerPressedProperty
        = IsPointerPressedPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether a date button is under the pointer during a gesture started on a valid date.
    /// </summary>
    /// <param name="element">The date button to inspect.</param>
    /// <returns>True while the button should display mouse press feedback.</returns>
    public static bool GetIsPointerPressed(DependencyObject element)
    {
        return (bool)element.GetValue(IsPointerPressedProperty);
    }

    private static readonly DependencyProperty TrackerProperty
        = DependencyProperty.RegisterAttached(
            "Tracker", typeof(Tracker), typeof(CalendarInteraction)
        );

    // Store one tracker per Calendar; all event subscriptions are scoped to that owner.
    private static void OnIsEnabledChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Calendar calendar)
        {
            return;
        }

        var previous = (Tracker?)calendar.GetValue(TrackerProperty);
        previous?.Detach();

        calendar.ClearValue(TrackerProperty);

        if ((bool)e.NewValue)
        {
            var tracker = new Tracker(calendar);
            calendar.SetValue(TrackerProperty, tracker);
        }
    }

    /// <summary>
    /// Tracks one gesture and one highlighted date, including capture transfer within the owner.
    /// </summary>
    internal sealed class Tracker
    {
        private readonly Calendar           _calendar;
        private          bool               _active;
        private          CalendarDayButton? _pressedDay;

        internal Tracker(Calendar calendar)
        {
            _calendar = calendar;

            calendar.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnMouseDown), true);
            calendar.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnMouseUp), true);
            calendar.AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(OnMouseMove), true);
            calendar.AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostCapture), true);

            calendar.MouseLeave       += OnMouseLeave;
            calendar.IsEnabledChanged += OnEnabledChanged;
            calendar.Unloaded         += OnUnloaded;
        }

        internal void Detach()
        {
            End();

            _calendar.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnMouseDown));
            _calendar.RemoveHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnMouseUp));
            _calendar.RemoveHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(OnMouseMove));
            _calendar.RemoveHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnLostCapture));

            _calendar.MouseLeave       -= OnMouseLeave;
            _calendar.IsEnabledChanged -= OnEnabledChanged;
            _calendar.Unloaded         -= OnUnloaded;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                var day = FindDay(e.OriginalSource as DependencyObject);
                Begin(day);
            }
        }

        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                End();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_active)
            {
                return;
            }

            // Capture can retarget OriginalSource to CalendarItem outside its bounds.
            var position = e.GetPosition(_calendar);
            var hit      = _calendar.InputHitTest(position) as DependencyObject;
            var day      = FindDay(hit);
            Move(day, e.LeftButton == MouseButtonState.Pressed);
        }

        private void OnLostCapture(object sender, MouseEventArgs e)
        {
            CaptureChanged(Mouse.Captured as DependencyObject);
        }

        private void OnMouseLeave(object sender, MouseEventArgs e)
        {
            Move(null, e.LeftButton == MouseButtonState.Pressed);
        }

        private void OnEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!_calendar.IsEnabled)
            {
                End();
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            End();
        }

        // Gesture transitions are separate from event decoding to exercise capture handoff in tests.
        internal void Begin(CalendarDayButton? day)
        {
            End();
            _active = IsValid(day);
            SetPressedDay(_active ? day : null);
        }

        internal void Move(CalendarDayButton? day, bool leftButtonDown)
        {
            if (!leftButtonDown)
            {
                End();
                return;
            }

            var valid = _active && IsValid(day);
            SetPressedDay(valid ? day : null);
        }

        internal void CaptureChanged(DependencyObject? captured)
        {
            // ButtonBase -> CalendarItem is the expected handoff, not a cancelled gesture.
            if (captured == null || !_calendar.IsAncestorOf(captured))
            {
                End();
            }
        }

        internal void End()
        {
            _active = false;
            SetPressedDay(null);
        }

        private bool IsValid(CalendarDayButton? day)
        {
            return _calendar.IsEnabled && day != null && day.IsEnabled && !day.IsBlackedOut
                && day.Visibility == Visibility.Visible && _calendar.IsAncestorOf(day);
        }

        // A single read-only flag feeds style triggers without replacing Interaction.State locally.
        private void SetPressedDay(CalendarDayButton? day)
        {
            if (_pressedDay == day)
            {
                return;
            }

            if (_pressedDay != null)
            {
                _pressedDay.IsEnabledChanged -= OnDayEnabledChanged;
                _pressedDay.Unloaded         -= OnUnloaded;
                _pressedDay.SetValue(IsPointerPressedPropertyKey, false);
            }

            _pressedDay = day;
            if (_pressedDay != null)
            {
                _pressedDay.IsEnabledChanged += OnDayEnabledChanged;
                _pressedDay.Unloaded         += OnUnloaded;
                _pressedDay.SetValue(IsPointerPressedPropertyKey, true);
            }
        }

        private void OnDayEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue)
            {
                End();
            }
        }

        private CalendarDayButton? FindDay(DependencyObject? element)
        {
            while (element != null && element != _calendar)
            {
                if (element is CalendarDayButton day)
                {
                    return day;
                }

                element = element is Visual
                    ? VisualTreeHelper.GetParent(element)
                    : LogicalTreeHelper.GetParent(element);
            }

            return null;
        }
    }
}