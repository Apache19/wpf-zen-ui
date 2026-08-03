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
/// Represents a WPF-rendered caption button hosted by a <see cref="TitleBar"/>.
/// </summary>
/// <remarks>
/// <para>
/// When hosted by <see cref="FluentWindow"/>, the window returns the native HT value associated with the button's
/// <see cref="TitleBar.HitTestRoleProperty"/>. This preserves Windows features such as the maximize-button Snap
/// Layout while FluentWindow converts the resulting non-client press into ordinary ButtonBase capture and click
/// semantics.
/// </para>
/// <para>
/// The control owns no HWND hook. Its only native-facing surface consists of internal state-transition methods
/// called by FluentWindow, which prevents message ordering from depending on template load order.
/// </para>
/// </remarks>
public class TitleBarButton : ButtonBase
{
    static TitleBarButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(typeof(TitleBarButton))
        );

        // Caption buttons do not become keyboard focus or tab stops. Alt+Space and the system menu retain the
        // platform keyboard contract, while application commands can still invoke the ButtonBase command directly.
        FocusableProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(false, null, static (_, _) => false)
        );
        IsTabStopProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(false, null, static (_, _) => false)
        );
        ClickModeProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(ClickMode.Release, null, static (_, _) => ClickMode.Release)
        );

        // A transparent background keeps the complete caption-button rectangle in WPF hit testing even when a
        // consumer clears Background explicitly.
        BackgroundProperty.OverrideMetadata(
            typeof(TitleBarButton),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                null,
                static (_, baseValue) => baseValue ?? Brushes.Transparent
            )
        );
    }

    private static readonly DependencyPropertyKey IsPointerOverPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPointerOver),
            typeof(bool),
            typeof(TitleBarButton),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsPointerOver"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsPointerOverProperty = IsPointerOverPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the pointer is resolved over this button in either client or non-client input mode.
    /// </summary>
    /// <remarks>
    /// WPF's <see cref="UIElement.IsMouseOver"/> is not updated while Windows treats the element as a non-client
    /// caption target. This property combines that native hover state with normal ButtonBase capture semantics so a
    /// single template trigger can render consistent pointer feedback. The default value is <see langword="false"/>.
    /// </remarks>
    public bool IsPointerOver
    {
        get => (bool)GetValue(IsPointerOverProperty);
        private set => SetValue(IsPointerOverPropertyKey, value);
    }

    private bool _isNonClientPointerOver;
    private bool _isNonClientPress;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleBarButton"/> class.
    /// </summary>
    public TitleBarButton() { }

    /// <summary>
    /// Updates the pointer state resolved by FluentWindow's non-client hit testing.
    /// </summary>
    internal void SetNonClientPointerOver(bool value)
    {
        _isNonClientPointerOver = value;
        if (!IsMouseCaptured)
        {
            IsPointerOver = value || IsMouseOver;
        }
    }

    /// <summary>
    /// Converts a native caption-button press into WPF mouse capture.
    /// </summary>
    /// <remarks>
    /// Windows must first see the button as HTMINBUTTON/HTMAXBUTTON/HTCLOSE/HTHELP to expose its native hover
    /// affordances. Capturing here moves the remainder of the gesture into ButtonBase so commands, routed Click and
    /// cancellation when the pointer leaves the button retain normal WPF behavior.
    /// </remarks>
    internal void BeginNonClientPress()
    {
        if (!IsEnabled || Mouse.Captured is not null)
        {
            return;
        }

        _isNonClientPress = true;
        IsPointerOver = true;

        // ButtonBase normally establishes these states in OnMouseLeftButtonDown, but that WPF event does not exist
        // for WM_NCLBUTTONDOWN. Its public OnMouseLeftButtonUp implementation still raises Click when capture and
        // IsPressed are present, so seeding precisely those states reuses ButtonBase's release/cancel/command path.
        #pragma warning disable WPF0041
        IsPressed = true;
        #pragma warning restore WPF0041

        if (!CaptureMouse())
        {
            _isNonClientPress = false;
            #pragma warning disable WPF0041
            IsPressed = false;
            #pragma warning restore WPF0041
        }
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdatePointerState();
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        UpdatePointerState();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdatePointerState();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        UpdatePointerState();
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        _isNonClientPress = false;
        _isNonClientPointerOver = false;
        base.OnLostMouseCapture(e);

        // The native hover bit described the location before capture started and is stale now. IsMouseOver is
        // recalculated by WPF at release; a following WM_NCHITTEST/WM_NCMOUSEMOVE will restore native hover when
        // the pointer is still over the caption button.
        IsPointerOver = IsMouseOver;

        // Non-client leave tracking is cancelled before capture so Windows does not send a false leave while the
        // gesture changes domains. Re-arm it only after ButtonBase has released capture.
        if (Window.GetWindow(this) is FluentWindow window)
        {
            window.RestoreNonClientMouseTracking();
        }
    }

    private void UpdatePointerState()
    {
        if (_isNonClientPress && IsMouseCaptured && Mouse.LeftButton == MouseButtonState.Pressed)
        {
            IsPointerOver = IsPressed;
            return;
        }

        IsPointerOver = IsMouseOver || _isNonClientPointerOver;
    }
}
