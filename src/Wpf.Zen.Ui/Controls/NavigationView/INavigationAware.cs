// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Synchronously participates in the successful entry lifecycle of a navigation host.
/// </summary>
/// <remarks>
/// Enters the view before its DataContext model for each activation at the Loaded scheduling
/// boundary.
/// Visual reload creates a new activation; an object occupying multiple roles is invoked only once.
/// </remarks>
public interface INavigationAware : INavigationExited
{
    /// <summary>
    /// Handles the component entering the current navigation state.
    /// </summary>
    /// <remarks>
    /// The navigation host invokes this callback only after it has successfully committed the
    /// destination and attached its visual tree. Failed or stopped requests do not enter the
    /// navigation lifecycle.
    /// </remarks>
    void OnNavigationEntered();
}
