// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Supplies a palette and a shared input state to bindings inside a control template.
/// </summary>
/// <remarks>
/// Set these properties on the templated control and update State from its input state.
/// Template bindings can consume the same state for multiple fills and strokes.
/// </remarks>
public static class Interaction
{
    /// <summary>
    /// Identifies the attached Palette property; its default is null.
    /// </summary>
    public static readonly DependencyProperty PaletteProperty
        = DependencyProperty.RegisterAttached(
            "Palette", typeof(InteractionBrushSet), typeof(Interaction),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Gets the default interaction palette from the owning control.
    /// </summary>
    /// <param name="element">The control that owns the template.</param>
    /// <returns>The current attached value.</returns>
    public static InteractionBrushSet? GetPalette(DependencyObject element)
    {
        return (InteractionBrushSet?)element.GetValue(PaletteProperty);
    }

    /// <summary>
    /// Sets the default interaction palette on the owning control.
    /// </summary>
    /// <param name="element">The control that owns the template.</param>
    /// <param name="value">The attached value to apply.</param>
    public static void SetPalette(DependencyObject element, InteractionBrushSet? value)
    {
        element.SetValue(PaletteProperty, value);
    }

    /// <summary>
    /// Identifies the attached State property; its default is InteractionState.Normal.
    /// </summary>
    public static readonly DependencyProperty StateProperty
        = DependencyProperty.RegisterAttached(
            "State", typeof(InteractionState), typeof(Interaction),
            new PropertyMetadata(InteractionState.Normal),
            IsValidState
        );

    /// <summary>
    /// Gets the shared input state from the owning control.
    /// </summary>
    /// <param name="element">The control that owns the template.</param>
    /// <returns>The current attached value.</returns>
    public static InteractionState GetState(DependencyObject element)
    {
        return (InteractionState)element.GetValue(StateProperty);
    }

    /// <summary>
    /// Sets the shared input state on the owning control.
    /// </summary>
    /// <param name="element">The control that owns the template.</param>
    /// <param name="value">The attached value to apply.</param>
    public static void SetState(DependencyObject element, InteractionState value)
    {
        element.SetValue(StateProperty, value);
    }

    // Reject unsupported values before they reach a template's brush resolver.
    private static bool IsValidState(object value)
    {
        return value is InteractionState.Normal or InteractionState.PointerOver
            or InteractionState.Pressed or InteractionState.Disabled;
    }
}