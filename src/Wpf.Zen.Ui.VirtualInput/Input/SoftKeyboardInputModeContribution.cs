// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Supplies independently optional state, presentation, and transition facets for an input mode.
/// </summary>
/// <remarks>
/// Each facet participates in its own extension-first fallback chain. An adapter can therefore
/// interpret state while retaining a platform presentation, replace presentation while retaining
/// a platform command, or provide a transition only when the platform has no suitable command.
/// </remarks>
public sealed class SoftKeyboardInputModeContribution
{
    /// <summary>
    /// Initializes a new input-mode contribution.
    /// </summary>
    /// <param name="mode">The semantic mode-state facet.</param>
    /// <param name="presentation">The mode-key presentation facet.</param>
    /// <param name="transition">The mode-transition facet.</param>
    public SoftKeyboardInputModeContribution(
        SoftKeyboardInputModeFacet<SoftKeyboardInputMode> mode = default,
        SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton> presentation = default,
        SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition> transition = default)
    {
        Mode         = mode;
        Presentation = presentation;
        Transition   = transition;
    }

    /// <summary>
    /// Gets a contribution that leaves every facet to subsequent providers.
    /// </summary>
    public static SoftKeyboardInputModeContribution Empty { get; } = new();

    /// <summary>
    /// Gets the independently resolved semantic mode-state facet.
    /// </summary>
    public SoftKeyboardInputModeFacet<SoftKeyboardInputMode> Mode { get; }

    /// <summary>
    /// Gets the independently resolved mode-key presentation facet.
    /// </summary>
    public SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton> Presentation { get; }

    /// <summary>
    /// Gets the independently resolved mode-transition facet.
    /// </summary>
    public SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition> Transition { get; }
}
