// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines a navigable view whose view model is exposed independently of its WPF data context.
/// </summary>
/// <typeparam name="T">
/// The type of the view model associated with the view. It may implement
/// <see cref="INavigationAware"/>, <see cref="IAsyncNavigationAware"/>, or
/// <see cref="ICancellableAsyncNavigationAware"/> to receive successful navigation entry and exit
/// notifications.
/// </typeparam>
public interface INavigableView<out T>
{
    /// <summary>
    /// Gets the view model used by the view.
    /// It may implement <see cref="INavigationAware"/>, <see cref="IAsyncNavigationAware"/>, or
    /// <see cref="ICancellableAsyncNavigationAware"/> to participate in the navigation lifecycle.
    /// </summary>
    T ViewModel { get; }
}
