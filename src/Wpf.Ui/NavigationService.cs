// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Abstractions;
using Wpf.Ui.Controls;

namespace Wpf.Ui;

/// <summary>
/// A service that provides methods related to navigation.
/// </summary>
public partial class NavigationService(INavigationViewPageProvider pageProvider) : INavigationService
{
    static NavigationService()
    {
        LibraryInitializer.EnsureInitialized();
    }

    /// <summary>
    /// Gets or sets the control representing navigation.
    /// </summary>
    protected INavigationView? NavigationControl { get; set; }

    /// <inheritdoc />
    public INavigationView GetNavigationControl()
    {
        return NavigationControl ?? throw new ArgumentNullException(nameof(NavigationControl));
    }

    /// <inheritdoc />
    public void SetNavigationControl(INavigationView navigation)
    {
        NavigationControl = navigation;
        NavigationControl.SetPageProviderService(pageProvider);
    }

    /// <inheritdoc />
    public bool Navigate(Type pageType) => Navigate(new NavigationKey(pageType));

    /// <inheritdoc />
    public bool Navigate(Type pageType, object? dataContext) => Navigate(new NavigationKey(pageType), dataContext);

    /// <inheritdoc />
    public bool Navigate(NavigationKey navigationKey)
    {
        ThrowIfNavigationControlIsNull();
        return NavigationControl!.Navigate(navigationKey);
    }

    /// <inheritdoc />
    public bool Navigate(NavigationKey navigationKey, object? dataContext)
    {
        ThrowIfNavigationControlIsNull();
        return NavigationControl!.Navigate(navigationKey, dataContext);
    }

    /// <inheritdoc />
    public bool GoBack()
    {
        ThrowIfNavigationControlIsNull();
        return NavigationControl!.GoBack();
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType) => NavigateWithHierarchy(new NavigationKey(pageType));

    /// <inheritdoc />
    public bool NavigateWithHierarchy(Type pageType, object? dataContext)
    {
        return NavigateWithHierarchy(new NavigationKey(pageType), dataContext);
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(NavigationKey navigationKey)
    {
        ThrowIfNavigationControlIsNull();
        return NavigationControl!.NavigateWithHierarchy(navigationKey);
    }

    /// <inheritdoc />
    public bool NavigateWithHierarchy(NavigationKey navigationKey, object? dataContext)
    {
        ThrowIfNavigationControlIsNull();
        return NavigationControl!.NavigateWithHierarchy(navigationKey, dataContext);
    }

    protected void ThrowIfNavigationControlIsNull()
    {
        if (NavigationControl is null)
        {
            throw new ArgumentNullException(nameof(NavigationControl));
        }
    }
}
