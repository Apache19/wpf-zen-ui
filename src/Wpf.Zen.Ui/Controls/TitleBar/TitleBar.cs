// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a customizable WPF title bar with semantic window hit-test regions.
/// </summary>
/// <remarks>
/// <para>
/// The default template assigns <see cref="HitTestRoleProperty"/> to draggable caption content, interactive content,
/// the application icon and caption buttons. A containing <see cref="FluentWindow"/> resolves those roles from its
/// visual tree and maps them to native HT values, preserving resize, drag, system-menu and Windows 11 Snap Layout
/// behavior without giving this control its own HWND hook.
/// </para>
/// <para>
/// TitleBar can also be hosted in an ordinary <see cref="Window"/>. Its caption buttons still execute the associated
/// <see cref="SystemCommands"/>, but native dragging, Snap Layout and non-client hit testing are guaranteed only when
/// a FluentWindow owns the HWND message pipeline.
/// </para>
/// </remarks>
public class TitleBar : ContentControl
{
    static TitleBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(typeof(TitleBar))
        );

        // Ensure a non-null Background by default to preserve hit-testing behavior.
        // A Transparent brush is used as a safe fallback to prevent hit testing from
        // being disabled when Background is explicitly set to null.
        BackgroundProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                null,
                static (_, baseValue) => baseValue ?? Brushes.Transparent
            )
        );

        // Set the default vertical alignment of the TitleBar to Top.
        // This aligns the control with typical window chrome positioning
        // and avoids unintended vertical centering when hosted in layout containers.
        VerticalAlignmentProperty.OverrideMetadata(
            typeof(TitleBar),
            new FrameworkPropertyMetadata(VerticalAlignment.Top)
        );
    }

    #region DP: LeftContent, LeftContentState, Title, IsIconInteractionEnabled, IsUserContentEnabled, HitTestRole

    #region LeftContent

    /// <summary>
    /// Identifies the <see cref="LeftContent"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftContentProperty = DependencyProperty.Register(
        nameof(LeftContent),
        typeof(object),
        typeof(TitleBar),
        new FrameworkPropertyMetadata(null, OnLeftContentChanged)
    );

    /// <summary>
    /// Gets or sets optional content displayed at the leading edge of the title bar,
    /// before the application icon and title.
    /// </summary>
    /// <remarks>
    /// This slot allows another control to contribute interactive title-bar content
    /// without depending on the title bar's template structure or modifying its layout
    /// properties. The slot measures to zero when the value is <see langword="null"/>.
    /// Interactive content is treated as user content during non-client hit testing so
    /// that it receives normal WPF input instead of initiating a window drag.
    /// <para/>
    /// When the assigned value is a direct <see cref="UIElement"/>, changes to its
    /// <see cref="UIElement.Visibility"/> are reflected by <see cref="LeftContentState"/>. Values
    /// that are not UI elements are considered visible because their eventual data-template root
    /// does not exist at the property boundary.
    /// </remarks>
    public object? LeftContent
    {
        get => GetValue(LeftContentProperty);
        set => SetValue(LeftContentProperty, value);
    }

    #endregion

    #region LeftContentState

    private static readonly DependencyPropertyKey LeftContentStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(LeftContentState),
            typeof(TitleBarContentState),
            typeof(TitleBar),
            new FrameworkPropertyMetadata(TitleBarContentState.Empty)
        );

    /// <summary>
    /// Identifies the read-only <see cref="LeftContentState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftContentStateProperty =
        LeftContentStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the effective presentation state of <see cref="LeftContent"/>.
    /// </summary>
    /// <remarks>
    /// The value is maintained by the title bar and is intended for templates and styles that need
    /// to distinguish a missing value from visible, hidden, or collapsed UI content. It is a
    /// read-only dependency property so external values cannot disagree with the assigned content.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public TitleBarContentState LeftContentState => (TitleBarContentState)GetValue(LeftContentStateProperty);

    // LeftContent usually changes only once when a cooperating control claims the slot. Its
    // Visibility can then change independently, so the title bar needs a second dependency-property
    // notification path. A private binding is preferable to DependencyPropertyDescriptor here:
    // WPF's descriptor keeps sources in a strong tracker table until explicitly detached, whereas
    // the binding follows the normal target lifetime and is replaced atomically with LeftContent.
    private static readonly DependencyProperty TrackedLeftContentVisibilityProperty =
        DependencyProperty.Register(
            "TrackedLeftContentVisibility",
            typeof(Visibility),
            typeof(TitleBar),
            new FrameworkPropertyMetadata(Visibility.Visible, OnTrackedLeftContentVisibilityChanged)
        );

    private static void OnLeftContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TitleBar titleBar)
        {
            return;
        }

        if (e.NewValue is UIElement element)
        {
            // Replacing the binding directly avoids publishing the default Visible value between
            // two UIElement assignments. Consumers therefore observe only genuine slot states.
            BindingOperations.SetBinding(
                titleBar,
                TrackedLeftContentVisibilityProperty,
                new Binding(nameof(UIElement.Visibility)) { Mode = BindingMode.OneWay, Source = element }
            );
        }
        else
        {
            BindingOperations.ClearBinding(titleBar, TrackedLeftContentVisibilityProperty);
        }

        // SetBinding transfers the current source value synchronously in the common path, but the
        // explicit refresh also covers null and data-object content and avoids depending on binding
        // engine scheduling during template construction.
        titleBar.UpdateLeftContentState();
    }

    private static void OnTrackedLeftContentVisibilityChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is TitleBar titleBar)
        {
            titleBar.UpdateLeftContentState();
        }
    }

    /// <summary>
    /// Projects the public content value and the tracked direct element visibility into the stable
    /// state consumed by title-bar templates. Ancestor visibility is deliberately ignored: using
    /// UIElement.IsVisible would report the slot as hidden merely because the window is not loaded.
    /// </summary>
    private void UpdateLeftContentState()
    {
        TitleBarContentState state = LeftContent switch
        {
            null => TitleBarContentState.Empty,
            UIElement _ => (Visibility)GetValue(TrackedLeftContentVisibilityProperty) switch
            {
                Visibility.Hidden => TitleBarContentState.Hidden,
                Visibility.Collapsed => TitleBarContentState.Collapsed,
                _ => TitleBarContentState.Visible,
            },
            _ => TitleBarContentState.Visible,
        };

        SetValue(LeftContentStatePropertyKey, state);
    }

    #endregion

    #region Title

    /// <summary>
    /// Identifies the <see cref="Title"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(object),
        typeof(TitleBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the title content displayed in the title bar.
    /// </summary>
    /// <remarks>
    /// This property represents the content displayed as the window title.
    /// Although commonly set to a <see cref="string"/>, it accepts any object.
    ///
    /// If a <see cref="string"/> is provided, it is automatically rendered as text.
    /// If a UI element is provided, it will be displayed directly, allowing
    /// full customization of the title presentation.
    ///
    /// The layout, alignment, and visual appearance of the title content are
    /// the responsibility of the consumer.
    ///
    /// If set to <see langword="null"/>, no title content is displayed.
    /// </remarks>
    public object? Title
    {
        get => (object?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    #endregion

    #region IsIconInteractionEnabled

    /// <summary>
    /// Identifies the <see cref="IsIconInteractionEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsIconInteractionEnabledProperty = DependencyProperty.Register(
        nameof(IsIconInteractionEnabled),
        typeof(bool),
        typeof(TitleBar),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether double-clicking the title bar icon closes the window.
    /// </summary>
    /// <remarks>
    /// When enabled and hosted by a <see cref="FluentWindow"/>, the icon participates in non-client hit testing and
    /// a double-click posts the standard close command. A single left click intentionally has no behavior; the
    /// legacy icon system menu does not fit the Fluent Design title-bar interaction model.
    ///
    /// When disabled, the icon area behaves as part of the draggable caption. A <see cref="TitleBar"/> hosted by a
    /// regular <see cref="Window"/> displays the icon but cannot provide this HWND-level interaction guarantee.
    ///
    /// The default value is <see langword="false"/>.
    /// </remarks>
    public bool IsIconInteractionEnabled
    {
        get => (bool)GetValue(IsIconInteractionEnabledProperty);
        set => SetValue(IsIconInteractionEnabledProperty, value);
    }

    #endregion

    #region IsUserContentEnabled

    /// <summary>
    /// Identifies the <see cref="IsUserContentEnabled"/> dependency property.
    /// </summary>
    internal static readonly DependencyProperty IsUserContentEnabledProperty = DependencyProperty.Register(
        nameof(IsUserContentEnabled),
        typeof(bool),
        typeof(TitleBar),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets a value indicating whether user-provided content is enabled.
    ///
    /// This property is intended for internal coordination between <see cref="TitleBar"/>
    /// and <see cref="ContentDialog"/> to support input isolation while a dialog is shown.
    /// Exposing this behavior publicly has no meaningful use case, as consumers are expected
    /// to manage the enabled state of their own content directly.
    /// </summary>
    public bool IsUserContentEnabled
    {
        get => (bool)GetValue(IsUserContentEnabledProperty);
        internal set => SetValue(IsUserContentEnabledProperty, value);
    }

    #endregion

    #region HitTestRole

    /// <summary>
    /// Identifies the <c>TitleBar.HitTestRole</c> attached dependency property.
    /// </summary>
    /// <remarks>
    /// This attached property is used to associate a visual element in the title bar
    /// with a semantic non-client hit-test role. <see cref="FluentWindow"/> maps the role to native HT values;
    /// templates therefore remain independent of Win32 constants.
    ///
    /// The property value is inherited, allowing container elements to define an
    /// identity that applies to their visual children. The default value is <see cref="TitleBarHitTestRole.None"/>.
    ///
    /// This property is not intended for general application logic and should only be
    /// used when integrating custom elements into the title bar interaction model.
    /// </remarks>
    public static readonly DependencyProperty HitTestRoleProperty = DependencyProperty.RegisterAttached(
        "HitTestRole",
        typeof(TitleBarHitTestRole),
        typeof(TitleBar),
        new FrameworkPropertyMetadata(TitleBarHitTestRole.None, FrameworkPropertyMetadataOptions.Inherits)
    );

    /// <summary>
    /// Gets the title-bar hit-test role inherited by <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// Dependency object whose role should be read.
    /// </param>
    /// <returns>
    /// The effective <see cref="TitleBarHitTestRole"/>.
    /// </returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static TitleBarHitTestRole GetHitTestRole(DependencyObject obj)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        return (TitleBarHitTestRole)obj.GetValue(HitTestRoleProperty);
    }

    /// <summary>
    /// Sets the title-bar hit-test role inherited from <paramref name="obj"/>.
    /// </summary>
    /// <param name="obj">
    /// Dependency object on which the role should be assigned.
    /// </param>
    /// <param name="value">
    /// Semantic role to assign.
    /// </param>
    public static void SetHitTestRole(DependencyObject obj, TitleBarHitTestRole value)
    {
        if (obj == null)
        {
            throw new ArgumentNullException(nameof(obj));
        }

        obj.SetValue(HitTestRoleProperty, value);
    }

    #endregion

    #endregion

    private Window? _window;

    /// <summary>
    /// Initializes a new instance of the <see cref="TitleBar"/> class.
    /// </summary>
    public TitleBar()
    {
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        _window = Window.GetWindow(this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _window = null;
    }

    private void CloseWindow()
    {
        if (_window is not null)
        {
            SystemCommands.CloseWindow(_window);
        }
    }

    private void MinimizeWindow()
    {
        if (_window is not null)
        {
            SystemCommands.MinimizeWindow(_window);
        }
    }

    private void MaximizeWindow()
    {
        if (_window is FluentWindow fluentWindow)
        {
            if (fluentWindow.ActualWindowState == WindowState.Normal)
            {
                SystemCommands.MaximizeWindow(fluentWindow);
            }
            else
            {
                SystemCommands.RestoreWindow(fluentWindow);
            }
        }
        else if (_window is not null)
        {
            if (_window.WindowState == WindowState.Normal)
            {
                SystemCommands.MaximizeWindow(_window);
            }
            else
            {
                SystemCommands.RestoreWindow(_window);
            }
        }
    }

    private void OnButtonClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TitleBarButton d)
        {
            var role = GetHitTestRole(d);
            switch (role)
            {
                case TitleBarHitTestRole.MaximizeButton:
                    MaximizeWindow();
                    break;

                case TitleBarHitTestRole.CloseButton:
                    CloseWindow();
                    break;

                case TitleBarHitTestRole.MinimizeButton:
                    MinimizeWindow();
                    break;
            }
        }
    }
}
