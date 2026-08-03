// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines how a detached popup shadow reacts while the popup content is opening.
/// </summary>
public enum PopupShadowAnimationMode
{
    /// <summary>
    /// Tracks the popup content's visible bounds during WPF popup animations.
    /// </summary>
    /// <remarks>
    /// This mode updates the shadow clip on each render tick while a slide or scroll popup animation is opening.
    /// </remarks>
    DynamicContentBounds,

    /// <summary>
    /// Follows fade animations and otherwise fades the shadow in after popup content animation completes.
    /// </summary>
    /// <remarks>
    /// This mode avoids per-frame content-bound tracking for slide and scroll animations.
    /// </remarks>
    DeferredFadeIn,
}
