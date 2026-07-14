// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Identifies the navigation command represented by a <see cref="NavigationViewButton"/>.
/// </summary>
/// <remarks>
/// The role is presentation-neutral. Templates can combine it with
/// <see cref="NavigationViewButton.Placement"/> and navigation state properties without depending
/// on a particular icon, automation identifier, or generated element name.
/// </remarks>
public enum NavigationViewButtonRole
{
    /// <summary>
    /// The button is not one of the commands managed by a <see cref="NavigationView"/>.
    /// </summary>
    None,

    /// <summary>
    /// The button navigates to the previous journal entry.
    /// </summary>
    Back,

    /// <summary>
    /// The button opens or closes the navigation pane.
    /// </summary>
    PaneToggle,
}
