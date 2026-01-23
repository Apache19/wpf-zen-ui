// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Defines the semantic identity of an element within the title bar.
/// </summary>
/// <remarks>
/// The values of this enumeration map directly to standard Windows non-client
/// hit-test constants (HT*). This allows title bar elements to participate
/// seamlessly in non-client hit testing and window message processing.
///
/// These identities are used internally to determine how interactions with
/// specific title bar elements should be translated into window behaviors.
/// </remarks>
public enum TitleBarElementIdentity : uint
{
    /// <summary>
    /// No specific identity.
    /// </summary>
    Unknown = PInvoke.HTNOWHERE,

    /// <summary>
    /// Minimize button.
    /// </summary>
    MinimizeButton = PInvoke.HTMINBUTTON,

    /// <summary>
    /// Close button.
    /// </summary>
    CloseButton = PInvoke.HTCLOSE,

    /// <summary>
    /// Maximize or restore button.
    /// </summary>
    MaximizeButton = PInvoke.HTMAXBUTTON,

    /// <summary>
    /// Window icon.
    /// </summary>
    Icon = PInvoke.HTSYSMENU,

    /// <summary>
    /// Help button.
    /// </summary>
    HelpButton = PInvoke.HTHELP,

    /// <summary>
    /// Caption (draggable title bar) area.
    /// </summary>
    Caption = PInvoke.HTCAPTION,

    /// <summary>
    /// User-defined content hosted within the title bar.
    /// </summary>
    UserContent = PInvoke.HTCLIENT,
}