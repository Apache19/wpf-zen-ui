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
/// <see cref="INavigationAware"/>, <see cref="IAsyncNavigationAware"/> or
/// <see cref="INavigationActivationAware"/> to participate in committed page activations.
/// </typeparam>
public interface INavigableView<out T>
{
    /// <summary>
    /// Gets the view model used by the view.
    /// Lifecycle participants are captured before entry and exited in reverse entry order.
    /// The same object exposed through DataContext and this property participates only once.
    /// </summary>
    T ViewModel { get; }
}
