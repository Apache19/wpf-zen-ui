// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// This Source Code is partially based on the source code provided by the .NET Foundation.

using System.Windows.Automation.Peers;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.AutomationPeers;

// ReSharper disable UnusedParameter.Local
// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a single-line numeric editor with optional spin buttons, range constraints,
/// culture-aware formatting, and basic arithmetic-expression evaluation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> is the authoritative numeric state. Text typed by the user is committed
/// when focus leaves the control, Enter is pressed, a spin operation is requested, or a paste is
/// accepted. Invalid text is restored or retained according to <see cref="ValidationMode"/>.
/// </para>
/// <para>
/// The default template requires <c>PART_ContentHost</c> through the inherited WPF text-box
/// contract. <c>PART_CompactPopup</c> and <c>PART_SpinButtons</c> are optional; compact placement
/// falls back to the control bounds when either is absent. Template buttons declare their
/// operation through <see cref="ButtonKindProperty"/>.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_CompactPopup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_SpinButtons",  Type = typeof(FrameworkElement))]
public class NumberBox : TextBox
{
    private static readonly ValidateNumberFormatter DefaultNumberFormatter = new();

    private Popup?            _compactPopup;
    private FrameworkElement? _spinButtons;
    private Window?           _parentWindow;

    #region Dependency properties

    #region ButtonKindProperty

    // Attached property to mark template buttons with their function.
    public static readonly DependencyProperty ButtonKindProperty =
        DependencyProperty.RegisterAttached(
            "ButtonKind",
            typeof(NumberBoxButtonKind),
            typeof(NumberBox),
            new PropertyMetadata(NumberBoxButtonKind.None),
            static value => value is NumberBoxButtonKind kind
                         && Enum.IsDefined(typeof(NumberBoxButtonKind), kind)
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
            propertyChangedCallback: static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.OnValueChangedCore((double?)e.OldValue, (double?)e.NewValue);
                }
            },
            coerceValueCallback: static (d, baseValue) =>
            {
                if (d is not NumberBox numberBox)
                {
                    return baseValue;
                }

                if (baseValue is double dv)
                {
                    // Value owns the committed precision invariant. Applying it in coercion keeps
                    // bindings, automation, direct assignment, expression input, and spin input
                    // consistent instead of rounding only the displayed string.
                    var clamped = Math.Round(dv, numberBox.MaxDecimalPlaces);

                    if (clamped > numberBox.Maximum)
                    {
                        clamped = numberBox.Maximum;
                    }

                    if (clamped < numberBox.Minimum)
                    {
                        clamped = numberBox.Minimum;
                    }

                    return clamped;
                }

                return baseValue;
            },
            false,
            UpdateSourceTrigger.LostFocus
        ),
        static value => value is null || (value is double dv && !double.IsNaN(dv))
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
        new PropertyMetadata(
            null,
            static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.UpdateTextToValue();
                }
            }
        ),
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
        new PropertyMetadata(
            6,
            propertyChangedCallback: static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    numberBox.CoerceValue(ValueProperty);
                    numberBox.UpdateTextToValue();
                }
            }
        ),
        validateValueCallback: static value => value is >= 0 and <= 15
    );

    /// <summary>
    /// Gets or sets the maximum number of decimal places retained by <see cref="Value"/>.
    /// Changing this property immediately rounds the current value; range coercion is applied
    /// afterward, so a more precise boundary remains authoritative when necessary.
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
            static (d, e) => NumberBoxAutomationPeer.RaiseSmallChangePropertyChangedEvent(
                (NumberBox)d,
                (double)e.OldValue,
                (double)e.NewValue
            ),
            coerceValueCallback: static (d, baseValue) =>
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
        validateValueCallback: static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv) && dv > 0
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
            static (d, e) => NumberBoxAutomationPeer.RaiseLargeChangePropertyChangedEvent(
                (NumberBox)d,
                (double)e.OldValue,
                (double)e.NewValue
            ),
            coerceValueCallback: static (d, baseValue) =>
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
        validateValueCallback: static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv) && dv > 0
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
            propertyChangedCallback: static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    // Step coercion depends on the effective range. Re-running it here both
                    // tightens a step after a range reduction and restores its base value after
                    // the range expands again.
                    numberBox.CoerceValue(SmallChangeProperty);
                    numberBox.CoerceValue(LargeChangeProperty);
                    numberBox.CoerceValue(ValueProperty);
                    NumberBoxAutomationPeer.RaiseMaximumPropertyChangedEvent(
                        numberBox,
                        (double)e.OldValue,
                        (double)e.NewValue
                    );
                }
            },
            coerceValueCallback: static (d, baseValue) =>
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
        validateValueCallback: static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv)
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
            propertyChangedCallback: static (d, e) =>
            {
                if (d is NumberBox numberBox)
                {
                    // Match WPF RangeBase's one-way range invariant: Minimum is authoritative and
                    // Maximum is coerced upward. Coercing Maximum again is essential because WPF
                    // retains its uncoerced base value and can restore it when Minimum decreases.
                    numberBox.CoerceValue(MaximumProperty);
                    numberBox.CoerceValue(SmallChangeProperty);
                    numberBox.CoerceValue(LargeChangeProperty);
                    numberBox.CoerceValue(ValueProperty);
                    NumberBoxAutomationPeer.RaiseMinimumPropertyChangedEvent(
                        numberBox,
                        (double)e.OldValue,
                        (double)e.NewValue
                    );
                }
            }
        ),
        validateValueCallback: static v => v is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv)
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
        new PropertyMetadata(NumberBoxSpinButtonPlacementMode.Inline),
        static value => value is NumberBoxSpinButtonPlacementMode mode
                     && Enum.IsDefined(typeof(NumberBoxSpinButtonPlacementMode), mode)
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
        new PropertyMetadata(NumberBoxValidationMode.InvalidInputOverwritten),
        static value => value is NumberBoxValidationMode mode
                     && Enum.IsDefined(typeof(NumberBoxValidationMode), mode)
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
        new PropertyMetadata(
            DefaultNumberFormatter,
            static (d, e) => ((NumberBox)d).UpdateTextToValue()
        ),
        static value => value is INumberFormatter and INumberParser
    );

    /// <summary>
    /// Gets or sets the formatter and parser used for committed numeric text.
    /// The value must implement both <see cref="INumberFormatter"/> and <see cref="INumberParser"/>.
    /// Changing it immediately reformats the current <see cref="Value"/>.
    /// </summary>
    public INumberFormatter NumberFormatter
    {
        get => (INumberFormatter)GetValue(NumberFormatterProperty);
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
    /// Occurs whenever the effective <see cref="Value"/> changes, whether through user input,
    /// automation, binding, coercion, or direct property assignment.
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

        IsReadOnlyProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                false,
                static (d, e) => NumberBoxAutomationPeer.RaiseIsReadOnlyPropertyChangedEvent(
                    (NumberBox)d,
                    (bool)e.OldValue,
                    (bool)e.NewValue
                )
            )
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberBox"/> class with the current-culture
    /// formatter and installs paste validation without replacing inherited TextBox behavior.
    /// </summary>
    public NumberBox()
    {
        DataObject.AddPastingHandler(this, OnClipboardPaste);
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            // TextBoxBase forwards this virtual call to WPF's internal TextEditor. Read-only text
            // still needs that path for caret movement, selection, and copy gestures.
            base.OnPreviewKeyDown(e);
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

        // Detach handlers from the previous visual tree before WPF replaces the template parts.
        RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        _compactPopup?.RemoveHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        base.OnApplyTemplate();

        // A template can be reapplied more than once. Keep exactly one class-level handler so
        // a single press cannot increment the value multiple times after a theme change.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        _compactPopup = GetTemplateChild("PART_CompactPopup") as Popup;
        _spinButtons  = GetTemplateChild("PART_SpinButtons") as FrameworkElement;

        if (_compactPopup is not null)
        {
            _compactPopup.CustomPopupPlacementCallback = CompactPopupPlacementCallback;
            _compactPopup.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        }

        // Text-only initialization is supported because NumberBox inherits TextBox.Text. Once a
        // Value exists it remains authoritative when both properties were supplied in XAML.
        if (!string.IsNullOrWhiteSpace(Text) && Value is null)
        {
            UpdateValueToText();
        }
        else
        {
            UpdateTextToValue();
        }

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
        if (!TryGetScreenOrigin(this, out var targetOrigin) ||
            _spinButtons is null ||
            !TryGetScreenBounds(_spinButtons, out var spinButtonBounds))
        {
            // The callback can run while a template is being detached. Keep the popup usable
            // during that transient state; the next layout pass will retry with the real part.
            var fallbackX = FlowDirection == FlowDirection.RightToLeft
                ? -targetSize.Width
                : targetSize.Width - popupSize.Width;
            return
            [
                new CustomPopupPlacement(
                    new Point(fallbackX, (targetSize.Height - popupSize.Height) / 2.0),
                    PopupPrimaryAxis.None
                ),
            ];
        }

        // WPF invokes this callback with sizes expressed in physical screen pixels after all DPI,
        // layout, render, and FlowDirection transforms. Its returned point is relative to the
        // target's transformed local (0, 0), which is the physical right edge under RTL—not the
        // target's axis-aligned left edge. PointToScreen supplies that exact origin and prevents
        // the large RTL offset caused by treating targetSize.Width as a left-to-right coordinate.
        //
        // The large popup grows toward the outside edge: in LTR its left edge covers the small
        // buttons; in RTL its right edge does. Measuring the named part keeps this valid when a
        // custom template changes the classic buttons' size, margin, alignment, or transform.
        var popupLeft = FlowDirection == FlowDirection.RightToLeft
            ? spinButtonBounds.Right - popupSize.Width
            : spinButtonBounds.Left;
        var popupTop = spinButtonBounds.Top + ((spinButtonBounds.Height - popupSize.Height) / 2.0);
        var x        = popupLeft - targetOrigin.X;
        var y        = popupTop - targetOrigin.Y;

        return [new CustomPopupPlacement(new Point(x, y), PopupPrimaryAxis.None)];
    }

    /// <summary>
    /// Gets the transformed local origin used by WPF as the base point for custom Popup placement.
    /// </summary>
    private static bool TryGetScreenOrigin(FrameworkElement element, out Point origin)
    {
        origin = default;

        if (PresentationSource.FromVisual(element) is null)
        {
            return false;
        }

        try
        {
            origin = element.PointToScreen(default);
            return true;
        }
        catch (InvalidOperationException)
        {
            // PointToScreen throws while PresentationSource is being replaced. The next layout
            // pass retries placement after the element reconnects to its presentation source.
            return false;
        }
    }

    /// <summary>
    /// Gets an element's axis-aligned screen bounds in physical pixels, matching the coordinate
    /// space used internally by WPF's custom Popup placement callback.
    /// </summary>
    private static bool TryGetScreenBounds(FrameworkElement element, out Rect bounds)
    {
        bounds = Rect.Empty;

        if (PresentationSource.FromVisual(element) is null)
        {
            return false;
        }

        try
        {
            // Transform every corner independently: FlowDirection and user supplied render
            // transforms can reverse or skew an axis, for which a two-corner shortcut is invalid.
            var topLeft     = element.PointToScreen(new Point(0.0,                 0.0));
            var topRight    = element.PointToScreen(new Point(element.ActualWidth, 0.0));
            var bottomLeft  = element.PointToScreen(new Point(0.0,                 element.ActualHeight));
            var bottomRight = element.PointToScreen(new Point(element.ActualWidth, element.ActualHeight));

            var left   = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
            var top    = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
            var right  = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
            var bottom = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

            bounds = new Rect(new Point(left, top), new Point(right, bottom));
            return !bounds.IsEmpty;
        }
        catch (InvalidOperationException)
        {
            // PointToScreen throws while PresentationSource is being replaced. Popup placement
            // is refreshed on the next layout pass, so falling back for one pass is sufficient.
            return false;
        }
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        // Templates are replaceable and local SpinButtonPlacementMode values outrank template
        // triggers. Enforce read-only semantics here instead of relying on buttons being hidden.
        if (!IsEnabled || IsReadOnly)
        {
            return;
        }

        if (e.OriginalSource is ButtonBase btn)
        {
            if (SpinButtonPlacementMode == NumberBoxSpinButtonPlacementMode.Compact &&
                _spinButtons?.IsAncestorOf(btn) == true)
            {
                // Compact mode keeps the classic buttons visible beneath the Popup. Ignore even
                // programmatically raised clicks here so monitor-edge nudging cannot expose a
                // working button and automation cannot bypass the template's hit-test guard.
                return;
            }

            Focus();

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
    private void OnValueChangedCore(double? oldValue, double? newValue)
    {
        if (Equals(newValue, oldValue))
        {
            return;
        }

        // Do not suppress nested DP changes. WPF property-change notifications are synchronous,
        // and each effective Value transition must produce its own routed and automation events.
        NumberBoxAutomationPeer.RaiseValuePropertyChangedEvent(this, oldValue, newValue);
        OnValueChanged(oldValue, newValue);
    }

    /// <summary>
    /// Raises the NumberBox value change event and updates the control's display text if the event is not handled.
    /// </summary>
    /// <remarks>
    /// Raises a NumberBoxValueChangedEventArgs object and calls RaiseEvent.
    /// If the event is not marked as Handled, calls UpdateTextToValue to update the Text property.
    /// Derived classes can override this method to add custom behavior, but should call base.OnValueChanged(oldValue, newValue)
    /// when the default behavior needs to be preserved, or manage text updates themselves by marking the event as Handled.
    /// </remarks>
    /// <param name="oldValue">The numeric value before the change; null if no value.</param>
    /// <param name="newValue">The numeric value after the change; null if no value.</param>
    protected virtual void OnValueChanged(double? oldValue, double? newValue)
    {
        var args = new NumberBoxValueChangedEventArgs(oldValue, newValue, this);

        RaiseEvent(args);

        // If the event is handled, do not update the Text property.
        if (!args.Handled)
        {
            UpdateTextToValue();
        }
    }

    /// <summary>
    /// Is called when something is pasted in this <see cref="NumberBox"/>.
    /// </summary>
    protected virtual void OnClipboardPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (sender is not NumberBox nb)
        {
            return;
        }

        if (nb.IsReadOnly)
        {
            e.CancelCommand();
            return;
        }

        // Handle pasted text: try to parse the resulting text and accept/format it immediately.
        // If parsing fails and validation mode is set to overwrite invalid input, cancel paste and restore.

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

            var parser = (INumberParser)nb.NumberFormatter;
            var parsed = parser.ParseDouble(trimmed);

            if (parsed is double directValue && double.IsNaN(directValue))
            {
                parsed = null;
            }

            // When direct number parsing fails and expression is enabled,
            // try to evaluate the expression instead.
            if (parsed is null && nb.AcceptsExpression)
            {
                if (NumberBoxExpressionParser.TryEvaluate(trimmed, parser, out var exprResult))
                {
                    parsed = exprResult;
                }
            }

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
        if (!IsEnabled || IsReadOnly)
        {
            return;
        }

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

        if (Value is not null)
        {
            var d = Math.Round((double)Value, MaxDecimalPlaces);
            newText = DecimalPlaces is { } dp
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

        var numberParser = (INumberParser)NumberFormatter;
        var value        = numberParser.ParseDouble(text);

        if (value is double directValue && double.IsNaN(directValue))
        {
            value = null;
        }

        // If direct parsing failed, try expression evaluation
        if (value is null && AcceptsExpression)
        {
            if (NumberBoxExpressionParser.TryEvaluate(text, numberParser, out var exprResult))
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

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new NumberBoxAutomationPeer(this);
}
