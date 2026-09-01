// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies the entrance transition used when a <see cref="NavigationView"/> presents a
/// navigation destination.
/// </summary>
/// <remarks>
/// The transition is applied to the navigation view's private frame rather than to the destination
/// itself. Consequently, selecting a transition does not replace animation clocks or render
/// transforms owned by application content.
/// </remarks>
public enum NavigationTransition
{
    /// <summary>Presents the destination without an entrance animation.</summary>
    None,

    /// <summary>Reveals the destination by increasing the frame opacity.</summary>
    FadeIn,

    /// <summary>Reveals the destination while moving the frame upward from below.</summary>
    FadeInWithSlide,

    /// <summary>Moves the frame upward from below without changing its opacity.</summary>
    SlideBottom,

    /// <summary>Moves the frame into place from the right.</summary>
    SlideRight,

    /// <summary>Moves the frame into place from the left.</summary>
    SlideLeft,
}