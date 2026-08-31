// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extended <see cref="System.Windows.Controls.TextBox"/> with additional parameters like <see cref="PlaceholderText"/>.
/// </summary>
[TemplatePart(Name = ClearButtonPartName, Type = typeof(ButtonBase))]
public class TextBox : System.Windows.Controls.TextBox
{
    private const string ClearButtonPartName = "PART_ClearButton";

    private readonly TextEditorMouseInputHelper _mouseInputHelper;
    private ButtonBase? _clearButton;

    #region Interaction appearance

    /// <summary>
    /// Identifies the <see cref="MouseOverForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverForegroundProperty = DependencyProperty.Register(
        nameof(MouseOverForeground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the foreground used while the pointer is over the text box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverForeground
    {
        get => (Brush?)GetValue(MouseOverForegroundProperty);
        set => SetValue(MouseOverForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MouseOverBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverBackgroundProperty = DependencyProperty.Register(
        nameof(MouseOverBackground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the background used while the pointer is over the text box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBackground
    {
        get => (Brush?)GetValue(MouseOverBackgroundProperty);
        set => SetValue(MouseOverBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MouseOverBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverBorderBrushProperty = DependencyProperty.Register(
        nameof(MouseOverBorderBrush),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the border brush used while the pointer is over the text box.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBorderBrush
    {
        get => (Brush?)GetValue(MouseOverBorderBrushProperty);
        set => SetValue(MouseOverBorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedForegroundProperty = DependencyProperty.Register(
        nameof(FocusedForeground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the foreground used while the text box has keyboard focus.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedForeground
    {
        get => (Brush?)GetValue(FocusedForegroundProperty);
        set => SetValue(FocusedForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedBackgroundProperty = DependencyProperty.Register(
        nameof(FocusedBackground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the background used while the text box has keyboard focus.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBackground
    {
        get => (Brush?)GetValue(FocusedBackgroundProperty);
        set => SetValue(FocusedBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedBorderBrushProperty = DependencyProperty.Register(
        nameof(FocusedBorderBrush),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the border brush used while the text box has keyboard focus.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBorderBrush
    {
        get => (Brush?)GetValue(FocusedBorderBrushProperty);
        set => SetValue(FocusedBorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledForegroundProperty = DependencyProperty.Register(
        nameof(DisabledForeground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the foreground used while the text box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledForeground
    {
        get => (Brush?)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledBackgroundProperty = DependencyProperty.Register(
        nameof(DisabledBackground),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the background used while the text box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBackground
    {
        get => (Brush?)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledBorderBrushProperty = DependencyProperty.Register(
        nameof(DisabledBorderBrush),
        typeof(Brush),
        typeof(TextBox),
        new PropertyMetadata(null, OnStateBrushChanged)
    );

    /// <summary>
    /// Gets or sets the border brush used while the text box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBorderBrush
    {
        get => (Brush?)GetValue(DisabledBorderBrushProperty);
        set => SetValue(DisabledBorderBrushProperty, value);
    }

    private static readonly DependencyPropertyKey VisualForegroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualForeground),
            typeof(Brush),
            typeof(TextBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualForegroundProperty =
        VisualForegroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the foreground selected for the current interaction state.
    /// This property is intended for control templates.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualForeground => (Brush?)GetValue(VisualForegroundProperty);

    private static readonly DependencyPropertyKey VisualBackgroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBackground),
            typeof(Brush),
            typeof(TextBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBackgroundProperty =
        VisualBackgroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the background selected for the current interaction state.
    /// This property is intended for control templates.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBackground => (Brush?)GetValue(VisualBackgroundProperty);

    private static readonly DependencyPropertyKey VisualBorderBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBorderBrush),
            typeof(Brush),
            typeof(TextBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBorderBrushProperty =
        VisualBorderBrushPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the border brush selected for the current interaction state.
    /// This property is intended for control templates.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBorderBrush => (Brush?)GetValue(VisualBorderBrushProperty);

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty = DependencyProperty.Register(
        nameof(CornerRadius),
        typeof(CornerRadius),
        typeof(TextBox),
        new FrameworkPropertyMetadata(default(CornerRadius))
    );

    /// <summary>
    /// Gets or sets the radius applied to the text box chrome and its focus indicator.
    /// The property affects template rendering but does not change layout measurement.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(TextBox),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>Identifies the <see cref="IconMargin"/> dependency property.</summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(TextBox),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

    /// <summary>Identifies the <see cref="IconPlacement"/> dependency property.</summary>
    public static readonly DependencyProperty IconPlacementProperty = DependencyProperty.Register(
        nameof(IconPlacement),
        typeof(ElementPlacement),
        typeof(TextBox),
        new PropertyMetadata(ElementPlacement.Left)
    );

    /// <summary>
    /// Gets or sets which side the icon should be placed on.
    /// </summary>
    public ElementPlacement IconPlacement
    {
        get => (ElementPlacement)GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    /// <summary>Identifies the <see cref="PlaceholderText"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(TextBox),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>
    /// Gets or sets placeholder text.
    /// </summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Identifies the <see cref="PlaceholderEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderEnabledProperty = DependencyProperty.Register(
        nameof(PlaceholderEnabled),
        typeof(bool),
        typeof(TextBox),
        new PropertyMetadata(true, OnPlaceholderEnabledChanged)
    );

    /// <summary>
    /// Gets or sets a value indicating whether to enable the placeholder text.
    /// </summary>
    public bool PlaceholderEnabled
    {
        get => (bool)GetValue(PlaceholderEnabledProperty);
        set => SetValue(PlaceholderEnabledProperty, value);
    }

    // ReSharper disable once MemberCanBePrivate.Global for IsPlaceholderVisiblePropertyKey

    /// <summary>
    /// Identifies the <see cref="IsPlaceholderVisible"/> dependency property.
    /// </summary>
    protected static readonly DependencyPropertyKey IsPlaceholderVisiblePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPlaceholderVisible),
            typeof(bool),
            typeof(TextBox),
            new PropertyMetadata(true)
        );

    /// <summary>Identifies the <see cref="IsPlaceholderVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPlaceholderVisibleProperty =
        IsPlaceholderVisiblePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets a value indicating whether the placeholder text is currently visible.
    /// </summary>
    public bool IsPlaceholderVisible
    {
        get => (bool)GetValue(IsPlaceholderVisibleProperty);
        protected set => SetValue(IsPlaceholderVisiblePropertyKey, value);
    }

    /// <summary>Identifies the <see cref="ClearButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty ClearButtonEnabledProperty = DependencyProperty.Register(
        nameof(ClearButtonEnabled),
        typeof(bool),
        typeof(TextBox),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether to enable the clear button.
    /// </summary>
    public bool ClearButtonEnabled
    {
        get => (bool)GetValue(ClearButtonEnabledProperty);
        set => SetValue(ClearButtonEnabledProperty, value);
    }

    // ReSharper disable once MemberCanBePrivate.Global for IsClearButtonVisiblePropertyKey

    /// <summary>
    /// Identifies the <see cref="IsClearButtonVisible"/> dependency property.
    /// </summary>
    protected static readonly DependencyPropertyKey IsClearButtonVisiblePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsClearButtonVisible),
            typeof(bool),
            typeof(TextBox),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsClearButtonVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsClearButtonVisibleProperty =
        IsClearButtonVisiblePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets or sets a value indicating whether the clear button is currently visible.
    /// </summary>
    public bool IsClearButtonVisible
    {
        get => (bool)GetValue(IsClearButtonVisibleProperty);
        protected set => SetValue(IsClearButtonVisiblePropertyKey, value);
    }

    /// <summary>Identifies the <see cref="IsFocusIndicatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsFocusIndicatorVisibleProperty = DependencyProperty.Register(
        nameof(IsFocusIndicatorVisible),
        typeof(bool),
        typeof(TextBox),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the focus indicator is visible.
    /// </summary>
    public bool IsFocusIndicatorVisible
    {
        get => (bool)GetValue(IsFocusIndicatorVisibleProperty);
        set => SetValue(IsFocusIndicatorVisibleProperty, value);
    }

    /// <summary>Identifies the <see cref="UseTextBlockAppearance"/> dependency property.</summary>
    public static readonly DependencyProperty UseTextBlockAppearanceProperty = DependencyProperty.Register(
        nameof(UseTextBlockAppearance),
        typeof(bool),
        typeof(TextBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the control should use a plain selectable text appearance.
    /// </summary>
    public bool UseTextBlockAppearance
    {
        get => (bool)GetValue(UseTextBlockAppearanceProperty);
        set => SetValue(UseTextBlockAppearanceProperty, value);
    }

    /// <summary>Identifies the <see cref="AutoSelect"/> dependency property.</summary>
    public static readonly DependencyProperty AutoSelectProperty = DependencyProperty.Register(
        nameof(AutoSelect),
        typeof(bool),
        typeof(TextBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether all text is selected whenever the text box receives
    /// keyboard focus. The default value is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Enabling this property deliberately replaces any existing selection on every keyboard-focus
    /// entry. This gives repeated mouse and keyboard entry the same predictable select-all behavior.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool AutoSelect
    {
        get => (bool)GetValue(AutoSelectProperty);
        set => SetValue(AutoSelectProperty, value);
    }

    static TextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TextBox),
            new FrameworkPropertyMetadata(typeof(TextBox))
        );

        // ---------------------------------------------------------------------------
        // Standard Control brushes remain the normal-state inputs. Metadata callbacks
        // keep template-facing outputs synchronized when bindings, styles, or dynamic
        // resources change without replacing the public properties' effective values.
        // ---------------------------------------------------------------------------

        BackgroundProperty.OverrideMetadata(
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                BackgroundProperty.GetMetadata(typeof(System.Windows.Controls.TextBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        BorderBrushProperty.OverrideMetadata(
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                BorderBrushProperty.GetMetadata(typeof(System.Windows.Controls.TextBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(TextBox),
            new FrameworkPropertyMetadata(
                ForegroundProperty.GetMetadata(typeof(System.Windows.Controls.TextBox)).DefaultValue,
                OnStandardBrushChanged,
                CoerceForeground
            )
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBox"/> class.
    /// </summary>
    public TextBox()
    {
        _mouseInputHelper = new TextEditorMouseInputHelper(this);

        IsPlaceholderVisible = PlaceholderEnabled;
        UpdateVisualBrushes();

        // Cursor is normally resolved through a bubbling QueryCursor event. Using
        // handledEventsToo lets us correct the cursor even if PART_ContentHost.Cursor has
        // already handled the event, while still forwarding real TextBoxView hits back to
        // WPF's internal cursor logic.
        AddHandler(
            Mouse.QueryCursorEvent,
            new QueryCursorEventHandler(OnTextBoxQueryCursor),
            handledEventsToo: true
        );
        IsEnabledChanged += OnTextBoxIsEnabledChanged;
    }

    private static void OnStandardBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((TextBox)d).UpdateVisualBrushes();
    }

    private static void OnStateBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var textBox = (TextBox)d;

        // TextBoxView constructs its text properties from the owning TextBox rather
        // than from template-child inheritance. Coercing Foreground is consequently
        // the only public WPF mechanism that changes editable text without replacing
        // the caller's binding, dynamic resource, or other base value.
        textBox.CoerceValue(ForegroundProperty);
        textBox.UpdateVisualBrushes();
    }

    private static object CoerceForeground(DependencyObject d, object? baseValue)
    {
        var textBox = (TextBox)d;

        if (!textBox.IsEnabled)
        {
            return textBox.DisabledForeground ?? baseValue!;
        }

        if (textBox.IsKeyboardFocused)
        {
            return textBox.FocusedForeground ?? baseValue!;
        }

        if (textBox.IsMouseOver && textBox.MouseOverForeground is not null)
        {
            return textBox.MouseOverForeground;
        }

        return baseValue!;
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnIsKeyboardFocusedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusedChanged(e);

        // Logical focus is retained when keyboard focus leaves the current focus scope, for
        // example when the application is deactivated. GotFocus/LostFocus consequently do not
        // cover every IsKeyboardFocused transition that affects the visual state.
        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();

        if ((bool)e.NewValue)
        {
            RevealClearButton();
        }
        else
        {
            HideClearButton();
        }
    }

    private void OnTextBoxIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // IsEnabled is a read-only dependency property and UIElement does not expose an
        // overridable state-change method. Its public changed event is therefore the stable
        // invalidation point for refreshing the template-facing state brushes.
        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Resolves the brushes exposed to the template. Keyboard focus takes precedence
    /// over hover because both states normally overlap while the user edits text.
    /// Null state brushes intentionally inherit the corresponding normal-state brush.
    /// </summary>
    private void UpdateVisualBrushes()
    {
        Brush? foreground = Foreground;
        Brush? background = Background;
        Brush? borderBrush = BorderBrush;

        if (!IsEnabled)
        {
            background = DisabledBackground ?? background;
            borderBrush = DisabledBorderBrush ?? borderBrush;
        }
        else if (IsKeyboardFocused)
        {
            background = FocusedBackground ?? background;
            borderBrush = FocusedBorderBrush ?? borderBrush;
        }
        else if (IsMouseOver)
        {
            background = MouseOverBackground ?? background;
            borderBrush = MouseOverBorderBrush ?? borderBrush;
        }

        SetValue(VisualForegroundPropertyKey, foreground);
        SetValue(VisualBackgroundPropertyKey, background);
        SetValue(VisualBorderBrushPropertyKey, borderBrush);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        DetachClearButton();
        base.OnApplyTemplate();

        _mouseInputHelper.OnApplyTemplate(GetTemplateChild("PART_ContentHost") as ScrollViewer);

        _clearButton = GetTemplateChild(ClearButtonPartName) as ButtonBase;
        if (_clearButton is not null)
        {
            _clearButton.Click += OnClearButtonPartClick;
        }
    }

    /// <inheritdoc />
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        // The old template can remain instantiated until layout applies the replacement. Detach
        // immediately so an obsolete clear button cannot continue mutating this editor.
        DetachClearButton();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        bool shouldRestoreAutomaticSelection = AutoSelect && !IsKeyboardFocused;

        // Let TextBoxBase run the native TextEditorMouse focus, IME and hit-test path first. A
        // padding hit is marked handled after focus but deliberately receives no caret or mouse
        // capture. Bridging it here keeps every observable focus event in WPF's normal MouseDown
        // order and prevents a focus handler from opening a Popup before the editor sees the click.
        base.OnMouseDown(e);

        // WPF's own TextEditorMouse.OnMouseDown ignores clicks that hit TextBox padding or
        // template chrome, even when they are inside PART_ContentHost. Intercept that narrow
        // case after the regular bubble handler runs and initialize the native selection state as
        // if the click landed on the nearest edge of TextBoxView.
        if (
            e.ChangedButton is MouseButton.Left
            && _mouseInputHelper.TryForwardMouseDownFromContentHostPadding(e)
        )
        {
            e.Handled = true;
        }

        if (shouldRestoreAutomaticSelection && IsKeyboardFocused)
        {
            // TextEditorMouse moves the caret after it assigns keyboard focus. That native ordering
            // collapses a selection made from the focus event for an ordinary single click, so apply
            // the same opt-in selection once more after WPF has completed its mouse editing path.
            SelectAll();
        }
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        SetPlaceholderTextVisibility();
        RevealClearButton();

        base.OnTextChanged(e);
    }

    protected void SetPlaceholderTextVisibility()
    {
        if (PlaceholderEnabled)
        {
            if (IsPlaceholderVisible && Text.Length > 0)
            {
                IsPlaceholderVisible = false;
            }

            if (!IsPlaceholderVisible && Text.Length < 1)
            {
                IsPlaceholderVisible = true;
            }
        }
        else if (IsPlaceholderVisible)
        {
            IsPlaceholderVisible = false;
        }
    }

    /// <inheritdoc />
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();

        if (!AutoSelect)
        {
            CaretIndex = Text.Length;
        }

        RevealClearButton();
    }

    /// <inheritdoc />
    protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        base.OnGotKeyboardFocus(e);

        if (AutoSelect)
        {
            // Keyboard focus can return while logical focus is retained by a focus scope. GotFocus
            // is not raised in that case, so the keyboard-focus event is the reliable boundary for
            // applying this behavior on every entry into the editor.
            SelectAll();
        }
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        CoerceValue(ForegroundProperty);
        UpdateVisualBrushes();

        HideClearButton();
    }

    /// <inheritdoc />
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        base.OnContextMenuOpening(e);

        _ = TextEditorContextMenuPlacementHelper.TryOpenKeyboardInvokedMenu(
            this,
            e,
            GetSelectionStartCharacterRect
        );
    }

    /// <summary>
    /// Reveals the clear button by <see cref="IsClearButtonVisible"/> property.
    /// </summary>
    protected void RevealClearButton()
    {
        if (ClearButtonEnabled && IsKeyboardFocusWithin)
        {
            bool shouldShowClearButton = Text.Length > 0;

            if (IsClearButtonVisible != shouldShowClearButton)
            {
                IsClearButtonVisible = shouldShowClearButton;
            }
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global for HideClearButton()

    /// <summary>
    /// Hides the clear button by <see cref="IsClearButtonVisible"/> property.
    /// </summary>
    protected void HideClearButton()
    {
        if (ClearButtonEnabled && !IsKeyboardFocusWithin && IsClearButtonVisible)
        {
            IsClearButtonVisible = false;
        }
    }

    /// <summary>
    /// Triggered when the user clicks the clear text button.
    /// </summary>
    protected virtual void OnClearButtonClick()
    {
        if (Text.Length > 0)
        {
            SetCurrentValue(TextProperty, string.Empty);
        }
    }

    private void OnClearButtonPartClick(object sender, RoutedEventArgs e)
    {
        OnClearButtonClick();
    }

    private void DetachClearButton()
    {
        if (_clearButton is null)
        {
            return;
        }

        _clearButton.Click -= OnClearButtonPartClick;
        _clearButton = null;
    }

    private static void OnPlaceholderEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox control)
        {
            return;
        }

        control.OnPlaceholderEnabledChanged();
    }

    protected virtual void OnPlaceholderEnabledChanged()
    {
        SetPlaceholderTextVisibility();
    }

    private void OnTextBoxQueryCursor(object sender, QueryCursorEventArgs e)
    {
        _mouseInputHelper.OnQueryCursor(e);
    }

    private Rect GetSelectionStartCharacterRect()
    {
        int characterIndex = Math.Max(0, Math.Min(SelectionStart, Text.Length));

        try
        {
            return GetRectFromCharacterIndex(characterIndex, trailingEdge: false);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Rect.Empty;
        }
    }
}
