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
/// Extends <see cref="System.Windows.Controls.Button"/> by adding support for
/// an <see cref="Icon"/> and <see cref="Appearance"/>.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:Button
///     Appearance="Success"
///     Content="WPF Zen UI button with font icon"
///     Icon="{ui:FluentIconSource Fluent24}" /&gt;
/// </code>
/// </example>
public class Button : System.Windows.Controls.Button
{
    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
            nameof(Icon),
            typeof(object),
            typeof(Button),
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

    #region DP: RightIcon

    /// <summary>Identifies the <see cref="RightIcon"/> dependency property.</summary>
    public static readonly DependencyProperty RightIconProperty =
        DependencyProperty.Register(
            nameof(RightIcon),
            typeof(object),
            typeof(Button),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the content displayed after the button content.
    /// When no right icon is supplied, the template removes its presenter from layout.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? RightIcon
    {
        get => GetValue(RightIconProperty);
        set => SetValue(RightIconProperty, value);
    }

    #endregion

    #region DP: Appearance

    /// <summary>Identifies the <see cref="Appearance"/> dependency property.</summary>
    public static readonly DependencyProperty AppearanceProperty =
        DependencyProperty.Register(
            nameof(Appearance),
            typeof(ControlAppearance),
            typeof(Button),
            new PropertyMetadata(ControlAppearance.Normal)
        );

    /// <summary>
    /// Gets or sets the <see cref="ControlAppearance"/> of the button.
    /// </summary>
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
        DependencyProperty.Register(
            nameof(MouseOverBackground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background brush used while the pointer is over the button.
    /// A value of <see langword="null"/> makes the button fall back to <see cref="Control.Background"/>.
    /// </summary>
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
        DependencyProperty.Register(
            nameof(MouseOverBorderBrush),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the pointer is over the button.
    /// A value of <see langword="null"/> makes the button fall back to <see cref="Control.BorderBrush"/>.
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
        DependencyProperty.Register(
            nameof(PressedForeground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground brush used while the button is pressed.
    /// A value of <see langword="null"/> makes the button fall back to <see cref="Control.Foreground"/>.
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
        DependencyProperty.Register(
            nameof(PressedBackground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background brush used while the button is pressed.
    /// A value of <see langword="null"/> makes the button fall back to <see cref="Control.Background"/>.
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
        DependencyProperty.Register(
            nameof(PressedBorderBrush),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the button is pressed.
    /// A value of <see langword="null"/> makes the button fall back to <see cref="Control.BorderBrush"/>.
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
        DependencyProperty.Register(
            nameof(DisabledBackground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background brush used while the button is disabled. A value of
    /// <see langword="null"/> makes the button fall back to <see cref="Control.Background"/>.
    /// </summary>
    /// <remarks>
    /// Disabled brushes are resolved by the control rather than by a template trigger. This keeps
    /// command-driven disabled visuals working when a composite control supplies the normal brush
    /// through a higher-precedence template value.
    /// </remarks>
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
        DependencyProperty.Register(
            nameof(DisabledBorderBrush),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the button is disabled. A value of
    /// <see langword="null"/> makes the button fall back to <see cref="Control.BorderBrush"/>.
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
        DependencyProperty.Register(
            nameof(DisabledForeground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground brush used while the button is disabled. A value of
    /// <see langword="null"/> makes the button fall back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledForeground
    {
        get => (Brush?)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    #endregion

    #region Read-only DP: VisualBackground

    private static readonly DependencyPropertyKey VisualBackgroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBackground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBackgroundProperty =
        VisualBackgroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the background brush selected for the button's current interaction state.
    /// Templates should consume this property instead of reproducing the state-selection
    /// rules with triggers.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBackground => (Brush?)GetValue(VisualBackgroundProperty);

    #endregion

    #region Read-only DP: VisualBorderBrush

    private static readonly DependencyPropertyKey VisualBorderBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBorderBrush),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBorderBrushProperty =
        VisualBorderBrushPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the border brush selected for the button's current interaction state.
    /// Templates should consume this property instead of reproducing the state-selection
    /// rules with triggers.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBorderBrush => (Brush?)GetValue(VisualBorderBrushProperty);

    #endregion

    #region Read-only DP: VisualForeground

    private static readonly DependencyPropertyKey VisualForegroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualForeground),
            typeof(Brush),
            typeof(Button),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualForegroundProperty =
        VisualForegroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the foreground brush selected for the button's current interaction state.
    /// Templates should consume this property instead of reproducing the state-selection
    /// rules with triggers.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualForeground => (Brush?)GetValue(VisualForegroundProperty);

    #endregion

    #region DP: CornerRadius

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        DependencyProperty.Register(
            nameof(CornerRadius),
            typeof(CornerRadius),
            typeof(Button),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

    /// <summary>
    /// Gets or sets a value that represents the degree to which the corners of a <see cref="T:System.Windows.Controls.Border" /> are rounded.
    /// </summary>
    [Category("Appearance")]
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    static Button()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(Button),
            new FrameworkPropertyMetadata(typeof(Button))
        );

        // ------------------------------------------------------------------------------
        // The standard brush properties remain the normal-state inputs. Observing them
        // lets bindings, style triggers, and dynamic resources update the visual outputs
        // without changing the standard properties' effective values or public meaning.
        // ------------------------------------------------------------------------------

        BackgroundProperty.OverrideMetadata(
            typeof(Button),
            new FrameworkPropertyMetadata(
                BackgroundProperty.GetMetadata(typeof(System.Windows.Controls.Button)).DefaultValue,
                OnStandardBrushChanged
            )
        );
        BorderBrushProperty.OverrideMetadata(
            typeof(Button),
            new FrameworkPropertyMetadata(
                BorderBrushProperty.GetMetadata(typeof(System.Windows.Controls.Button)).DefaultValue,
                OnStandardBrushChanged
            )
        );
        ForegroundProperty.OverrideMetadata(
            typeof(Button),
            new FrameworkPropertyMetadata(
                ForegroundProperty.GetMetadata(typeof(System.Windows.Controls.Button)).DefaultValue,
                OnStandardBrushChanged
            )
        );
    }

    private static void OnStandardBrushChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((Button)d).UpdateVisualBrushes();
    }

    private static void OnStateBrushChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        ((Button)d).UpdateVisualBrushes();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Button"/> class.
    /// </summary>
    public Button()
    {
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Updates the pressed state on behalf of a focus-owning composite control.
    /// </summary>
    /// <remarks>
    /// <see cref="ButtonBase.IsPressed"/> has a protected setter because WPF normally derives the
    /// state from input received directly by the button. Composite controls such as
    /// <see cref="SplitButton"/> deliberately keep keyboard focus on their public wrapper while
    /// their private button parts retain responsibility for rendering interaction states. This
    /// narrow internal bridge lets that wrapper use the same read-only dependency property and
    /// visual-state callbacks as native <see cref="ButtonBase"/> keyboard handling, without
    /// reflecting over WPF internals or duplicating the button template's pressed visuals.
    /// </remarks>
    internal void SetPressedFromOwningControl(bool isPressed)
    {
#pragma warning disable WPF0041 // IsPressed is read-only; the inherited protected setter owns its key.
        IsPressed = isPressed;
#pragma warning restore WPF0041
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.Property == IsEnabledProperty)
        {
            // ICommand.CanExecute changes ButtonBase.IsEnabledCore through coercion rather than a
            // local IsEnabled assignment. Observing the effective dependency-property change here
            // covers both paths without overriding WPF's initialization-order-sensitive metadata.
            UpdateVisualBrushes();
        }
    }

    /// <inheritdoc />
    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);

        // IsMouseOver is a read-only dependency property whose key is private to WPF,
        // so derived controls cannot attach a metadata callback to it. MouseEnter and
        // MouseLeave follow the same IsMouseOver transitions, including movement into
        // or out of template descendants, and are therefore the supported invalidation
        // points available to this control.
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Resolves all template-facing brushes in one pass so every template observes the
    /// same state precedence. Pressed wins over pointer-over because a pressed button
    /// normally remains under the pointer and must not retain its hover appearance.
    /// A null state brush deliberately falls back to the corresponding standard brush;
    /// this makes state brushes optional but means null cannot request a state-specific
    /// absence of painting.
    /// </summary>
    private void UpdateVisualBrushes()
    {
        Brush? visualBackground  = Background;
        Brush? visualBorderBrush = BorderBrush;
        Brush? visualForeground  = Foreground;

        // Disabled wins over pointer and pressed states. In particular, ButtonBase can change its
        // effective IsEnabled value from ICommand.CanExecute without changing any brush input.
        // Resolving the state here prevents template bindings in composite controls from masking
        // the disabled palette through dependency-property precedence.
        if (!IsEnabled)
        {
            visualBackground  = DisabledBackground ?? visualBackground;
            visualBorderBrush = DisabledBorderBrush ?? visualBorderBrush;
            visualForeground  = DisabledForeground ?? visualForeground;
        }
        else if (IsPressed)
        {
            visualBackground  = PressedBackground ?? visualBackground;
            visualBorderBrush = PressedBorderBrush ?? visualBorderBrush;
            visualForeground  = PressedForeground ?? visualForeground;
        }
        else if (IsMouseOver)
        {
            visualBackground  = MouseOverBackground ?? visualBackground;
            visualBorderBrush = MouseOverBorderBrush ?? visualBorderBrush;
        }

        SetValue(VisualBackgroundPropertyKey,  visualBackground);
        SetValue(VisualBorderBrushPropertyKey, visualBorderBrush);
        SetValue(VisualForegroundPropertyKey,  visualForeground);
    }
}
