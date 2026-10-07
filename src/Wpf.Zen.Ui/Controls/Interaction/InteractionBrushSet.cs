// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Holds reusable brushes for the input states of a surface, stroke, or foreground.
/// </summary>
/// <remarks>
/// All entries default to null. Missing state brushes fall back to Normal; use an explicit
/// transparent brush when a state should draw nothing. Dynamic resources remain live through
/// Freezable inheritance contexts. A palette with resource expressions must remain unfrozen.
/// WPF creates per-control copies of unfrozen style values so their resources resolve in the
/// owning control's scope. Change theme resources to update all controls using a recipe.
/// </remarks>
public sealed class InteractionBrushSet : Freezable
{
    /// <summary>
    /// Identifies the Normal dependency property.
    /// </summary>
    public static readonly DependencyProperty NormalProperty
        = DependencyProperty.Register(
            nameof(Normal), typeof(Brush), typeof(InteractionBrushSet),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the brush used for the resting appearance. The default is null.
    /// </summary>
    public Brush? Normal
    {
        get => (Brush?)GetValue(NormalProperty);
        set => SetValue(NormalProperty, value);
    }

    /// <summary>
    /// Identifies the PointerOver dependency property.
    /// </summary>
    public static readonly DependencyProperty PointerOverProperty
        = DependencyProperty.Register(
            nameof(PointerOver), typeof(Brush), typeof(InteractionBrushSet),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the brush used for pointer feedback. The default is null.
    /// </summary>
    public Brush? PointerOver
    {
        get => (Brush?)GetValue(PointerOverProperty);
        set => SetValue(PointerOverProperty, value);
    }

    /// <summary>
    /// Identifies the Pressed dependency property.
    /// </summary>
    public static readonly DependencyProperty PressedProperty
        = DependencyProperty.Register(
            nameof(Pressed), typeof(Brush), typeof(InteractionBrushSet),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the brush used for press feedback. The default is null.
    /// </summary>
    public Brush? Pressed
    {
        get => (Brush?)GetValue(PressedProperty);
        set => SetValue(PressedProperty, value);
    }

    /// <summary>
    /// Identifies the Disabled dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledProperty
        = DependencyProperty.Register(
            nameof(Disabled), typeof(Brush), typeof(InteractionBrushSet),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets or sets the brush used for the disabled appearance. The default is null.
    /// </summary>
    public Brush? Disabled
    {
        get => (Brush?)GetValue(DisabledProperty);
        set => SetValue(DisabledProperty, value);
    }

    /// <inheritdoc />
    protected override Freezable CreateInstanceCore()
    {
        return new InteractionBrushSet();
    }
}