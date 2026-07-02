// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes the detached shadow window created for a <see cref="Popup"/> or <see cref="ContextMenu"/>.
/// </summary>
/// <remarks>
/// The popup content itself must not reserve transparent shadow space when this chrome is used. The shadow is rendered
/// by a separate transparent HWND that follows the popup HWND.
/// </remarks>
public sealed class PopupShadowChrome : Freezable
{
    /// <summary>
    /// Identifies the <see cref="ShadowThickness"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ShadowThicknessProperty = DependencyProperty.Register(
        nameof(ShadowThickness),
        typeof(Thickness),
        typeof(PopupShadowChrome),
        new PropertyMetadata(new Thickness(30D)),
        ValidateShadowThickness
    );

    /// <summary>
    /// Identifies the <see cref="BlurRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty BlurRadiusProperty = DependencyProperty.Register(
        nameof(BlurRadius),
        typeof(double),
        typeof(PopupShadowChrome),
        new PropertyMetadata(20D),
        value => value is double doubleValue && IsValidDouble(doubleValue)
    );

    /// <summary>
    /// Identifies the <see cref="Direction"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction),
        typeof(double),
        typeof(PopupShadowChrome),
        new PropertyMetadata(270D),
        value => value is double doubleValue && IsValidDouble(doubleValue)
    );

    /// <summary>
    /// Identifies the <see cref="ShadowDepth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ShadowDepthProperty = DependencyProperty.Register(
        nameof(ShadowDepth),
        typeof(double),
        typeof(PopupShadowChrome),
        new PropertyMetadata(10D),
        value => value is double doubleValue && IsValidDouble(doubleValue)
    );

    /// <summary>
    /// Identifies the <see cref="Opacity"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OpacityProperty = DependencyProperty.Register(
        nameof(Opacity),
        typeof(double),
        typeof(PopupShadowChrome),
        new PropertyMetadata(0.135D),
        value => value is double doubleValue
              && IsValidDouble(doubleValue)
              && doubleValue is >= 0D and <= 1D
    );

    /// <summary>
    /// Identifies the <see cref="Color"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColorProperty = DependencyProperty.Register(
        nameof(Color),
        typeof(Color),
        typeof(PopupShadowChrome),
        new PropertyMetadata(Color.FromRgb(0x20, 0x20, 0x20))
    );

    /// <summary>
    /// Identifies the <see cref="AnimationMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AnimationModeProperty = DependencyProperty.Register(
        nameof(AnimationMode),
        typeof(PopupShadowAnimationMode),
        typeof(PopupShadowChrome),
        new PropertyMetadata(PopupShadowAnimationMode.DynamicContentBounds),
        value => value is
            PopupShadowAnimationMode.DynamicContentBounds
         or PopupShadowAnimationMode.DeferredFadeIn
    );

    /// <summary>
    /// Gets or sets the logical thickness reserved around the popup HWND for the detached shadow.
    /// </summary>
    /// <remarks>
    /// Increase this value when larger blur radius or shadow depth values need more room outside the popup bounds.
    /// </remarks>
    public Thickness ShadowThickness
    {
        get { return (Thickness)GetValue(ShadowThicknessProperty); }
        set { SetValue(ShadowThicknessProperty, value); }
    }

    /// <summary>
    /// Gets or sets the blur radius applied to the shadow border.
    /// </summary>
    /// <remarks>
    /// This value is passed to <see cref="DropShadowEffect.BlurRadius"/>.
    /// </remarks>
    public double BlurRadius
    {
        get { return (double)GetValue(BlurRadiusProperty); }
        set { SetValue(BlurRadiusProperty, value); }
    }

    /// <summary>
    /// Gets or sets the direction, in degrees, in which the shadow is cast.
    /// </summary>
    /// <remarks>
    /// This value is passed to <see cref="DropShadowEffect.Direction"/>.
    /// </remarks>
    public double Direction
    {
        get { return (double)GetValue(DirectionProperty); }
        set { SetValue(DirectionProperty, value); }
    }

    /// <summary>
    /// Gets or sets the distance from the shadow border to the start of the shadow.
    /// </summary>
    /// <remarks>
    /// This value is passed to <see cref="DropShadowEffect.ShadowDepth"/>.
    /// </remarks>
    public double ShadowDepth
    {
        get { return (double)GetValue(ShadowDepthProperty); }
        set { SetValue(ShadowDepthProperty, value); }
    }

    /// <summary>
    /// Gets or sets the opacity of the detached shadow.
    /// </summary>
    /// <remarks>
    /// Valid values are from <c>0</c> to <c>1</c>.
    /// </remarks>
    public double Opacity
    {
        get { return (double)GetValue(OpacityProperty); }
        set { SetValue(OpacityProperty, value); }
    }

    /// <summary>
    /// Gets or sets the color of the detached shadow.
    /// </summary>
    public Color Color
    {
        get { return (Color)GetValue(ColorProperty); }
        set { SetValue(ColorProperty, value); }
    }

    /// <summary>
    /// Gets or sets how the detached shadow is shown while the popup content is opening.
    /// </summary>
    /// <remarks>
    /// Use <see cref="PopupShadowAnimationMode.DynamicContentBounds"/> for the closest match to slide and scroll popup
    /// animations. Use <see cref="PopupShadowAnimationMode.DeferredFadeIn"/> when a simpler delayed fade is preferred.
    /// </remarks>
    public PopupShadowAnimationMode AnimationMode
    {
        get { return (PopupShadowAnimationMode)GetValue(AnimationModeProperty); }
        set { SetValue(AnimationModeProperty, value); }
    }

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore()
    {
        return new PopupShadowChrome();
    }

    /// <summary>
    /// Creates the WPF effect used by the detached shadow visual.
    /// </summary>
    /// <returns>A configured <see cref="DropShadowEffect"/> instance.</returns>
    internal DropShadowEffect CreateEffect()
    {
        return new DropShadowEffect
        {
            BlurRadius  = BlurRadius,
            Direction   = Direction,
            Opacity     = Opacity,
            ShadowDepth = ShadowDepth,
            Color       = Color,
        };
    }

    private static bool ValidateShadowThickness(object? value)
    {
        if (value is not Thickness thickness)
        {
            return false;
        }

        return IsValidDouble(thickness.Left)
            && IsValidDouble(thickness.Top)
            && IsValidDouble(thickness.Right)
            && IsValidDouble(thickness.Bottom);
    }

    private static bool IsValidDouble(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0D;
    }
}
