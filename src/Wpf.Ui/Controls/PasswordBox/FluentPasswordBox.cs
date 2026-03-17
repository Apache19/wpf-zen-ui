// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

[TemplatePart(Name = PartPasswordBox,  Type = typeof(PasswordBox))]
[TemplatePart(Name = PartTextBox,      Type = typeof(TextBox))]
[TemplatePart(Name = PartRevealButton, Type = typeof(ToggleButton))]
public class FluentPasswordBox : Control
{
    private const string PartPasswordBox  = "PART_PasswordBox";
    private const string PartTextBox      = "PART_TextBox";
    private const string PartRevealButton = "PART_RevealButton";

    private PasswordBox? _passwordBox;
    private TextBox?     _textBox;

    private bool _updating;

    static FluentPasswordBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(FluentPasswordBox),
            new FrameworkPropertyMetadata(typeof(FluentPasswordBox)));
    }

    #region Password

    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(FluentPasswordBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPasswordChanged));

    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    private static void OnPasswordChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e)
    {
        var c     = (FluentPasswordBox)d;
        var value = (string?)e.NewValue ?? "";

        if (c._passwordBox != null && c._passwordBox.Password != value)
            c._passwordBox.Password = value;

        if (c._textBox != null && c._textBox.Text != value)
            c._textBox.Text = value;
    }

    #endregion

    #region IsPasswordRevealed

    public static readonly DependencyProperty IsPasswordRevealedProperty =
        DependencyProperty.Register(
            nameof(IsPasswordRevealed),
            typeof(bool),
            typeof(FluentPasswordBox),
            new PropertyMetadata(false));

    public bool IsPasswordRevealed
    {
        get => (bool)GetValue(IsPasswordRevealedProperty);
        set => SetValue(IsPasswordRevealedProperty, value);
    }

    #endregion

    #region RevealButtonVisibility

    public static readonly DependencyProperty RevealButtonVisibilityProperty =
        DependencyProperty.Register(
            nameof(RevealButtonVisibility),
            typeof(Visibility),
            typeof(FluentPasswordBox),
            new PropertyMetadata(Visibility.Visible));

    public Visibility RevealButtonVisibility
    {
        get => (Visibility)GetValue(RevealButtonVisibilityProperty);
        set => SetValue(RevealButtonVisibilityProperty, value);
    }

    #endregion

    #region PlaceholderText

    public static readonly DependencyProperty PlaceholderTextProperty =
        DependencyProperty.Register(
            nameof(PlaceholderText),
            typeof(string),
            typeof(FluentPasswordBox),
            new PropertyMetadata(string.Empty));

    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    #endregion

    #region CapsLockIndicator

    public static readonly DependencyProperty IsCapsLockOnProperty =
        DependencyProperty.Register(
            nameof(IsCapsLockOn),
            typeof(bool),
            typeof(FluentPasswordBox),
            new PropertyMetadata(false));

    public bool IsCapsLockOn
    {
        get => (bool)GetValue(IsCapsLockOnProperty);
        private set => SetValue(IsCapsLockOnProperty, value);
    }

    #endregion

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (_passwordBox != null)
            _passwordBox.PasswordChanged -= PasswordBoxChanged;

        if (_textBox != null)
            _textBox.TextChanged -= TextBoxChanged;

        _passwordBox = GetTemplateChild(PartPasswordBox) as PasswordBox;
        _textBox     = GetTemplateChild(PartTextBox) as TextBox;

        if (_passwordBox != null)
        {
            _passwordBox.Password        =  Password ?? "";
            _passwordBox.PasswordChanged += PasswordBoxChanged;
            _passwordBox.PreviewKeyDown  += OnKey;
        }

        if (_textBox != null)
        {
            _textBox.Text           =  Password ?? "";
            _textBox.TextChanged    += TextBoxChanged;
            _textBox.PreviewKeyDown += OnKey;
        }
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        IsCapsLockOn = Keyboard.IsKeyToggled(Key.CapsLock);
    }

    private void PasswordBoxChanged(object sender, RoutedEventArgs e)
    {
        if (_updating) return;

        _updating = true;

        Password = _passwordBox?.Password ?? "";

        if (_textBox != null)
            _textBox.Text = Password;

        _updating = false;
    }

    private void TextBoxChanged(object sender, TextChangedEventArgs e)
    {
        if (_updating) return;

        _updating = true;

        Password = _textBox?.Text ?? "";

        if (_passwordBox != null)
            _passwordBox.Password = Password;

        _updating = false;
    }
}