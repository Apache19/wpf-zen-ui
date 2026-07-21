// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Identifies legacy browser-navigation input that a <see cref="NavigationView"/> can suppress
/// before it reaches navigated content or an ancestor.
/// </summary>
/// <remarks>
/// WPF's navigation stack inherits browser-era gestures from its original XBAP and document
/// navigation model. Modern desktop applications may either reuse those inputs for application
/// commands or deliberately suppress them. The flags keep that policy explicit and granular;
/// merely hosting content in a <see cref="NavigationView"/> does not otherwise consume raw input.
/// </remarks>
[Flags]
public enum NavigationViewInput
{
    /// <summary>
    /// Does not suppress any navigation input.
    /// </summary>
    None = 0,

    /// <summary>
    /// Suppresses F5 and <see cref="System.Windows.Input.NavigationCommands.Refresh"/>, including
    /// refresh commands produced by <c>WM_APPCOMMAND</c> devices.
    /// </summary>
    Refresh = 1 << 0,

    /// <summary>
    /// Suppresses XButton1 and <see cref="System.Windows.Input.NavigationCommands.BrowseBack"/>,
    /// including the command's default keyboard and <c>WM_APPCOMMAND</c> gestures.
    /// </summary>
    BrowserBack = 1 << 1,

    /// <summary>
    /// Suppresses XButton2 and <see cref="System.Windows.Input.NavigationCommands.BrowseForward"/>,
    /// including the command's default keyboard and <c>WM_APPCOMMAND</c> gestures.
    /// </summary>
    BrowserForward = 1 << 2,

    /// <summary>
    /// Suppresses both browser back and browser forward input.
    /// </summary>
    BrowserNavigation = BrowserBack | BrowserForward,

    /// <summary>
    /// Suppresses all legacy browser-navigation input recognized by <see cref="NavigationView"/>.
    /// </summary>
    All = Refresh | BrowserNavigation,
}
