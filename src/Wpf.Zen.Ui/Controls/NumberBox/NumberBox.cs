// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// This Source Code is partially based on the source code provided by the .NET Foundation.

using System.Diagnostics;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Zen.Ui.AutomationPeers;
using Wpf.Zen.Ui.Extensions.Wpf;

// ReSharper disable UnusedParameter.Local
// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a single-line editor for nullable numeric values, with range constraints,
/// culture-aware display formatting, keyboard and pointer stepping, and optional arithmetic
/// expression evaluation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Value"/> follows the text as soon as the current input can be parsed. An empty input
/// produces <see langword="null"/>. Incomplete input may remain visible while editing, in which
/// case <see cref="Value"/> retains the most recent successfully parsed result. Escape discards
/// only that incomplete input; it does not undo an already parsed value.
/// </para>
/// <para>
/// When <see cref="AcceptsExpression"/> is enabled, complete arithmetic expressions are evaluated
/// immediately. Enter, focus loss, and stepping normalize the text using <see cref="FormatString"/>
/// and <see cref="CultureInfo.CurrentCulture"/>. All parsed and stepped values are constrained to
/// <see cref="Minimum"/> and <see cref="Maximum"/>.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_CompactPopup", Type = typeof(Popup))]
[TemplatePart(Name = "PART_SpinButtons",  Type = typeof(FrameworkElement))]
public class NumberBox : TextBox
{
    private const double LargeStepHoldDurationSeconds = 3.0;

    private Popup?            _compactPopup;
    private FrameworkElement? _spinButtons;
    private Window?           _parentWindow;
    private ButtonBase?       _heldSpinButton;
    private long              _spinButtonPressTimestamp;

    // Text and Value notify synchronously through the dependency-property system. These guards
    // identify which side initiated a synchronization pass; without them, formatting Text would
    // be parsed back into Value and a text edit would be formatted before the user finishes it.
    private bool _isWritingFormattedText;
    private bool _isApplyingTextValue;

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
                    var clamped = dv;

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
            isAnimationProhibited: false,
            defaultUpdateSourceTrigger: UpdateSourceTrigger.LostFocus
        ),
        static value =>
            value is null || (value is double dv && !double.IsNaN(dv) && !double.IsInfinity(dv))
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

    #region FormatStringProperty

    /// <summary>Identifies the <see cref="FormatString"/> dependency property.</summary>
    public static readonly DependencyProperty FormatStringProperty = DependencyProperty.Register(
        nameof(FormatString),
        typeof(string),
        typeof(NumberBox),
        new PropertyMetadata(
            "G",
            static (d, e) => ((NumberBox)d).WriteFormattedValueToText()
        ),
        static value => value is string format && IsValidFormatString(format)
    );

    /// <summary>
    /// Gets or sets the standard or custom numeric format string used to display <see cref="Value"/>.
    /// </summary>
    /// <remarks>
    /// The default is <c>G</c>. Formatting uses <see cref="CultureInfo.CurrentCulture"/> and only
    /// affects displayed text; it never rounds or otherwise changes the stored numeric value.
    /// </remarks>
    public string FormatString
    {
        get => (string)GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    #endregion

    #region SmallStepProperty

    /// <summary>Identifies the <see cref="SmallStep"/> dependency property.</summary>
    public static readonly DependencyProperty SmallStepProperty = DependencyProperty.Register(
        nameof(SmallStep),
        typeof(double),
        typeof(NumberBox),
        new PropertyMetadata(
            1.0d,
            static (d, e) => NumberBoxAutomationPeer.RaiseSmallStepPropertyChangedEvent(
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
    /// Gets or sets the value added to or subtracted from <see cref="Value"/> by an arrow key,
    /// mouse-wheel notch, or spin-button click when large-step activation is not active.
    /// </summary>
    public double SmallStep
    {
        get => (double)GetValue(SmallStepProperty);
        set => SetValue(SmallStepProperty, value);
    }

    #endregion

    #region LargeStepProperty

    /// <summary>Identifies the <see cref="LargeStep"/> dependency property.</summary>
    public static readonly DependencyProperty LargeStepProperty = DependencyProperty.Register(
        nameof(LargeStep),
        typeof(double),
        typeof(NumberBox),
        new PropertyMetadata(
            10.0d,
            static (d, e) => NumberBoxAutomationPeer.RaiseLargeStepPropertyChangedEvent(
                (NumberBox)d,
                (double)e.OldValue,
                (double)e.NewValue
            ),
            coerceValueCallback: static (d, baseValue) =>
            {
                if (baseValue is double dv)
                {
                    // Coerce the large step relative to instance range;
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
    /// Gets or sets the value added to or subtracted from <see cref="Value"/> by Page Up and Page
    /// Down, or while Ctrl, Alt, or Shift is held during an arrow-key, wheel, or spin operation.
    /// </summary>
    /// <remarks>Holding a spin button continuously for three seconds also activates this step.</remarks>
    public double LargeStep
    {
        get => (double)GetValue(LargeStepProperty);
        set => SetValue(LargeStepProperty, value);
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
                    numberBox.CoerceValue(SmallStepProperty);
                    numberBox.CoerceValue(LargeStepProperty);
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
                    numberBox.CoerceValue(SmallStepProperty);
                    numberBox.CoerceValue(LargeStepProperty);
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
                    nb.CommitInput();
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

        // A numeric/expression editor gains nothing from IME word composition, so turn the input
        // method off by default. Consumers that need it can re-enable it per instance with
        // InputMethod.SetIsInputMethodEnabled. The TextBoxBase PropertyChangedCallback (which
        // re-hooks the TextStore when IME is re-enabled) is preserved by the metadata merge.
        InputMethod.IsInputMethodEnabledProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(false)
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

        // ===========================================================================================
        // NumberBox edits a single numeric value, so a multi-line editor has no meaning. Enforce
        // the TextBox line-management properties to their single-line values. Coercion rather than
        // a plain default is deliberate: it also reverts any attempt to re-enable AcceptsReturn,
        // text wrapping, or extra lines, keeping the control single-line regardless of configuration.
        // ===========================================================================================

        AcceptsReturnProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                false,
                null,
                (d, baseValue) => false
            )
        );

        TextWrappingProperty.OverrideMetadata(
            typeof(NumberBox),
            new FrameworkPropertyMetadata(
                TextWrapping.NoWrap,
                null,
                (d, baseValue) => TextWrapping.NoWrap  // Enforce a single-line editor.
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

    /// <summary>
    /// Initializes a new instance of the <see cref="NumberBox"/> class and installs paste
    /// validation without replacing inherited <see cref="TextBox"/> behavior.
    /// </summary>
    public NumberBox()
    {
        DataObject.AddPastingHandler(this, OnClipboardPaste);

        // Popup reconnects routed events to its PlacementTarget. Keep handlers for events that
        // cross that boundary on NumberBox itself; registering the same callbacks on the popup
        // would invoke them once at Popup and again when the route reaches this control.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        AddHandler(Mouse.LostMouseCaptureEvent, new MouseEventHandler(OnSpinButtonLostMouseCapture));
    }

    /// <inheritdoc />
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            // Read-only mode disables keyboard stepping. Forward to the base implementation so
            // WPF's internal TextEditor still handles caret movement, selection, and copy gestures.
            base.OnPreviewKeyDown(e);
            return;
        }

        // WPF reports Alt-modified keys through Key.System. Normalize to SystemKey so Alt can
        // participate in the same large-step gesture as Ctrl and Shift.
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        switch (key)
        {
            case Key.PageUp:
                StepValue(LargeStep);
                e.Handled = true;
                break;

            case Key.PageDown:
                StepValue(-LargeStep);
                e.Handled = true;
                break;

            case Key.Up:
                StepValue(GetStep());
                e.Handled = true;
                break;

            case Key.Down:
                StepValue(-GetStep());
                e.Handled = true;
                break;

            case Key.Enter:
                // Ignore auto-repeat; only the physical press commits. When there is nothing left
                // to do (text already canonical and the caret already at the end), leave the key
                // unhandled so the default button can still respond to Enter.
                if (e.IsRepeat || !IsCommitPending())
                {
                    break;
                }

                CommitInput();
                MoveCaretToTextEnd();
                e.Handled = true;
                break;

            case Key.Escape:
                // Escape only discards an incomplete edit whose text cannot currently update
                // Value. A successfully parsed number or expression is already the current value
                // and must not acquire a second, implicit undo model.
                if (e.IsRepeat || !HasIncompleteInput())
                {
                    break;
                }

                DiscardIncompleteInput();
                e.Handled = true;
                break;
        }

        base.OnPreviewKeyDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (!IsReadOnly && !ContainsOnlyInputCharacters(e.Text))
        {
            // TextBox would otherwise retain characters that can never form a numeric value.
            // Syntax-level validation remains deferred so transient input such as "-" or "1e"
            // is still editable, but unrelated characters never enter the document.
            e.Handled = true;
        }

        base.OnPreviewTextInput(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        BeginSpinButtonHold(e.OriginalSource as DependencyObject);
        base.OnPreviewMouseLeftButtonDown(e);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        EndSpinButtonHold();
        base.OnPreviewMouseLeftButtonUp(e);
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

        var step = GetStep();
        StepValue(e.Delta > 0 ? step : -step);
        e.Handled = true;

        base.OnPreviewMouseWheel(e);
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnLostKeyboardFocus(e);

        // Value is already kept in sync with valid text by OnTextChanged. This pass normalizes a
        // completed expression or discards incomplete transient text such as a lone sign.
        CommitInput();

        // The binding's LostFocus trigger is tied to the focus scope's logical FocusedElement, so
        // it is not raised when keyboard focus moves to a different focus scope (e.g. a ToolBar).
        // Live text synchronization writes Value through SetCurrentValue, which only feeds a
        // PropertyChanged trigger. Push LostFocus bindings explicitly here; PropertyChanged is
        // already current and Explicit remains under the caller's control.
        var binding = GetBindingExpression(ValueProperty);
        if (binding is not null && GetEffectiveUpdateSourceTrigger(binding.ParentBinding) == UpdateSourceTrigger.LostFocus)
        {
            binding.UpdateSource();
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        // LostFocus fires when the focus scope's logical FocusedElement changes without a
        // keyboard-focus transition (a rare programmatic path). Normal keyboard-focus loss is
        // handled by OnLostKeyboardFocus; the binding's own LostFocus trigger covers this path.
        CommitInput();
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        SynchronizeValueFromText();
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // Preview mouse events do not continue from a standalone Popup to its PlacementTarget,
        // so these are the only handlers owned by the current popup template part.
        _compactPopup?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnSpinButtonMouseDown));
        _compactPopup?.RemoveHandler(Mouse.PreviewMouseUpEvent,   new MouseButtonEventHandler(OnSpinButtonMouseUp));

        base.OnApplyTemplate();

        _compactPopup = GetTemplateChild("PART_CompactPopup") as Popup;
        _spinButtons  = GetTemplateChild("PART_SpinButtons") as FrameworkElement;

        if (_compactPopup is not null)
        {
            #pragma warning disable WPF0041 // Set mutable dependency properties using SetCurrentValue
            _compactPopup.Placement                    = PlacementMode.Custom;
            _compactPopup.CustomPopupPlacementCallback = CompactPopupPlacementCallback;
            #pragma warning restore WPF0041 // Set mutable dependency properties using SetCurrentValue

            _compactPopup.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnSpinButtonMouseDown));
            _compactPopup.AddHandler(Mouse.PreviewMouseUpEvent,   new MouseButtonEventHandler(OnSpinButtonMouseUp));
        }

        // Text and Value synchronization is independent of the visual tree. Reapplying a template
        // must not commit or otherwise alter an edit that is currently in progress.
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

    private void OnParentWindowLocationChanged(object? sender, EventArgs e)
        => RefreshCompactPopupPosition();

    private void OnParentWindowSizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshCompactPopupPosition();

    private void OnNumberBoxSizeChanged(object sender, SizeChangedEventArgs e)
        => RefreshCompactPopupPosition();

    private void OnNumberBoxLayoutUpdated(object? sender, EventArgs e)
        => RefreshCompactPopupPosition();

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

        var x = popupLeft - targetOrigin.X;
        var y = popupTop - targetOrigin.Y;

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
                    StepValue(GetStep(btn));
                    break;

                case NumberBoxButtonKind.Decrement:
                    StepValue(-GetStep(btn));
                    break;

                case NumberBoxButtonKind.None:
                default:
                    break;
            }
        }
    }

    private void OnSpinButtonMouseDown(object sender, MouseButtonEventArgs e)
    {
        BeginSpinButtonHold(e.OriginalSource as DependencyObject);
    }

    private void OnSpinButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        EndSpinButtonHold();
    }

    private void OnSpinButtonLostMouseCapture(object sender, MouseEventArgs e)
    {
        var button = e.OriginalSource as ButtonBase
                  ?? (e.OriginalSource as DependencyObject).FindAncestor<ButtonBase>();
        if (ReferenceEquals(button, _heldSpinButton))
        {
            EndSpinButtonHold();
        }
    }

    private void BeginSpinButtonHold(DependencyObject? source)
    {
        var button = source as ButtonBase ?? source.FindAncestor<ButtonBase>();
        if (button is null || GetButtonKind(button) is not (NumberBoxButtonKind.Increment or NumberBoxButtonKind.Decrement))
        {
            return;
        }

        // RepeatButton raises an initial Click immediately and subsequent Click events from its
        // internal timer. Recording the physical press here gives every template the same
        // three-second acceleration threshold without replacing or reflecting that WPF timer.
        _heldSpinButton           = button;
        _spinButtonPressTimestamp = Stopwatch.GetTimestamp();
    }

    private void EndSpinButtonHold()
    {
        // Mouse capture can retarget the release away from the original template child. Any
        // release ends the one physical press tracked by this NumberBox.
        _heldSpinButton           = null;
        _spinButtonPressTimestamp = 0;
    }

    private double GetStep(ButtonBase? spinButton = null)
    {
        if (IsLargeStepModifierActive())
        {
            return LargeStep;
        }

        if (
            spinButton is not null
            && ReferenceEquals(spinButton, _heldSpinButton)
            && _spinButtonPressTimestamp != 0
            &&
               (Stopwatch.GetTimestamp() - _spinButtonPressTimestamp)
               /
               (double)Stopwatch.Frequency
               >=
               LargeStepHoldDurationSeconds
        )
        {
            return LargeStep;
        }

        return SmallStep;
    }

    private static bool IsLargeStepModifierActive()
    {
        const ModifierKeys flags = ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Shift;
        return (Keyboard.Modifiers & flags) != ModifierKeys.None;
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
    /// Raises the value-change event and refreshes the display for changes that did not originate
    /// from the editable text.
    /// </summary>
    /// <remarks>
    /// <see cref="RoutedEventArgs.Handled"/> only controls routed-event propagation; it does not
    /// suppress the invariant that external Value changes update Text. During a text-originated
    /// change, formatting is deferred so a valid expression remains editable until it is committed.
    /// Overrides should call the base implementation to preserve those synchronization semantics.
    /// </remarks>
    /// <param name="oldValue">The numeric value before the change; null if no value.</param>
    /// <param name="newValue">The numeric value after the change; null if no value.</param>
    protected virtual void OnValueChanged(double? oldValue, double? newValue)
    {
        var args = new NumberBoxValueChangedEventArgs(oldValue, newValue, this);

        RaiseEvent(args);

        // Handled controls routed-event propagation; it must not allow Text and Value to diverge.
        // Live text parsing is the sole exception because replacing a valid expression with its
        // result while the user is still typing would destroy the editable expression.
        if (!_isApplyingTextValue)
        {
            WriteFormattedValueToText();
        }
    }

    /// <summary>
    /// Is called when something is pasted in this <see cref="NumberBox"/>.
    /// </summary>
    protected virtual void OnClipboardPaste(object sender, DataObjectPastingEventArgs e)
    {
        if (!ReferenceEquals(sender, this))
        {
            return;
        }

        if (IsReadOnly || !e.DataObject.GetDataPresent(DataFormats.UnicodeText))
        {
            e.CancelCommand();
            return;
        }

        var pastedText = e.DataObject.GetData(DataFormats.UnicodeText) as string ?? string.Empty;
        var resultingText = GetTextAfterInsertion(pastedText);

        if (!ContainsOnlyInputCharacters(pastedText) || !TryParseText(resultingText, out _))
        {
            // Paste is atomic: reject the command before TextBox mutates its document. A valid
            // paste is left to TextBox itself so selection replacement, caret placement, undo,
            // and TextChanged all retain their standard WPF behavior.
            e.CancelCommand();
        }
    }

    private void StepValue(double change)
    {
        if (!IsEnabled || IsReadOnly)
        {
            return;
        }

        // Stepping ends any incomplete edit before applying the delta to the effective Value.
        CommitInput();

        var currentValue = Value ?? 0;

        // NumberBox edits decimal text, so ordinary stepping should follow decimal arithmetic
        // rather than expose IEEE 754 artifacts such as 128.45 - 1 becoming
        // 127.44999999999999. Values outside decimal's substantially smaller exponent range still
        // use double arithmetic so this implementation does not narrow the public Value domain.
        var newValue = NumberBoxDecimalMath.TryAdd(currentValue, change, out var decimalResult)
            ? decimalResult
            : currentValue + change;

        // Adding two finite doubles can overflow even though both dependency-property values are
        // individually valid. Value rejects infinity, so saturate at the configured boundary
        // before entering the property system. Ordinary finite overflow beyond the configured
        // range continues to be handled by Value's coercion callback.
        if (double.IsPositiveInfinity(newValue))
        {
            newValue = Maximum;
        }
        else if (double.IsNegativeInfinity(newValue))
        {
            newValue = Minimum;
        }

        SetCurrentValue(ValueProperty, newValue);

        MoveCaretToTextEnd();
    }

    #region Text and value synchronization

    /// <summary>
    /// Applies a parseable edit to <see cref="Value"/> without ending the edit. Keeping this path
    /// separate from <see cref="CommitInput"/> lets an expression remain visible while its
    /// evaluated result is already available through Value.
    /// </summary>
    private void SynchronizeValueFromText()
    {
        if (_isWritingFormattedText || !TryParseText(Text, out var value))
        {
            return;
        }

        ApplyTextValue(value);
    }

    /// <summary>
    /// Ends the current edit. Parseable text is applied once more for lifecycle paths that do not
    /// originate in TextChanged, then all text—including incomplete input—is replaced by the
    /// canonical representation of the effective Value.
    /// </summary>
    private void CommitInput()
    {
        if (TryParseText(Text, out var value))
        {
            ApplyTextValue(value);
        }

        WriteFormattedValueToText();
    }

    /// <summary>
    /// Applies a value produced by the text parser while preserving the user's editable text.
    /// </summary>
    private void ApplyTextValue(double? value)
    {
        if (Equals(Value, value))
        {
            return;
        }

        _isApplyingTextValue = true;
        try
        {
            SetCurrentValue(ValueProperty, value);
        }
        finally
        {
            _isApplyingTextValue = false;
        }

        // Range coercion or a reentrant ValueChanged handler can replace the parsed value. In
        // that case preserving the original text would expose two contradictory control states.
        if (!Equals(Value, value))
        {
            WriteFormattedValueToText();
        }
    }

    /// <summary>
    /// Writes the effective <see cref="Value"/> to the inherited <c>Text</c> property using the
    /// display format.
    /// </summary>
    private void WriteFormattedValueToText()
    {
        // Do not parse our formatted representation back into Value. FormatString can be lossy
        // (for example, F0), so doing so would silently round the stored value.
        _isWritingFormattedText = true;
        try
        {
            SetCurrentValue(TextProperty, FormatValue());
        }
        finally
        {
            _isWritingFormattedText = false;
        }
    }

    /// <summary>
    /// Gets the canonical display text for the effective <see cref="Value"/>.
    /// </summary>
    private string FormatValue()
    {
        return Value is null
            ? string.Empty
            : Value.Value.ToString(FormatString, CultureInfo.CurrentCulture);
    }

    /// <summary>
    /// Reads an editable text state. Empty text is a valid <see langword="null"/> value; any
    /// nonempty text must be a complete finite number or expression.
    /// </summary>
    private bool TryParseText(string text, out double? value)
    {
        var trimmedText = text.Trim();
        if (string.IsNullOrEmpty(trimmedText))
        {
            value = null;
            return true;
        }

        if (TryParseFiniteValue(trimmedText, out var parsedValue))
        {
            value = parsedValue;
            return true;
        }

        value = null;
        return false;
    }

    /// <summary>
    /// Determines whether Escape can discard the current text without undoing a successfully
    /// parsed number or expression.
    /// </summary>
    private bool HasIncompleteInput() => !TryParseText(Text, out _);

    /// <summary>
    /// Discards only text that has not produced a Value. Callers must first establish that the
    /// current input is incomplete.
    /// </summary>
    private void DiscardIncompleteInput() => WriteFormattedValueToText();

    /// <summary>
    /// Determines whether Enter still has a visible effect: the editable text needs committing or
    /// the caret needs moving to the end.
    /// </summary>
    private bool IsCommitPending() => Text != FormatValue() || CaretIndex != Text.Length;

    private bool TryParseFiniteValue(string text, out double value)
    {
        if (double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out value
            )
         && !double.IsNaN(value)
         && !double.IsInfinity(value))
        {
            return true;
        }

        return AcceptsExpression
            && NumberBoxExpressionParser.TryEvaluate(text, CultureInfo.CurrentCulture, out value)
            && !double.IsNaN(value)
            && !double.IsInfinity(value);
    }

    private bool ContainsOnlyInputCharacters(string text)
    {
        var format = CultureInfo.CurrentCulture.NumberFormat;
        foreach (var character in text)
        {
            if (character is >= '0' and <= '9'
             || format.NumberDecimalSeparator.Contains(character)
             || format.NumberGroupSeparator.Contains(character)
             || format.PositiveSign.Contains(character)
             || format.NegativeSign.Contains(character)
             || character is '+' or '-' or 'e' or 'E'
             || (AcceptsExpression && character is '*' or '/' or '(' or ')'))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// Builds the document text that WPF's paste command will produce after replacing the current
    /// selection. This allows the entire paste to be accepted or rejected before mutation.
    /// </summary>
    private string GetTextAfterInsertion(string insertedText)
    {
        // Math.Clamp is unavailable on the net462 target. Keep the bounds explicit here rather
        // than adding a compatibility helper for this single, paste-specific calculation.
        var start  = Math.Min(Math.Max(SelectionStart,  0), Text.Length);
        var length = Math.Min(Math.Max(SelectionLength, 0), Text.Length - start);

        return Text.Remove(start, length).Insert(start, insertedText);
    }

    #endregion

    private static bool IsValidFormatString(string format)
    {
        try
        {
            _ = 0.0.ToString(format, CultureInfo.InvariantCulture);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves a binding's <see cref="UpdateSourceTrigger.Default"/> against the
    /// <see cref="ValueProperty"/> metadata, returning the effective trigger the binding uses.
    /// </summary>
    private static UpdateSourceTrigger GetEffectiveUpdateSourceTrigger(Binding binding)
    {
        var trigger = binding.UpdateSourceTrigger;

        return trigger == UpdateSourceTrigger.Default
            && ValueProperty.GetMetadata(typeof(NumberBox)) is FrameworkPropertyMetadata metadata
            ? metadata.DefaultUpdateSourceTrigger
            : trigger;
    }

    private void MoveCaretToTextEnd()
    {
        CaretIndex = Text.Length;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NumberBoxAutomationPeer(this);
    }
}
