// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Abstractions.Controls;

/// <summary>
/// A component whose ViewModel is separate from the DataContext and can be navigated by INavigationView.
/// </summary>
/// <typeparam name="T">
/// The type of the ViewModel associated with the view. It may implement
/// <see cref="INavigationAware"/> to receive successful navigation entry and exit notifications.
/// </typeparam>
public interface INavigableView<out T>
{
    /// <summary>
    /// Gets the view model used by the view.
    /// It may implement <see cref="INavigationAware"/> to participate in the navigation lifecycle.
    /// </summary>
    T ViewModel { get; }
}
