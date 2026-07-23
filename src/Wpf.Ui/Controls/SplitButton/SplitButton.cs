// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Ui.AutomationPeers;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Presents a primary action and an adjacent button that opens a <see cref="ContextMenu"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SplitButton"/> is deliberately a <see cref="ContentControl"/>, not a
/// <see cref="ButtonBase"/>. Its template owns two independent <see cref="Button"/> controls so
/// the primary action and the drop-down action cannot accidentally share WPF's routed
/// <see cref="ButtonBase.ClickEvent"/> or pressed state.
/// </para>
/// <para>
/// Custom templates should provide <c>PART_PrimaryButton</c> and
/// <c>PART_DropDownButton</c>. Missing parts are tolerated; the corresponding interaction is
/// simply unavailable. The default template binds <see cref="Appearance"/> to both buttons so
/// every segment uses the standard WPF UI button appearance system.
/// </para>
/// </remarks>
[TemplatePart(Name = PrimaryButtonPartName,  Type = typeof(Button))]
[TemplatePart(Name = DropDownButtonPartName, Type = typeof(Button))]
public class SplitButton : ContentControl, ICommandSource, IAppearanceControl, IIconControl
{
    private const string PrimaryButtonPartName  = "PART_PrimaryButton";
    private const string DropDownButtonPartName = "PART_DropDownButton";

    private Button?      _primaryButton;
    private Button?      _dropDownButton;
    private ContextMenu? _contextMenu;

    // A ContextMenu captures the mouse while open. WPF raises
    // PreviewMouseDownOutsideCapturedElement on the menu and dismisses it before forwarding the
    // original press to the placement target. Preserve that input timestamp until the forwarded
    // press reaches the drop-down button, so its Click cannot reopen the menu it just dismissed.
    private int? _dismissedDropDownMouseTimestamp;
    private bool _isSynchronizingDropDownState;
    private bool _isSpaceKeyDown;

    #region Routed Event: Click

    /// <summary>Identifies the <see cref="Click"/> routed event.</summary>
    public static readonly RoutedEvent ClickEvent = EventManager.RegisterRoutedEvent(
        nameof(Click),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SplitButton)
    );

    /// <summary>
    /// Occurs when the primary segment is invoked. This event is distinct from
    /// <see cref="ButtonBase.ClickEvent"/> raised by the button inside the control template.
    /// </summary>
    [Category("Behavior")]
    public event RoutedEventHandler Click
    {
        add => AddHandler(ClickEvent, value);
        remove => RemoveHandler(ClickEvent, value);
    }

    #endregion

    #region Routed Event: DropDownOpened

    /// <summary>Identifies the <see cref="DropDownOpened"/> routed event.</summary>
    public static readonly RoutedEvent DropDownOpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(DropDownOpened),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SplitButton)
    );

    /// <summary>Occurs after the associated <see cref="ContextMenu"/> has opened.</summary>
    [Category("Behavior")]
    public event RoutedEventHandler DropDownOpened
    {
        add => AddHandler(DropDownOpenedEvent, value);
        remove => RemoveHandler(DropDownOpenedEvent, value);
    }

    #endregion

    #region Routed Event: DropDownClosed

    /// <summary>Identifies the <see cref="DropDownClosed"/> routed event.</summary>
    public static readonly RoutedEvent DropDownClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(DropDownClosed),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(SplitButton)
    );

    /// <summary>Occurs after the associated <see cref="ContextMenu"/> has closed.</summary>
    [Category("Behavior")]
    public event RoutedEventHandler DropDownClosed
    {
        add => AddHandler(DropDownClosedEvent, value);
        remove => RemoveHandler(DropDownClosedEvent, value);
    }

    #endregion

    #region DP: Appearance

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty = Button.AppearanceProperty.AddOwner(
        typeof(SplitButton),
        new FrameworkPropertyMetadata(ControlAppearance.Normal)
    );

    /// <inheritdoc />
    [Bindable(true)]
    [Category("Appearance")]
    public ControlAppearance Appearance
    {
        get => (ControlAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    #endregion

    #region DP: MouseOverBackground

    /// <summary>Identifies the <see cref="MouseOverBackground"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverBackgroundProperty =
        Button.MouseOverBackgroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the background brush used by either segment while the pointer is over that
    /// segment. A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    /// <remarks>
    /// The primary and drop-down segments evaluate pointer state independently, so hovering one
    /// segment does not apply the brush to the other segment.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBackground
    {
        get => (Brush?)GetValue(MouseOverBackgroundProperty);
        set => SetValue(MouseOverBackgroundProperty, value);
    }

    #endregion

    #region DP: MouseOverBorderBrush

    /// <summary>Identifies the <see cref="MouseOverBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverBorderBrushProperty =
        Button.MouseOverBorderBrushProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the border brush used by either segment while the pointer is over that segment.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBorderBrush
    {
        get => (Brush?)GetValue(MouseOverBorderBrushProperty);
        set => SetValue(MouseOverBorderBrushProperty, value);
    }

    #endregion

    #region DP: PressedForeground

    /// <summary>Identifies the <see cref="PressedForeground"/> dependency property.</summary>
    public static readonly DependencyProperty PressedForegroundProperty =
        Button.PressedForegroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the foreground brush used by either segment while that segment is pressed.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? PressedForeground
    {
        get => (Brush?)GetValue(PressedForegroundProperty);
        set => SetValue(PressedForegroundProperty, value);
    }

    #endregion

    #region DP: PressedBackground

    /// <summary>Identifies the <see cref="PressedBackground"/> dependency property.</summary>
    public static readonly DependencyProperty PressedBackgroundProperty =
        Button.PressedBackgroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the background brush used by either segment while that segment is pressed.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? PressedBackground
    {
        get => (Brush?)GetValue(PressedBackgroundProperty);
        set => SetValue(PressedBackgroundProperty, value);
    }

    #endregion

    #region DP: PressedBorderBrush

    /// <summary>Identifies the <see cref="PressedBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty PressedBorderBrushProperty =
        Button.PressedBorderBrushProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the border brush used by either segment while that segment is pressed.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? PressedBorderBrush
    {
        get => (Brush?)GetValue(PressedBorderBrushProperty);
        set => SetValue(PressedBorderBrushProperty, value);
    }

    #endregion

    #region DP: DisabledBackground

    /// <summary>Identifies the <see cref="DisabledBackground"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledBackgroundProperty =
        Button.DisabledBackgroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the background brush used by a disabled segment. A segment can be disabled
    /// independently when the primary <see cref="Command"/> cannot execute.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBackground
    {
        get => (Brush?)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    #endregion

    #region DP: DisabledBorderBrush

    /// <summary>Identifies the <see cref="DisabledBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledBorderBrushProperty =
        Button.DisabledBorderBrushProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the border brush used by a disabled segment. A <see langword="null"/> value
    /// falls back to that segment's normal border brush.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBorderBrush
    {
        get => (Brush?)GetValue(DisabledBorderBrushProperty);
        set => SetValue(DisabledBorderBrushProperty, value);
    }

    #endregion

    #region DP: DisabledForeground

    /// <summary>Identifies the <see cref="DisabledForeground"/> dependency property.</summary>
    public static readonly DependencyProperty DisabledForegroundProperty =
        Button.DisabledForegroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the foreground brush used by a disabled segment. A <see langword="null"/>
    /// value falls back to that segment's normal foreground brush.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledForeground
    {
        get => (Brush?)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    #endregion

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = Button.IconProperty.AddOwner(
        typeof(SplitButton),
        new FrameworkPropertyMetadata(null, null, IconElement.Coerce)
    );

    /// <inheritdoc />
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: CornerRadius

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty = Button.CornerRadiusProperty.AddOwner(
        typeof(SplitButton),
        new FrameworkPropertyMetadata(default(CornerRadius))
    );

    /// <summary>
    /// Gets or sets the corner radius of the complete split-button outline. The default template
    /// copies only the outer corners to each internal button.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: ClickMode

    /// <summary>Identifies the <see cref="ClickMode"/> dependency property.</summary>
    public static readonly DependencyProperty ClickModeProperty = DependencyProperty.Register(
        nameof(ClickMode),
        typeof(ClickMode),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(ClickMode.Release),
        value => value is ClickMode.Release or ClickMode.Press or ClickMode.Hover
    );

    /// <summary>
    /// Gets or sets when the primary segment raises <see cref="Click"/> and executes
    /// <see cref="Command"/>. The default is <see cref="System.Windows.Controls.ClickMode.Release"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public ClickMode ClickMode
    {
        get => (ClickMode)GetValue(ClickModeProperty);
        set => SetValue(ClickModeProperty, value);
    }

    #endregion

    #region DP: Command

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the command executed by the primary segment. Command availability disables
    /// only that segment; the drop-down remains available while the control itself is enabled.
    /// </summary>
    [Bindable(true)]
    [Category("Action")]
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    #endregion

    #region DP: CommandParameter

    /// <summary>Identifies the <see cref="CommandParameter"/> dependency property.</summary>
    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter),
        typeof(object),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets the parameter passed to <see cref="Command"/>.</summary>
    [Bindable(true)]
    [Category("Action")]
    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    #endregion

    #region DP: CommandTarget

    /// <summary>Identifies the <see cref="CommandTarget"/> dependency property.</summary>
    public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(
        nameof(CommandTarget),
        typeof(IInputElement),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets the element against which a routed <see cref="Command"/> is raised.</summary>
    [Bindable(true)]
    [Category("Action")]
    public IInputElement? CommandTarget
    {
        get => (IInputElement?)GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    #endregion

    #region DP: Flyout

    /// <summary>Identifies the <see cref="Flyout"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutProperty = DependencyProperty.Register(
        nameof(Flyout),
        typeof(ContextMenu),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(
            null,
            static (d, e) => ((SplitButton)d).OnFlyoutChanged(
                (ContextMenu?)e.OldValue,
                (ContextMenu?)e.NewValue
            )
        )
    );

    /// <summary>
    /// Gets or sets the context menu displayed by the drop-down segment. The control supplies a
    /// placement target, bottom placement, and minimum width only when those values were not set
    /// by the application.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public ContextMenu? Flyout
    {
        get => (ContextMenu?)GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    #endregion

    #region DP: IsDropDownOpen

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(SplitButton),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            static (d, e) => ((SplitButton)d).OnIsDropDownOpenChanged(
                (bool)e.OldValue,
                (bool)e.NewValue
            )
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Flyout"/> is open. The property synchronizes in both
    /// directions with <see cref="ContextMenu.IsOpen"/> and binds two-way by default.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    #endregion

    static SplitButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(typeof(SplitButton))
        );

        // SplitButton is one keyboard-focus unit even though its template contains two mouse
        // buttons. This makes ContextMenu's focus restoration return to the public control rather
        // than to a private template part.
        FocusableProperty.OverrideMetadata(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(true)
        );
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        DetachTemplatePartHandlers();
        base.OnApplyTemplate();

        _primaryButton  = GetTemplateChild(PrimaryButtonPartName) as Button;
        _dropDownButton = GetTemplateChild(DropDownButtonPartName) as Button;

        // Template parts are optional by design. This lets minimal or diagnostic templates keep
        // rendering without turning a visual customization error into an application crash.
        if (_primaryButton is not null)
        {
            _primaryButton.PreviewMouseLeftButtonDown += OnPrimaryButtonPreviewMouseLeftButtonDown;
            _primaryButton.Click                      += OnPrimaryButtonClick;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click                      += OnDropDownButtonClick;
            _dropDownButton.PreviewMouseLeftButtonDown += OnDropDownButtonPreviewMouseLeftButtonDown;
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new SplitButtonAutomationPeer(this);
    }

    /// <summary>Opens <see cref="Flyout"/> when one is assigned and the control is enabled.</summary>
    public void OpenDropDown()
    {
        if (!IsEnabled || _contextMenu is null || _contextMenu.IsOpen)
        {
            return;
        }

        ConfigureContextMenu(_contextMenu);
        SetCurrentValue(IsDropDownOpenProperty, true);
    }

    /// <summary>Closes <see cref="Flyout"/> if it is currently open.</summary>
    public void CloseDropDown()
    {
        SetCurrentValue(IsDropDownOpenProperty, false);
    }

    /// <summary>Opens a closed <see cref="Flyout"/> or closes an open one.</summary>
    public void ToggleDropDown()
    {
        if (IsDropDownOpen || _contextMenu?.IsOpen == true)
        {
            CloseDropDown();
        }
        else
        {
            OpenDropDown();
        }
    }

    /// <summary>Raises the control-owned <see cref="Click"/> event.</summary>
    protected virtual void OnClick()
    {
        SplitButtonAutomationPeer.RaiseInvokeAutomationEvent(this);
        RaiseEvent(new RoutedEventArgs(ClickEvent, this));
    }

    /// <summary>Raises the control-owned <see cref="DropDownOpened"/> event.</summary>
    protected virtual void OnDropDownOpened()
    {
        RaiseEvent(new RoutedEventArgs(DropDownOpenedEvent, this));
    }

    /// <summary>Raises the control-owned <see cref="DropDownClosed"/> event.</summary>
    protected virtual void OnDropDownClosed()
    {
        RaiseEvent(new RoutedEventArgs(DropDownClosedEvent, this));
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Handled)
        {
            return;
        }

        Key key = GetActualKey(e);
        if (key != Key.Space)
        {
            // ButtonBase cancels a pending Space activation when any other key is pressed. Without
            // this reset, Space followed by F4 or another shortcut would invoke the primary action
            // when the original Space key is eventually released.
            _isSpaceKeyDown = false;
        }

        if (IsDropDownShortcut(e))
        {
            // F4 and Alt+Down are toggle gestures, so key auto-repeat must not repeatedly open and
            // close the menu while the user is holding the shortcut.
            if (!e.IsRepeat)
            {
                ToggleDropDown();
            }

            e.Handled = true;
        }
        else if (key == Key.Escape && IsDropDownOpen)
        {
            CloseDropDown();
            e.Handled = true;
        }
        else if (
            key == Key.Space
            && ClickMode != ClickMode.Hover
            && ReferenceEquals(e.OriginalSource, this)
            && !IsAltSpaceGesture()
        )
        {
            // Repeated KeyDown events represent the same physical press. ClickMode.Press invokes
            // once at the transition, matching ButtonBase rather than executing a command at the
            // keyboard repeat rate.
            if (!_isSpaceKeyDown)
            {
                _isSpaceKeyDown = true;
                if (ClickMode == ClickMode.Press)
                {
                    InvokePrimaryAction();
                }
            }

            e.Handled = true;
        }
        else if (
            key == Key.Enter
            && ClickMode != ClickMode.Hover
            && ReferenceEquals(e.OriginalSource, this)
        )
        {
            InvokePrimaryAction();
            e.Handled = true;
        }
    }

    /// <inheritdoc />
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);

        if (
            e.Handled
            || GetActualKey(e) != Key.Space
            || !_isSpaceKeyDown
            || !ReferenceEquals(e.OriginalSource, this)
        )
        {
            return;
        }

        _isSpaceKeyDown = false;
        if (ClickMode == ClickMode.Release)
        {
            InvokePrimaryAction();
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        _isSpaceKeyDown = false;
        base.OnLostKeyboardFocus(e);
    }

    private static bool IsDropDownShortcut(KeyEventArgs e)
    {
        Key key = GetActualKey(e);
        return key == Key.F4 || (key == Key.Down && Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
    }

    private static Key GetActualKey(KeyEventArgs e)
    {
        return e.Key == Key.System ? e.SystemKey : e.Key;
    }

    private static bool IsAltSpaceGesture()
    {
        // Alt+Space belongs to the hosting Window's system menu. Ctrl+Space and Ctrl+Alt+Space
        // retain normal button semantics, matching WPF ButtonBase's modifier check.
        return (Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) == ModifierKeys.Alt;
    }

    private void OnFlyoutChanged(ContextMenu? oldValue, ContextMenu? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.RemoveHandler(
                Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OnPreviewMouseDownOutsideCapturedElement)
            );
            oldValue.PreviewKeyDown -= OnContextMenuPreviewKeyDown;
            oldValue.Opened         -= OnContextMenuOpened;
            oldValue.Closed         -= OnContextMenuClosed;

            if (oldValue.IsOpen)
            {
                oldValue.SetCurrentValue(ContextMenu.IsOpenProperty, false);
            }
        }

        _contextMenu = newValue;

        if (newValue is not null)
        {
            newValue.AddHandler(
                Mouse.PreviewMouseDownOutsideCapturedElementEvent,
                new MouseButtonEventHandler(OnPreviewMouseDownOutsideCapturedElement),
                true
            );
            newValue.PreviewKeyDown += OnContextMenuPreviewKeyDown;
            newValue.Opened         += OnContextMenuOpened;
            newValue.Closed         += OnContextMenuClosed;
        }

        SetCurrentValue(IsDropDownOpenProperty, newValue?.IsOpen == true);
    }

    private void OnIsDropDownOpenChanged(bool oldIsOpen, bool shouldOpen)
    {
        if (_isSynchronizingDropDownState)
        {
            SplitButtonAutomationPeer.RaiseExpandCollapseAutomationEvent(
                this,
                oldIsOpen,
                shouldOpen
            );
            return;
        }

        if (_contextMenu is null)
        {
            if (shouldOpen)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }

            return;
        }

        if (shouldOpen)
        {
            ConfigureContextMenu(_contextMenu);
        }

        if (_contextMenu.IsOpen != shouldOpen)
        {
            _contextMenu.SetCurrentValue(ContextMenu.IsOpenProperty, shouldOpen);
        }

        SplitButtonAutomationPeer.RaiseExpandCollapseAutomationEvent(
            this,
            oldIsOpen,
            shouldOpen
        );
    }

    private void ConfigureContextMenu(ContextMenu contextMenu)
    {
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, FrameworkElement.MinWidthProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(FrameworkElement.MinWidthProperty, ActualWidth);
        }

        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, ContextMenu.PlacementTargetProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(ContextMenu.PlacementTargetProperty, this);
        }

        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, ContextMenu.PlacementProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(ContextMenu.PlacementProperty, PlacementMode.Bottom);
        }
    }

    private void OnPrimaryButtonClick(object sender, RoutedEventArgs e)
    {
        // Consume the template child's ButtonBase event at its source. Only the SplitButton-owned
        // event is allowed to cross the template boundary, which gives consumers one stable source.
        e.Handled = true;
        OnClick();
    }

    private void OnPrimaryButtonPreviewMouseLeftButtonDown(
        object               sender,
        MouseButtonEventArgs e
    )
    {
        FocusFromTemplateButtonPress();
    }

    private void OnDropDownButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (_dismissedDropDownMouseTimestamp.HasValue)
        {
            _dismissedDropDownMouseTimestamp = null;
            return;
        }

        ToggleDropDown();
    }

    private void OnDropDownButtonPreviewMouseLeftButtonDown(
        object               sender,
        MouseButtonEventArgs e
    )
    {
        FocusFromTemplateButtonPress();

        // If WPF did not forward the captured menu's original press, a timestamp can survive until
        // a later gesture. A different timestamp proves this is a new press and must not be ignored.
        if (_dismissedDropDownMouseTimestamp != e.Timestamp)
        {
            _dismissedDropDownMouseTimestamp = null;
        }

        if (!IsDropDownOpen && _contextMenu?.IsOpen != true)
        {
            return;
        }

        _dismissedDropDownMouseTimestamp = e.Timestamp;
        CloseDropDown();
    }

    private void FocusFromTemplateButtonPress()
    {
        // ButtonBase normally focuses itself from OnMouseLeftButtonDown, before Click is raised.
        // Both private template buttons are intentionally non-focusable, so transfer focus to the
        // public SplitButton during the preview phase instead. Besides matching normal button
        // timing, this lets applications observe the correct focus inside Press-mode Click handlers.
        Focus();
    }

    private void OnPreviewMouseDownOutsideCapturedElement(
        object               sender,
        MouseButtonEventArgs e
    )
    {
        if (
            e.ChangedButton != MouseButton.Left
         || e.ButtonState != MouseButtonState.Pressed
         || !IsPointerOverDropDownButton(e)
        )
        {
            return;
        }

        _dismissedDropDownMouseTimestamp = e.Timestamp;
        Focus();
    }

    private void OnContextMenuPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!IsDropDownShortcut(e))
        {
            return;
        }

        e.Handled = true;
        CloseDropDown();
        Focus();
    }

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        UpdateDropDownStateFromContextMenu(true);
        OnDropDownOpened();
    }

    private void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        UpdateDropDownStateFromContextMenu(false);
        OnDropDownClosed();
    }

    private void UpdateDropDownStateFromContextMenu(bool isOpen)
    {
        // A two-way binding may execute application code while SetCurrentValue propagates. Always
        // release the recursion guard even when that code throws, otherwise a caught binding error
        // would permanently disconnect this control from subsequent ContextMenu state changes.
        _isSynchronizingDropDownState = true;
        try
        {
            SetCurrentValue(IsDropDownOpenProperty, isOpen);
        }
        finally
        {
            _isSynchronizingDropDownState = false;
        }
    }

    private bool IsPointerOverDropDownButton(MouseEventArgs e)
    {
        if (_dropDownButton is null || !_dropDownButton.IsVisible)
        {
            return false;
        }

        var position = e.GetPosition(_dropDownButton);
        return position.X >= 0D
            && position.X <= _dropDownButton.ActualWidth
            && position.Y >= 0D
            && position.Y <= _dropDownButton.ActualHeight;
    }

    /// <summary>
    /// Gets a value indicating whether the primary template part exists and can currently invoke
    /// its command.
    /// </summary>
    internal bool CanInvokePrimaryAction => IsEnabled && _primaryButton?.IsEnabled == true;

    /// <summary>Returns whether an automation child belongs to the private button implementation.</summary>
    internal bool IsTemplateButton(UIElement element)
    {
        return ReferenceEquals(element, _primaryButton) || ReferenceEquals(element, _dropDownButton);
    }

    /// <summary>
    /// Invokes the primary action for keyboard and UI Automation input while respecting the
    /// command availability maintained by the primary button part.
    /// </summary>
    internal void InvokePrimaryAction()
    {
        // A missing template part means the corresponding interaction is unavailable. The explicit
        // null check is important: a null-conditional comparison with false would otherwise let
        // keyboard and automation input invoke an action that has no visual surface.
        if (!CanInvokePrimaryAction)
        {
            return;
        }

        OnClick();

        if (Command is RoutedCommand routedCommand)
        {
            var target = CommandTarget ?? this;
            if (routedCommand.CanExecute(CommandParameter, target))
            {
                routedCommand.Execute(CommandParameter, target);
            }
        }
        else if (Command?.CanExecute(CommandParameter) == true)
        {
            Command.Execute(CommandParameter);
        }
    }

    private void DetachTemplatePartHandlers()
    {
        if (_primaryButton is not null)
        {
            _primaryButton.PreviewMouseLeftButtonDown -= OnPrimaryButtonPreviewMouseLeftButtonDown;
            _primaryButton.Click                      -= OnPrimaryButtonClick;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click                      -= OnDropDownButtonClick;
            _dropDownButton.PreviewMouseLeftButtonDown -= OnDropDownButtonPreviewMouseLeftButtonDown;
        }

        _primaryButton  = null;
        _dropDownButton = null;
    }
}
