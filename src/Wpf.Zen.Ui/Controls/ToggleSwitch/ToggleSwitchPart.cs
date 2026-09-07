// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a styleable visual part for a <see cref="ToggleSwitch"/> and exposes the owning
/// switch's orientation and interaction state as trigger-friendly read-only dependency
/// properties.
/// </summary>
/// <remarks>
/// <para>
/// The default switch template creates one instance for the track and one for the thumb. This
/// type deliberately contains no built-in drawing: <see cref="Control.Template"/> and the normal
/// <see cref="Control"/> appearance properties remain available for flat, outlined, illustrated,
/// or skeuomorphic skins.
/// </para>
/// <para>
/// State values are maintained by the owning <see cref="ToggleSwitch"/> when the instance is a
/// named template part. They are read-only so a part style cannot accidentally diverge from input
/// and toggle state. A standalone instance retains the default unchecked, non-interactive values.
/// </para>
/// </remarks>
public class ToggleSwitchPart : Control
{
    #region Constructor

    static ToggleSwitchPart()
    {
        // These objects are cosmetic children of one input control, not nested controls. Keeping
        // them outside hit testing and keyboard focus preserves ToggleButton's single automation
        // and input surface even when a replacement part style omits the corresponding setters.
        FocusableProperty.OverrideMetadata(
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(false)
        );

        IsHitTestVisibleProperty.OverrideMetadata(
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(false)
        );
    }

    #endregion

    #region DP: CornerRadius

    /// <summary>Identifies the <see cref="CornerRadius"/> dependency property.</summary>
    public static readonly DependencyProperty CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner(
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(default(CornerRadius))
        );

    /// <summary>
    /// Gets or sets the corner radius made available to the part template. The default is zero.
    /// </summary>
    /// <remarks>
    /// This property does not affect layout or rendering by itself; the active
    /// <see cref="Control.Template"/> decides how it is applied. The default track and thumb
    /// templates forward it to their border, allowing common rounded or square variants to be
    /// created with a single style setter.
    /// </remarks>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: CheckedBackground

    /// <summary>Identifies the <see cref="CheckedBackground"/> dependency property.</summary>
    public static readonly DependencyProperty CheckedBackgroundProperty =
        DependencyProperty.Register(
            nameof(CheckedBackground),
            typeof(Brush),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the brush made available to the part template for its checked appearance.
    /// </summary>
    /// <remarks>
    /// The property does not draw or animate anything by itself. A track template can place a
    /// checked layer above its ordinary <see cref="Control.Background"/> and bind that layer's
    /// opacity to <see cref="TransitionProgress"/>. Keeping both endpoint brushes available at the
    /// same time avoids replacing a brush when <see cref="IsChecked"/> changes, which would make a
    /// reverse transition jump directly to its unchecked color.
    /// </remarks>
    public Brush? CheckedBackground
    {
        get => (Brush?)GetValue(CheckedBackgroundProperty);
        set => SetValue(CheckedBackgroundProperty, value);
    }

    #endregion

    #region DP: CheckedBorderBrush

    /// <summary>Identifies the <see cref="CheckedBorderBrush"/> dependency property.</summary>
    public static readonly DependencyProperty CheckedBorderBrushProperty =
        DependencyProperty.Register(
            nameof(CheckedBorderBrush),
            typeof(Brush),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the border brush made available to the part template for its checked
    /// appearance.
    /// </summary>
    /// <remarks>
    /// This property is independent of <see cref="Control.BorderBrush"/> so both endpoint borders
    /// can coexist while a template cross-fades them with <see cref="TransitionProgress"/>. It has
    /// no rendering or layout effect unless the active part template consumes it.
    /// </remarks>
    public Brush? CheckedBorderBrush
    {
        get => (Brush?)GetValue(CheckedBorderBrushProperty);
        set => SetValue(CheckedBorderBrushProperty, value);
    }

    #endregion

    #region Read-Only DP: Orientation

    private static readonly DependencyPropertyKey OrientationPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Orientation),
            typeof(Orientation),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(Orientation.Horizontal)
        );

    /// <summary>Identifies the <see cref="Orientation"/> dependency property.</summary>
    public static readonly DependencyProperty OrientationProperty =
        OrientationPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the axis selected by the owning switch. The default is
    /// <see cref="System.Windows.Controls.Orientation.Horizontal"/>.
    /// </summary>
    /// <remarks>
    /// The value does not rotate or arrange the part by itself. It is exposed so track and thumb
    /// styles can select direction-specific dimensions and geometry with ordinary property
    /// triggers. The owning <see cref="ToggleSwitch"/> updates it when this instance is a named
    /// template part; a standalone part retains the horizontal default.
    /// </remarks>
    public Orientation Orientation => (Orientation)GetValue(OrientationProperty);

    #endregion

    #region Read-Only DP: IsChecked

    private static readonly DependencyPropertyKey IsCheckedPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsChecked),
            typeof(bool?),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsChecked"/> dependency property.</summary>
    public static readonly DependencyProperty IsCheckedProperty =
        IsCheckedPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the owning switch's checked state. A null value represents the indeterminate state.
    /// </summary>
    /// <remarks>
    /// This property is intended for <see cref="Style.Triggers"/> and does not independently change
    /// switch behavior.
    /// </remarks>
    public bool? IsChecked => (bool?)GetValue(IsCheckedProperty);

    #endregion

    #region Read-Only DP: IsPointerOver

    private static readonly DependencyPropertyKey IsPointerOverPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPointerOver),
            typeof(bool),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsPointerOver"/> dependency property.</summary>
    public static readonly DependencyProperty IsPointerOverProperty =
        IsPointerOverPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether the pointer is over the owning switch.</summary>
    /// <remarks>
    /// Unlike the inherited <see cref="UIElement.IsMouseOver"/>, this value covers the complete
    /// switch even when this particular visual part is not directly beneath the pointer.
    /// </remarks>
    public bool IsPointerOver => (bool)GetValue(IsPointerOverProperty);

    #endregion

    #region Read-Only DP: IsPressed

    private static readonly DependencyPropertyKey IsPressedPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPressed),
            typeof(bool),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsPressed"/> dependency property.</summary>
    public static readonly DependencyProperty IsPressedProperty =
        IsPressedPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the owning switch is currently pressed by pointer or
    /// keyboard input.
    /// </summary>
    /// <remarks>
    /// The value mirrors WPF <see cref="ButtonBase.IsPressed"/> semantics, including mouse capture
    /// cancellation, rather than approximating the state from individual input events.
    /// </remarks>
    public bool IsPressed => (bool)GetValue(IsPressedProperty);

    #endregion

    #region Read-Only DP: TransitionProgress

    private static readonly DependencyPropertyKey TransitionProgressPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(TransitionProgress),
            typeof(double),
            typeof(ToggleSwitchPart),
            new FrameworkPropertyMetadata(0.0d)
        );

    /// <summary>Identifies the <see cref="TransitionProgress"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionProgressProperty =
        TransitionProgressPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the owning switch's current eased visual progress from unchecked (0) to checked (1).
    /// </summary>
    /// <remarks>
    /// The indeterminate endpoint is 0.5. The owning <see cref="ToggleSwitch"/> publishes this
    /// value on every transition tick, allowing a part template to drive opacity, transforms, and
    /// other animation-friendly dependency properties from the same timeline as thumb movement.
    /// A standalone part retains the default value of 0.
    /// </remarks>
    public double TransitionProgress => (double)GetValue(TransitionProgressProperty);

    /// <summary>
    /// Publishes the owning switch's rendered transition position to this visual part.
    /// </summary>
    /// <remarks>
    /// State and progress are updated independently because progress changes on animation frames,
    /// while interaction state changes only on input or selection events. Keeping this hot path
    /// narrow avoids repeatedly invalidating every state property during a transition.
    /// </remarks>
    internal void SetOwnerTransitionProgress(double progress)
    {
        SetValue(TransitionProgressPropertyKey, progress);
    }

    #endregion

    /// <summary>
    /// Updates the state snapshot exposed to styles for this named template part.
    /// </summary>
    /// <remarks>
    /// This internal entry point keeps the read-only dependency-property keys private to the part
    /// while allowing <see cref="ToggleSwitch"/> to publish one coherent state to both visuals.
    /// </remarks>
    internal void SetOwnerState(
        Orientation orientation,
        bool? isChecked,
        bool isPointerOver,
        bool isPressed)
    {
        // Publish orientation first. A style may combine it with an interaction state (for
        // example, a vertical pressed thumb), and this order prevents that trigger from briefly
        // evaluating against the previous axis while the snapshot is refreshed.
        SetValue(OrientationPropertyKey, orientation);
        SetValue(IsCheckedPropertyKey, isChecked);
        SetValue(IsPointerOverPropertyKey, isPointerOver);
        SetValue(IsPressedPropertyKey, isPressed);
    }
}
