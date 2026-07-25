// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Defines how an element hosted by a <see cref="TitleBar"/> participates in native window hit testing.
/// </summary>
/// <remarks>
/// These values describe UI intent and deliberately do not reuse Win32 HT constants. FluentWindow owns the
/// platform mapping so templates and application content do not depend on native numeric values.
/// </remarks>
public enum TitleBarHitTestRole
{
    /// <summary>
    /// The element does not define non-client behavior.
    /// </summary>
    None,

    /// <summary>
    /// The element receives ordinary WPF client-area input.
    /// </summary>
    Client,

    /// <summary>
    /// The element acts as draggable caption content.
    /// </summary>
    Caption,

    /// <summary>
    /// The element represents the window icon and its opt-in non-client interactions.
    /// </summary>
    /// <remarks>
    /// FluentWindow uses this role for double-click close and title-bar context-menu hit testing. A single left
    /// click deliberately does not open the legacy icon system menu.
    /// </remarks>
    Icon,

    /// <summary>
    /// The element represents the minimize caption button.
    /// </summary>
    MinimizeButton,

    /// <summary>
    /// The element represents the maximize or restore caption button.
    /// </summary>
    MaximizeButton,

    /// <summary>
    /// The element represents the close caption button.
    /// </summary>
    CloseButton,

    /// <summary>
    /// The element represents the context-help caption button.
    /// </summary>
    HelpButton,
}
