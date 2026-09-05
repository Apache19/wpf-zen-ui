// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.AutomationPeers;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A command button that opens a native ContextMenu/Popup or presents arbitrary content in a Popup.
/// </summary>
/// <remarks>
/// Content and its template properties describe the button label; Flyout and its template properties
/// independently describe the drop-down. Native flyouts retain their own presentation behavior;
/// ContextMenu keeps WPF menu input semantics, while Popup receives the same initial focus traversal
/// as generated content.
/// Other values use a ContentControl and WPF's content presentation pipeline, including implicit data
/// templates, selectors, string formatting, and direct UIElement presentation. UI content must not already
/// belong to another control. Null means no flyout, even when a data template is supplied.
/// Generated and explicit Popups are logical children, so resources and inherited properties remain available across
/// their separate windows, including while closed. No named part is required in the button template.
/// FlyoutPresenterTemplate targets ContentControl and must present Content (normally using a
/// ContentPresenter with ContentSource="Content"). Omitting that presenter intentionally hides content.
/// Replacing ordinary content preserves the open panel; switching native flyouts closes and detaches
/// the previous host and adopts the new native flyout's state. Generated panels support Escape, F4,
/// Alt+Down/Up and cyclic Tab navigation; they do not acquire ContextMenu item/command semantics.
/// </remarks>
[StyleTypedProperty(Property = nameof(FlyoutPopupStyle), StyleTargetType = typeof(Popup))]
public class DropDownButton : Button
{
    /// <summary>Gets the resource key for the style of automatically generated flyout Popups.</summary>
    /// <remarks>
    /// Define a Style targeting Popup under this key in button, ancestor, or application resources
    /// to customize a scope. WPF dynamically resolves the nearest resource through the Popup's logical
    /// parent and falls back to the library theme. The same key supplies the Popup's default theme style,
    /// so an override need only specify the setters it changes. This is independent of the button template
    /// and style. A non-null FlyoutPopupStyle takes precedence; native flyouts are unaffected.
    /// </remarks>
    public static ComponentResourceKey FlyoutPopupStyleKey { get; } =
        new(typeof(DropDownButton), nameof(FlyoutPopupStyleKey));

    static DropDownButton()
    {
        // DropDownButton owns the trailing-chevron contract, so it needs its own default template
        // instead of forcing every Button to carry a second public icon slot.
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(DropDownButton),
            new FrameworkPropertyMetadata(typeof(DropDownButton))
        );
    }

    private bool _isSynchronizingDropDownState;
    private bool _isChangingFlyout;
    private Popup? _generatedPopup;
    private Popup? _nativePopup;
    private ContentControl? _flyoutPresenter;

    /// <summary>Initializes a new instance of the <see cref="DropDownButton"/> class.</summary>
    public DropDownButton()
    {
        // A Flyout Popup need not belong to the visual tree, so WPF's Popup.Unloaded handling
        // alone cannot close it when navigation removes the owning button.
        Unloaded += (_, _) => SetCurrentValue(IsDropDownOpenProperty, false);
        Loaded += (_, _) => OnIsDropDownOpenChanged(IsDropDownOpen);
        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }
        };
    }

    #region DP: Flyout

    /// <summary>Identifies the <see cref="Flyout"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutProperty = DependencyProperty.Register(
        nameof(Flyout),
        typeof(object),
        typeof(DropDownButton),
        new PropertyMetadata(null, OnFlyoutChanged, CoerceFlyout)
    );

    /// <summary>Gets or sets the native flyout, data object, or UI content. The default is null.</summary>
    /// <remarks>
    /// Supports binding, does not inherit, and does not affect button layout. Changes update or replace
    /// the flyout host. Native ContextMenu and Popup values bypass all Flyout presentation properties.
    /// When opening native flyouts, unset placement defaults to this button and Bottom; unset Popup
    /// StaysOpen defaults to false. Explicit values, styles, and bindings are retained.
    /// Explicit Popups become logical children and must not belong to another owner. ContextMenu
    /// retains its internal WPF Popup parent and inherits through PlacementTarget when open.
    /// Coercion validates UI ownership before committing the value, so rejected content leaves
    /// the selected Flyout and its host intact. Content elements follow the same ownership rule.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public object? Flyout
    {
        get => GetValue(FlyoutProperty);
        set => SetValue(FlyoutProperty, value);
    }

    #endregion

    #region DP: FlyoutTemplate

    /// <summary>Identifies the <see cref="FlyoutTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutTemplateProperty = DependencyProperty.Register(
        nameof(FlyoutTemplate),
        typeof(DataTemplate),
        typeof(DropDownButton),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the explicit data template for generated flyout content.</summary>
    /// <remarks>
    /// The default is null. WPF gives an explicit template precedence over the selector and implicit templates.
    /// Supports binding; does not inherit or affect button layout. Changes propagate to the generated
    /// host through bindings and may remeasure or redraw the open flyout. Native flyouts are unaffected.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public DataTemplate? FlyoutTemplate
    {
        get => (DataTemplate?)GetValue(FlyoutTemplateProperty);
        set => SetValue(FlyoutTemplateProperty, value);
    }

    #endregion

    #region DP: FlyoutTemplateSelector

    /// <summary>Identifies the <see cref="FlyoutTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutTemplateSelectorProperty = DependencyProperty.Register(
        nameof(FlyoutTemplateSelector),
        typeof(DataTemplateSelector),
        typeof(DropDownButton),
        new PropertyMetadata(null)
    );

    /// <summary>Gets or sets the selector for generated flyout content.</summary>
    /// <remarks>
    /// The default is null. It is consulted when FlyoutTemplate is null; returning null permits implicit template lookup.
    /// Supports binding; does not inherit or affect button layout. Changes propagate to the generated
    /// host through bindings and may remeasure or redraw the open flyout. Native flyouts are unaffected.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public DataTemplateSelector? FlyoutTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(FlyoutTemplateSelectorProperty);
        set => SetValue(FlyoutTemplateSelectorProperty, value);
    }

    #endregion

    #region DP: FlyoutStringFormat

    /// <summary>Identifies the <see cref="FlyoutStringFormat"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutStringFormatProperty = DependencyProperty.Register(
        nameof(FlyoutStringFormat),
        typeof(string),
        typeof(DropDownButton),
        new PropertyMetadata(null, OnFlyoutStringFormatChanged)
    );

    /// <summary>Gets or sets the composite format used when WPF renders flyout content as text.</summary>
    /// <remarks>
    /// The default is null. Templates and UI content determine their own rendering and do not use this format.
    /// Changing the format rebuilds the wrapper's visual tree for data content so WPF regenerates formatted text.
    /// Supports binding; does not inherit or affect button layout. Changes propagate to the generated
    /// host through bindings and may remeasure or redraw the open flyout. Native flyouts are unaffected.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public string? FlyoutStringFormat
    {
        get => (string?)GetValue(FlyoutStringFormatProperty);
        set => SetValue(FlyoutStringFormatProperty, value);
    }

    #endregion

    #region DP: FlyoutPresenterTemplate

    /// <summary>Identifies the <see cref="FlyoutPresenterTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutPresenterTemplateProperty = DependencyProperty.Register(
        nameof(FlyoutPresenterTemplate),
        typeof(ControlTemplate),
        typeof(DropDownButton),
        new PropertyMetadata(null),
        value => value is null || ((ControlTemplate)value).TargetType.IsAssignableFrom(typeof(ContentControl))
    );

    /// <summary>Gets or sets the visual wrapper template of the generated ContentControl.</summary>
    /// <remarks>
    /// The default metadata value is null; the theme supplies a padded, bordered surface. Target ContentControl
    /// and use a ContentPresenter with ContentSource="Content" to retain WPF template selection.
    /// A null template supplies no visual surface; custom button styles should provide this template.
    /// Validation rejects a template whose target type cannot be applied to ContentControl.
    /// Supports binding; does not inherit or affect button layout. Changes propagate to the generated
    /// host through bindings and may remeasure or redraw the open flyout. Native flyouts are unaffected.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public ControlTemplate? FlyoutPresenterTemplate
    {
        get => (ControlTemplate?)GetValue(FlyoutPresenterTemplateProperty);
        set => SetValue(FlyoutPresenterTemplateProperty, value);
    }

    #endregion

    #region DP: FlyoutPopupStyle

    /// <summary>Identifies the <see cref="FlyoutPopupStyle"/> dependency property.</summary>
    public static readonly DependencyProperty FlyoutPopupStyleProperty = DependencyProperty.Register(
        nameof(FlyoutPopupStyle),
        typeof(Style),
        typeof(DropDownButton),
        new PropertyMetadata(null, OnFlyoutPopupStyleChanged),
        value => value is null || ((Style)value).TargetType.IsAssignableFrom(typeof(Popup))
    );

    /// <summary>Gets or sets the style applied only to the generated Popup.</summary>
    /// <remarks>
    /// The default is null, which dynamically resolves FlyoutPopupStyleKey through normal WPF resource lookup.
    /// A non-null value overrides that resource reference; returning to null restores it. The independent
    /// theme style supplies default setters in either case, unless the applied style sets OverridesDefaultStyle.
    /// Target Popup to configure placement, offsets, sizing, animation, or StaysOpen.
    /// Child, PlacementTarget and IsOpen are owned by this control and must not be configured by the style.
    /// Validation rejects a style whose target type cannot be applied to Popup.
    /// Supports binding; does not inherit or affect button layout. Changes propagate to the generated
    /// host through a property-change callback and may remeasure or redraw the open flyout. Native flyouts are unaffected.
    /// </remarks>
    [Bindable(true)]
    [Category("Content")]
    public Style? FlyoutPopupStyle
    {
        get => (Style?)GetValue(FlyoutPopupStyleProperty);
        set => SetValue(FlyoutPopupStyleProperty, value);
    }

    #endregion

    #region DP: IsDropDownOpen

    /// <summary>Identifies the <see cref="IsDropDownOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen),
        typeof(bool),
        typeof(DropDownButton),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsDropDownOpenChanged
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the drop-down for a button is currently open.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if the drop-down is open; otherwise, <see langword="false" />. The default is <see langword="false" />.</returns>
    /// <remarks>
    /// Binds two-way by default and synchronizes popup opening and dismissal without replacing
    /// application bindings. Opening without a flyout or while disabled resets this value to false. A generated panel
    /// requested before Loaded opens after loading, when its placement target has a window. The property
    /// does not inherit or invalidate button layout; its callback operates the popup.
    /// </remarks>
    [Bindable(true)]
    [Browsable(false)]
    [Category("Appearance")]
    public bool IsDropDownOpen
    {
        get => (bool)GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, value);
    }

    #endregion

    #region DP: IsDropDownArrowVisible

    /// <summary>Identifies the <see cref="IsDropDownArrowVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsDropDownArrowVisibleProperty = DependencyProperty.Register(
        nameof(IsDropDownArrowVisible),
        typeof(bool),
        typeof(DropDownButton),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the control displays the drop-down arrow.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public bool IsDropDownArrowVisible
    {
        get => (bool)GetValue(IsDropDownArrowVisibleProperty);
        set => SetValue(IsDropDownArrowVisibleProperty, value);
    }

    #endregion

    private static void OnFlyoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (DropDownButton)d;
        if (button._isChangingFlyout)
        {
            return;
        }

        // Inheritance, bindings and native Closed handlers can synchronously change Flyout.
        // Complete each ownership transfer before processing the latest requested value;
        // mutating a ContentControl's logical children during its own callback is unsafe.
        button._isChangingFlyout = true;
        try
        {
            var attached = e.OldValue;
            object? requested;
            do
            {
                requested = button.Flyout;
                button.OnFlyoutChanged(attached, requested);
                attached = requested;
            }
            while (!ReferenceEquals(button.Flyout, requested));
        }
        finally
        {
            button._isChangingFlyout = false;
        }

        DropDownButtonAutomationPeer.RaiseStateChanged(button);
    }

    private static object? CoerceFlyout(DependencyObject d, object? value)
    {
        var button = (DropDownButton)d;
        if (value is ContextMenu || value is not DependencyObject content)
        {
            return value;
        }

        // ContentControl also logically parents FrameworkContentElement values. Validate before
        // WPF commits Flyout: an exception in PropertyChangedCallback does not roll back the DP.
        if (ReferenceEquals(content, button)
            || IsLogicalAncestor(content, button)
            || (LogicalTreeHelper.GetParent(content) is { } parent
                && !ReferenceEquals(parent, content is Popup ? button : button._flyoutPresenter))
            || (content is UIElement element && (element.IsAncestorOf(button)
                || (VisualTreeHelper.GetParent(element) is not null && !ReferenceEquals(value, button.Flyout)))))
        {
            throw new InvalidOperationException("Flyout UI content must be unparented and cannot be its owner.");
        }

        return value;
    }

    private static bool IsLogicalAncestor(DependencyObject ancestor, DependencyObject child)
    {
        // An untemplated ContentControl can already own the button logically while having
        // no visual descendants. Visual.IsAncestorOf alone cannot prevent that ownership cycle.
        for (var parent = LogicalTreeHelper.GetParent(child); parent is not null;
             parent = LogicalTreeHelper.GetParent(parent))
        {
            if (ReferenceEquals(parent, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is DropDownButton dropDownButton)
        {
            dropDownButton.OnIsDropDownOpenChanged((bool)e.NewValue);
            DropDownButtonAutomationPeer.RaiseStateChanged(dropDownButton);
        }
    }

    /// <summary>Updates generated content or transfers native flyout subscriptions and state.</summary>
    /// <param name="oldValue">The previous flyout value, or null.</param>
    /// <param name="newValue">The new flyout value, or null.</param>
    protected virtual void OnFlyoutChanged(object? oldValue, object? newValue)
    {
        if (_generatedPopup is not null && IsGeneratedContent(newValue))
        {
            // Keep the HWND and current focus alive for view-model updates. A content binding here
            // would also push native Popup values into the presenter during an adapter switch.
            _flyoutPresenter!.SetCurrentValue(ContentProperty, newValue);
            return;
        }

        ReleaseGeneratedPopup();

        // Detach before closing: Closed can arrive asynchronously and must not reset the
        // replacement's state or keep this button subscribed to a detached flyout.
        switch (oldValue)
        {
            case ContextMenu menu:
                menu.RemoveHandler(ContextMenu.OpenedEvent, new RoutedEventHandler(OnContextMenuOpened));
                menu.RemoveHandler(ContextMenu.ClosedEvent, new RoutedEventHandler(OnContextMenuClosed));
                menu.SetCurrentValue(ContextMenu.IsOpenProperty, false);
                ReleaseDefaultPlacementTarget(menu, ContextMenu.PlacementTargetProperty);
                break;
            case Popup popup:
                popup.Opened -= OnPopupOpened;
                popup.Closed -= OnPopupClosed;
                DetachPopupInputBoundary(popup);
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                ReleaseDefaultPlacementTarget(popup, Popup.PlacementTargetProperty);
                _nativePopup = null;
                RemoveLogicalChild(popup);
                break;
        }

        switch (newValue)
        {
            case ContextMenu menu:
                // Presentation state must stay synchronized even when application handlers
                // mark the routed notification handled before this adapter receives it.
                menu.AddHandler(ContextMenu.OpenedEvent, new RoutedEventHandler(OnContextMenuOpened), true);
                menu.AddHandler(ContextMenu.ClosedEvent, new RoutedEventHandler(OnContextMenuClosed), true);
                break;
            case Popup popup:
                popup.Opened += OnPopupOpened;
                popup.Closed += OnPopupClosed;
                AttachPopupInputBoundary(popup);
                // Publish the actual owned child before inheritance invokes application callbacks.
                // Flyout may change reentrantly; LogicalChildren must describe completed ownership.
                _nativePopup = popup;
                AddLogicalChild(popup);
                break;
        }

        if (IsGeneratedContent(newValue))
        {
            CreateGeneratedPopup(newValue!);
        }

        if (ReferenceEquals(Flyout, newValue))
        {
            UpdateDropDownStateFromFlyout(newValue switch
            {
                ContextMenu menu => menu.IsOpen,
                Popup popup => popup.IsOpen,
                _ => false,
            });
        }
    }

    /// <summary>Synchronizes popup dismissal back to the button and its binding source.</summary>
    /// <param name="sender">The menu that raised the event.</param>
    /// <param name="e">The routed close event.</param>
    protected virtual void OnContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, Flyout) && sender is ContextMenu menu)
        {
            UpdateDropDownStateFromFlyout(menu.IsOpen);
        }
    }

    /// <summary>Synchronizes popup opening back to the button and its binding source.</summary>
    /// <param name="sender">The menu that raised the event.</param>
    /// <param name="e">The routed open event.</param>
    protected virtual void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, Flyout) && sender is ContextMenu menu)
        {
            // Earlier application handlers may have already closed or reopened this menu.
            // The effective property is authoritative, not the name of the event being delivered.
            UpdateDropDownStateFromFlyout(menu.IsOpen);
        }
    }

    // Popup exposes CLR events rather than ContextMenu's routed events. Both feed the same
    // guarded state update so dismissal preserves IsDropDownOpen bindings without reopening.
    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if ((ReferenceEquals(sender, Flyout) || ReferenceEquals(sender, _generatedPopup))
            && sender is Popup popup)
        {
            UpdateDropDownStateFromFlyout(popup.IsOpen);

            if (popup.IsOpen && popup.Child is { } child)
            {
                // Popup.OnOpened runs after WPF has created, laid out and shown PopupRoot. Moving
                // focus synchronously here follows DatePicker.PopUp_Opened and works for both the
                // generated host and a caller-supplied Popup. If the content has no focusable
                // descendant, MoveFocus simply returns false and WPF keeps the existing focus.
                child.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Flyout) || ReferenceEquals(sender, _generatedPopup))
        {
            // A delayed Closed from a previous HWND must not undo a newer open request.
            if (sender is Popup { IsOpen: false })
            {
                UpdateDropDownStateFromFlyout(false);
            }
        }
    }

    /// <summary>This method is invoked when the <see cref="IsDropDownOpenProperty"/> changes.</summary>
    /// <param name="currentValue">The new value of <see cref="IsDropDownOpenProperty"/>.</param>
    protected virtual void OnIsDropDownOpenChanged(bool currentValue)
    {
        if (_isSynchronizingDropDownState)
        {
            return;
        }

        if (Flyout is null || (currentValue && !IsEnabled))
        {
            if (currentValue)
            {
                SetCurrentValue(IsDropDownOpenProperty, false);
            }

            return;
        }

        if (_generatedPopup is not null)
        {
            // Unlike caller-owned popups, this host always uses the button as its target.
            // Do not create an HWND at screen origin for an initial XAML/binding open request.
            if (!currentValue || IsLoaded)
            {
                var restoreFocus = !currentValue && _flyoutPresenter?.IsKeyboardFocusWithin == true;
                ConfigurePopup(_generatedPopup);
                _generatedPopup.SetCurrentValue(Popup.IsOpenProperty, currentValue);
                if (restoreFocus && IsLoaded && IsVisible && IsEnabled && Window.GetWindow(this)?.IsActive == true)
                {
                    Focus();
                }
            }

            return;
        }

        switch (Flyout)
        {
            case ContextMenu menu:
                if (currentValue)
                {
                    ConfigureContextMenu(menu);
                }

                menu.SetCurrentValue(ContextMenu.IsOpenProperty, currentValue);
                break;

            case Popup popup:
                if (currentValue)
                {
                    ConfigurePopup(popup);
                }

                popup.SetCurrentValue(Popup.IsOpenProperty, currentValue);
                break;
        }
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        var wasOpen = IsDropDownOpen;

        base.OnClick();

        // PopupDismissal preserves generated/native Popup capture across owner presses, so a
        // second activation closes it. Native menus retain their own outside-click dismissal.
        SetCurrentValue(IsDropDownOpenProperty, !wasOpen);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new DropDownButtonAutomationPeer(this);
    }

    /// <summary>Handles drop-down shortcuts without executing the button command.</summary>
    /// <param name="e">The key event; ordinary button activation remains owned by Button.</param>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!HandleDropDownKey(e))
        {
            base.OnKeyDown(e);
        }
    }

    private bool HandleDropDownKey(KeyEventArgs e)
    {
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((key == Key.Escape && IsDropDownOpen)
            || key == Key.F4
            || ((Keyboard.Modifiers & ModifierKeys.Alt) != 0 && (key == Key.Down || key == Key.Up)))
        {
            if (Flyout is null || !IsEnabled)
            {
                return false;
            }

            SetCurrentValue(IsDropDownOpenProperty, key != Key.Escape && !IsDropDownOpen);
            e.Handled = true;
            return true;
        }

        return false;
    }

    private void OnGeneratedPopupKeyDown(object sender, KeyEventArgs e)
    {
        HandleDropDownKey(e);
    }

    private static void OnPopupClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
    }

    private static void OnPopupMouseButton(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
    }

    private static void OnPopupMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Every owned Popup is deliberately a logical child so its content inherits the owner's
        // DataContext, FlowDirection and resources even while closed. Popup.IgnoreModelParentBuildRoute
        // therefore permits unhandled input to continue from PopupRoot through the button's logical
        // tree. This boundary consumes only wheel input that no descendant handled: an inner
        // ScrollViewer still scrolls normally, while wheel input captured outside PopupRoot cannot
        // reach a ScrollViewer behind the flyout.
        e.Handled = true;
    }

    /// <summary>Stops unconsumed scrolling keys before they reach the owner's ScrollViewer.</summary>
    private static void OnPopupScrollKeyDown(object sender, KeyEventArgs e)
    {
        // Popup's logical parent extends the bubbling route into the background page. These are
        // the keys handled by WPF ScrollViewer.OnKeyDown/ScrollInDirection (including Ctrl+Home/End).
        // Descendants have already had their opportunity to edit text, navigate or scroll. Generated
        // flyout shortcuts are registered first, so Alt+Up/Down retain their dismissal behavior.
        // Do not consume every key: KeyboardNavigation processes unhandled Tab in PostProcessInput,
        // and application shortcuts may intentionally use the owner route. Preview handlers would
        // suppress input before the flyout content sees it; KeyUp/TextInput do not cause this scroll.
        if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down
            or Key.PageUp or Key.PageDown or Key.Home or Key.End)
        {
            e.Handled = true;
        }
    }

    /// <summary>Isolates unhandled flyout input from the logical ButtonBase owner, after child handlers run.</summary>
    private static void AttachPopupInputBoundary(Popup popup)
    {
        popup.AddHandler(ClickEvent, new RoutedEventHandler(OnPopupClick));
        popup.MouseDown += OnPopupMouseButton;
        popup.MouseUp += OnPopupMouseButton;
        popup.MouseWheel += OnPopupMouseWheel;
        popup.KeyDown += OnPopupScrollKeyDown;
    }

    /// <summary>Releases the input boundary when the popup leaves this control's logical tree.</summary>
    private static void DetachPopupInputBoundary(Popup popup)
    {
        popup.RemoveHandler(ClickEvent, new RoutedEventHandler(OnPopupClick));
        popup.MouseDown -= OnPopupMouseButton;
        popup.MouseUp -= OnPopupMouseButton;
        popup.MouseWheel -= OnPopupMouseWheel;
        popup.KeyDown -= OnPopupScrollKeyDown;
    }

    /// <summary>
    /// Gets the logical children, including the owned Popup in logical traversal so resource and inherited-property changes
    /// reach closed content as well as open content. Button.Content retains its ordinary ownership.
    /// </summary>
    protected override IEnumerator LogicalChildren
    {
        get
        {
            var children     = new List<object>(2);
            var baseChildren = base.LogicalChildren;

            while (baseChildren?.MoveNext() == true)
            {
                children.Add(baseChildren.Current);
            }

            if (_generatedPopup is not null)
            {
                children.Add(_generatedPopup);
            }

            if (_nativePopup is not null)
            {
                children.Add(_nativePopup);
            }

            return new LogicalChildrenEnumerator(children);
        }
    }

    private static bool IsGeneratedContent(object? value)
    {
        return value is not null and not System.Windows.Controls.ContextMenu and not Popup;
    }

    /// <summary>Builds a content host independent of the button's replaceable visual template.</summary>
    private void CreateGeneratedPopup(object content)
    {
        _flyoutPresenter = new ContentControl
        {
            Focusable = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };

        _generatedPopup = new Popup { Child = _flyoutPresenter, PlacementTarget = this };

        // Popup owns its Child logically. Connecting the other end to the button supplies resource
        // lookup and inherited DataContext/FlowDirection without copying or overwriting local values.
        AddLogicalChild(_generatedPopup);

        BindPresenter(TemplateProperty, nameof(FlyoutPresenterTemplate));
        BindPresenter(ContentTemplateProperty, nameof(FlyoutTemplate));
        BindPresenter(ContentTemplateSelectorProperty, nameof(FlyoutTemplateSelector));
        BindPresenter(ContentStringFormatProperty, nameof(FlyoutStringFormat));

        // DefaultStyleKey supplies theme setters independently of the explicit Style. A component key
        // identifies our assembly even though this is a native Popup. The dynamic Style reference adds
        // scoped overrides; DefaultStyleKey alone would only search system/theme dictionaries.
        // This owned host requires a local key, not a current value that style reevaluation can discard.

#pragma warning disable WPF0041 // The generated Popup's theme identity is owned by this control.
        _generatedPopup.SetValue(DefaultStyleKeyProperty, FlyoutPopupStyleKey);
#pragma warning restore WPF0041
        UpdateFlyoutPopupStyle();

        KeyboardNavigation.SetTabNavigation(_flyoutPresenter, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(_flyoutPresenter, KeyboardNavigationMode.Cycle);

        _generatedPopup.KeyDown += OnGeneratedPopupKeyDown;

        // Logical parenting enables inheritance, but also extends input routes through ButtonBase.
        // Consume unhandled content clicks and mouse presses at the popup boundary: a blank panel
        // surface must not press its owner, and an inner button must not raise the owner's Click.
        // Child handlers and commands have already run; routed commands keep their normal route.
        AttachPopupInputBoundary(_generatedPopup);

        _generatedPopup.Opened += OnPopupOpened;
        _generatedPopup.Closed += OnPopupClosed;

        // Content inheritance can run application callbacks; all host behavior must be ready first.
        _flyoutPresenter.SetCurrentValue(ContentProperty, content);
    }

    private void BindPresenter(DependencyProperty property, string path)
    {
        _flyoutPresenter!.SetBinding(property, new Binding(path) { Source = this });
    }

    private static void OnFlyoutPopupStyleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((DropDownButton)d).UpdateFlyoutPopupStyle();
    }

    /// <summary>Applies an explicit override or restores WPF's live, scoped style resource lookup.</summary>
    private void UpdateFlyoutPopupStyle()
    {
        if (_generatedPopup is not { } popup)
        {
            return;
        }

        // Do not resolve with FindResource or copy dictionaries: the expression must remain live for
        // resource replacement and logical reparenting, including while the Popup window is closed.
        if (FlyoutPopupStyle is { } style)
        {
            popup.Style = style;
        }
        else
        {
            popup.SetResourceReference(StyleProperty, FlyoutPopupStyleKey);
        }
    }

    private static void OnFlyoutStringFormatChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var button = (DropDownButton)d;
        if (button._flyoutPresenter is not { } presenter
            || button.Flyout is UIElement || button.FlyoutTemplate is not null)
        {
            return;
        }

        // WPF ContentPresenter.OnContentStringFormatChanged clears formatting caches but does not
        // invalidate its selected template; its default object expansion can retain stale text.
        // Reapply the wrapper through its existing binding, after updating the format binding.
        // This uses public APIs and leaves the Popup, content object and logical ownership intact.
        presenter.GetBindingExpression(ContentStringFormatProperty)?.UpdateTarget();
        presenter.SetCurrentValue(TemplateProperty, null);
        presenter.GetBindingExpression(TemplateProperty)?.UpdateTarget();
    }

    /// <summary>Releases UI ownership and all bindings before switching to a different adapter.</summary>
    private void ReleaseGeneratedPopup()
    {
        if (_generatedPopup is not { } popup)
        {
            return;
        }

        var presenter = _flyoutPresenter!;

        _generatedPopup  = null;
        _flyoutPresenter = null;

        popup.Opened    -= OnPopupOpened;
        popup.Closed    -= OnPopupClosed;
        popup.KeyDown   -= OnGeneratedPopupKeyDown;

        DetachPopupInputBoundary(popup);

        popup.SetCurrentValue(Popup.IsOpenProperty, false);
        presenter.SetCurrentValue(ContentProperty, null);

        BindingOperations.ClearAllBindings(presenter);
        popup.Child = null;
        BindingOperations.ClearAllBindings(popup);

        popup.PlacementTarget = null;

        RemoveLogicalChild(popup);
    }

    /// <summary>Adopts flyout events without issuing another popup operation from the DP callback.</summary>
    private void UpdateDropDownStateFromFlyout(bool isOpen)
    {
        var wasSynchronizing = _isSynchronizingDropDownState;
        _isSynchronizingDropDownState = true;
        try
        {
            SetCurrentValue(IsDropDownOpenProperty, isOpen && IsEnabled);
        }
        finally
        {
            // Binding source setters can run application code and throw during propagation.
            _isSynchronizingDropDownState = wasSynchronizing;
        }

        // A two-way source setter can synchronously reject the reported state or replace Flyout.
        // Reconcile its final request after releasing the guard instead of dropping that update.
        if (!wasSynchronizing && IsDropDownOpen != isOpen)
        {
            OnIsDropDownOpenChanged(IsDropDownOpen);
        }
    }

    /// <summary>Removes an owner reference supplied as a default, retaining application configuration.</summary>
    private void ReleaseDefaultPlacementTarget(DependencyObject flyout, DependencyProperty property)
    {
        if (!ReferenceEquals(Flyout, flyout) && ReferenceEquals(flyout.GetValue(property), this)
            && DependencyPropertyHelper.GetValueSource(flyout, property).BaseValueSource == BaseValueSource.Default)
        {
            flyout.ClearValue(property);
        }
    }

    /// <summary>Supplies drop-down defaults without overwriting application configuration.</summary>
    private void ConfigurePopup(Popup popup)
    {
        if (DependencyPropertyHelper.GetValueSource(popup, Popup.PlacementTargetProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.PlacementTargetProperty, this);
        }

        if (DependencyPropertyHelper.GetValueSource(popup, Popup.PlacementProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.PlacementProperty, PlacementMode.Bottom);
        }

        // Native Popup defaults to StaysOpen=true. A button flyout normally dismisses on an
        // outside click; WPF owns capture and dismissal once this property is false.
        if (DependencyPropertyHelper.GetValueSource(popup, Popup.StaysOpenProperty).BaseValueSource
            == BaseValueSource.Default)
        {
            popup.SetCurrentValue(Popup.StaysOpenProperty, false);
        }
    }

    /// <summary>Supplies placement defaults while retaining local values, styles, and bindings.</summary>
    private void ConfigureContextMenu(ContextMenu contextMenu)
    {
        // Sets MinWidth only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper.GetValueSource(contextMenu, MinWidthProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(MinWidthProperty, ActualWidth);
        }

        // Sets PlacementTarget only when it has not been explicitly set by the user
        // (via local value, style, or other sources).
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, ContextMenu.PlacementTargetProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(ContextMenu.PlacementTargetProperty, this);
        }

        // Sets Placement only when it has not been explicitly set by the user.
        // When no explicit value is provided, the menu is displayed below the button by default.
        if (DependencyPropertyHelper
               .GetValueSource(contextMenu, ContextMenu.PlacementProperty)
               .BaseValueSource == BaseValueSource.Default)
        {
            contextMenu.SetCurrentValue(ContextMenu.PlacementProperty, PlacementMode.Bottom);
        }
    }
}
