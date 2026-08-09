// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Demo.Mvvm.ViewModels;

public abstract class ViewModel : ObservableObject, INavigationAware
{
    /// <inheritdoc />
    public virtual Task OnNavigationEnteredAsync()
    {
        OnNavigationEntered();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the component entering the current navigation state.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigationEntered() { }

    /// <inheritdoc />
    public virtual Task OnNavigationExitedAsync()
    {
        OnNavigationExited();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the component leaving the current navigation state.
    /// </summary>
    // ReSharper disable once MemberCanBeProtected.Global
    public virtual void OnNavigationExited() { }
}
