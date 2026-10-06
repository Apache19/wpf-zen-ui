// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Automation;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// DateTimePicker 模板中的循环时间字段，支持数字输入、方向键、滚轮和点击相邻值。
/// </summary>
/// <remarks>
/// 仅循环当前字段，不向其它字段进位。有效的一位数字立即更新值，失焦统一显示两位。
/// 此部件服务于 DateTimePicker 模板定制，并非独立的时间选择器。
/// </remarks>
[TemplatePart(Name = "PART_Input", Type = typeof(NativeTextBox))]
public class DateTimePickerTimeColumn : Control
{
    private NativeTextBox? _input;
    private bool _updatingInput;
    private bool _editingInput;
    private int _wheelDelta;

    /// <summary>
    /// 标识字段值，默认 0，范围由 Maximum 决定。
    /// </summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(int), typeof(DateTimePickerTimeColumn),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnValueChanged, CoerceValue));

    /// <summary>
    /// 获取或设置字段值，超出范围时约束到最近的端点。
    /// </summary>
    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// 标识循环上限，默认 59；小时列使用 23。
    /// </summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(int), typeof(DateTimePickerTimeColumn),
        new PropertyMetadata(59, OnMaximumChanged), IsValidMaximum);

    /// <summary>
    /// 获取或设置包含在范围内的上限，允许 1 至 99。
    /// </summary>
    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static readonly DependencyPropertyKey VisibleValuesPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(VisibleValues), typeof(IReadOnlyList<string>), typeof(DateTimePickerTimeColumn),
        new PropertyMetadata(null));

    /// <summary>
    /// 标识五个可见值的只读集合属性。
    /// </summary>
    public static readonly DependencyProperty VisibleValuesProperty = VisibleValuesPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取按 -2、-1、0、1、2 偏移排列的两位数字，中间项始终为当前值。
    /// </summary>
    public IReadOnlyList<string> VisibleValues => (IReadOnlyList<string>)GetValue(VisibleValuesProperty);

    private static readonly DependencyPropertyKey HasValidInputPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(HasValidInput), typeof(bool), typeof(DateTimePickerTimeColumn), new PropertyMetadata(true));

    /// <summary>
    /// 标识输入合法状态的只读属性。
    /// </summary>
    public static readonly DependencyProperty HasValidInputProperty = HasValidInputPropertyKey.DependencyProperty;

    /// <summary>
    /// 获取当前文本是否为合法字段值。空文本和越界文本为 false。
    /// </summary>
    public bool HasValidInput => (bool)GetValue(HasValidInputProperty);

    /// <summary>
    /// 标识字段值变化的冒泡事件。
    /// </summary>
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<int>),
        typeof(DateTimePickerTimeColumn));

    /// <summary>
    /// 字段合法值变化后触发；非法文本不触发。
    /// </summary>
    public event RoutedPropertyChangedEventHandler<int> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>
    /// 按整数偏移循环选择字段，参数可以是整数或整数字符串。
    /// </summary>
    public static RoutedUICommand StepCommand { get; } =
        new("Step", nameof(StepCommand), typeof(DateTimePickerTimeColumn));

    static DateTimePickerTimeColumn()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(DateTimePickerTimeColumn),
            new FrameworkPropertyMetadata(typeof(DateTimePickerTimeColumn)));
    }

    /// <summary>
    /// 初始化五行循环视口及步进命令。
    /// </summary>
    public DateTimePickerTimeColumn()
    {
        CommandBindings.Add(new CommandBinding(StepCommand, OnStepExecuted));
        UpdateVisibleValues();
    }

    private static bool IsValidMaximum(object value)
    {
        int maximum = (int)value;
        return maximum >= 1 && maximum <= 99;
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var column = (DateTimePickerTimeColumn)d;
        int upperBoundedValue = Math.Min(column.Maximum, (int)value);
        return Math.Max(0, upperBoundedValue);
    }

    private static void OnMaximumChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (DateTimePickerTimeColumn)d;
        column.CoerceValue(ValueProperty);
        column.UpdateVisibleValues();
        column.RefreshText();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (DateTimePickerTimeColumn)d;
        column.UpdateVisibleValues();
        if (!column._editingInput)
        {
            column.RefreshText();
        }
        var args = new RoutedPropertyChangedEventArgs<int>((int)e.OldValue, (int)e.NewValue, ValueChangedEvent);
        column.RaiseEvent(args);
        var peer = UIElementAutomationPeer.FromElement(column);
        peer?.RaisePropertyChangedEvent(RangeValuePatternIdentifiers.ValueProperty,
            (double)(int)e.OldValue, (double)(int)e.NewValue);
    }

    /// <summary>
    /// 移除旧模板的事件与粘贴处理，使旧输入框不再能修改字段值。
    /// </summary>
    private void DetachInput()
    {
        if (_input is not null)
        {
            _input.TextChanged -= OnInputTextChanged;
            _input.PreviewTextInput -= OnPreviewTextInput;
            _input.LostKeyboardFocus -= OnInputLostKeyboardFocus;
            _input.GotKeyboardFocus -= OnInputGotKeyboardFocus;
            DataObject.RemovePastingHandler(_input, OnPasting);
        }
        _input = null;
    }

    /// <summary>
    /// 模板清空时立即解绑输入框并恢复合法输入状态。
    /// </summary>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        DetachInput();
        RefreshText();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// 获取新模板输入框并连接输入、焦点与粘贴事件。
    /// </summary>
    public override void OnApplyTemplate()
    {
        DetachInput();
        base.OnApplyTemplate();
        _input = GetTemplateChild("PART_Input") as NativeTextBox;
        if (_input is not null)
        {
            InputMethod.SetIsInputMethodEnabled(_input, false);
            _input.TextChanged += OnInputTextChanged;
            _input.PreviewTextInput += OnPreviewTextInput;
            _input.LostKeyboardFocus += OnInputLostKeyboardFocus;
            _input.GotKeyboardFocus += OnInputGotKeyboardFocus;
            DataObject.AddPastingHandler(_input, OnPasting);
        }
        RefreshText();
    }

    /// <summary>
    /// 生成有限五行视口，通过模运算映射循环值，保持选中项居中。
    /// </summary>
    private void UpdateVisibleValues()
    {
        var values = new string[5];
        for (int index = 0; index < values.Length; index++)
        {
            int value = Wrap(Value + index - 2);
            values[index] = value.ToString("D2", CultureInfo.InvariantCulture);
        }
        var readOnlyValues = Array.AsReadOnly(values);
        SetValue(VisibleValuesPropertyKey, readOnlyValues);
    }

    private int Wrap(int value)
    {
        int count = Maximum + 1;
        return (value % count + count) % count;
    }

    /// <summary>
    /// 从最后有效值恢复输入；供新事务和外部值同步使用。
    /// </summary>
    internal void RefreshText()
    {
        if (_editingInput)
        {
            return;
        }
        _updatingInput = true;
        try
        {
            if (_input is not null)
            {
                string text = Value.ToString("D2", CultureInfo.InvariantCulture);
                _input.SetCurrentValue(NativeTextBox.TextProperty, text);
            }
            SetValue(HasValidInputPropertyKey, true);
            CommandManager.InvalidateRequerySuggested();
        }
        finally
        {
            _updatingInput = false;
        }
    }

    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingInput || _input is null)
        {
            return;
        }
        string text = _input.Text;
        bool parsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value);
        bool valid = parsed && IsDigits(text) && text.Length <= 2 && value <= Maximum;
        SetValue(HasValidInputPropertyKey, valid);
        if (valid)
        {
            _editingInput = true;
            try
            {
                SetCurrentValue(ValueProperty, value);
            }
            finally
            {
                _editingInput = false;
            }
        }
        CommandManager.InvalidateRequerySuggested();
    }

    private static bool IsDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }
        foreach (char digit in text)
        {
            if (digit < '0' || digit > '9')
            {
                return false;
            }
        }
        return true;
    }

    private void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !IsDigits(e.Text);
    }

    private void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        var text = e.DataObject.GetData(DataFormats.UnicodeText) as string;
        if (text is null || !IsDigits(text) || text.Length > 2)
        {
            e.CancelCommand();
        }
    }

    private void OnInputLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        RefreshText();
    }

    private void OnInputGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        _input?.SelectAll();
    }

    private void OnStepExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string? parameter = Convert.ToString(e.Parameter, CultureInfo.InvariantCulture);
        if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out int offset))
        {
            Step(offset);
        }
        e.Handled = true;
    }

    private void Step(int offset)
    {
        int wrapped = Wrap(Value + offset % (Maximum + 1));
        SetCurrentValue(ValueProperty, wrapped);
        RefreshText();
    }

    /// <summary>
    /// 滚轮按标准 120 单位步进；高分辨率滚轮的余量保留到下一次事件。
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        _wheelDelta += e.Delta;
        int steps = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        _wheelDelta %= Mouse.MouseWheelDeltaForOneLine;
        if (steps != 0)
        {
            Step(-steps);
        }
        e.Handled = true;
    }

    /// <summary>
    /// 上下方向键分别选择上一项和下一项，Home / End 选择字段端点。
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Key == Key.Up || e.Key == Key.Down)
        {
            Step(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Home || e.Key == Key.End)
        {
            SetCurrentValue(ValueProperty, e.Key == Key.Home ? 0 : Maximum);
            RefreshText();
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            e.Handled = true;
        }
    }

    /// <summary>
    /// 提供辅助技术可读写的数值范围。
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new TimeColumnAutomationPeer(this);
    }

    private sealed class TimeColumnAutomationPeer : FrameworkElementAutomationPeer, IRangeValueProvider
    {
        internal TimeColumnAutomationPeer(DateTimePickerTimeColumn owner) : base(owner)
        {
        }

        private DateTimePickerTimeColumn Column => (DateTimePickerTimeColumn)Owner;
        /// <inheritdoc />
        public bool IsReadOnly => !IsEnabled();
        /// <inheritdoc />
        public double LargeChange => 5;
        /// <inheritdoc />
        public double SmallChange => 1;
        /// <inheritdoc />
        public double Maximum => Column.Maximum;
        /// <inheritdoc />
        public double Minimum => 0;
        /// <inheritdoc />
        public double Value => Column.Value;

        /// <inheritdoc />
        public override object? GetPattern(PatternInterface patternInterface)
        {
            return patternInterface == PatternInterface.RangeValue ? this : base.GetPattern(patternInterface);
        }

        /// <inheritdoc />
        protected override string GetClassNameCore()
        {
            return nameof(DateTimePickerTimeColumn);
        }

        /// <inheritdoc />
        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Spinner;
        }

        /// <inheritdoc />
        public void SetValue(double value)
        {
            if (!IsEnabled())
            {
                throw new System.Windows.Automation.ElementNotEnabledException();
            }
            if (double.IsNaN(value) || value < Minimum || value > Maximum || value != Math.Truncate(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            Column.SetCurrentValue(ValueProperty, (int)value);
            Column.RefreshText();
        }
    }
}
