// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using Calendar = System.Windows.Controls.Calendar;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Edits a complete date and time using a native calendar and separate time fields. Popup changes
/// are applied only on confirmation.
/// </summary>
/// <remarks>
/// SelectedDateTime is the committed value; SelectedDate is its date projection. External changes
/// to the committed value while the popup is open
/// restart the editing transaction. Field edits preserve DateTime.Kind and unedited subsecond
/// precision without converting time zones.
/// Language defaults to an empty tag: labels use CurrentUICulture and date formatting uses
/// CurrentCulture.
/// A nonempty Language, whether set explicitly or inherited, selects the culture for both labels
/// and dates.
/// Combines Control and Calendar to manage complete date/time text and editing transactions
/// consistently.
/// </remarks>
[ContentProperty(nameof(BlackoutDates))]
[TemplatePart(Name = "PART_TextBox", Type = typeof(System.Windows.Controls.TextBox))]
[TemplatePart(Name = "PART_ClearButton", Type = typeof(System.Windows.Controls.Primitives.ButtonBase))]
[TemplatePart(Name = "PART_Button", Type = typeof(System.Windows.Controls.Primitives.ButtonBase))]
[TemplatePart(Name = "PART_Popup", Type = typeof(System.Windows.Controls.Primitives.Popup))]
[TemplatePart(Name = "PART_CalendarHost", Type = typeof(ContentControl))]
[TemplatePart(Name = "PART_Hour", Type = typeof(DateTimePickerTimeColumn))]
[TemplatePart(Name = "PART_Minute", Type = typeof(DateTimePickerTimeColumn))]
[TemplatePart(Name = "PART_Second", Type = typeof(DateTimePickerTimeColumn))]
public partial class DateTimePicker : Control
{
    private static readonly DependencyPropertyKey HasInputErrorPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(HasInputError), typeof(bool), typeof(DateTimePicker),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only dependency property that reports errors in the current input.
    /// </summary>
    public static readonly DependencyProperty HasInputErrorProperty
        = HasInputErrorPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the current input violates the control's rules. The default is false; an empty
    /// value is allowed.
    /// </summary>
    /// <remarks>
    /// Text editing checks parsing and date restrictions; popup editing checks the pending date and
    /// time fields.
    /// Editing updates this state; failed commits raise DateValidationError. Restoring or canceling
    /// input recalculates it.
    /// This state excludes binding and business validation errors. A false value does not imply a
    /// committed binding source value.
    /// </remarks>
    public bool HasInputError => (bool)GetValue(HasInputErrorProperty);

    private readonly Calendar                _calendar              = new();
    private readonly List<CalendarDateRange> _observedBlackoutDates = new();

    private bool _synchronizing;
    private bool _updatingText;
    private bool _updatingCalendar;

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty
        = Border.CornerRadiusProperty.AddOwner(
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

    /// <summary>
    /// Gets or sets the radius applied to the control chrome and its focus indicator.
    /// The property affects template rendering but does not change layout measurement.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>
    /// Identifies the dependency property that enables the clear button. The default is true.
    /// </summary>
    public static readonly DependencyProperty IsClearButtonEnabledProperty
        = DependencyProperty.Register(
            nameof(IsClearButtonEnabled), typeof(bool), typeof(DateTimePicker),
            new PropertyMetadata(true)
        );

    /// <summary>
    /// Gets or sets whether the clear button is available. The default template shows it when the
    /// editor has focus and text is nonempty.
    /// Clearing resets the committed date/time, date projection, and text, and cancels popup
    /// editing.
    /// </summary>
    public bool IsClearButtonEnabled
    {
        get => (bool)GetValue(IsClearButtonEnabledProperty);
        set => SetValue(IsClearButtonEnabledProperty, value);
    }

    /// <summary>
    /// Identifies the complete date/time dependency property. The default is null; bindings are
    /// two-way by default.
    /// </summary>
    public static readonly DependencyProperty SelectedDateTimeProperty
        = DependencyProperty.Register(
            nameof(SelectedDateTime), typeof(DateTime?), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateTimeChanged,
                CoerceSelectedDateTime
            )
        );

    /// <summary>
    /// Gets or sets the committed date/time. Blackout dates and values outside the allowed range
    /// throw ArgumentOutOfRangeException.
    /// </summary>
    public DateTime? SelectedDateTime
    {
        get => (DateTime?)GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    /// <summary>
    /// Identifies the committed value's date projection. The default is null; bindings are two-way
    /// by default.
    /// </summary>
    public static readonly DependencyProperty SelectedDateProperty
        = DependencyProperty.Register(
            nameof(SelectedDate), typeof(DateTime?), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedDateChanged,
                CoerceSelectedDate
            )
        );

    /// <summary>
    /// Gets or sets the date projection. Setting a date preserves the time, or uses midnight when
    /// empty; setting null clears the committed value.
    /// </summary>
    public DateTime? SelectedDate
    {
        get => (DateTime?)GetValue(SelectedDateProperty);
        set => SetValue(SelectedDateProperty, value);
    }

    private static readonly DependencyPropertyKey PendingDateTimePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(PendingDateTime), typeof(DateTime?), typeof(DateTimePicker),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only dependency property for the pending value.
    /// </summary>
    public static readonly DependencyProperty PendingDateTimeProperty
        = PendingDateTimePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the value being edited in the popup, or null while closed. This property does not write
    /// back to application data.
    /// </summary>
    public DateTime? PendingDateTime => (DateTime?)GetValue(PendingDateTimeProperty);

    /// <summary>
    /// Identifies the format used for display and exact parsing. The default is yyyy-MM-dd
    /// HH:mm:ss.
    /// </summary>
    public static readonly DependencyProperty DateTimeFormatProperty
        = DependencyProperty.Register(
            nameof(DateTimeFormat), typeof(string), typeof(DateTimePicker),
            new PropertyMetadata("yyyy-MM-dd HH:mm:ss", OnFormatChanged),
            IsValidFormat
        );

    /// <summary>
    /// Gets or sets the DateTime format string. Formatting uses the current culture unless a
    /// nonempty Language selects another culture.
    /// </summary>
    public string DateTimeFormat
    {
        get => (string)GetValue(DateTimeFormatProperty);
        set => SetValue(DateTimeFormatProperty, value);
    }

    /// <summary>
    /// Identifies the complete text dependency property. The default is an empty string; bindings
    /// are two-way by default.
    /// </summary>
    public static readonly DependencyProperty TextProperty
        = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnTextChanged
            )
        );

    /// <summary>
    /// Gets or sets the complete date/time text. External assignments are parsed immediately;
    /// editor input is parsed on Enter or focus loss.
    /// Invalid text raises DateValidationError and preserves the committed value. A failed
    /// focus-loss commit restores the committed text.
    /// Failed Enter or explicit commits retain text for correction. Draft text is retained while
    /// the popup is open.
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// Identifies the popup state dependency property. The default is false; bindings are two-way
    /// by default.
    /// </summary>
    public static readonly DependencyProperty IsDropDownOpenProperty
        = DependencyProperty.Register(
            nameof(IsDropDownOpen), typeof(bool), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsDropDownOpenChanged,
                CoerceIsDropDownOpen
            )
        );

    /// <summary>
    /// Gets or sets whether the popup is open. Opening starts a transaction; directly closing
    /// cancels it.
    /// </summary>
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>
    /// Identifies the calendar's displayed date. Each instance initially displays today.
    /// </summary>
    public static readonly DependencyProperty DisplayDateProperty
        = DependencyProperty.Register(
            nameof(DisplayDate), typeof(DateTime), typeof(DateTimePicker),
            new FrameworkPropertyMetadata(
                DateTime.MinValue,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnCalendarPropertyChanged
            )
        );

    /// <summary>
    /// Gets or sets the calendar's displayed date. Browsing months does not change the pending
    /// value.
    /// </summary>
    public DateTime DisplayDate
    {
        get => (DateTime)GetValue(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    /// <summary>
    /// Identifies the earliest selectable date. The default is null; the time component is ignored.
    /// </summary>
    public static readonly DependencyProperty DisplayDateStartProperty
        = DependencyProperty.Register(
            nameof(DisplayDateStart), typeof(DateTime?), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged, CoerceStart)
        );

    /// <summary>
    /// Gets or sets the earliest allowed date. The range must include the committed value and must
    /// not exceed the upper bound.
    /// </summary>
    public DateTime? DisplayDateStart
    {
        get => (DateTime?)GetValue(DisplayDateStartProperty);
        set => SetValue(DisplayDateStartProperty, value);
    }

    /// <summary>
    /// Identifies the latest selectable date. The default is null; the time component is ignored.
    /// </summary>
    public static readonly DependencyProperty DisplayDateEndProperty
        = DependencyProperty.Register(
            nameof(DisplayDateEnd), typeof(DateTime?), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged, CoerceEnd)
        );

    /// <summary>
    /// Gets or sets the latest allowed date. The range must include the committed value and must
    /// not precede the lower bound.
    /// </summary>
    public DateTime? DisplayDateEnd
    {
        get => (DateTime?)GetValue(DisplayDateEndProperty);
        set => SetValue(DisplayDateEndProperty, value);
    }

    /// <summary>
    /// Identifies the first day of the week. Its initial value comes from the current culture, or
    /// from a nonempty Language.
    /// </summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty
        = DependencyProperty.Register(
            nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(DateTimePicker),
            new PropertyMetadata(DayOfWeek.Sunday, OnCalendarPropertyChanged),
            IsValidDayOfWeek
        );

    /// <summary>
    /// Gets or sets the first day of the week, shared by the calendar and the Week shortcut.
    /// </summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    /// <summary>
    /// Identifies whether today is highlighted. The default is true.
    /// </summary>
    public static readonly DependencyProperty IsTodayHighlightedProperty
        = DependencyProperty.Register(
            nameof(IsTodayHighlighted), typeof(bool), typeof(DateTimePicker),
            new PropertyMetadata(true, OnCalendarPropertyChanged)
        );

    /// <summary>
    /// Gets or sets whether the calendar highlights today.
    /// </summary>
    public bool IsTodayHighlighted
    {
        get => (bool)GetValue(IsTodayHighlightedProperty);
        set => SetValue(IsTodayHighlightedProperty, value);
    }

    /// <summary>
    /// Identifies the calendar style dependency property. The default is null, using the native
    /// calendar theme.
    /// </summary>
    public static readonly DependencyProperty CalendarStyleProperty
        = DependencyProperty.Register(
            nameof(CalendarStyle), typeof(Style), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged)
        );

    /// <summary>
    /// Gets or sets the style of the internal native Calendar.
    /// </summary>
    public Style? CalendarStyle
    {
        get => (Style?)GetValue(CalendarStyleProperty);
        set => SetValue(CalendarStyleProperty, value);
    }

    /// <summary>
    /// Gets the native calendar's blackout dates. Modify the collection and its ranges on the
    /// control's UI thread.
    /// Calendar restrictions prevent adding the currently selected calendar date to this
    /// collection.
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public CalendarBlackoutDatesCollection BlackoutDates => _calendar.BlackoutDates;

    /// <summary>
    /// Identifies the bubbling event raised when the committed date/time changes.
    /// </summary>
    public static readonly RoutedEvent SelectedDateTimeChangedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(SelectedDateTimeChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<DateTime?>),
            typeof(DateTimePicker)
        );

    /// <summary>
    /// Occurs when the committed value changes. Temporary popup edits do not raise this event.
    /// </summary>
    public event RoutedPropertyChangedEventHandler<DateTime?> SelectedDateTimeChanged
    {
        add => AddHandler(SelectedDateTimeChangedEvent, value);
        remove => RemoveHandler(SelectedDateTimeChangedEvent, value);
    }

    /// <summary>
    /// Occurs when text parsing or a user commit fails. Set ThrowException on the event arguments
    /// to rethrow the exception.
    /// </summary>
    public event EventHandler<DatePickerDateValidationErrorEventArgs>? DateValidationError;

    /// <summary>
    /// Occurs after the actual popup window opens.
    /// </summary>
    public event EventHandler? CalendarOpened;

    /// <summary>
    /// Occurs after the actual popup window closes.
    /// </summary>
    public event EventHandler? CalendarClosed;

    /// <summary>
    /// Registers the default style and automatic language mode while retaining Language inheritance
    /// and change notifications.
    /// </summary>
    static DateTimePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(typeof(DateTimePicker))
        );

        LanguageProperty.OverrideMetadata(
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(string.Empty), OnLanguageChanged)
        );
    }

    /// <summary>
    /// Creates a control with its own calendar, blackout dates, and editing transaction.
    /// </summary>
    public DateTimePicker()
    {
        SetCurrentValue(DisplayDateProperty, DateTime.Today);
        UpdateDefaultFirstDayOfWeek();

        _calendar.SelectedDatesChanged  += OnCalendarSelectionChanged;
        _calendar.DisplayDateChanged    += OnCalendarDisplayDateChanged;
        BlackoutDates.CollectionChanged += OnBlackoutDatesChanged;

        InitializeInteraction();
        SynchronizeCalendar();
    }

    /// <summary>
    /// Gets the formatting culture, falling back to the current culture for an empty or unresolved
    /// language tag.
    /// </summary>
    private CultureInfo GetCulture()
    {
        return DateTimePickerLocalization.GetCulture(Language, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Gets the label culture. Automatic language mode uses the current UI culture independently of
    /// date formatting.
    /// </summary>
    private CultureInfo GetResourceCulture()
    {
        return DateTimePickerLocalization.GetCulture(Language, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Accepts nonempty format strings that DateTime can use for invariant formatting.
    /// </summary>
    private static bool IsValidFormat(object value)
    {
        if (value is not string format || string.IsNullOrWhiteSpace(format))
        {
            return false;
        }

        try
        {
            _ = DateTime.Now.ToString(format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsValidDayOfWeek(object value)
    {
        var day = (DayOfWeek)value;
        return day >= DayOfWeek.Sunday && day <= DayOfWeek.Saturday;
    }

    /// <summary>
    /// Initializes the week boundary from the formatting culture while the value source is default.
    /// </summary>
    private void UpdateDefaultFirstDayOfWeek()
    {
        var source = DependencyPropertyHelper.GetValueSource(this, FirstDayOfWeekProperty);
        if (source.BaseValueSource == BaseValueSource.Default)
        {
            var culture = GetCulture();
            SetCurrentValue(FirstDayOfWeekProperty, culture.DateTimeFormat.FirstDayOfWeek);
        }
    }

    private static object? CoerceSelectedDateTime(DependencyObject d, object? value)
    {
        var picker = (DateTimePicker)d;
        if (value is DateTime date && !picker.IsDateAllowed(date))
        {
            var culture = picker.GetResourceCulture();
            var message = DateTimePickerLocalization.GetString("DateOutOfRange", culture);
            throw new ArgumentOutOfRangeException(nameof(SelectedDateTime), value, message);
        }

        return value;
    }

    private static object? CoerceSelectedDate(DependencyObject d, object? value)
    {
        var result = CoerceSelectedDateTime(d, value);
        return result is DateTime date ? date.Date : null;
    }

    private static object? CoerceStart(DependencyObject d, object? value)
    {
        var picker = (DateTimePicker)d;
        if (value is not DateTime date)
        {
            return null;
        }

        if (date.Date > picker.DisplayDateEnd?.Date || date.Date > picker.SelectedDate)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayDateStart));
        }

        return date.Date;
    }

    private static object? CoerceEnd(DependencyObject d, object? value)
    {
        var picker = (DateTimePicker)d;
        if (value is not DateTime date)
        {
            return null;
        }

        if (date.Date < picker.DisplayDateStart?.Date || date.Date < picker.SelectedDate)
        {
            throw new ArgumentOutOfRangeException(nameof(DisplayDateEnd));
        }

        return date.Date;
    }

    private static void OnSelectedDateTimeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        picker.SynchronizeCommittedValue();

        var args = new RoutedPropertyChangedEventArgs<DateTime?>(
            (DateTime?)e.OldValue,
            (DateTime?)e.NewValue,
            SelectedDateTimeChangedEvent
        );

        picker.OnSelectedDateTimeChanged(args);
    }

    /// <summary>
    /// Notifies listeners after synchronizing the committed value and its date projection.
    /// </summary>
    /// <param name="e">The event arguments containing the old and new values.</param>
    protected virtual void OnSelectedDateTimeChanged(RoutedPropertyChangedEventArgs<DateTime?> e)
    {
        RaiseEvent(e);
    }

    private static void OnSelectedDateChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        if (picker._synchronizing)
        {
            return;
        }

        DateTime? result = null;
        if (e.NewValue is DateTime date)
        {
            var original = picker.SelectedDateTime ?? date.Date;
            result = ReplaceDate(original, date);
        }

        picker.SetCurrentValue(SelectedDateTimeProperty, result);
    }

    /// <summary>
    /// Uses SetCurrentValue for internal updates to preserve caller bindings and style value
    /// sources.
    /// </summary>
    private void SynchronizeCommittedValue()
    {
        _synchronizing = true;
        try
        {
            SetCurrentValue(SelectedDateProperty, SelectedDateTime?.Date);
        }
        finally
        {
            _synchronizing = false;
        }

        UpdateFormattedText();

        if (IsDropDownOpen)
        {
            SetPendingValue(SelectedDateTime ?? DateTime.Now);
        }
        else
        {
            SynchronizeCalendar();
        }

        Debug.WriteLine($"[DateTimePicker.Value] COMMITTED value applied; value={SelectedDateTime:O}.");
    }

    private static void OnFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        picker.UpdateFormattedText();
    }

    private static void OnLanguageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        picker.UpdateDefaultFirstDayOfWeek();
        picker.SynchronizeCalendar();
        picker.UpdateFormattedText();
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        if (!picker._updatingText)
        {
            picker.UpdateEditorText();
            picker.CommitText();
        }

        picker.UpdateInputError();
        var peer = UIElementAutomationPeer.FromElement(picker);
        peer?.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, e.OldValue, e.NewValue);
    }

    /// <summary>
    /// Attempts to commit text, completing unambiguous date/time fragments using the format and
    /// field ranges. Empty text clears the value; invalid text reports an error.
    /// </summary>
    /// <returns>True when the text is valid and has been applied.</returns>
    public bool CommitText()
    {
        if (!TryReadText(out var result, out var error))
        {
            UpdateInputError();

            var culture = GetResourceCulture();
            var message = DateTimePickerLocalization.GetString(error!, culture);

            Exception exception = error == "InvalidDateTime"
                ? new FormatException(message)
                : new ArgumentOutOfRangeException(nameof(Text), message);

            return ReportValidationError(exception, Text);
        }

        SetCurrentValue(SelectedDateTimeProperty, result);
        UpdateFormattedText();

        return true;
    }

    /// <summary>
    /// Reads draft text using the commit rules without changing the committed value or raising
    /// validation events.
    /// </summary>
    private bool TryReadText(out DateTime? result, out string? error)
    {
        result = null;
        error  = null;

        var text = Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var culture = GetCulture();
        var parsed  = DateTimePickerTextParser.TryParse(text, DateTimeFormat, culture, out var value);
        if (!parsed)
        {
            error = "InvalidDateTime";
            return false;
        }

        if (SelectedDateTime.HasValue)
        {
            var formatted = SelectedDateTime.Value.ToString(DateTimeFormat, culture);
            if (text == formatted)
            {
                value = SelectedDateTime.Value;
            }
            else if (value.Kind == DateTimeKind.Unspecified)
            {
                value = DateTime.SpecifyKind(value, SelectedDateTime.Value.Kind);
            }
        }

        if (!IsDateAllowed(value))
        {
            error = "DateOutOfRange";
            return false;
        }

        result = value;
        return true;
    }

    /// <summary>
    /// Updates the active editor's error state for immediate observation through bindings and
    /// styles.
    /// </summary>
    private void UpdateInputError()
    {
        var valid = IsDropDownOpen ? CanCommit() : TryReadText(out _, out _);
        SetValue(HasInputErrorPropertyKey, !valid);
    }

    /// <summary>
    /// Rebuilds weak subscriptions after collection changes to observe in-place edits to existing
    /// blackout ranges.
    /// </summary>
    private void OnBlackoutDatesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        foreach (var range in _observedBlackoutDates)
        {
            PropertyChangedEventManager.RemoveHandler(range, OnBlackoutRangeChanged, string.Empty);
        }

        _observedBlackoutDates.Clear();

        foreach (var range in BlackoutDates)
        {
            _observedBlackoutDates.Add(range);
            PropertyChangedEventManager.AddHandler(range, OnBlackoutRangeChanged, string.Empty);
        }

        UpdateInputError();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Rechecks draft input when the start or end of a configured blackout range changes.
    /// </summary>
    private void OnBlackoutRangeChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateInputError();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Reports rejected input and honors the exception policy selected by validation handlers.
    /// </summary>
    private bool ReportValidationError(Exception exception, string text)
    {
        var args = new DatePickerDateValidationErrorEventArgs(exception, text);

        DateValidationError?.Invoke(this, args);

        if (args.ThrowException)
        {
            throw args.Exception;
        }

        return false;
    }

    /// <summary>
    /// Formats the committed value and updates the editor without reparsing the generated text.
    /// </summary>
    private void UpdateFormattedText()
    {
        var culture = GetCulture();
        var text    = SelectedDateTime?.ToString(DateTimeFormat, culture) ?? string.Empty;

        _updatingText = true;

        try
        {
            SetCurrentValue(TextProperty, text);
            UpdateEditorText();
        }
        finally
        {
            _updatingText = false;
        }

        UpdateInputError();
    }

    private static void OnCalendarPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;

        picker.SynchronizeCalendar();
        picker.UpdateInputError();

        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Applies the same date restrictions to every commit path, including shortcuts and text input.
    /// </summary>
    private bool IsDateAllowed(DateTime value)
    {
        var date = value.Date;
        return !(date < DisplayDateStart || date > DisplayDateEnd || BlackoutDates.Contains(date));
    }

    /// <summary>
    /// Synchronizes calendar selection with the active transaction. Clears the selection before
    /// applying bounds so Calendar does not expand them automatically.
    /// </summary>
    private void SynchronizeCalendar()
    {
        if (_updatingCalendar)
        {
            return;
        }

        _updatingCalendar = true;
        try
        {
            var culture = GetCulture();
            var calendarLanguage = XmlLanguage.GetLanguage(culture.IetfLanguageTag);
            _calendar.SelectedDate       = null;
            _calendar.DisplayDateStart   = null;
            _calendar.DisplayDateEnd     = null;
            _calendar.DisplayDateStart   = DisplayDateStart;
            _calendar.DisplayDateEnd     = DisplayDateEnd;
            _calendar.FirstDayOfWeek     = FirstDayOfWeek;
            _calendar.IsTodayHighlighted = IsTodayHighlighted;
            _calendar.Language           = calendarLanguage;
            _calendar.FlowDirection      = FlowDirection;

            if (CalendarStyle is null)
            {
                _calendar.ClearValue(StyleProperty);
            }
            else
            {
                _calendar.SetValue(StyleProperty, CalendarStyle);
            }

            var value = IsDropDownOpen ? PendingDateTime : SelectedDateTime;
            if (value.HasValue && IsDateAllowed(value.Value))
            {
                _calendar.SelectedDate = value.Value.Date;
            }

            _calendar.DisplayDate = DisplayDate;
            SetCurrentValue(DisplayDateProperty, _calendar.DisplayDate);
        }
        finally
        {
            _updatingCalendar = false;
        }
    }

    private void OnCalendarSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_updatingCalendar && IsDropDownOpen && _calendar.SelectedDate is DateTime date
            && PendingDateTime is DateTime pending)
        {
            var value = ReplaceDate(pending, date);
            SetValue(PendingDateTimePropertyKey, value);

            UpdateInputError();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
    {
        if (!_updatingCalendar && e.AddedDate.HasValue)
        {
            SetCurrentValue(DisplayDateProperty, e.AddedDate.Value);
        }
    }

    /// <summary>
    /// Replaces the date while preserving the complete time-of-day ticks and DateTime.Kind.
    /// </summary>
    private static DateTime ReplaceDate(DateTime original, DateTime date)
    {
        var ticks = date.Date.Ticks + original.TimeOfDay.Ticks;
        return new DateTime(ticks, original.Kind);
    }

    /// <summary>
    /// Starts or resets the pending draft and synchronizes calendar, time fields, and commit state.
    /// </summary>
    private void SetPendingValue(DateTime value)
    {
        SetValue(PendingDateTimePropertyKey, value);

        SynchronizeCalendar();
        SynchronizeTimeColumns();

        UpdateInputError();
        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;

        if ((bool)e.NewValue)
        {
            var initial = picker.SelectedDateTime ?? DateTime.Now;

            picker.SetCurrentValue(DisplayDateProperty, initial.Date);
            picker.SetPendingValue(initial);
            picker._calendar.DisplayMode = CalendarMode.Month;

            Debug.WriteLine($"[DateTimePicker.Transaction] OPEN editing; pending={initial:O}.");
        }
        else
        {
            picker.SetValue(PendingDateTimePropertyKey, null);
            picker.SynchronizeCalendar();
        }

        picker.UpdatePopupState();
        picker.UpdateInputError();

        var peer     = UIElementAutomationPeer.FromElement(picker);
        var oldState = (bool)e.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
        var newState = (bool)e.NewValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        peer?.RaisePropertyChangedEvent(
            ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldState,
            newState
        );
    }

    private static object CoerceIsDropDownOpen(DependencyObject d, object value)
    {
        var picker = (DateTimePicker)d;
        return (bool)value && picker.IsEnabled;
    }

    /// <summary>
    /// Propagates layout direction changes to the calendar hosted in a separate popup window.
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == FlowDirectionProperty)
        {
            _calendar.FlowDirection = FlowDirection;
        }
    }
}
