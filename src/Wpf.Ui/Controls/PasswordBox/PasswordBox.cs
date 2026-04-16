// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// The modified password control.
/// </summary>
public partial class PasswordBox : TextBox
{
    private readonly PasswordHelper _passwordHelper;
    private bool _isUpdating;

    /// <summary>
    /// Identifies the <see cref="Password"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PasswordProperty = DependencyProperty.Register(
        nameof(Password),
        typeof(string),
        typeof(PasswordBox),
        new FrameworkPropertyMetadata(
            string.Empty,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnPasswordChanged,
            static (o, value) => value as string ?? string.Empty,
            false,
            UpdateSourceTrigger.PropertyChanged
        )
    );

    /// <summary>
    /// Identifies the <see cref="PasswordChar"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PasswordCharProperty = DependencyProperty.Register(
        nameof(PasswordChar),
        typeof(char),
        typeof(PasswordBox),
        new PropertyMetadata('\u25cf', OnPasswordCharChanged)
    );

    /// <summary>
    /// Identifies the <see cref="IsPasswordRevealed"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsPasswordRevealedProperty = DependencyProperty.Register(
        nameof(IsPasswordRevealed),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(false, OnIsPasswordRevealedChanged)
    );

    /// <summary>
    /// Identifies the <see cref="RevealButtonEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RevealButtonEnabledProperty = DependencyProperty.Register(
        nameof(RevealButtonEnabled),
        typeof(bool),
        typeof(PasswordBox),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Identifies the <see cref="PasswordChanged"/> routed event.
    /// </summary>
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
        _passwordHelper = new PasswordHelper(this);
    }

    /// <summary>
    /// Gets or sets the actual password (not asterisks).
    /// </summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    /// <summary>
    /// Gets or sets the character used to mask the password.
    /// </summary>
    public char PasswordChar
    {
        get => (char)GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether the password is currently revealed.
    /// </summary>
    public bool IsPasswordRevealed
    {
        get => (bool)GetValue(IsPasswordRevealedProperty);
        private set => SetValue(IsPasswordRevealedProperty, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the password reveal button is enabled.
    /// </summary>
    public bool RevealButtonEnabled
    {
        get => (bool)GetValue(RevealButtonEnabledProperty);
        set => SetValue(RevealButtonEnabledProperty, value);
    }

    /// <summary>
    /// Occurs when the password content changes.
    /// </summary>
    public event RoutedEventHandler PasswordChanged
    {
        add => AddHandler(PasswordChangedEvent, value);
        remove => RemoveHandler(PasswordChangedEvent, value);
    }

    /// <inheritdoc/>
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        bool isInternalUpdate = _isUpdating;

        UpdateTextContents(e.Changes);

        if (isInternalUpdate || _isUpdating)
        {
            SetPlaceholderTextVisibility();
            RevealClearButton();

            return;
        }

        base.OnTextChanged(e);
    }

    /// <inheritdoc/>
    protected override void OnPreviewTextInput(TextCompositionEventArgs e)
    {
        if (!IsPasswordRevealed)
        {
            _passwordHelper.RememberInputText(e.Text);
        }

        base.OnPreviewTextInput(e);
    }

    /// <inheritdoc/>
    protected override void OnClearButtonClick()
    {
        if (Password.Length < 1)
        {
            return;
        }

        SetCurrentValue(PasswordProperty, string.Empty);
    }

    /// <summary>
    /// Called when the <see cref="Password"/> property changes.
    /// </summary>
    protected virtual void OnPasswordChanged()
    {
        if (_isUpdating)
        {
            return;
        }

        string password = Password ?? string.Empty;
        string displayText = IsPasswordRevealed ? password : new string(PasswordChar, password.Length);
        int caretIndex = Math.Min(CaretIndex, displayText.Length);

        UpdateWithLock(() =>
        {
            SetCurrentValue(TextProperty, displayText);
            CaretIndex = caretIndex;
        });

        RaisePasswordChangedEvent();
    }

    /// <summary>
    /// Called when the <see cref="PasswordChar"/> property changes.
    /// </summary>
    protected virtual void OnPasswordCharChanged()
    {
        if (IsPasswordRevealed)
        {
            return;
        }

        UpdateWithLock(() =>
            SetCurrentValue(TextProperty, new string(PasswordChar, Password?.Length ?? 0))
        );
    }

    /// <summary>
    /// Called when the <see cref="IsPasswordRevealed"/> property changes.
    /// </summary>
    protected virtual void OnIsPasswordRevealedChanged()
    {
        string password = Password ?? string.Empty;

        UpdateWithLock(() =>
            SetCurrentValue(
                TextProperty,
                IsPasswordRevealed ? password : new string(PasswordChar, password.Length)
            )
        );
    }

    /// <inheritdoc/>
    protected override void OnTemplateButtonClick(string? parameter)
    {
        if (parameter == "reveal")
        {
            SetCurrentValue(IsPasswordRevealedProperty, !IsPasswordRevealed);
            _ = Focus();
            CaretIndex = Text.Length;
        }
        else
        {
            base.OnTemplateButtonClick(parameter);
        }
    }

    /// <summary>
    /// Updates the text contents based on the current state.
    /// </summary>
    /// <param name="textChanges">The text changes.</param>
    private void UpdateTextContents(ICollection<TextChange> textChanges)
    {
        if (_isUpdating)
        {
            return;
        }

        if (IsPasswordRevealed)
        {
            HandleRevealedModeUpdate();
            return;
        }

        HandleHiddenModeUpdate(textChanges);
    }

    /// <summary>
    /// Handles updates when password is in revealed mode.
    /// </summary>
    private void HandleRevealedModeUpdate()
    {
        if (Password == Text)
        {
            return;
        }

        UpdateWithLock(() =>
        {
            SetCurrentValue(PasswordProperty, Text);
            RaisePasswordChangedEvent();
        });
    }

    /// <summary>
    /// Handles updates when password is in hidden mode.
    /// </summary>
    /// <param name="textChanges">The text changes.</param>
    private void HandleHiddenModeUpdate(ICollection<TextChange> textChanges)
    {
        var caretIndex = CaretIndex;
        string newPassword = textChanges.Count > 0 ? _passwordHelper.GetNewPassword(textChanges) : Password;
        var passwordLength = newPassword.Length;

        UpdateWithLock(() =>
        {
            SetCurrentValue(TextProperty, new string(PasswordChar, passwordLength));
            SetCurrentValue(PasswordProperty, newPassword);
            CaretIndex = Math.Min(caretIndex, passwordLength);
            RaisePasswordChangedEvent();
        });
    }

    /// <summary>
    /// Executes an action while preventing recursive updates.
    /// </summary>
    /// <param name="updateAction">The action to execute.</param>
    private void UpdateWithLock(Action updateAction)
    {
        _isUpdating = true;

        try
        {
            updateAction();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Raises the <see cref="PasswordChangedEvent"/>.
    /// </summary>
    private void RaisePasswordChangedEvent() => RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));

    /// <summary>
    /// Handles changes to the <see cref="Password"/> dependency property.
    /// </summary>
    /// <param name="dependencyObject">The <see cref="DependencyObject"/> that raised the event.</param>
    /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> containing the event data.</param>
    private static void OnPasswordChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (dependencyObject is PasswordBox passwordBox)
        {
            passwordBox.OnPasswordChanged();
        }
    }

    /// <summary>
    /// Handles changes to the <see cref="PasswordChar"/> dependency property.
    /// </summary>
    /// <param name="dependencyObject">The <see cref="DependencyObject"/> instance where the change occurred.</param>
    /// <param name="e">Event data that contains information about the property change.</param>
    private static void OnPasswordCharChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (dependencyObject is PasswordBox passwordBox)
        {
            passwordBox.OnPasswordCharChanged();
        }
    }

    /// <summary>
    /// Handles changes to the <see cref="IsPasswordRevealed"/> dependency property.
    /// </summary>
    /// <param name="dependencyObject">The <see cref="DependencyObject"/> instance where the property changed.</param>
    /// <param name="e">The <see cref="DependencyPropertyChangedEventArgs"/> containing the old and new values.</param>
    private static void OnIsPasswordRevealedChanged(
        DependencyObject dependencyObject,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (dependencyObject is PasswordBox passwordBox)
        {
            passwordBox.OnIsPasswordRevealedChanged();
        }
    }
}