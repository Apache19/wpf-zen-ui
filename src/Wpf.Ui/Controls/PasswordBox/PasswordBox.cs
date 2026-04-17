// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Wpf.Ui.Extensions.Wpf;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// The modified password control.
/// </summary>
public sealed partial class PasswordBox : TextBox
{
    private readonly PasswordHelper _passwordHelper;

    private bool          _isUpdating;
    private ScrollViewer? _contentHost;

    #region DP PasswordProperty,PasswordCharProperty,IsPasswordRevealedProperty,RevealButtonEnabledProperty

    /// <summary>
    /// Identifies the <see cref="Password"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PasswordProperty =
        DependencyProperty.Register(
            nameof(Password),
            typeof(string),
            typeof(PasswordBox),
            new FrameworkPropertyMetadata(
                string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                static (d, e) =>
                {
                    if (d is PasswordBox passwordBox)
                    {
                        passwordBox.OnPasswordChanged((string)e.NewValue ?? string.Empty);
                    }
                },
                static (_, value) => value as string ?? string.Empty,
                false,
                UpdateSourceTrigger.PropertyChanged
            )
        );

    /// <summary>
    /// Gets or sets the actual password (not asterisks).
    /// </summary>
    public string Password
    {
        get => (string)GetValue(PasswordProperty);
        set => SetValue(PasswordProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="PasswordChar"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PasswordCharProperty =
        DependencyProperty.Register(
            nameof(PasswordChar),
            typeof(char),
            typeof(PasswordBox),
            new PropertyMetadata(
                '\u25cf',
                static (d, e) =>
                {
                    if (d is PasswordBox passwordBox)
                    {
                        passwordBox.OnPasswordCharChanged();
                    }
                }
            )
        );

    /// <summary>
    /// Gets or sets the character used to mask the password.
    /// </summary>
    public char PasswordChar
    {
        get => (char)GetValue(PasswordCharProperty);
        set => SetValue(PasswordCharProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsPasswordRevealed"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsPasswordRevealedProperty =
        DependencyProperty.Register(
            nameof(IsPasswordRevealed),
            typeof(bool),
            typeof(PasswordBox),
            new PropertyMetadata(
                false,
                static (d, e) =>
                {
                    if (d is PasswordBox passwordBox)
                    {
                        passwordBox.OnIsPasswordRevealedChanged((bool)e.NewValue);
                    }
                }
            )
        );

    /// <summary>
    /// Gets a value indicating whether the password is currently revealed.
    /// </summary>
    public bool IsPasswordRevealed
    {
        get => (bool)GetValue(IsPasswordRevealedProperty);
        private set => SetValue(IsPasswordRevealedProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="RevealButtonEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RevealButtonEnabledProperty =
        DependencyProperty.Register(
            nameof(RevealButtonEnabled),
            typeof(bool),
            typeof(PasswordBox),
            new PropertyMetadata(true)
        );

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether the password reveal button is enabled.
    /// </summary>
    public bool RevealButtonEnabled
    {
        get => (bool)GetValue(RevealButtonEnabledProperty);
        set => SetValue(RevealButtonEnabledProperty, value);
    }

    #endregion

    /// <summary>
    /// Identifies the <see cref="PasswordChanged"/> routed event.
    /// </summary>
    private static readonly RoutedEvent PasswordChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(PasswordChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(PasswordBox)
        );

    /// <summary>
    /// Occurs when the password content changes.
    /// </summary>
    public event RoutedEventHandler PasswordChanged
    {
        add => AddHandler(PasswordChangedEvent, value);
        remove => RemoveHandler(PasswordChangedEvent, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsPasswordRevealedChanged"/> routed event.
    /// </summary>
    private static readonly RoutedEvent IsPasswordRevealedChangedEvent =
        EventManager.RegisterRoutedEvent(
            nameof(IsPasswordRevealedChanged),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(PasswordBox)
        );

    /// <summary>
    /// Occurs when the IsPasswordRevealed property changes.
    /// </summary>
    public event RoutedEventHandler IsPasswordRevealedChanged
    {
        add => AddHandler(IsPasswordRevealedChangedEvent, value);
        remove => RemoveHandler(IsPasswordRevealedChangedEvent, value);
    }

    static PasswordBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(PasswordBox),
            new FrameworkPropertyMetadata(typeof(PasswordBox))
        );

        // Force disable input methods to ensure entered text is treated as password characters
        InputMethod.IsInputMethodEnabledProperty.OverrideMetadata(
            typeof(PasswordBox),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.Inherits,
                null,
                static (d, v) => false
            )
        );

        // Disable undo functionality
        IsUndoEnabledProperty.OverrideMetadata(
            typeof(PasswordBox),
            new FrameworkPropertyMetadata(
                false,
                null,
                static (d, v) => false
            )
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PasswordBox"/> class.
    /// </summary>
    public PasswordBox()
    {
        _passwordHelper = new PasswordHelper(this);

        // Disable copy and cut commands via CommandBindings so ContextMenu MenuItem
        // CommandTarget (PlacementTarget) queries will hit these handlers.
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, HandleExecutedCommand, HandleCanExecuteCommand));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut,  HandleExecutedCommand, HandleCanExecuteCommand));

        Loaded += (s, e) =>
        {
            // Disable drag/copy operations
            DataObject.AddCopyingHandler(this, HandleDataObjectCopying);
        };

        Unloaded += (s, e) => { DataObject.RemoveCopyingHandler(this, HandleDataObjectCopying); };
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _contentHost = GetTemplateChild("PART_ContentHost") as ScrollViewer;
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        // Prevent TextBox from entering drag mode when clicking on selected text,
        // which would keep the old selection anchor and cause unexpected selection behavior.
        if (SelectionLength > 0
            && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)
            && IsDescendantOfContentHost(e.OriginalSource as DependencyObject))
        {
            var index = GetCharacterIndexFromPoint(e.GetPosition(this), true);

            if (index >= SelectionStart && index < SelectionStart + SelectionLength)
            {
                CaretIndex = index;
                return;
            }
        }

        base.OnPreviewMouseLeftButtonDown(e);
    }

    /// <inheritdoc/>
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        var isInternalUpdate = _isUpdating;

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
    protected override void OnClearButtonClick()
    {
        if (Password.Length < 1)
        {
            return;
        }

        SetCurrentValue(PasswordProperty, string.Empty);
    }

    /// <summary>
    /// Called when the <see cref="Password"/> property changes with the new value provided by the property system.
    /// </summary>
    private void OnPasswordChanged(string newPassword)
    {
        if (_isUpdating)
        {
            return;
        }

        var displayText = IsPasswordRevealed ? newPassword : new string(PasswordChar, newPassword.Length);
        var caretIndex  = Math.Min(CaretIndex, displayText.Length);

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
    private void OnPasswordCharChanged()
    {
        if (IsPasswordRevealed)
        {
            return;
        }

        var password     = Password;
        var passwordChar = PasswordChar;
        var length       = password.Length;

        UpdateWithLock(() =>
            SetCurrentValue(TextProperty, new string(passwordChar, length))
        );
    }

    /// <summary>This method is invoked when the <see cref="IsPasswordRevealedProperty"/> changes.</summary>
    private void OnIsPasswordRevealedChanged(bool isRevealed)
    {
        var password        = Password;
        var passwordChar    = PasswordChar;
        var selectionStart  = SelectionStart;
        var selectionLength = SelectionLength;
        var passwordLength  = password.Length;

        UpdateWithLock(() =>
        {
            SetCurrentValue(
                TextProperty,
                isRevealed ? password : new string(passwordChar, passwordLength)
            );

            selectionStart  = SelectionStart = Math.Min(selectionStart, passwordLength);
            SelectionLength = Math.Min(selectionLength, passwordLength - selectionStart);
        });

        RaiseEvent(new RoutedEventArgs(IsPasswordRevealedChangedEvent));
    }

    /// <inheritdoc/>
    protected override void OnTemplateButtonClick(string? parameter)
    {
        if (parameter == "reveal")
        {
            SetCurrentValue(IsPasswordRevealedProperty, !IsPasswordRevealed);
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
        var caretIndex     = CaretIndex;
        var newPassword    = textChanges.Count > 0 ? _passwordHelper.GetNewPassword(textChanges) : Password;
        var passwordLength = newPassword.Length;
        var passwordChar   = PasswordChar;

        UpdateWithLock(() =>
        {
            SetCurrentValue(TextProperty,     new string(passwordChar, passwordLength));
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
    private void RaisePasswordChangedEvent()
    {
        RaiseEvent(new RoutedEventArgs(PasswordChangedEvent));
    }

    private static void HandleDataObjectCopying(object sender, DataObjectCopyingEventArgs e)
    {
        e.CancelCommand();
    }

    private static void HandleCanExecuteCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = false;
        e.Handled    = true;
    }

    private static void HandleExecutedCommand(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
    }

    private bool IsDescendantOfContentHost(DependencyObject? element)
    {
        return _contentHost is not null &&
               element.FindAncestor<ScrollViewer>() == _contentHost;
    }
}