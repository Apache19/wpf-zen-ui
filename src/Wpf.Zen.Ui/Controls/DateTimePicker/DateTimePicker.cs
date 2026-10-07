// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using Calendar = System.Windows.Controls.Calendar;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// 使用原生日历和独立时间字段编辑完整日期时间，只有确认才提交弹出层中的修改。
/// </summary>
/// <remarks>
/// SelectedDateTime 是正式值，SelectedDate 是日期投影。弹出层打开期间外部修改正式值
/// 会重新开始编辑事务。控件不转换时区；修改字段时保留 Kind 和未编辑的亚秒精度。
/// 使用 Control 与 Calendar 组合，以统一管理完整日期时间的文本及事务语义。
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
    private readonly Calendar _calendar = new();

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
    /// 标识清除按钮开关属性，默认启用。
    /// </summary>
    public static readonly DependencyProperty IsClearButtonEnabledProperty
        = DependencyProperty.Register(
            nameof(IsClearButtonEnabled), typeof(bool), typeof(DateTimePicker),
            new PropertyMetadata(true)
        );

    /// <summary>
    /// 获取或设置是否提供清除按钮。默认模板在编辑器有焦点且文本非空时显示按钮。
    /// 清除会同步重置正式日期时间、日期投影和文本，并取消弹出层编辑。
    /// </summary>
    public bool IsClearButtonEnabled
    {
        get => (bool)GetValue(IsClearButtonEnabledProperty);
        set => SetValue(IsClearButtonEnabledProperty, value);
    }

    /// <summary>
    /// 标识完整日期时间属性，默认 null，默认双向绑定。
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
    /// 获取或设置正式日期时间。禁止日期和范围外的值会抛出 ArgumentOutOfRangeException。
    /// </summary>
    public DateTime? SelectedDateTime
    {
        get => (DateTime?)GetValue(SelectedDateTimeProperty);
        set => SetValue(SelectedDateTimeProperty, value);
    }

    /// <summary>
    /// 标识正式值的日期投影属性，默认 null，默认双向绑定。
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
    /// 获取或设置日期投影。设置日期保留原时间；原值为空时采用零点；设置 null 清空正式值。
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
    /// 标识只读待提交值属性。
    /// </summary>
    public static readonly DependencyProperty PendingDateTimeProperty
        = PendingDateTimePropertyKey.DependencyProperty;

    /// <summary>
    /// 获取弹出层中正在编辑的值；关闭时为 null。此属性不会回写业务数据。
    /// </summary>
    public DateTime? PendingDateTime => (DateTime?)GetValue(PendingDateTimeProperty);

    /// <summary>
    /// 标识显示和精确解析使用的日期时间格式，默认 yyyy-MM-dd HH:mm:ss。
    /// </summary>
    public static readonly DependencyProperty DateTimeFormatProperty
        = DependencyProperty.Register(
            nameof(DateTimeFormat), typeof(string), typeof(DateTimePicker),
            new PropertyMetadata("yyyy-MM-dd HH:mm:ss", OnFormatChanged),
            IsValidFormat
        );

    /// <summary>
    /// 获取或设置 DateTime 格式字符串。显示和解析使用 Language 指定的区域设置。
    /// </summary>
    public string DateTimeFormat
    {
        get => (string)GetValue(DateTimeFormatProperty);
        set => SetValue(DateTimeFormatProperty, value);
    }

    /// <summary>
    /// 标识完整文本属性，默认空字符串，默认双向绑定。
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
    /// 获取或设置完整日期时间文本。外部设置立即解析；编辑框在 Enter 或失焦时解析。
    /// 无效文本触发 DateValidationError，正式值保持不变；编辑框失焦提交失败时恢复正式值文本。
    /// Enter 或显式提交失败时保留文本供修正，弹出层打开期间保留草稿。
    /// </summary>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>
    /// 标识弹出层打开状态，默认 false，默认双向绑定。
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
    /// 获取或设置弹出层状态。打开建立事务，直接关闭视为取消。
    /// </summary>
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    /// <summary>
    /// 标识日历浏览日期，实例默认当天。
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
    /// 获取或设置日历浏览日期。浏览月份不会改变待提交值。
    /// </summary>
    public DateTime DisplayDate
    {
        get => (DateTime)GetValue(DisplayDateProperty);
        set => SetValue(DisplayDateProperty, value);
    }

    /// <summary>
    /// 标识可选日期下限，默认 null，忽略时间部分。
    /// </summary>
    public static readonly DependencyProperty DisplayDateStartProperty
        = DependencyProperty.Register(
            nameof(DisplayDateStart), typeof(DateTime?), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged, CoerceStart)
        );

    /// <summary>
    /// 获取或设置日期下限。范围必须包含当前正式值，且不得晚于上限。
    /// </summary>
    public DateTime? DisplayDateStart
    {
        get => (DateTime?)GetValue(DisplayDateStartProperty);
        set => SetValue(DisplayDateStartProperty, value);
    }

    /// <summary>
    /// 标识可选日期上限，默认 null，忽略时间部分。
    /// </summary>
    public static readonly DependencyProperty DisplayDateEndProperty
        = DependencyProperty.Register(
            nameof(DisplayDateEnd), typeof(DateTime?), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged, CoerceEnd)
        );

    /// <summary>
    /// 获取或设置日期上限。范围必须包含当前正式值，且不得早于下限。
    /// </summary>
    public DateTime? DisplayDateEnd
    {
        get => (DateTime?)GetValue(DisplayDateEndProperty);
        set => SetValue(DisplayDateEndProperty, value);
    }

    /// <summary>
    /// 标识周起始日，默认值来自 Language 对应的区域设置。
    /// </summary>
    public static readonly DependencyProperty FirstDayOfWeekProperty
        = DependencyProperty.Register(
            nameof(FirstDayOfWeek), typeof(DayOfWeek), typeof(DateTimePicker),
            new PropertyMetadata(DayOfWeek.Sunday, OnCalendarPropertyChanged),
            IsValidDayOfWeek
        );

    /// <summary>
    /// 获取或设置周起始日，日历和 Week 快捷命令使用相同值。
    /// </summary>
    public DayOfWeek FirstDayOfWeek
    {
        get => (DayOfWeek)GetValue(FirstDayOfWeekProperty);
        set => SetValue(FirstDayOfWeekProperty, value);
    }

    /// <summary>
    /// 标识是否突出显示今天，默认 true。
    /// </summary>
    public static readonly DependencyProperty IsTodayHighlightedProperty
        = DependencyProperty.Register(
            nameof(IsTodayHighlighted), typeof(bool), typeof(DateTimePicker),
            new PropertyMetadata(true, OnCalendarPropertyChanged)
        );

    /// <summary>
    /// 获取或设置是否突出显示今天。
    /// </summary>
    public bool IsTodayHighlighted
    {
        get => (bool)GetValue(IsTodayHighlightedProperty);
        set => SetValue(IsTodayHighlightedProperty, value);
    }

    /// <summary>
    /// 标识日历样式，默认 null，使用原生日历主题。
    /// </summary>
    public static readonly DependencyProperty CalendarStyleProperty
        = DependencyProperty.Register(
            nameof(CalendarStyle), typeof(Style), typeof(DateTimePicker),
            new PropertyMetadata(null, OnCalendarPropertyChanged)
        );

    /// <summary>
    /// 获取或设置内部原生 Calendar 的样式。
    /// </summary>
    public Style? CalendarStyle
    {
        get => (Style?)GetValue(CalendarStyleProperty);
        set => SetValue(CalendarStyleProperty, value);
    }

    /// <summary>
    /// 获取原生日历的禁止日期集合；该集合及其成员必须在控件的 UI 线程修改。
    /// 日历当前选中的日期不能加入禁止集合，遵循 Calendar 的约束。
    /// </summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public CalendarBlackoutDatesCollection BlackoutDates => _calendar.BlackoutDates;

    /// <summary>
    /// 标识正式日期时间变化的冒泡事件。
    /// </summary>
    public static readonly RoutedEvent SelectedDateTimeChangedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(SelectedDateTimeChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedPropertyChangedEventHandler<DateTime?>),
            typeof(DateTimePicker)
        );

    /// <summary>
    /// 正式值发生变化时触发；弹出层中的临时修改不会触发此事件。
    /// </summary>
    public event RoutedPropertyChangedEventHandler<DateTime?> SelectedDateTimeChanged
    {
        add => AddHandler(SelectedDateTimeChangedEvent, value);
        remove => RemoveHandler(SelectedDateTimeChangedEvent, value);
    }

    /// <summary>
    /// 文本解析或用户提交失败时触发。可设置 ThrowException 以重新抛出异常。
    /// </summary>
    public event EventHandler<DatePickerDateValidationErrorEventArgs>? DateValidationError;

    /// <summary>
    /// 实际弹出窗口打开后触发。
    /// </summary>
    public event EventHandler? CalendarOpened;

    /// <summary>
    /// 实际弹出窗口关闭后触发。
    /// </summary>
    public event EventHandler? CalendarClosed;

    static DateTimePicker()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(typeof(DateTimePicker))
        );

        LanguageProperty.OverrideMetadata(
            typeof(DateTimePicker),
            new FrameworkPropertyMetadata(OnLanguageChanged)
        );
    }

    /// <summary>
    /// 创建拥有独立日历、禁止日期集合及编辑事务的控件。
    /// </summary>
    public DateTimePicker()
    {
        SetCurrentValue(DisplayDateProperty, DateTime.Today);
        UpdateDefaultFirstDayOfWeek();

        _calendar.SelectedDatesChanged += OnCalendarSelectionChanged;
        _calendar.DisplayDateChanged   += OnCalendarDisplayDateChanged;

        InitializeInteraction();
        SynchronizeCalendar();
    }

    /// <summary>
    /// 获取 Language 对应的区域设置；无法解析特定区域时采用当前区域设置。
    /// </summary>
    private CultureInfo GetCulture()
    {
        try
        {
            return Language.GetSpecificCulture();
        }
        catch (InvalidOperationException)
        {
            return CultureInfo.CurrentCulture;
        }
    }

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
            var culture = picker.GetCulture();
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
    /// 在正式值及日期投影同步后通知监听者。
    /// </summary>
    /// <param name="e">包含原值和新值的事件参数。</param>
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
    /// 内部回写使用 SetCurrentValue，保留调用者的绑定和样式来源。
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

        var peer = UIElementAutomationPeer.FromElement(picker);
        peer?.RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, e.OldValue, e.NewValue);
    }

    /// <summary>
    /// 尝试提交文本，按格式及字段范围补全明确的日期时间片段；空文本清空值，非法文本只报告错误。
    /// </summary>
    /// <returns>文本合法并已应用时为 true。</returns>
    public bool CommitText()
    {
        var       text   = Text ?? string.Empty;
        DateTime? result = null;
        if (!string.IsNullOrWhiteSpace(text))
        {
            var culture = GetCulture();
            var parsed  = DateTimePickerTextParser.TryParse(text, DateTimeFormat, culture, out var value);
            if (!parsed)
            {
                var message   = DateTimePickerLocalization.GetString("InvalidDateTime", culture);
                var exception = new FormatException(message);
                return ReportValidationError(exception, text);
            }

            // 文本相同不重建值，避免短格式损失隐藏的秒、亚秒或 Kind。
            if (SelectedDateTime.HasValue)
            {
                var formatted = SelectedDateTime.Value.ToString(DateTimeFormat, culture);
                if (text == formatted)
                {
                    return true;
                }

                if (value.Kind == DateTimeKind.Unspecified)
                {
                    value = DateTime.SpecifyKind(value, SelectedDateTime.Value.Kind);
                }
            }

            result = value;
        }

        if (result.HasValue && !IsDateAllowed(result.Value))
        {
            var culture   = GetCulture();
            var message   = DateTimePickerLocalization.GetString("DateOutOfRange", culture);
            var exception = new ArgumentOutOfRangeException(nameof(Text), message);
            return ReportValidationError(exception, text);
        }

        SetCurrentValue(SelectedDateTimeProperty, result);
        UpdateFormattedText();
        return true;
    }

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
    }

    private static void OnCalendarPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var picker = (DateTimePicker)d;
        picker.SynchronizeCalendar();
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// 所有提交路径共享此日期约束判断，防止快捷命令和文本绕过日历限制。
    /// </summary>
    private bool IsDateAllowed(DateTime value)
    {
        var date = value.Date;
        return !(date < DisplayDateStart || date > DisplayDateEnd || BlackoutDates.Contains(date));
    }

    /// <summary>
    /// 日历选择只反映当前事务；清除旧选择后设置范围，避免 Calendar 自动扩大范围。
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
            _calendar.SelectedDate       = null;
            _calendar.DisplayDateStart   = null;
            _calendar.DisplayDateEnd     = null;
            _calendar.DisplayDateStart   = DisplayDateStart;
            _calendar.DisplayDateEnd     = DisplayDateEnd;
            _calendar.FirstDayOfWeek     = FirstDayOfWeek;
            _calendar.IsTodayHighlighted = IsTodayHighlighted;
            _calendar.Language           = Language;
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
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private void OnCalendarDisplayDateChanged(object? sender, CalendarDateChangedEventArgs e)
    {
        if (!_updatingCalendar && e.AddedDate.HasValue)
        {
            SetCurrentValue(DisplayDateProperty, e.AddedDate.Value);
        }
    }

    private static DateTime ReplaceDate(DateTime original, DateTime date)
    {
        var ticks = date.Date.Ticks + original.TimeOfDay.Ticks;
        return new DateTime(ticks, original.Kind);
    }

    private void SetPendingValue(DateTime value)
    {
        SetValue(PendingDateTimePropertyKey, value);

        SynchronizeCalendar();
        SynchronizeTimeColumns();

        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
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
    /// 将动态布局方向传递给拥有独立弹出窗口的日历。
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == FlowDirectionProperty && _calendar is not null)
        {
            _calendar.FlowDirection = FlowDirection;
        }
    }
}
