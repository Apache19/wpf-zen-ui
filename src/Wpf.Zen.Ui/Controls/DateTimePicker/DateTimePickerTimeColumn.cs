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
/// Provides a cyclic time field for DateTimePicker templates, supporting numeric input, arrow keys,
/// the mouse wheel, and adjacent-value buttons.
/// </summary>
/// <remarks>
/// Only this field wraps; other fields do not receive a carry. Valid single-digit input updates the
/// value immediately and is padded to two digits on focus loss.
/// This component supports DateTimePicker template customization rather than standalone time
/// selection.
/// </remarks>
[TemplatePart(Name = "PART_Input", Type = typeof(NativeTextBox))]
public class DateTimePickerTimeColumn : Control
{
    private NativeTextBox? _input;

    private bool _updatingInput;
    private bool _editingInput;
    private int  _wheelDelta;

    /// <summary>
    /// Identifies the field value dependency property. The default is zero; Maximum defines its
    /// range.
    /// </summary>
    public static readonly DependencyProperty ValueProperty
        = DependencyProperty.Register(
            nameof(Value), typeof(int), typeof(DateTimePickerTimeColumn),
            new FrameworkPropertyMetadata(
                0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged,
                CoerceValue
            )
        );

    /// <summary>
    /// Gets or sets the field value. Values outside the range are clamped to the nearest endpoint.
    /// </summary>
    public int Value
    {
        get => (int)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>
    /// Identifies the inclusive wrap limit. The default is 59; hour columns use 23.
    /// </summary>
    public static readonly DependencyProperty MaximumProperty
        = DependencyProperty.Register(
            nameof(Maximum), typeof(int), typeof(DateTimePickerTimeColumn),
            new PropertyMetadata(59, OnMaximumChanged),
            IsValidMaximum
        );

    /// <summary>
    /// Gets or sets the inclusive upper bound, from 1 through 99.
    /// </summary>
    public int Maximum
    {
        get => (int)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    private static readonly DependencyPropertyKey VisibleValuesPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(VisibleValues), typeof(IReadOnlyList<string>), typeof(DateTimePickerTimeColumn),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only collection of five visible values.
    /// </summary>
    public static readonly DependencyProperty VisibleValuesProperty
        = VisibleValuesPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets two-digit values at offsets -2, -1, 0, 1, and 2. The middle item is always the current
    /// value.
    /// </summary>
    public IReadOnlyList<string> VisibleValues
    {
        get => (IReadOnlyList<string>)GetValue(VisibleValuesProperty);
    }

    private static readonly DependencyPropertyKey HasValidInputPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(HasValidInput), typeof(bool), typeof(DateTimePickerTimeColumn),
            new PropertyMetadata(true, OnInputValidityChanged)
        );

    /// <summary>
    /// Notifies the owning picker to update input validity, including invalid drafts that leave the
    /// field value unchanged.
    /// </summary>
    internal event EventHandler? InputValidityChanged;

    /// <summary>
    /// Notifies the owner when field input validity changes.
    /// </summary>
    private static void OnInputValidityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var column = (DateTimePickerTimeColumn)d;
        column.InputValidityChanged?.Invoke(column, EventArgs.Empty);
    }

    /// <summary>
    /// Identifies the read-only input validity dependency property.
    /// </summary>
    public static readonly DependencyProperty HasValidInputProperty
        = HasValidInputPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the current text is a valid field value. Empty or out-of-range text is invalid.
    /// </summary>
    public bool HasValidInput
    {
        get => (bool)GetValue(HasValidInputProperty);
    }

    /// <summary>
    /// Identifies the bubbling field value change event.
    /// </summary>
    public static readonly RoutedEvent ValueChangedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(ValueChanged), RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<int>),
            typeof(DateTimePickerTimeColumn)
        );

    /// <summary>
    /// Occurs when the valid field value changes. Invalid text does not raise this event.
    /// </summary>
    public event RoutedPropertyChangedEventHandler<int> ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    /// <summary>
    /// Selects a field value by a wrapping integer offset. The parameter may be an integer or an
    /// integer string.
    /// </summary>
    public static RoutedUICommand StepCommand { get; } = new(
        "Step", nameof(StepCommand), typeof(DateTimePickerTimeColumn)
    );

    static DateTimePickerTimeColumn()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DateTimePickerTimeColumn),
            new FrameworkPropertyMetadata(typeof(DateTimePickerTimeColumn))
        );
    }

    /// <summary>
    /// Initializes the five-row cyclic viewport and step command.
    /// </summary>
    public DateTimePickerTimeColumn()
    {
        CommandBindings.Add(new CommandBinding(StepCommand, OnStepExecuted));
        UpdateVisibleValues();
    }

    private static bool IsValidMaximum(object value)
    {
        var maximum = (int)value;
        return maximum >= 1 && maximum <= 99;
    }

    private static object CoerceValue(DependencyObject d, object value)
    {
        var column            = (DateTimePickerTimeColumn)d;
        var upperBoundedValue = Math.Min(column.Maximum, (int)value);
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
        peer?.RaisePropertyChangedEvent(
            RangeValuePatternIdentifiers.ValueProperty,
            (double)(int)e.OldValue,
            (double)(int)e.NewValue
        );
    }

    /// <summary>
    /// Detaches old template events and paste handling so the old editor can no longer change the
    /// field value.
    /// </summary>
    private void DetachInput()
    {
        if (_input is not null)
        {
            _input.TextChanged       -= OnInputTextChanged;
            _input.PreviewTextInput  -= OnPreviewTextInput;
            _input.LostKeyboardFocus -= OnInputLostKeyboardFocus;
            _input.GotKeyboardFocus  -= OnInputGotKeyboardFocus;

            DataObject.RemovePastingHandler(_input, OnPasting);
        }

        _input = null;
    }

    /// <summary>
    /// Detaches the editor immediately when the template changes and restores valid input state.
    /// </summary>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        DetachInput();
        RefreshText();

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// Retrieves the new template editor and attaches input, focus, and paste handlers.
    /// </summary>
    public override void OnApplyTemplate()
    {
        DetachInput();

        base.OnApplyTemplate();

        _input = GetTemplateChild("PART_Input") as NativeTextBox;
        if (_input is not null)
        {
            InputMethod.SetIsInputMethodEnabled(_input, false);

            _input.TextChanged       += OnInputTextChanged;
            _input.PreviewTextInput  += OnPreviewTextInput;
            _input.LostKeyboardFocus += OnInputLostKeyboardFocus;
            _input.GotKeyboardFocus  += OnInputGotKeyboardFocus;

            DataObject.AddPastingHandler(_input, OnPasting);
        }

        RefreshText();
    }

    /// <summary>
    /// Builds a five-row viewport using modular arithmetic to keep the selected value centered.
    /// </summary>
    private void UpdateVisibleValues()
    {
        var values = new string[5];
        for (var index = 0; index < values.Length; index++)
        {
            var value = Wrap(Value + index - 2);
            values[index] = value.ToString("D2", CultureInfo.InvariantCulture);
        }

        var readOnlyValues = Array.AsReadOnly(values);
        SetValue(VisibleValuesPropertyKey, readOnlyValues);
    }

    /// <summary>
    /// Maps positive or negative offsets into the inclusive range from zero through Maximum.
    /// </summary>
    private int Wrap(int value)
    {
        var count = Maximum + 1;
        return (value % count + count) % count;
    }

    /// <summary>
    /// Restores input from the last valid value for new transactions and external value
    /// synchronization.
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
                var text = Value.ToString("D2", CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Applies valid numeric drafts immediately while retaining invalid text for correction.
    /// </summary>
    private void OnInputTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingInput || _input is null)
        {
            return;
        }

        var text   = _input.Text;
        var parsed = int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value);
        var valid  = parsed && IsDigits(text) && text.Length <= 2 && value <= Maximum;

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

        foreach (var digit in text)
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
        var parameter = Convert.ToString(e.Parameter, CultureInfo.InvariantCulture);
        if (int.TryParse(parameter, NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
        {
            Step(offset);
        }

        e.Handled = true;
    }

    private void Step(int offset)
    {
        var wrapped = Wrap(Value + offset % (Maximum + 1));
        SetCurrentValue(ValueProperty, wrapped);
        RefreshText();
    }

    /// <summary>
    /// Steps once per standard 120-unit wheel delta, retaining high-resolution wheel remainders for
    /// the next event.
    /// </summary>
    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);

        _wheelDelta += e.Delta;
        var steps = _wheelDelta / Mouse.MouseWheelDeltaForOneLine;
        _wheelDelta %= Mouse.MouseWheelDeltaForOneLine;

        if (steps != 0)
        {
            Step(-steps);
        }

        e.Handled = true;
    }

    /// <summary>
    /// Selects the previous or next value with Up or Down; Home and End select the field endpoints.
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
    /// Exposes a numeric range that assistive technology can read and write.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new TimeColumnAutomationPeer(this);
    }

    /// <summary>
    /// Exposes the field as an integer spinner with the column's current range and enabled state.
    /// </summary>
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
                throw new ElementNotEnabledException();
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
