// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a control that allows a user to pick a date from a calendar display.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:CalendarDatePicker /&gt;
/// </code>
/// </example>
public class CalendarDatePicker : ToggleButton
{
    private Popup? _popup;
    private SymbolIcon? _defaultIcon;

    /// <summary>
    /// Identifies the <see cref="IsCalendarOpen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsCalendarOpenProperty = DependencyProperty.Register(
        nameof(IsCalendarOpen),
        typeof(bool),
        typeof(CalendarDatePicker),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault
        )
    );

    /// <summary>Identifies the <see cref="IsTodayHighlighted"/> dependency property.</summary>
    public static readonly DependencyProperty IsTodayHighlightedProperty = DependencyProperty.Register(
        nameof(IsTodayHighlighted),
        typeof(bool),
        typeof(CalendarDatePicker),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="Date"/> dependency property.</summary>
    public static readonly DependencyProperty DateProperty = DependencyProperty.Register(
        nameof(Date),
        typeof(DateTime?),
        typeof(CalendarDatePicker),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="FirstDayOfWeek"/> dependency property.</summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty = DependencyProperty.Register(
        nameof(FirstDayOfWeek),
        typeof(DayOfWeek),
        typeof(CalendarDatePicker),
        new PropertyMetadata(DateTimeHelper.GetCurrentDateFormat().FirstDayOfWeek)
    );

    static CalendarDatePicker()
    {
        IconProperty.OverrideMetadata(
            typeof(CalendarDatePicker),
            new FrameworkPropertyMetadata(null, null, CoerceIcon)
        );
        IsCheckedProperty.OverrideMetadata(
            typeof(CalendarDatePicker),
            new FrameworkPropertyMetadata(false, null, CoerceIsChecked)
        );
        IsThreeStateProperty.OverrideMetadata(
            typeof(CalendarDatePicker),
            new FrameworkPropertyMetadata(false, null, CoerceIsThreeState)
        );
    }

    private static object CoerceIsChecked(DependencyObject dependencyObject, object? baseValue)
    {
        // A calendar popup has only open and closed states. Normalize an externally supplied null instead of
        // allowing ToggleButton's indeterminate state to diverge from the non-nullable IsCalendarOpen property.
        return baseValue is true;
    }

    private static object CoerceIsThreeState(DependencyObject dependencyObject, object? baseValue)
    {
        // IsChecked is the interaction surface for IsCalendarOpen, whose bool contract cannot represent a third
        // state. Keeping IsThreeState false also preserves the ordinary click cycle: closed -> open -> closed.
        return false;
    }

    private static object? CoerceIcon(DependencyObject dependencyObject, object? baseValue)
    {
        return baseValue is null
            ? ((CalendarDatePicker)dependencyObject)._defaultIcon ??=
                new SymbolIcon(SymbolRegular.CalendarRtl24)
            : baseValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CalendarDatePicker"/> class.
    /// </summary>
    public CalendarDatePicker()
    {
        CoerceValue(IconProperty);

        // IsCalendarOpen is the public, non-nullable state exposed by the date picker. Keep WPF's nullable
        // ToggleButton state as the binding target so an application remains free to bind IsCalendarOpen without
        // replacing this internal synchronization binding. Metadata coercion keeps the inherited toggle state
        // strictly two-state so it cannot diverge from the bool property.
        _ = SetBinding(
            IsCheckedProperty,
            new Binding(nameof(IsCalendarOpen))
            {
                Source              = this,
                Mode                = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the current date is highlighted.
    /// </summary>
    public bool IsTodayHighlighted
    {
        get { return (bool)GetValue(IsTodayHighlightedProperty); }
        set { SetValue(IsTodayHighlightedProperty, value); }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the calendar popup is open.
    /// This property is synchronized bidirectionally with <see cref="System.Windows.Controls.Primitives.ToggleButton.IsChecked"/>
    /// and binds two-way by default.
    /// </summary>
    [Bindable(true)]
    public bool IsCalendarOpen
    {
        get => (bool)GetValue(IsCalendarOpenProperty);
        set => SetValue(IsCalendarOpenProperty, value);
    }

    /// <summary>
    /// Gets or sets the day that is considered the beginning of the week.
    /// </summary>
    public DayOfWeek FirstDayOfWeek
    {
        get { return (DayOfWeek)GetValue(FirstDayOfWeekProperty); }
        set { SetValue(FirstDayOfWeekProperty, value); }
    }

    /// <summary>
    /// Gets or sets the date currently set in the calendar picker.
    /// </summary>
    [Bindable(true)]
    public DateTime? Date
    {
        get => (DateTime?)GetValue(DateProperty);
        set => SetValue(DateProperty, value);
    }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        // Creating a Popup does not create its native window; WPF defers that work until IsOpen becomes true.
        // Initialize the managed Calendar, bindings, and popup behaviors here so the first pointer interaction
        // only has to open the window instead of also constructing and wiring the complete popup content tree.
        InitializePopup();
    }

    private void InitializePopup()
    {
        if (_popup is not null)
        {
            return;
        }

        var calendar = new System.Windows.Controls.Calendar();
        _ = calendar.SetBinding(
            System.Windows.Controls.Calendar.SelectedDateProperty,
            new Binding(nameof(Date))
            {
                Source              = this,
                Mode                = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );
        _ = calendar.SetBinding(
            System.Windows.Controls.Calendar.IsTodayHighlightedProperty,
            new Binding(nameof(IsTodayHighlighted))
            {
                Source              = this,
                Mode                = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );
        _ = calendar.SetBinding(
            System.Windows.Controls.Calendar.FirstDayOfWeekProperty,
            new Binding(nameof(FirstDayOfWeek))
            {
                Source              = this,
                Mode                = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );

        calendar.SelectedDatesChanged += OnSelectedDatesChanged;

        _popup = new Popup
        {
            PlacementTarget    = this,
            Placement          = PlacementMode.Bottom,
            Child              = calendar,
            Focusable          = false,
            StaysOpen          = false,
            VerticalOffset     = 1D,
            VerticalAlignment  = VerticalAlignment.Center,
            PopupAnimation     = PopupAnimation.None,
            AllowsTransparency = true,
        };

        _ = _popup.SetBinding(
            Popup.IsOpenProperty,
            new Binding(nameof(IsCalendarOpen))
            {
                Source              = this,
                Mode                = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );

        _popup.Opened += OnPopupOpened;

        PopupShadow.SetIsEnabled(_popup, true);
        PopupAnimator.SetIsEnabled(_popup, true);
    }

    /// <summary>
    /// Moves keyboard focus into the calendar after WPF has created and shown the popup window.
    /// </summary>
    /// <param name="sender">The popup that has finished opening.</param>
    /// <param name="e">The event data.</param>
    protected virtual void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }

        if (popup.Child is null)
        {
            return;
        }

        _ = popup.Focus();
        _ = Keyboard.Focus(popup.Child);
    }

    /// <summary>
    /// Closes the calendar after its selection changes.
    /// </summary>
    /// <param name="sender">The calendar whose selection changed.</param>
    /// <param name="e">The selection change data.</param>
    protected virtual void OnSelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (IsCalendarOpen)
        {
            SetCurrentValue(IsCalendarOpenProperty, false);
        }
    }
}
