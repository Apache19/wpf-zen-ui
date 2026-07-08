// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Ui.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extended <see cref="System.Windows.Controls.TextBox"/> with additional parameters like <see cref="PlaceholderText"/>.
/// </summary>
public class TextBox : System.Windows.Controls.TextBox
{
    private readonly TextEditorMouseInputHelper _mouseInputHelper;

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(IconElement),
            typeof(TextBox),
            new PropertyMetadata(null, null, IconElement.Coerce)
        );

    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Identifies the <see cref="IconPlacement"/> dependency property.</summary>
    public static readonly DependencyProperty IconPlacementProperty =
        DependencyProperty.Register(
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
    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
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
    public static readonly DependencyProperty PlaceholderEnabledProperty =
        DependencyProperty.Register(
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
    public static readonly DependencyProperty ClearButtonEnabledProperty =
        DependencyProperty.Register(
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
    public static readonly DependencyProperty IsFocusIndicatorVisibleProperty =
        DependencyProperty.Register(
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
    public static readonly DependencyProperty UseTextBlockAppearanceProperty =
        DependencyProperty.Register(
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

    /// <summary>Identifies the <see cref="TemplateButtonCommand"/> dependency property.</summary>
    public static readonly DependencyProperty TemplateButtonCommandProperty =
        DependencyProperty.Register(
            nameof(TemplateButtonCommand),
            typeof(IRelayCommand),
            typeof(TextBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets the command triggered when clicking the button.
    /// </summary>
    public IRelayCommand TemplateButtonCommand => (IRelayCommand)GetValue(TemplateButtonCommandProperty);

    static TextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TextBox),
            new FrameworkPropertyMetadata(typeof(TextBox))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextBox"/> class.
    /// </summary>
    public TextBox()
    {
        _mouseInputHelper = new TextEditorMouseInputHelper(this);

        SetValue(TemplateButtonCommandProperty, new RelayCommand<string>(OnTemplateButtonClick));
        IsPlaceholderVisible = PlaceholderEnabled;

        // Cursor is normally resolved through a bubbling QueryCursor event. Using
        // handledEventsToo lets us correct the cursor even if PART_ContentHost.Cursor has
        // already handled the event, while still forwarding real TextBoxView hits back to
        // WPF's internal cursor logic.
        AddHandler(Mouse.QueryCursorEvent, new QueryCursorEventHandler(OnTextBoxQueryCursor), handledEventsToo: true);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _mouseInputHelper.OnApplyTemplate(GetTemplateChild("PART_ContentHost") as ScrollViewer);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // WPF's own TextEditorMouse.OnMouseDown ignores clicks that hit TextBox padding or
        // template chrome, even when they are inside PART_ContentHost. Intercept that narrow
        // case before the regular bubble handler runs and initialize the native selection
        // state as if the click landed on the nearest edge of TextBoxView.
        if (_mouseInputHelper.TryForwardMouseDownFromContentHostPadding(e))
        {
            e.Handled = true;
            return;
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);

        SetPlaceholderTextVisibility();

        RevealClearButton();
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

        CaretIndex = Text.Length;

        RevealClearButton();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

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

    /// <summary>
    /// Triggered by clicking a button in the control template.
    /// </summary>
    protected virtual void OnTemplateButtonClick(string? parameter)
    {
        Debug.WriteLine($"INFO: {typeof(TextBox)} button clicked", "Wpf.Ui.TextBox");

        OnClearButtonClick();
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
