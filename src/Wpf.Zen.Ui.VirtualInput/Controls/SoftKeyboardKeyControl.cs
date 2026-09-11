// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.VirtualInput.AutomationPeers;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Presents one keyboard key and owns its press, repeat and long-press alternative interaction.
/// Unlike a command button, this control only raises input requests; it never focuses an editor,
/// changes an input method or injects input into Windows.
/// </summary>
/// <remarks>
/// <para>
/// The optional <c>PART_AlternatePopup</c> template part displays <see cref="SoftKeyboardKey.Alternates"/>.
/// Its non-focusable buttons carry alternative strings as their data contexts. Missing this part
/// leaves ordinary invocation available. The popup belongs to this key and does not affect measurement.
/// </para>
/// <para>
/// Mouse input, including WPF-promoted pen and primary touch input, drives a single active gesture.
/// Alternatives take precedence over repeat. Release after opening alternatives does not invoke the
/// primary action; a variant may be selected on release over it or by a subsequent click.
/// Hosts cancel interaction when their input context changes. Unload, disabling, hiding, template or
/// key replacement and unexpected capture loss also cancel without submitting input.
/// </para>
/// </remarks>
[TemplatePart(Name = AlternatePopupPartName, Type = typeof(Popup))]
public class SoftKeyboardKeyControl : ContentControl
{
    private const string AlternatePopupPartName = "PART_AlternatePopup";

    // todo: 将来要公布 Popup 是否打开的状态，所以监听自己的属性变更就行了，不一定用这种反向监听模式
    private static readonly DependencyPropertyDescriptor PopupIsOpenDescriptor
        = DependencyPropertyDescriptor.FromProperty(
            Popup.IsOpenProperty,
            typeof(Popup)
        );

    private readonly DispatcherTimer _timer;

    private Popup?           _popup;
    private SoftKeyboardKeyInteractionState _state;
    private bool             _releasingCapture;
    private int              _generation;

    static SoftKeyboardKeyControl()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SoftKeyboardKeyControl),
            new FrameworkPropertyMetadata(typeof(SoftKeyboardKeyControl))
        );

        FocusableProperty.OverrideMetadata(
            typeof(SoftKeyboardKeyControl),
            new FrameworkPropertyMetadata(false)
        );

        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(SoftKeyboardKeyControl),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardKeyControl"/> class.
    /// No timer runs until a gesture begins.
    /// </summary>
    public SoftKeyboardKeyControl()
    {
        _timer = new DispatcherTimer(DispatcherPriority.Input, Dispatcher);
        _timer.Tick += OnTimerTick;

        Loaded += (_, _) => AttachPopup();

        Unloaded += (_, _) =>
        {
            CancelInteraction();
            DetachPopup();
        };

        IsEnabledChanged += (_, _) =>
        {
            if (!IsEnabled)
            {
                CancelInteraction();
            }
        };

        IsVisibleChanged += (_, _) =>
        {
            if (!IsVisible)
            {
                CancelInteraction();
            }
        };
    }

    /// <summary>
    /// Identifies the <see cref="Key"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty KeyProperty
        = DependencyProperty.Register(
            nameof(Key),
            typeof(SoftKeyboardKey),
            typeof(SoftKeyboardKeyControl),
            new PropertyMetadata(
                null,
                static (owner, _) => ((SoftKeyboardKeyControl)owner).CancelInteraction()
            )
        );

    /// <summary>
    /// Gets or sets the immutable key description. Defaults to null, which prevents input requests.
    /// The property is not inherited; the template binds presentation and alternatives to this model.
    /// </summary>
    public SoftKeyboardKey? Key
    {
        get => (SoftKeyboardKey?)GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    private static readonly DependencyPropertyKey IsPressedPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(IsPressed),
            typeof(bool),
            typeof(SoftKeyboardKeyControl),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsPressed"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsPressedProperty
        = IsPressedPropertyKey.DependencyProperty;

    /// <summary
    /// >Gets a value indicating whether the active pointer is pressing this cap.
    /// Defaults to false; used by template triggers.
    /// </summary>
    public bool IsPressed => (bool)GetValue(IsPressedProperty);

    private static readonly DependencyPropertyKey IsAlternatePopupOpenPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(IsAlternatePopupOpen),
            typeof(bool),
            typeof(SoftKeyboardKeyControl),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsAlternatePopupOpen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsAlternatePopupOpenProperty
        = IsAlternatePopupOpenPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this key is presenting its alternatives.
    /// Defaults to false.
    /// </summary>
    public bool IsAlternatePopupOpen => (bool)GetValue(IsAlternatePopupOpenProperty);

    /// <summary>
    /// Identifies the <see cref="InteractionStarted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent InteractionStartedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(InteractionStarted),
            RoutingStrategy.Bubble,
            typeof(RoutedEventHandler),
            typeof(SoftKeyboardKeyControl)
        );

    /// <summary>
    /// Occurs before a gesture or programmatic invocation.
    /// A host may cancel this key synchronously.
    /// </summary>
    public event RoutedEventHandler InteractionStarted
    {
        add => AddHandler(InteractionStartedEvent, value);
        remove => RemoveHandler(InteractionStartedEvent, value);
    }

    /// <summary>
    /// Identifies the <see cref="InputRequested"/> routed event.
    /// </summary>
    public static readonly RoutedEvent InputRequestedEvent
        = EventManager.RegisterRoutedEvent(
            nameof(InputRequested),
            RoutingStrategy.Bubble,
            typeof(EventHandler<SoftKeyboardInputRequestedEventArgs>),
            typeof(SoftKeyboardKeyControl)
        );

    /// <summary>
    /// Occurs for primary invocation, each repeat, or a selected alternative.
    /// Handling it does not itself inject input.
    /// </summary>
    public event EventHandler<SoftKeyboardInputRequestedEventArgs> InputRequested
    {
        add => AddHandler(InputRequestedEvent, value);
        remove => RemoveHandler(InputRequestedEvent, value);
    }

    /// <summary>
    /// Requests the primary action once, including for UI Automation. Cancels any unfinished gesture
    /// first; a disabled, hidden or unconfigured key does nothing. Editor validation belongs to the host.
    /// </summary>
    public void Invoke()
    {
        CancelInteraction();

        if (!CanInteract)
        {
            return;
        }

        var generation = _generation;

        RaiseEvent(new RoutedEventArgs(InteractionStartedEvent, this));

        if (generation == _generation && CanInteract)
        {
            Submit(null);
        }
    }

    /// <summary>
    /// Ends timers, capture and alternative presentation without emitting an input request.
    /// Safe to call repeatedly.
    /// </summary>
    public void CancelInteraction()
    {
        ++_generation;

        _state = SoftKeyboardKeyInteractionState.Idle;
        _timer.Stop();

        SetValue(IsPressedPropertyKey, false);
        SetValue(IsAlternatePopupOpenPropertyKey, false);

        ReleasePointer();

        _popup?.SetCurrentValue(Popup.IsOpenProperty, false);
    }

    /// <summary>
    /// Disconnects the previous popup before connecting the optional part in the current template.
    /// </summary>
    public override void OnApplyTemplate()
    {
        CancelInteraction();
        DetachPopup();

        base.OnApplyTemplate();

        AttachPopup();
    }

    /// <summary>
    /// Cancels before the old visual tree disappears, including replacement with an empty template.
    /// </summary>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CancelInteraction();
        DetachPopup();

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <summary>
    /// Creates the Invoke provider without introducing Button command or Click execution paths.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new SoftKeyboardKeyControlAutomationPeer(this);
    }

    /// <summary>
    /// Captures the primary pointer without moving keyboard focus away from the editor.
    /// </summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        if (!CanInteract || _state == SoftKeyboardKeyInteractionState.Alternatives)
        {
            return;
        }

        e.Handled = true;

        BeginPress();

        if (_state != SoftKeyboardKeyInteractionState.Idle && !CaptureMouse())
        {
            CancelInteraction();
        }
    }

    /// <summary>
    /// Completes the gesture locally; long press and repeat cannot fall through to a primary click.
    /// </summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_state == SoftKeyboardKeyInteractionState.Idle)
        {
            return;
        }

        e.Handled = true;

        var alternate = HitAlternative(e);
        var inside    = new Rect(RenderSize).Contains(e.GetPosition(this));

        CompletePress(inside, alternate);
    }

    /// <summary>
    /// Tracks cap highlighting while captured. Repeats pause outside the cap.
    /// </summary>
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);

        if (_state is SoftKeyboardKeyInteractionState.Pressed or SoftKeyboardKeyInteractionState.Repeating)
        {
            SetValue(
                IsPressedPropertyKey,
                new Rect(RenderSize).Contains(e.GetPosition(this))
            );
        }
    }

    /// <summary>
    /// Unexpected capture transfer cancels the gesture;
    /// intentional release preserves the variant selection session.
    /// </summary>
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);

        if (ReferenceEquals(e.OriginalSource, this) && !_releasingCapture)
        {
            CancelInteraction();
        }
    }

    private bool CanInteract => IsEnabled && IsVisible && Key is not null;

    // Pointer handlers translate device events into these transitions. The gesture state depends
    // only on the key model and visual lifetime, never keyboard targets or native input services.
    internal void BeginPress()
    {
        CancelInteraction();

        if (!CanInteract)
        {
            return;
        }

        _state = SoftKeyboardKeyInteractionState.Pressed;

        SetValue(IsPressedPropertyKey, true);

        var generation = _generation;

        RaiseEvent(new RoutedEventArgs(InteractionStartedEvent, this));

        if (generation != _generation || !CanInteract)
        {
            return;
        }

        _timer.Interval = TimeSpan.FromMilliseconds((SystemParameters.KeyboardDelay + 1) * 250d);
        _timer.Start();
    }

    internal void CompletePress(bool inside, string? alternate = null)
    {
        _timer.Stop();

        if (_state == SoftKeyboardKeyInteractionState.Alternatives)
        {
            ReleasePointer();
            SetValue(IsPressedPropertyKey, false);

            if (alternate is not null)
            {
                SelectAlternative(alternate);
            }
            else
            {
                // Popup auto-close must acquire capture only after the originating cap releases it.
                // Otherwise, the opening gesture itself is treated as an outside dismissal.
                _popup?.SetCurrentValue(Popup.StaysOpenProperty, false);
            }

            return;
        }

        var submit = _state == SoftKeyboardKeyInteractionState.Pressed && inside && CanInteract;
        var key    = Key;

        CancelInteraction();

        if (submit && ReferenceEquals(Key, key))
        {
            Submit(null);
        }
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        AdvanceHold();
    }

    internal void AdvanceHold()
    {
        if (!CanInteract)
        {
            CancelInteraction();
            return;
        }

        if (!IsPressed
            || _state is not (SoftKeyboardKeyInteractionState.Pressed
                or SoftKeyboardKeyInteractionState.Repeating))
        {
            return;
        }

        if (_state == SoftKeyboardKeyInteractionState.Pressed && Key!.Alternates.Count > 0)
        {
            _timer.Stop();

            if (_popup is null)
            {
                return;
            }

            _state = SoftKeyboardKeyInteractionState.Alternatives;

            _popup.SetCurrentValue(Popup.PlacementTargetProperty, this);
            _popup.SetCurrentValue(Popup.StaysOpenProperty, true);

            SetValue(IsAlternatePopupOpenPropertyKey, true);

            _popup.SetCurrentValue(Popup.IsOpenProperty, true);

            return;
        }

        if (!Key!.IsRepeatEnabled)
        {
            _timer.Stop();
            return;
        }

        _state = SoftKeyboardKeyInteractionState.Repeating;

        // Set up the next tick before calling the host: request handlers may replace this key,
        // close the keyboard or cancel the gesture. No timer may be restarted after that callout.
        _timer.Interval = TimeSpan.FromMilliseconds(
            1000d / (2.5d + (SystemParameters.KeyboardSpeed * 27.5d / 31d))
        );

        Submit(null);
    }

    internal void SelectAlternative(string value)
    {
        if (_state != SoftKeyboardKeyInteractionState.Alternatives
            || !CanInteract || !Key!.Alternates.Contains(value))
        {
            return;
        }

        var key = Key;

        CancelInteraction();

        if (ReferenceEquals(key, Key))
        {
            Submit(value);
        }
    }

    private void Submit(string? alternate)
    {
        if (CanInteract)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.InvokePatternOnInvoked))
            {
                UIElementAutomationPeer.FromElement(this)?.RaiseAutomationEvent(
                    AutomationEvents.InvokePatternOnInvoked
                );
            }

            OnInputRequested(
                new SoftKeyboardInputRequestedEventArgs(
                    InputRequestedEvent, this, Key!, alternate
                )
            );
        }
    }

    /// <summary>
    /// Raises a semantic request.
    /// Overrides may customize notification, but input execution remains the host's responsibility.
    /// </summary>
    /// <param name="eventArgs">
    /// The primary or alternative action selected by this control.
    /// </param>
    protected virtual void OnInputRequested(SoftKeyboardInputRequestedEventArgs eventArgs)
    {
        RaiseEvent(eventArgs);
    }

    private void ReleasePointer()
    {
        _releasingCapture = true;

        try
        {
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
        }
        finally
        {
            _releasingCapture = false;
        }
    }

    private void AttachPopup()
    {
        DetachPopup();

        _popup = GetTemplateChild(AlternatePopupPartName) as Popup;

        if (_popup is null)
        {
            return;
        }

        // Closed may follow native-window destruction asynchronously. Observe IsOpen itself
        // so an outside dismissal immediately invalidates pending selection requests.
        PopupIsOpenDescriptor.AddValueChanged(_popup, OnPopupIsOpenChanged);

        // A Popup has a separate presentation source. Translate selection into an event on the
        // cap instead of depending on an ancestor route across the popup boundary.
        _popup.AddHandler(
            ButtonBase.ClickEvent, new RoutedEventHandler(OnAlternativeClick)
        );
    }

    private void DetachPopup()
    {
        if (_popup is null)
        {
            return;
        }

        // Property descriptors retain their listeners. Remove subscriptions on both template
        // replacement and unload; Loaded reconnects an unchanged template when reused.
        PopupIsOpenDescriptor.RemoveValueChanged(_popup, OnPopupIsOpenChanged);

        _popup.RemoveHandler(
            ButtonBase.ClickEvent, new RoutedEventHandler(OnAlternativeClick)
        );

        _popup = null;
    }

    private void OnPopupIsOpenChanged(object? sender, EventArgs e)
    {
        if (_popup?.IsOpen == false && _state == SoftKeyboardKeyInteractionState.Alternatives)
        {
            CancelInteraction();
        }
    }

    private void OnAlternativeClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is ButtonBase { DataContext: string value })
        {
            e.Handled = true;
            SelectAlternative(value);
        }
    }

    private string? HitAlternative(MouseEventArgs e)
    {
        if (_popup is not { IsOpen: true, Child: { } surface })
        {
            return null;
        }

        // Translate into the popup's presentation source; keyboard-local hit testing cannot
        // find a variant outside the keyboard's window bounds.

        var current = surface.InputHitTest(e.GetPosition(surface)) as DependencyObject;

        while (current is not null)
        {
            if (current is ButtonBase { DataContext: string value })
            {
                return value;
            }

            current = current is Visual ? VisualTreeHelper.GetParent(current) : null;
        }

        return null;
    }
}
