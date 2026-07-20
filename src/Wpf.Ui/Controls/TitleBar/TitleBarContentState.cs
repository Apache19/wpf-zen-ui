// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Describes the presentation state of content assigned to a title-bar content slot.
/// </summary>
/// <remarks>
/// Unlike a simple null check, this state distinguishes a missing value from a direct
/// <see cref="UIElement"/> whose <see cref="UIElement.Visibility"/> prevents it from being
/// rendered. Title-bar templates can therefore adapt spacing without having to know the concrete
/// content type or reproduce the state-tracking logic.
/// </remarks>
public enum TitleBarContentState
{
    /// <summary>
    /// The content slot has no assigned value.
    /// </summary>
    Empty,

    /// <summary>
    /// The content is present and participates in rendering and layout.
    /// </summary>
    Visible,

    /// <summary>
    /// The content is hidden. A template may preserve its layout space or collapse the containing
    /// slot when an empty title-bar region would be undesirable.
    /// </summary>
    Hidden,

    /// <summary>
    /// The content is collapsed and does not participate in layout.
    /// </summary>
    Collapsed,
}
