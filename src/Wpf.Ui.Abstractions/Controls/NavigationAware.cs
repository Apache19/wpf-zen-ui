// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Ui.Abstractions.Controls;

/// <summary>
/// Provides a base class for navigation-aware components.
/// </summary>
public abstract class NavigationAware : INavigationAware
{
    /// <inheritdoc />
    public virtual Task OnNavigationEnteredAsync()
    {
        OnNavigationEntered();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles the component entering the current navigation state after a successful navigation.
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
