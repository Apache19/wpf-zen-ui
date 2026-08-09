// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines constants that specify whether the back button is visible in NavigationView.
/// </summary>
public enum NavigationViewBackButtonVisible
{
    /// <summary>
    /// Do not display the back button in NavigationView, and do not reserve space for it in layout.
    /// </summary>
    Collapsed,

    /// <summary>
    /// Display the back button in NavigationView. The button remains visible when no back operation
    /// is available and uses <see cref="NavigationView.IsBackEnabled"/> to expose its disabled state.
    /// </summary>
    Visible,

    /// <summary>
    /// Display the back button only when the navigation journal contains an entry to return to.
    /// The button is collapsed when <see cref="NavigationView.IsBackEnabled"/> is
    /// <see langword="false"/> and becomes visible when a back operation is available.
    /// </summary>
    Auto,
}
