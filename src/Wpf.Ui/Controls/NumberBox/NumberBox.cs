// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// This Source Code is partially based on the source code provided by the .NET Foundation.

using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

[TemplatePart(Name = "PART_CompactPopup", Type = typeof(Popup))]
public class NumberBox : TextBox
{
    private bool    _valueUpdating;
    private Popup?  _compactPopup;
    private Window? _parentWindow;

    #region Dependency properties

    #region ButtonKindProperty

    // Attached property to mark template buttons with their function.
    public static readonly DependencyProperty ButtonKindProperty =
        DependencyProperty.RegisterAttached(
            "ButtonKind",
            typeof(NumberBoxButtonKind),
            typeof(NumberBox),
            new PropertyMetadata(NumberBoxButtonKind.None)
        );

    /// <summary>
    /// Helper for setting <see cref="ButtonKindProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ButtonKindProperty"/> on.
    /// </param>
    /// <param name="value">
    /// ButtonKind property value.
    /// </param>
    public static void SetButtonKind(DependencyObject element, NumberBoxButtonKind value)
    {
        element.SetValue(ButtonKindProperty, value);
    }

    /// <summary>
    /// Helper for getting <see cref="ButtonKindProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ButtonKindProperty"/> from.
    /// </param>
    /// <returns>
    /// ButtonKind property value.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static NumberBoxButtonKind GetButtonKind(DependencyObject element)
    {
        return (NumberBoxButtonKind)element.GetValue(ButtonKindProperty);
    }

    #endregion

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

    #region DecimalPlacesProperty

    /// <summary>Identifies the <see cref="DecimalPlaces"/> dependency property.</summary>
    public static readonly DependencyProperty DecimalPlacesProperty = DependencyProperty.Register(
        nameof(DecimalPlaces),
        typeof(int?),
        typeof(NumberBox),
        new PropertyMetadata(null),
        static value => value is null or (>= 0 and <= 15)
    );

    /// <summary>
    /// Gets or sets the number of decimal places to always display when formatting the value.
    /// When set, the value is always shown with exactly this many decimal places (e.g., <c>1</c> displays as <c>1.00</c> when <see cref="DecimalPlaces"/> is <c>2</c>).
    /// When <see langword="null"/>, the default formatter behavior is used.
    /// </summary>
    /// <example>
    /// <code lang="xml">
    /// &lt;ui:NumberBox DecimalPlaces="2" /&gt;
    /// </code>
    /// </example>
    public int? DecimalPlaces
    {
        get => (int?)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
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
        new PropertyMetadata(
            false,
            static (d, e) =>
            {
                if (d is NumberBox nb && e.NewValue is false)
                {
                    nb.ValidateInput();
                }
            }
        )
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

    #region RestrictInputToNumbersProperty

    /// <summary>Identifies the <see cref="RestrictInputToNumbers"/> dependency property.</summary>
    public static readonly DependencyProperty RestrictInputToNumbersProperty = DependencyProperty.Register(
        nameof(RestrictInputToNumbers),
        typeof(bool),
        typeof(NumberBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether keyboard input is restricted to numeric characters only.
    /// When <see langword="true"/>, non-numeric characters (except decimal separators and sign characters) are blocked at input time.
    /// When <see langword="false"/>, input is unrestricted and validation occurs on commit.
    /// </summary>
    /// <example>
    /// <code lang="xml">
    /// &lt;ui:NumberBox RestrictInputToNumbers="True" /&gt;
    /// </code>
    /// </example>
    public bool RestrictInputToNumbers
    {
        get => (bool)GetValue(RestrictInputToNumbersProperty);
        set => SetValue(RestrictInputToNumbersProperty, value);
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
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(typeof(NumberBox))
        );

        AcceptsReturnProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                false,
                null,
                (d, baseValue) => false
            )
        );

        MaxLinesProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                1,
                null,
                (d, baseValue) => 1
            )
        );

        MinLinesProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                1,
                null,
                (d, baseValue) => 1
            )
        );
    }

    public NumberBox()
    {
        NumberFormatter ??= GetRegionalSettingsAwareDecimalFormatter();

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
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (RestrictInputToNumbers && !IsReadOnly)
        {
            var text             = e.Text;
            var decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var groupSeparator   = CultureInfo.CurrentCulture.NumberFormat.NumberGroupSeparator;

            foreach (var ch in text)
            {
                var s = ch.ToString();
                if (!char.IsDigit(ch)
                 && s != decimalSeparator
                 && s != groupSeparator
                 && ch != '-'
                 && ch != '+')
                {
                    // Allow expression operators only when AcceptsExpression is enabled
                    if (!AcceptsExpression || (ch != '*' && ch != '/' && ch != '(' && ch != ')'))
                    {
                        e.Handled = true;
                        break;
                    }
                }
            }
        }

        base.OnPreviewTextInput(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
    {
        // Only respond to mouse wheel when control is not read-only and has keyboard focus.
        if (IsReadOnly)
        {
            base.OnPreviewMouseWheel(e);
            return;
        }

        if (!IsKeyboardFocusWithin)
        {
            // Let parent controls (e.g., ScrollViewer) handle the wheel when this control isn't focused.
            base.OnPreviewMouseWheel(e);
            return;
        }

        StepValue(e.Delta > 0 ? SmallChange : -SmallChange);
        e.Handled = true;

        base.OnPreviewMouseWheel(e);
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

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        #pragma warning disable WPF0091 // Name the invoked method OnEventName
        #pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue

        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        _compactPopup?.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        _compactPopup = GetTemplateChild("PART_CompactPopup") as Popup;

        if (_compactPopup is not null)
        {
            _compactPopup.CustomPopupPlacementCallback = CompactPopupPlacementCallback;
            _compactPopup.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }

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

        #pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue
        #pragma warning restore WPF0091 // Name the invoked method OnEventName
    }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        Loaded   += OnNumberBoxLoaded;
        Unloaded += OnNumberBoxUnloaded;
    }

    private void OnNumberBoxLoaded(object sender, RoutedEventArgs e)
    {
        SizeChanged   += OnNumberBoxSizeChanged;
        LayoutUpdated += OnNumberBoxLayoutUpdated;

        var window = Window.GetWindow(this);
        if (window == _parentWindow)
        {
            return;
        }

        if (_parentWindow is not null)
        {
            _parentWindow.LocationChanged -= OnParentWindowLocationChanged;
            _parentWindow.SizeChanged     -= OnParentWindowSizeChanged;
        }

        _parentWindow = window;

        if (_parentWindow is not null)
        {
            _parentWindow.LocationChanged += OnParentWindowLocationChanged;
            _parentWindow.SizeChanged     += OnParentWindowSizeChanged;
        }
    }

    private void OnNumberBoxUnloaded(object sender, RoutedEventArgs e)
    {
        SizeChanged   -= OnNumberBoxSizeChanged;
        LayoutUpdated -= OnNumberBoxLayoutUpdated;

        if (_parentWindow is not null)
        {
            _parentWindow.LocationChanged -= OnParentWindowLocationChanged;
            _parentWindow.SizeChanged     -= OnParentWindowSizeChanged;
            _parentWindow                 =  null;
        }
    }

    private void OnParentWindowLocationChanged(object? sender, EventArgs e) => RefreshCompactPopupPosition();

    private void OnParentWindowSizeChanged(object sender, SizeChangedEventArgs e) => RefreshCompactPopupPosition();

    private void OnNumberBoxSizeChanged(object sender, SizeChangedEventArgs e) => RefreshCompactPopupPosition();

    private void OnNumberBoxLayoutUpdated(object? sender, EventArgs e) => RefreshCompactPopupPosition();

    private void RefreshCompactPopupPosition()
    {
        if (_compactPopup is not { IsOpen: true })
        {
            return;
        }

        // Nudge HorizontalOffset to force WPF to re-invoke the placement callback.
        _compactPopup.HorizontalOffset += 0.01;
        _compactPopup.HorizontalOffset -= 0.01;
    }

    private CustomPopupPlacement[] CompactPopupPlacementCallback(Size popupSize, Size targetSize, Point offset)
    {
        // Place popup so its left edge aligns with the control's right edge,
        // then shift it left by 27px so it visually overlays the classic buttons area.
        //
        // Note: The 27px offset represents the total horizontal footprint of the small
        // classic buttons, including both their width and surrounding margin. This ensures
        // consistent visual alignment across different DPI settings.
        var dpi = VisualTreeHelper.GetDpi(this);
        var x   = targetSize.Width - (27.0 * dpi.DpiScaleX);
        var y   = (targetSize.Height - popupSize.Height) / 2.0;
        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None)];
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is ButtonBase btn)
        {
            var buttonKind = GetButtonKind(btn);

            switch (buttonKind)
            {
                case NumberBoxButtonKind.Clear:
                    OnClearButtonClick();
                    break;

                case NumberBoxButtonKind.Increment:
                    StepValue(SmallChange);
                    break;

                case NumberBoxButtonKind.Decrement:
                    StepValue(-SmallChange);
                    break;

                case NumberBoxButtonKind.None:
                default:
                    break;
            }
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
            var d = Math.Round((double)Value, MaxDecimalPlaces);
            newText = DecimalPlaces is int dp
                ? d.ToString("F" + dp, CultureInfo.CurrentCulture)
                : NumberFormatter.FormatDouble(d);
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

        // If direct parsing failed, try expression evaluation
        if (value is null && AcceptsExpression)
        {
            if (NumberBoxExpressionParser.TryEvaluate(text, out var exprResult))
            {
                value = exprResult;
            }
        }

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