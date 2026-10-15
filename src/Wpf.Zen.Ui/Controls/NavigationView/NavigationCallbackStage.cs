// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Identifies the navigation-aware callback that failed.
/// </summary>
public enum NavigationCallbackStage
{
    /// <summary>
    /// A page operation registered through the activation context failed.
    /// </summary>
    Operation = 2,

    /// <summary>
    /// The synchronous notification that a component left navigation state.
    /// </summary>
    Exited = 0,

    /// <summary>
    /// The notification that a component entered navigation state.
    /// </summary>
    Entered,
}
