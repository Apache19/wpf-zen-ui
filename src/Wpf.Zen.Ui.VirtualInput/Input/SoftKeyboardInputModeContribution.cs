// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Contributes presentation, layout policy and a service-specific fallback command.
/// </summary>
public sealed class SoftKeyboardInputModeContribution
{
    /// <summary>
    /// Creates a contribution. Every capability is optional and platform presentation wins by
    /// default.
    /// </summary>
    /// <param name="button">The extension's button, or null to use platform presentation.</param>
    /// <param name="transition">An opaque command interpreted by the contributing adapter.</param>
    /// <param name="layoutProvider">An optional observed-data-dependent layout policy.</param>
    /// <param name="overridePlatformButton">
    /// Whether a nonnull button overrides platform content.
    /// </param>
    public SoftKeyboardInputModeContribution(
        SoftKeyboardInputModeButton? button = null,
        SoftKeyboardInputModeTransition? transition = null,
        ISoftKeyboardLayoutProvider? layoutProvider = null,
        bool overridePlatformButton = false)
    {
        Button = button;
        Transition = transition;
        LayoutProvider = layoutProvider;
        OverridePlatformButton = overridePlatformButton;
    }

    /// <summary>
    /// Gets a contribution with no extension capabilities.
    /// </summary>
    public static SoftKeyboardInputModeContribution Empty { get; } = new();

    /// <summary>
    /// Gets the extension button. Ordinary fallback presentation also requires a transition.
    /// </summary>
    public SoftKeyboardInputModeButton? Button { get; }

    /// <summary>
    /// Gets the fallback command descriptor, interpreted only by this contribution's adapter.
    /// </summary>
    public SoftKeyboardInputModeTransition? Transition { get; }

    /// <summary>
    /// Gets the optional layout policy. Reuse provider instances while their behavior is unchanged.
    /// </summary>
    public ISoftKeyboardLayoutProvider? LayoutProvider { get; }

    /// <summary>
    /// Gets whether a supplied button overrides platform presentation. Defaults to false.
    /// This affects only appearance; the platform command retains execution priority.
    /// </summary>
    public bool OverridePlatformButton { get; }
}
