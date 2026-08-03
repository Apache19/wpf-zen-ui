// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines the host in which a <see cref="NavigationViewButton"/> is placed.
/// </summary>
public enum NavigationViewButtonPlacement
{
    /// <summary>
    /// The button has no host-specific treatment.
    /// </summary>
    None,

    /// <summary>
    /// The button is placed in the <see cref="NavigationView"/> pane.
    /// </summary>
    Pane,

    /// <summary>
    /// The button is placed in a <see cref="TitleBar"/>.
    /// </summary>
    TitleBar,
}
