// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Zen.Ui.AutomationPeers;

// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

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
/// every segment uses the standard WPF Zen UI button appearance system.
/// </para>
/// </remarks>
[TemplatePart(Name = PrimaryButtonPartName,  Type = typeof(Button))]
[TemplatePart(Name = DropDownButtonPartName, Type = typeof(Button))]
public class SplitButton : ContentControl, ICommandSource
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

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(SplitButton),
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
        typeof(SplitButton),
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

    #region DP: Appearance

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty = Button.AppearanceProperty.AddOwner(
        typeof(SplitButton),
        new FrameworkPropertyMetadata(ControlAppearance.Normal)
    );

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> of the split button.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public ControlAppearance Appearance
    {
        get => (ControlAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    #endregion

    #region DP: MouseOverForeground

    /// <summary>Identifies the <see cref="MouseOverForeground"/> dependency property.</summary>
    public static readonly DependencyProperty MouseOverForegroundProperty =
        Button.MouseOverForegroundProperty.AddOwner(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the foreground brush used by either enabled, unpressed segment while the
    /// pointer is over that segment. The default is <see langword="null"/>, which falls back to
    /// <see cref="Control.Foreground"/>.
    /// </summary>
    /// <remarks>
    /// The default template forwards this brush to both button parts, which resolve hover state
    /// independently for the primary content and drop-down arrow. This property does not inherit
    /// or invalidate layout and supports bindings, dynamic resources, and animations. It needs no
    /// wrapper callback: template bindings deliver changes to each button's state-brush callback.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverForeground
    {
        get => (Brush?)GetValue(MouseOverForegroundProperty);
        set => SetValue(MouseOverForegroundProperty, value);
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

        // ButtonBase opts into Enter activation through this attached property. SplitButton does
        // not derive from ButtonBase, so it must establish the same default explicitly while still
        // allowing an application to turn the behavior off on an individual control.
        KeyboardNavigation.AcceptsReturnProperty.OverrideMetadata(
            typeof(SplitButton),
            new FrameworkPropertyMetadata(true)
        );

        // AccessText lives inside the non-focusable primary template button. ButtonBase therefore
        // initially selects that private button as the access-key target. Rewrite only that target
        // at the public wrapper boundary so a single match invokes the primary action and multiple
        // matches can cycle keyboard focus between SplitButton controls.
        EventManager.RegisterClassHandler(
            typeof(SplitButton),
            AccessKeyManager.AccessKeyPressedEvent,
            new AccessKeyPressedEventHandler(OnAccessKeyPressed)
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
            _primaryButton.IsEnabledChanged           += OnPrimaryButtonIsEnabledChanged;
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
            CancelSpaceKeyPress();
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
                SetPrimaryButtonPressed(true);
                if (ClickMode == ClickMode.Press)
                {
                    try
                    {
                        InvokePrimaryAction();
                    }
                    catch
                    {
                        // ButtonBase clears its input state if a Press-mode Click or command throws.
                        // Otherwise an exception caught by application code would leave the primary
                        // segment looking permanently pressed until another keyboard transition.
                        CancelSpaceKeyPress();
                        throw;
                    }
                }
            }

            e.Handled = true;
        }
        else if (
            key == Key.Enter
            && ClickMode != ClickMode.Hover
            && KeyboardNavigation.GetAcceptsReturn(this)
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

        bool shouldInvoke = ClickMode == ClickMode.Release && _primaryButton?.IsPressed == true;
        CancelSpaceKeyPress();
        if (shouldInvoke)
        {
            InvokePrimaryAction();
        }

        e.Handled = true;
    }

    /// <inheritdoc />
    protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
    {
        CancelSpaceKeyPress();
        base.OnLostKeyboardFocus(e);
    }

    /// <inheritdoc />
    protected override void OnAccessKey(AccessKeyEventArgs e)
    {
        if (e.IsMultiple)
        {
            // WPF buttons focus rather than invoke when an access key has multiple matches. Focus
            // the public wrapper so the next press can advance to the following SplitButton.
            base.OnAccessKey(e);
            return;
        }

        InvokePrimaryAction();
    }

    private static void OnAccessKeyPressed(object sender, AccessKeyPressedEventArgs e)
    {
        var splitButton = (SplitButton)sender;
        if (
            e.Key is not "\r" and not "\x1B"
         && ReferenceEquals(e.Target, splitButton._primaryButton)
        )
        {
            // Enter/Escape registrations belong to Button.IsDefault/IsCancel and must stay on the
            // actual button so WPF can apply their specialized behavior. Mnemonics rendered by
            // the primary content instead target the focus-owning wrapper.
            e.Target = splitButton;
        }
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

    private void OnPrimaryButtonIsEnabledChanged(
        object sender,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (!(bool)e.NewValue)
        {
            // A command can become unavailable while Space is held (including from a Press-mode
            // Click handler). Clear the synthetic state just as ButtonBase loses its mouse capture
            // when a native button becomes disabled.
            CancelSpaceKeyPress();
        }
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

        Button primaryButton = _primaryButton!;

        // Raise the private ButtonBase event so every activation route crosses the same adapter as
        // a mouse click. Raising the event directly intentionally avoids publishing a second UIA
        // Invoke notification for a template child that SplitButtonAutomationPeer hides.
        primaryButton.RaiseEvent(
            new RoutedEventArgs(ButtonBase.ClickEvent, primaryButton)
        );

        ICommand? command   = primaryButton.Command;
        object?  parameter = primaryButton.CommandParameter;
        if (command is RoutedCommand routedCommand)
        {
            // ButtonBase defaults a routed command's target to the command source itself. Using
            // the primary template button here keeps keyboard and automation input identical to
            // mouse and access-key input when CommandTarget is omitted.
            IInputElement target = primaryButton.CommandTarget ?? primaryButton;
            if (routedCommand.CanExecute(parameter, target))
            {
                routedCommand.Execute(parameter, target);
            }
        }
        else if (command?.CanExecute(parameter) == true)
        {
            command.Execute(parameter);
        }
    }

    private void SetPrimaryButtonPressed(bool isPressed)
    {
        if (_primaryButton is null || (isPressed && !_primaryButton.IsEnabled))
        {
            return;
        }

        _primaryButton.SetPressedFromOwningControl(isPressed);
    }

    private void CancelSpaceKeyPress()
    {
        _isSpaceKeyDown = false;
        SetPrimaryButtonPressed(false);
    }

    private void DetachTemplatePartHandlers()
    {
        if (_primaryButton is not null)
        {
            _primaryButton.SetPressedFromOwningControl(false);
            _primaryButton.PreviewMouseLeftButtonDown -= OnPrimaryButtonPreviewMouseLeftButtonDown;
            _primaryButton.Click                      -= OnPrimaryButtonClick;
            _primaryButton.IsEnabledChanged           -= OnPrimaryButtonIsEnabledChanged;
        }

        if (_dropDownButton is not null)
        {
            _dropDownButton.Click                      -= OnDropDownButtonClick;
            _dropDownButton.PreviewMouseLeftButtonDown -= OnDropDownButtonPreviewMouseLeftButtonDown;
        }

        _primaryButton  = null;
        _dropDownButton = null;
        _isSpaceKeyDown = false;
    }
}
