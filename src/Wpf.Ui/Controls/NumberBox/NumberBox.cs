// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// This Source Code is partially based on the source code provided by the .NET Foundation.

// TODO: Mask (with placeholder);
// TODO: Constant decimals when formatting. Although this can actually be done with NumberFormatter.
// TODO: Disable expression by default
// TODO: Lock to digit characters only by property
//

using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using WinButton = System.Windows.Controls.Button;
using WinRepeatButton = System.Windows.Controls.Primitives.RepeatButton;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a control that can be used to display and edit numbers.
/// </summary>
[TemplatePart(Name = PART_ClearButton,           Type = typeof(WinButton))]
[TemplatePart(Name = PART_InlineIncrementButton, Type = typeof(WinRepeatButton))]
[TemplatePart(Name = PART_InlineDecrementButton, Type = typeof(WinRepeatButton))]
public class NumberBox : TextBox
{
    // todo: 这些按钮并非刚需控件，没有相关依赖，没必要作为PART出现
    // Template part names
    private const string PART_ClearButton           = nameof(PART_ClearButton);
    private const string PART_InlineIncrementButton = nameof(PART_InlineIncrementButton);
    private const string PART_InlineDecrementButton = nameof(PART_InlineDecrementButton);

    private bool _valueUpdating;

    #region Dependency properties

    #region ValueProperty

    /// <summary>Identifies the <see cref="Value"/> dependency property.</summary>
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value),
        typeof(double?),
        typeof(NumberBox),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.OnValueChanged(d, (double?)e.OldValue);
                }
            },
            static (d, baseValue) =>
            {
                if (d is not NumberBox numberBox)
                {
                    return baseValue;
                }

                if (baseValue is double dv)
                {
                    var clamped = dv;
                    var maximum = numberBox.Maximum;
                    var minimum = numberBox.Minimum;

                    if (clamped > maximum)
                    {
                        clamped = maximum;
                    }

                    if (clamped < minimum)
                    {
                        clamped = minimum;
                    }

                    return clamped;
                }

                return baseValue;
            },
            false,
            UpdateSourceTrigger.LostFocus
        )
    );

    /// <summary>
    /// Gets or sets the numeric value of a <see cref="NumberBox"/>.
    /// </summary>
    public double? Value
    {
        get => (double?)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    #endregion

    #region MaxDecimalPlacesProperty

    /// <summary>Identifies the <see cref="MaxDecimalPlaces"/> dependency property.</summary>
    public static readonly DependencyProperty MaxDecimalPlacesProperty = DependencyProperty.Register(
        nameof(MaxDecimalPlaces),
        typeof(int),
        typeof(NumberBox),
        new PropertyMetadata(6),
        static value => value is >= 0 and <= 15
    );

    /// <summary>
    /// Gets or sets the number of decimal places to be rounded when converting from Text to Value.
    /// </summary>
    public int MaxDecimalPlaces
    {
        get => (int)GetValue(MaxDecimalPlacesProperty);
        set => SetValue(MaxDecimalPlacesProperty, value);
    }

    #endregion

    #region SmallChangeProperty

    /// <summary>Identifies the <see cref="SmallChange"/> dependency property.</summary>
    public static readonly DependencyProperty SmallChangeProperty = DependencyProperty.Register(
        nameof(SmallChange),
        typeof(double),
        typeof(NumberBox),
        new PropertyMetadata(
            1.0d,
            null,
            static (d, baseValue) =>
            {
                if (baseValue is double dv)
                {
                    // Coerce only adjusts relative to instance state (range);
                    // positivity/finite checks are handled by Validate callbacks.
                    if (d is NumberBox nb)
                    {
                        var range = nb.Maximum - nb.Minimum;
                        if (!double.IsNaN(range) && !double.IsInfinity(range) && range > 0 && dv > range)
                        {
                            return range;
                        }
                    }

                    return dv;
                }

                return 1.0d;
            }
        ),
        static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv) && dv > 0
    );

    /// <summary>
    /// Gets or sets the value that is added to or subtracted from <see cref="Value"/> when a small change is made, such as with an arrow key or scrolling.
    /// </summary>
    public double SmallChange
    {
        get => (double)GetValue(SmallChangeProperty);
        set => SetValue(SmallChangeProperty, value);
    }

    #endregion

    #region LargeChangeProperty

    /// <summary>Identifies the <see cref="LargeChange"/> dependency property.</summary>
    public static readonly DependencyProperty LargeChangeProperty = DependencyProperty.Register(
        nameof(LargeChange),
        typeof(double),
        typeof(NumberBox),
        new PropertyMetadata(
            10.0d,
            null,
            static (d, baseValue) =>
            {
                if (baseValue is double dv)
                {
                    // Coerce large change relative to instance range;
                    // validation rejects non-positive/invalid values.
                    if (d is NumberBox nb)
                    {
                        var range = nb.Maximum - nb.Minimum;
                        if (!double.IsNaN(range) && !double.IsInfinity(range) && range > 0 && dv > range)
                        {
                            return range;
                        }
                    }

                    return dv;
                }

                return 10.0d;
            }
        ),
        static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv) && dv > 0
    );

    /// <summary>
    /// Gets or sets the value that is added to or subtracted from <see cref="Value"/> when a large change is made, such as with the PageUP and PageDown keys.
    /// </summary>
    public double LargeChange
    {
        get => (double)GetValue(LargeChangeProperty);
        set => SetValue(LargeChangeProperty, value);
    }

    #endregion

    #region MaximumProperty

    /// <summary>Identifies the <see cref="Maximum"/> dependency property.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum),
        typeof(double),
        typeof(NumberBox),
        new FrameworkPropertyMetadata(
            double.MaxValue,
            FrameworkPropertyMetadataOptions.None,
            static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.CoerceValue(ValueProperty);
                }
            },
            static (d, baseValue) =>
            {
                if (d is not NumberBox numberBox)
                {
                    return baseValue;
                }

                if (baseValue is double dv)
                {
                    // Coerce Maximum to be at least Minimum
                    var minimum = numberBox.Minimum;
                    return dv < minimum ? minimum : dv;
                }

                return baseValue;
            }
        ),
        static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv)
    );

    /// <summary>
    /// Gets or sets the numerical maximum for <see cref="Value"/>.
    /// </summary>
    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    #endregion

    #region MinimumProperty

    /// <summary>Identifies the <see cref="Minimum"/> dependency property.</summary>
    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum),
        typeof(double),
        typeof(NumberBox),
        new FrameworkPropertyMetadata(
            double.MinValue,
            FrameworkPropertyMetadataOptions.None,
            static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.CoerceValue(ValueProperty);
                }
            },
            static (d, baseValue) =>
            {
                if (d is not NumberBox numberBox)
                {
                    return baseValue;
                }

                if (baseValue is double dv)
                {
                    // Coerce Minimum to be at most Maximum
                    var maximum = numberBox.Maximum;
                    return dv > maximum ? maximum : dv;
                }

                return baseValue;
            }
        ),
        static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv)
    );

    /// <summary>
    /// Gets or sets the numerical minimum for <see cref="Value"/>.
    /// </summary>
    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    #endregion

    #region AcceptsExpressionProperty

    /// <summary>Identifies the <see cref="AcceptsExpression"/> dependency property.</summary>
    public static readonly DependencyProperty AcceptsExpressionProperty = DependencyProperty.Register(
        nameof(AcceptsExpression),
        typeof(bool),
        typeof(NumberBox),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the control will accept and evaluate a basic formulaic expression entered as input.
    /// </summary>
    public bool AcceptsExpression
    {
        get => (bool)GetValue(AcceptsExpressionProperty);
        set => SetValue(AcceptsExpressionProperty, value);
    }

    #endregion

    #region SpinButtonPlacementModeProperty

    /// <summary>Identifies the <see cref="SpinButtonPlacementMode"/> dependency property.</summary>
    public static readonly DependencyProperty SpinButtonPlacementModeProperty = DependencyProperty.Register(
        nameof(SpinButtonPlacementMode),
        typeof(NumberBoxSpinButtonPlacementMode),
        typeof(NumberBox),
        new PropertyMetadata(NumberBoxSpinButtonPlacementMode.Inline)
    );

    /// <summary>
    /// Gets or sets a value that indicates the placement of buttons used to increment or decrement the <see cref="Value"/> property.
    /// </summary>
    public NumberBoxSpinButtonPlacementMode SpinButtonPlacementMode
    {
        get => (NumberBoxSpinButtonPlacementMode)GetValue(SpinButtonPlacementModeProperty);
        set => SetValue(SpinButtonPlacementModeProperty, value);
    }

    #endregion

    #region ValidationModeProperty

    /// <summary>Identifies the <see cref="ValidationMode"/> dependency property.</summary>
    public static readonly DependencyProperty ValidationModeProperty = DependencyProperty.Register(
        nameof(ValidationMode),
        typeof(NumberBoxValidationMode),
        typeof(NumberBox),
        new PropertyMetadata(NumberBoxValidationMode.InvalidInputOverwritten)
    );

    /// <summary>
    /// Gets or sets the input validation behavior to invoke when invalid input is entered.
    /// </summary>
    public NumberBoxValidationMode ValidationMode
    {
        get => (NumberBoxValidationMode)GetValue(ValidationModeProperty);
        set => SetValue(ValidationModeProperty, value);
    }

    #endregion

    #region NumberFormatterProperty

    /// <summary>Identifies the <see cref="NumberFormatter"/> dependency property.</summary>
    public static readonly DependencyProperty NumberFormatterProperty = DependencyProperty.Register(
        nameof(NumberFormatter),
        typeof(INumberFormatter),
        typeof(NumberBox),
        new PropertyMetadata(null),
        static (value) => value is null or INumberParser
    );

    /// <summary>
    /// Gets or sets the number formatter.
    /// </summary>
    public INumberFormatter? NumberFormatter
    {
        get => (INumberFormatter?)GetValue(NumberFormatterProperty);
        set => SetValue(NumberFormatterProperty, value);
    }

    #endregion

    #endregion

    #region Events

    /// <summary>Identifies the <see cref="ValueChanged"/> routed event.</summary>
    public static readonly RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(ValueChanged),
        RoutingStrategy.Bubble,
        typeof(NumberBoxValueChangedEvent),
        typeof(NumberBox)
    );

    /// <summary>
    /// Occurs after the user triggers evaluation of new input by pressing the Enter key, clicking a spin button, or by changing focus.
    /// </summary>
    public event NumberBoxValueChangedEvent ValueChanged
    {
        add => AddHandler(ValueChangedEvent, value);
        remove => RemoveHandler(ValueChangedEvent, value);
    }

    #endregion

    static NumberBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(typeof(NumberBox)));
        // todo: 已经强制最大最小行为1了，是否接受回车键是否还有必要强制？
        AcceptsReturnProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(false));
        // todo: 强制最大最小行数为1
        MaxLinesProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(1));
        MinLinesProperty.OverrideMetadata(typeof(NumberBox), new FrameworkPropertyMetadata(1));
    }

    public NumberBox()
    {
        NumberFormatter ??= NumberBox.GetRegionalSettingsAwareDecimalFormatter();

        DataObject.AddPastingHandler(this, OnClipboardPaste);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.PageUp:
                StepValue(LargeChange);
                e.Handled = true;
                break;
            case Key.PageDown:
                StepValue(-LargeChange);
                e.Handled = true;
                break;
            case Key.Up:
                StepValue(SmallChange);
                e.Handled = true;
                break;
            case Key.Down:
                StepValue(-SmallChange);
                e.Handled = true;
                break;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (TextWrapping != TextWrapping.Wrap)
                {
                    ValidateInput();
                    MoveCaretToTextEnd();
                }

                e.Handled = true;
                break;

            case Key.Escape:
                UpdateTextToValue();
                e.Handled = true;
                break;
        }

        base.OnPreviewKeyUp(e);
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        var oldValue = Value;
        ValidateInput();

        // Update binding source if value changed
        if (!Equals(oldValue, Value))
        {
            GetBindingExpression(ValueProperty)?.UpdateSource();
        }
    }

    /*/// <inheritdoc />
    protected override void OnTextChanged(System.Windows.Controls.TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        //if (new string[] { ",", ".", " " }.Any(s => Text.EndsWith(s)))
        //    return;

        //if (!_textUpdating)
        //    UpdateValueToText();
    }*/

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // todo: 对于事件，订阅路由事件，并区分按钮参数（TAG或其他东西用于区分）来执行相应逻辑
        SubscribeToButtonClickEvent<WinButton>(PART_ClearButton, OnClearButtonClick);
        SubscribeToButtonClickEvent<WinRepeatButton>(PART_InlineIncrementButton, () => StepValue(SmallChange));
        SubscribeToButtonClickEvent<WinRepeatButton>(PART_InlineDecrementButton, () => StepValue(-SmallChange));

        // If Text has been set, but Value hasn't, update Value based on Text.
        if (string.IsNullOrEmpty(Text) && Value != null)
        {
            UpdateValueToText();
        }
        else
        {
            UpdateTextToValue();
        }

        base.OnApplyTemplate();
    }

    private void SubscribeToButtonClickEvent<TButton>(string elementName, Action action)
        where TButton : ButtonBase
    {
        if (GetTemplateChild(elementName) is TButton button)
        {
            button.Click += (s, e) =>
            {
                action();
            };
        }
    }

    /// <summary>
    /// Is called when <see cref="Value"/> in this <see cref="NumberBox"/> changes.
    /// </summary>
    protected virtual void OnValueChanged(DependencyObject d, double? oldValue)
    {
        if (_valueUpdating)
        {
            return;
        }

        _valueUpdating = true;

        var newValue = Value;

        // Coercion is handled by CoerceValueCallback at the property system level.

        if (!Equals(newValue, oldValue))
        {
            RaiseEvent(new NumberBoxValueChangedEventArgs(oldValue, newValue, this));
        }

        UpdateTextToValue();

        _valueUpdating = false;
    }

    /// <summary>
    /// Is called when something is pasted in this <see cref="NumberBox"/>.
    /// </summary>
    protected virtual void OnClipboardPaste(object sender, DataObjectPastingEventArgs e)
    {
        // Handle pasted text: try to parse the resulting text and accept/format it immediately.
        // If parsing fails and validation mode is set to overwrite invalid input, cancel paste and restore.
        if (sender is not NumberBox nb)
        {
            return;
        }

        if (e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            var pasteText = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;

            // Compute resulting text after paste (respecting current selection)
            var current   = nb.Text;
            var selStart  = Math.Max(0, nb.SelectionStart);
            var selLength = Math.Max(0, nb.SelectionLength);

            var result = selLength > 0
                ? current.Remove(selStart, selLength).Insert(selStart, pasteText)
                : current.Insert(selStart, pasteText);

            var trimmed = result.Trim();

            var parser = nb.NumberFormatter as INumberParser;
            var parsed = parser?.ParseDouble(trimmed);

            if (parsed is not null)
            {
                // Accept parsed value: update Value and display formatted text instead of raw paste
                nb.SetCurrentValue(ValueProperty, parsed);
                nb.UpdateTextToValue();
                nb.MoveCaretToTextEnd();
                e.CancelCommand();
                return;
            }

            if (nb.ValidationMode == NumberBoxValidationMode.InvalidInputOverwritten)
            {
                // Reject paste and restore formatted value
                e.CancelCommand();
                nb.UpdateTextToValue();
                return;
            }
        }

        // Let the default paste occur, then validate asynchronously to avoid interfering with paste operation
        nb.Dispatcher.BeginInvoke(() => nb.ValidateInput(), DispatcherPriority.Input);
    }

    private void StepValue(double change)
    {
        // Before adjusting the value, validate the contents of the textbox so we don't override it.
        ValidateInput();

        var newValue = Value ?? 0;
        newValue += change;

        SetCurrentValue(ValueProperty, newValue);

        MoveCaretToTextEnd();
    }

    private void UpdateTextToValue()
    {
        var newText = string.Empty;

        if (Value is not null && NumberFormatter is not null)
        {
            newText = NumberFormatter.FormatDouble(Math.Round((double)Value, MaxDecimalPlaces));
        }

        SetCurrentValue(TextProperty, newText);
    }

    private void UpdateValueToText()
    {
        ValidateInput();
    }

    private void ValidateInput()
    {
        var text = Text.Trim();

        if (string.IsNullOrEmpty(text))
        {
            SetCurrentValue(ValueProperty, null);

            return;
        }

        var numberParser = NumberFormatter as INumberParser;
        var value        = numberParser!.ParseDouble(text);

        // If parsing failed
        if (value is null)
        {
            if (ValidationMode == NumberBoxValidationMode.InvalidInputOverwritten)
            {
                // Restore formatted current Value
                UpdateTextToValue();
            }

            // If validation is disabled, leave the user's text as-is
            return;
        }

        // If parsed value equals current Value, reformat text to keep consistent display
        if (Equals(Value, value))
        {
            UpdateTextToValue();

            return;
        }

        SetCurrentValue(ValueProperty, value);
    }

    private void MoveCaretToTextEnd()
    {
        CaretIndex = Text.Length;
    }

    private static ValidateNumberFormatter GetRegionalSettingsAwareDecimalFormatter()
    {
        return new ValidateNumberFormatter();
    }
}