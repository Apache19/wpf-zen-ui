// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// A password input control that uses a native <see cref="System.Windows.Controls.PasswordBox"/> for masked mode
/// and a <see cref="System.Windows.Controls.TextBox"/> for revealed mode, switching between them seamlessly.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:PasswordBox PlaceholderText="Password..." Icon="{ui:SymbolIcon Password24}" /&gt;
/// </code>
/// </example>
[TemplatePart(Name = PasswordBoxPartName, Type = typeof(System.Windows.Controls.PasswordBox))]
[TemplatePart(Name = TextBoxPartName,     Type = typeof(System.Windows.Controls.TextBox))]
public class PasswordBox : Control
{
    private const string PasswordBoxPartName = "PART_PasswordBox";
    private const string TextBoxPartName     = "PART_TextBox";

    private System.Windows.Controls.PasswordBox? _passwordBox;
    private System.Windows.Controls.TextBox?     _textBox;
    private bool                                 _isSyncing;
    private int                                  _caretIndex;
    private int                                  _selectionLength;

    /// <summary>Identifies the <see cref="Password"/> dependency property.</summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password),
        typeof(string),
        typeof(PasswordBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordChanged,
            static (_, value) => value as string ?? string.Empty,
            false,
            UpdateSourceTrigger.PropertyChanged
        )
    );

    /// <summary>Identifies the <see cref="PasswordChar"/> dependency property.</summary>
    public static readonly DependencyProperty PasswordCharProperty = DependencyProperty.Register(
        nameof(PasswordChar),
        typeof(char),
        typeof(PasswordBox),
        new PropertyMetadata('\u25cf', OnPasswordCharChanged)
    );

    /// <summary>Identifies the <see cref="IsPasswordRevealed"/> dependency property.</summary>
    public static readonly DependencyProperty IsPasswordRevealedProperty = DependencyProperty.Register(
        nameof(IsPasswordRevealed),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(false, OnIsPasswordRevealedChanged)
    );

    /// <summary>Identifies the <see cref="RevealButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty RevealButtonEnabledProperty = DependencyProperty.Register(
        nameof(RevealButtonEnabled),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="ClearButtonEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty ClearButtonEnabledProperty = DependencyProperty.Register(
        nameof(ClearButtonEnabled),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="ShowClearButton"/> dependency property.</summary>
    public static readonly DependencyProperty ShowClearButtonProperty = DependencyProperty.Register(
        nameof(ShowClearButton),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="PlaceholderText"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(PasswordBox),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>Identifies the <see cref="PlaceholderEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty PlaceholderEnabledProperty = DependencyProperty.Register(
        nameof(PlaceholderEnabled),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="CurrentPlaceholderEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentPlaceholderEnabledProperty = DependencyProperty.Register(
        nameof(CurrentPlaceholderEnabled),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(true)
    );

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(PasswordBox),
        new PropertyMetadata(null, null, IconElement.Coerce)
    );

    /// <summary>Identifies the <see cref="IconPlacement"/> dependency property.</summary>
    public static readonly DependencyProperty IconPlacementProperty = DependencyProperty.Register(
        nameof(IconPlacement),
        typeof(ElementPlacement),
        typeof(PasswordBox),
        new PropertyMetadata(ElementPlacement.Left)
    );

    /// <summary>Identifies the <see cref="MaxLength"/> dependency property.</summary>
    public static readonly DependencyProperty MaxLengthProperty = DependencyProperty.Register(
        nameof(MaxLength),
        typeof(int),
        typeof(PasswordBox),
        new PropertyMetadata(0)
    );

    /// <summary>Identifies the <see cref="TemplateButtonCommand"/> dependency property.</summary>
    public static readonly DependencyProperty TemplateButtonCommandProperty = DependencyProperty.Register(
        nameof(TemplateButtonCommand),
        typeof(IRelayCommand),
        typeof(PasswordBox),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="PasswordChanged"/> routed event.</summary>
    public static readonly RoutedEvent PasswordChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(PasswordChanged),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(PasswordBox)
    );

    static PasswordBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PasswordBox),
            new FrameworkPropertyMetadata(typeof(PasswordBox))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordBox"/> class.
    /// </summary>
    public PasswordBox()
    {
        SetValue(TemplateButtonCommandProperty, new RelayCommand<string>(OnTemplateButtonClick));
    }

    /// <summary>
    /// Gets or sets the password value.
    /// </summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    /// <summary>
    /// Gets or sets the masking character for the password.
    /// </summary>
    public char PasswordChar
    {
        get => (char)GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the password is currently shown in plain text.
    /// </summary>
    public bool IsPasswordRevealed
    {
        get => (bool)GetValue(IsPasswordRevealedProperty);
        private set => SetValue(IsPasswordRevealedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the reveal button is enabled.
    /// </summary>
    public bool RevealButtonEnabled
    {
        get => (bool)GetValue(RevealButtonEnabledProperty);
        set => SetValue(RevealButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the clear button is enabled.
    /// </summary>
    public bool ClearButtonEnabled
    {
        get => (bool)GetValue(ClearButtonEnabledProperty);
        set => SetValue(ClearButtonEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to show the clear button.
    /// </summary>
    public bool ShowClearButton
    {
        get => (bool)GetValue(ShowClearButtonProperty);
        protected set => SetValue(ShowClearButtonProperty, value);
    }

    /// <summary>
    /// Gets or sets the placeholder text displayed when the password is empty.
    /// </summary>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether to enable the placeholder text.
    /// </summary>
    public bool PlaceholderEnabled
    {
        get => (bool)GetValue(PlaceholderEnabledProperty);
        set => SetValue(PlaceholderEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the placeholder text is currently visible.
    /// </summary>
    public bool CurrentPlaceholderEnabled
    {
        get => (bool)GetValue(CurrentPlaceholderEnabledProperty);
        protected set => SetValue(CurrentPlaceholderEnabledProperty, value);
    }

    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>
    /// Gets or sets which side the icon should be placed on.
    /// </summary>
    public ElementPlacement IconPlacement
    {
        get => (ElementPlacement)GetValue(IconPlacementProperty);
        set => SetValue(IconPlacementProperty, value);
    }

    /// <summary>
    /// Gets or sets the maximum number of characters the password can contain.
    /// </summary>
    public int MaxLength
    {
        get => (int)GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    /// <summary>
    /// Gets the command triggered when clicking a button in the control template.
    /// </summary>
    public IRelayCommand TemplateButtonCommand => (IRelayCommand)GetValue(TemplateButtonCommandProperty);

    /// <summary>
    /// Occurs when the value of the <see cref="Password"/> property changes.
    /// </summary>
    public event RoutedEventHandler PasswordChanged
    {
        add => AddHandler(PasswordChangedEvent, value);
        remove => RemoveHandler(PasswordChangedEvent, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        DetachEvents();

        _passwordBox = GetTemplateChild(PasswordBoxPartName) as System.Windows.Controls.PasswordBox;
        _textBox     = GetTemplateChild(TextBoxPartName) as System.Windows.Controls.TextBox;

        if (_textBox is not null)
        {
            InputMethod.SetIsInputMethodEnabled(_textBox, false);
            _textBox.SetCurrentValue(System.Windows.Controls.Primitives.TextBoxBase.IsUndoEnabledProperty, false);

            CommandManager.AddPreviewCanExecuteHandler(_textBox, OnTextBoxPreviewCanExecute);
            CommandManager.AddPreviewExecutedHandler(_textBox, OnTextBoxPreviewExecuted);
        }

        AttachEvents();
        SyncToInternalControls();
        UpdatePlaceholderVisibility();
    }

    /// <inheritdoc />
    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);

        FocusActiveInput();
        RevealClearButton();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(RoutedEventArgs e)
    {
        base.OnLostFocus(e);

        HideClearButton();
    }

    /// <inheritdoc />
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsKeyboardFocusWithinChanged(e);

        if ((bool)e.NewValue)
        {
            RevealClearButton();
        }
        else
        {
            HideClearButton();
        }
    }

    /// <summary>This method is invoked when the <see cref="PasswordProperty"/> changes.</summary>
    protected virtual void OnPasswordChanged()
    {
        if (_isSyncing)
        {
            return;
        }

        SyncToInternalControls();
        UpdatePlaceholderVisibility();
        RevealClearButton();
        RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));
    }

    /// <summary>
    /// Called when the <see cref="IsPasswordRevealed"/> property changes.
    /// </summary>
    protected virtual void OnIsPasswordRevealedChanged()
    {
        bool hadFocus = IsKeyboardFocusWithin;
        SaveCaretState();
        SyncToInternalControls();

        if (hadFocus)
        {
            FocusActiveInput();
        }
    }

    /// <summary>
    /// Called when a template button is clicked.
    /// </summary>
    protected virtual void OnTemplateButtonClick(string? parameter)
    {
        switch (parameter)
        {
            case "reveal":
                SetCurrentValue(IsPasswordRevealedProperty, !IsPasswordRevealed);
                break;

            case "clear":
                if (Password.Length > 0)
                {
                    SetCurrentValue(PasswordProperty, string.Empty);
                }

                break;
        }
    }

    private void AttachEvents()
    {
        if (_passwordBox is not null)
        {
            _passwordBox.PasswordChanged += OnInternalPasswordChanged;
            _passwordBox.GotFocus        += OnInternalGotFocus;
            _passwordBox.LostFocus       += OnInternalLostFocus;
        }

        if (_textBox is not null)
        {
            _textBox.TextChanged += OnInternalTextChanged;
            _textBox.GotFocus    += OnInternalGotFocus;
            _textBox.LostFocus   += OnInternalLostFocus;
        }
    }

    private void DetachEvents()
    {
        if (_passwordBox is not null)
        {
            _passwordBox.PasswordChanged -= OnInternalPasswordChanged;
            _passwordBox.GotFocus        -= OnInternalGotFocus;
            _passwordBox.LostFocus       -= OnInternalLostFocus;
        }

        if (_textBox is not null)
        {
            _textBox.TextChanged -= OnInternalTextChanged;
            _textBox.GotFocus    -= OnInternalGotFocus;
            _textBox.LostFocus   -= OnInternalLostFocus;
            CommandManager.RemovePreviewCanExecuteHandler(_textBox, OnTextBoxPreviewCanExecute);
            CommandManager.RemovePreviewExecutedHandler(_textBox, OnTextBoxPreviewExecuted);
        }
    }

    private void OnInternalPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_isSyncing || _passwordBox is null)
        {
            return;
        }

        _isSyncing = true;

        try
        {
            SetCurrentValue(PasswordProperty, _passwordBox.Password);
            UpdatePlaceholderVisibility();
            RevealClearButton();
            RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnInternalTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSyncing || _textBox is null)
        {
            return;
        }

        _isSyncing = true;

        try
        {
            SetCurrentValue(PasswordProperty, _textBox.Text);
            UpdatePlaceholderVisibility();
            RevealClearButton();
            RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void OnInternalGotFocus(object sender, RoutedEventArgs e)
    {
        RevealClearButton();
    }

    private void OnInternalLostFocus(object sender, RoutedEventArgs e)
    {
        HideClearButton();
    }

    private void SyncToInternalControls()
    {
        if (_isSyncing)
        {
            return;
        }

        _isSyncing = true;

        try
        {
            string password = Password ?? string.Empty;

            if (_passwordBox is not null && _passwordBox.Password != password)
            {
                _passwordBox.Password = password;
            }

            if (_textBox is not null && _textBox.Text != password)
            {
                _textBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, password);
            }
        }
        finally
        {
            _isSyncing = false;
        }
    }

    private void SaveCaretState()
    {
        if (IsPasswordRevealed)
        {
            if (_textBox is not null)
            {
                _caretIndex      = _textBox.CaretIndex;
                _selectionLength = _textBox.SelectionLength;
            }
        }
        else
        {
            if (_passwordBox is not null)
            {
                _caretIndex      = (Password ?? string.Empty).Length;
                _selectionLength = 0;
            }
        }
    }

    private void FocusActiveInput()
    {
        _ = Dispatcher.InvokeAsync(
            () =>
            {
                if (IsPasswordRevealed)
                {
                    if (_textBox is { Visibility: Visibility.Visible })
                    {
                        _ = _textBox.Focus();
                        int len   = _textBox.Text.Length;
                        int caret = Math.Min(_caretIndex,      len);
                        int sel   = Math.Min(_selectionLength, len - caret);
                        _textBox.Select(caret, sel);
                    }
                }
                else
                {
                    if (_passwordBox is { Visibility: Visibility.Visible })
                    {
                        _passwordBox.SelectAll();
                        _ = _passwordBox.Focus();
                    }
                }
            },
            System.Windows.Threading.DispatcherPriority.Input);
    }

    private void UpdatePlaceholderVisibility()
    {
        if (!PlaceholderEnabled)
        {
            SetCurrentValue(CurrentPlaceholderEnabledProperty, false);
            return;
        }

        SetCurrentValue(CurrentPlaceholderEnabledProperty, (Password?.Length ?? 0) == 0);
    }

    private void RevealClearButton()
    {
        if (ClearButtonEnabled && IsKeyboardFocusWithin)
        {
            SetCurrentValue(ShowClearButtonProperty, (Password?.Length ?? 0) > 0);
        }
    }

    private void HideClearButton()
    {
        if (ShowClearButton && !IsKeyboardFocusWithin)
        {
            SetCurrentValue(ShowClearButtonProperty, false);
        }
    }

    private static void OnPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox control)
        {
            control.OnPasswordChanged();
        }
    }

    private static void OnPasswordCharChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox control && control._passwordBox is not null)
        {
            control._passwordBox.SetCurrentValue(System.Windows.Controls.PasswordBox.PasswordCharProperty, e.NewValue);
        }
    }

    private static void OnIsPasswordRevealedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is PasswordBox control)
        {
            control.OnIsPasswordRevealedChanged();
        }
    }

    private static void OnTextBoxPreviewCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut)
        {
            e.ContinueRouting = true;
            e.Handled         = true;
        }
    }

    private static void OnTextBoxPreviewExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Command == ApplicationCommands.Copy || e.Command == ApplicationCommands.Cut)
        {
            e.Handled = true;
        }
    }
}