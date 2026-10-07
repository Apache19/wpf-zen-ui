// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.Controls;

public partial class DateTimePicker
{
    private readonly DateTimePickerTimeColumn?[] _columns = new DateTimePickerTimeColumn?[3];

    private NativeTextBox?  _textBox;
    private ButtonBase?     _button;
    private ButtonBase?     _clearButton;
    private Popup?          _popup;
    private ContentControl? _calendarHost;

    private bool _updatingColumns;
    private bool _updatingEditor;
    private bool _textDirty;

    /// <summary>
    /// 提交待提交值并关闭弹出层；无效或未完成的时间输入会禁用此命令。
    /// </summary>
    public static RoutedUICommand CommitCommand { get; } = new(
        "OK", nameof(CommitCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// 取消待提交修改并关闭弹出层。
    /// </summary>
    public static RoutedUICommand CancelCommand { get; } = new(
        "Cancel", nameof(CancelCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// 提交本地时间快捷值。参数为 Year、Month、Week、Today 或 Now，区分大小写。
    /// </summary>
    public static RoutedUICommand SelectShortcutCommand { get; } = new(
        "Select date and time", nameof(SelectShortcutCommand), typeof(DateTimePicker)
    );

    /// <summary>
    /// 安装实例命令及生命周期处理；Popup 内容通过 CommandTarget 显式定位所属控件。
    /// </summary>
    private void InitializeInteraction()
    {
        _calendar.AddHandler(
            Mouse.MouseUpEvent, new MouseButtonEventHandler(OnCalendarMouseUp),
            handledEventsToo: true
        );

        CommandBindings.Add(new CommandBinding(CommitCommand, OnCommitExecuted, OnCommitCanExecute));
        CommandBindings.Add(new CommandBinding(CancelCommand, OnCancelExecuted, OnCancelCanExecute));
        CommandBindings.Add(new CommandBinding(SelectShortcutCommand, OnShortcutExecuted, OnShortcutCanExecute));

        Unloaded         += OnUnloaded;
        IsEnabledChanged += OnIsEnabledChanged;
    }

    /// <summary>
    /// 日历完成左键选择后释放残留捕获，让 Popup 接管外围交互和外部点击关闭。
    /// </summary>
    /// <remarks>
    /// CalendarItem 在按下时捕获自身子树，而日期按钮将抬起事件标记为已处理，
    /// 可能跳过 CalendarItem.OnMouseUp 的释放逻辑。因此在冒泡阶段监听已处理事件，
    /// 等日历完成选择后仅释放本日历的 CalendarItem 捕获，不干扰其它部件的捕获。
    /// 此事件随实例日历注册一次，不依赖模板结构或模板重建。
    /// </remarks>
    private void OnCalendarMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || Mouse.Captured is not CalendarItem item)
        {
            return;
        }

        if (_calendar.IsAncestorOf(item))
        {
            // Popup 的 LostMouseCapture 处理会自行恢复子树捕获，保留正常关闭策略。
            item.ReleaseMouseCapture();
        }
    }

    /// <summary>
    /// 替换模板时解除旧部件事件，取消旧事务，并将同一日历移入新模板。
    /// 缺少任意可选部件时仍可通过公开属性和命令使用控件。
    /// </summary>
    private void DetachTemplateParts()
    {
        if (_popup is not null)
        {
            Cancel();

            _popup.Opened -= OnPopupOpened;
            _popup.Closed -= OnPopupClosed;

            if (_popup.Child is UIElement oldChild)
            {
                oldChild.PreviewKeyDown -= OnPopupPreviewKeyDown;
            }
        }

        if (_calendarHost is not null)
        {
            _calendarHost.Content = null;
        }

        if (_button is not null)
        {
            _button.Click                      -= OnDropDownButtonClick;
            _button.PreviewMouseLeftButtonDown -= OnEditorButtonMouseDown;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click                      -= OnClearButtonClick;
            _clearButton.PreviewMouseLeftButtonDown -= OnEditorButtonMouseDown;
        }

        if (_textBox is not null)
        {
            _textBox.TextChanged       -= OnEditorTextChanged;
            _textBox.LostKeyboardFocus -= OnEditorLostKeyboardFocus;
        }

        foreach (var column in _columns)
        {
            if (column is not null)
            {
                column.ValueChanged         -= OnColumnValueChanged;
                column.InputValidityChanged -= OnColumnInputValidityChanged;
            }
        }

        _popup        = null;
        _calendarHost = null;
        _button       = null;
        _clearButton  = null;
        _textBox      = null;

        Array.Clear(_columns, 0, _columns.Length);
    }

    /// <summary>
    /// 模板被移除时立即释放旧部件，即使新模板为空或尚未应用也会取消旧事务。
    /// </summary>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        if (oldTemplate is not null)
        {
            Cancel();
        }

        DetachTemplateParts();

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// 将日历和时间列接入新模板，缺少部件时保留属性与命令能力。
    /// </summary>
    public override void OnApplyTemplate()
    {
        DetachTemplateParts();

        base.OnApplyTemplate();

        _textBox      = GetTemplateChild("PART_TextBox") as NativeTextBox;
        _button       = GetTemplateChild("PART_Button") as ButtonBase;
        _clearButton  = GetTemplateChild("PART_ClearButton") as ButtonBase;
        _popup        = GetTemplateChild("PART_Popup") as Popup;
        _calendarHost = GetTemplateChild("PART_CalendarHost") as ContentControl;
        _columns[0]   = GetTemplateChild("PART_Hour") as DateTimePickerTimeColumn;
        _columns[1]   = GetTemplateChild("PART_Minute") as DateTimePickerTimeColumn;
        _columns[2]   = GetTemplateChild("PART_Second") as DateTimePickerTimeColumn;

        if (_textBox is not null)
        {
            _textBox.TextChanged       += OnEditorTextChanged;
            _textBox.LostKeyboardFocus += OnEditorLostKeyboardFocus;
        }

        if (_button is not null)
        {
            _button.Click                      += OnDropDownButtonClick;
            _button.PreviewMouseLeftButtonDown += OnEditorButtonMouseDown;
        }

        if (_clearButton is not null)
        {
            _clearButton.Click                      += OnClearButtonClick;
            _clearButton.PreviewMouseLeftButtonDown += OnEditorButtonMouseDown;
        }

        if (_calendarHost is not null)
        {
            _calendarHost.Content = _calendar;
        }

        for (var index = 0; index < _columns.Length; index++)
        {
            var column = _columns[index];
            if (column is not null)
            {
                column.SetCurrentValue(DateTimePickerTimeColumn.MaximumProperty, index == 0 ? 23 : 59);
                column.ValueChanged         += OnColumnValueChanged;
                column.InputValidityChanged += OnColumnInputValidityChanged;
            }
        }

        if (_popup is not null)
        {
            _popup.Opened += OnPopupOpened;
            _popup.Closed += OnPopupClosed;

            PopupDismissal.SetExcludedElement(_popup, _button);

            if (_popup.Child is UIElement child)
            {
                child.PreviewKeyDown += OnPopupPreviewKeyDown;
            }
        }

        UpdateEditorText();
        SynchronizeCalendar();
        SynchronizeTimeColumns();
        UpdatePopupState();
        UpdateInputError();
    }

    /// <summary>
    /// 确认弹出层事务。输入尚未完成或日期不允许时返回 false 并保留编辑状态。
    /// </summary>
    /// <returns>成功提交并关闭时为 true。</returns>
    public bool Commit()
    {
        if (!CanCommit())
        {
            return false;
        }

        var value = PendingDateTime!.Value;
        SetCurrentValue(SelectedDateTimeProperty, value);

        UpdateFormattedText();
        CloseAndRestoreFocus();

        return true;
    }

    /// <summary>
    /// 放弃本次弹出层编辑。正式值及其绑定保持原值。
    /// </summary>
    public void Cancel()
    {
        if (IsDropDownOpen)
        {
            Debug.WriteLine(
                $"[DateTimePicker.Transaction] CANCEL editing; pending={PendingDateTime:O}."
            );

            SetCurrentValue(IsDropDownOpenProperty, false);
        }
    }

    private bool CanCommit()
    {
        if (!IsDropDownOpen || !PendingDateTime.HasValue || !IsDateAllowed(PendingDateTime.Value))
        {
            return false;
        }

        foreach (var column in _columns)
        {
            if (column is not null && !column.HasValidInput)
            {
                return false;
            }
        }

        return true;
    }

    private void OnCommitCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsEnabled && CanCommit();
        e.Handled    = true;
    }

    private void OnCommitExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        Commit();
        e.Handled = true;
    }

    private void OnCancelCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = IsDropDownOpen;
        e.Handled    = true;
    }

    private void OnCancelExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        CloseAndRestoreFocus();
        e.Handled = true;
    }

    private void OnShortcutCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var value = GetShortcutValue(e.Parameter as string);
        e.CanExecute = IsEnabled && IsDropDownOpen && value.HasValue && IsDateAllowed(value.Value);
        e.Handled    = true;
    }

    private void OnShortcutExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var value = GetShortcutValue(e.Parameter as string);
        // 再次求值及验证，涵盖命令查询与执行之间的跨日和范围变化。
        if (IsDropDownOpen && value.HasValue && IsDateAllowed(value.Value))
        {
            SetCurrentValue(SelectedDateTimeProperty, value.Value);
            UpdateFormattedText();
            CloseAndRestoreFocus();
        }

        e.Handled = true;
    }

    private DateTime? GetShortcutValue(string? shortcut)
    {
        var now = DateTime.Now;

        switch (shortcut)
        {
            case "Year":
                return new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);

            case "Month":
                return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Local);

            case "Week":
            {
                var days = ((int)now.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
                return now.Date.AddDays(-days);
            }

            case "Today":
                return now.Date;

            case "Now":
                return now;

            default:
                return null;
        }
    }

    private void SynchronizeTimeColumns()
    {
        if (_updatingColumns || PendingDateTime is not DateTime value)
        {
            return;
        }

        _updatingColumns = true;

        try
        {
            int[] values =
            {
                value.Hour, value.Minute, value.Second
            };

            for (var index = 0; index < values.Length; index++)
            {
                var column = _columns[index];
                if (column is not null)
                {
                    column.SetCurrentValue(DateTimePickerTimeColumn.ValueProperty, values[index]);
                    column.RefreshText();
                }
            }
        }
        finally
        {
            _updatingColumns = false;
        }

        UpdateInputError();
    }

    /// <summary>
    /// 时间列的非法草稿不会改变字段值，需单独同步输入状态。
    /// </summary>
    private void OnColumnInputValidityChanged(object? sender, EventArgs e)
    {
        if (!_updatingColumns)
        {
            UpdateInputError();
        }
    }

    private void OnColumnValueChanged(object sender, RoutedPropertyChangedEventArgs<int> e)
    {
        if (_updatingColumns || !IsDropDownOpen || PendingDateTime is not DateTime value)
        {
            return;
        }

        var column = sender as DateTimePickerTimeColumn;
        var index  = Array.IndexOf(_columns, column);

        long ticksPerUnit;

        switch (index)
        {
            case 0:
                ticksPerUnit = TimeSpan.TicksPerHour;
                break;

            case 1:
                ticksPerUnit = TimeSpan.TicksPerMinute;
                break;

            case 2:
                ticksPerUnit = TimeSpan.TicksPerSecond;
                break;

            default:
                return;
        }

        var ticks   = value.Ticks + (e.NewValue - e.OldValue) * ticksPerUnit;
        var updated = new DateTime(ticks, value.Kind);

        // 本次列已完成本地同步；避免刷新其它列而丢失尚未完成的输入。
        SetValue(PendingDateTimePropertyKey, updated);
        UpdateInputError();

        CommandManager.InvalidateRequerySuggested();
    }

    private void UpdateEditorText()
    {
        if (_textBox is null)
        {
            return;
        }

        _updatingEditor = true;

        try
        {
            var text = Text;
            if (string.IsNullOrEmpty(text))
            {
                _textBox.SetCurrentValue(NativeTextBox.TextProperty, string.Empty);
            }
            else
            {
                _textBox.SetCurrentValue(NativeTextBox.TextProperty, text);
            }

            _textDirty = false;
        }
        finally
        {
            _updatingEditor = false;
        }
    }

    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingEditor || _textBox is null)
        {
            return;
        }

        _updatingText = true;

        try
        {
            SetCurrentValue(TextProperty, _textBox.Text);
            _textDirty = true;
        }
        finally
        {
            _updatingText = false;
        }
    }

    /// <summary>
    /// 编辑框失焦时提交草稿；校验失败则恢复正式值的显示，正式值为空时清空文本。
    /// 弹出层打开期间保留草稿，由弹出层的确认操作决定最终文本。
    /// </summary>
    private void OnEditorLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!IsDropDownOpen && _textDirty)
        {
            _textDirty = !CommitText();
            if (_textDirty)
            {
                UpdateFormattedText();
            }
        }
    }

    /// <summary>
    /// 主编辑框支持 Enter 提交、Esc 恢复，以及 F4 / Alt+Down 打开选择器。
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        var key    = e.Key == Key.System ? e.SystemKey : e.Key;
        var toggle = key == Key.F4 || (key == Key.Down && Keyboard.Modifiers == ModifierKeys.Alt);

        if (toggle)
        {
            if (IsDropDownOpen)
            {
                CloseAndRestoreFocus();
            }
            else
            {
                OpenFromEditor();
            }

            e.Handled = true;
        }
        else if (!IsDropDownOpen && key == Key.Enter)
        {
            _textDirty = !CommitText();
            e.Handled  = true;
        }
        else if (!IsDropDownOpen && key == Key.Escape)
        {
            UpdateFormattedText();
            e.Handled = true;
        }
    }

    private void OnPopupPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.System && e.SystemKey == Key.Up))
        {
            CloseAndRestoreFocus();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            // 日历年/月模式中的 Enter 用于下钻；按钮保留自己的 Enter 命令语义。
            if ((_calendar.IsKeyboardFocusWithin && _calendar.DisplayMode != CalendarMode.Month)
                || Keyboard.FocusedElement is ButtonBase)
            {
                return;
            }

            Commit();
            e.Handled = true;
        }
    }

    /// <summary>
    /// 按钮按下时即进入编辑框，避免 ButtonBase 处理冒泡事件后控件无法获得焦点。
    /// 不标记事件已处理，以保留按钮的捕获、点击和触摸提升行为。
    /// </summary>
    private void OnEditorButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        _textBox?.Focus();
    }

    /// <summary>
    /// 清除正式值及无效草稿，并结束待提交事务；保留调用者设置的属性绑定。
    /// </summary>
    private void OnClearButtonClick(object sender, RoutedEventArgs e)
    {
        if (!IsEnabled || !IsClearButtonEnabled)
        {
            return;
        }

        Cancel();
        SetCurrentValue(SelectedDateTimeProperty, null);
        UpdateFormattedText();

        _textBox?.Focus();
        e.Handled = true;
    }

    private void OnDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        if (IsDropDownOpen)
        {
            CloseAndRestoreFocus();
        }
        else
        {
            OpenFromEditor();
        }
    }

    private void OpenFromEditor()
    {
        if (!IsEnabled)
        {
            return;
        }

        // 合法草稿仍先应用；无效草稿保留，用户可以通过弹出层重新选择。
        // 打开操作不报告文本错误，因此也不受 ThrowException 错误策略影响。
        if (_textDirty)
        {
            var culture  = GetCulture();
            var text     = Text;
            var parsed   = DateTimePickerTextParser.TryParse(text, DateTimeFormat, culture, out var value);
            var canApply = string.IsNullOrWhiteSpace(text) || (parsed && IsDateAllowed(value));
            if (canApply)
            {
                CommitText();
            }
        }

        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    /// <summary>
    /// 控件自身获得焦点时把编辑焦点交给内部文本框，保留子控件的正常焦点行为。
    /// </summary>
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        if (ReferenceEquals(e.OriginalSource, this))
        {
            ApplyTemplate();
            _textBox?.Focus();
        }
    }

    /// <summary>
    /// 点击模板本体的空白区域进入文本编辑；已被文本框或按钮处理的点击保持原行为。
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!e.Handled && !IsDropDownOpen)
        {
            Focus();
        }
    }

    private void UpdatePopupState()
    {
        if (_popup is not null)
        {
            _popup.SetCurrentValue(Popup.IsOpenProperty, IsDropDownOpen && IsEnabled);
        }
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (!IsDropDownOpen)
        {
            SetCurrentValue(IsDropDownOpenProperty, true);
        }

        _calendar.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        CalendarOpened?.Invoke(this, EventArgs.Empty);
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        // Closed 可能在本次调度的尾部到达，不能取消已经重新打开的新事务。
        if (_popup?.IsOpen != true)
        {
            SetCurrentValue(IsDropDownOpenProperty, false);
        }

        CalendarClosed?.Invoke(this, EventArgs.Empty);
    }

    private void CloseAndRestoreFocus()
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
        _textBox?.Focus();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Cancel();
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue)
        {
            Cancel();
        }

        CoerceValue(IsDropDownOpenProperty);
    }

    /// <summary>
    /// 提供完整文本值和展开/折叠的 UI Automation 模式。
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new DateTimePickerAutomationPeer(this);
    }
}