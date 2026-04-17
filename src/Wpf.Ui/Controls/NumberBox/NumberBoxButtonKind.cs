// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a control that can be used to edit numbers.
/// </summary>
public enum NumberBoxButtonKind
{
    /// <summary>
    /// The button does not have any functionality.
    /// </summary>
    None,

    /// <summary>
    /// The button clears the value of the <see cref="NumberBox"/> when clicked.
    /// </summary>
    Clear,

    /// <summary>
    /// The button increments the value of the <see cref="NumberBox"/> when clicked.
    /// </summary>
    Increment,

    /// <summary>
    /// The button decrements the value of the <see cref="NumberBox"/> when clicked.
    /// </summary>
    Decrement
}