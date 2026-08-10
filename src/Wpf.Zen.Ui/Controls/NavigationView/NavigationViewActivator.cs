// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Internal activator for creating content instances of the navigation view items.
/// </summary>
internal static class NavigationViewActivator
{
    /// <summary>
    /// Creates new instance of type derived from <see cref="FrameworkElement"/>.
    /// </summary>
    /// <param name="pageType"><see cref="FrameworkElement"/> to instantiate.</param>
    /// <param name="dataContext">
    /// Optional context. A constructor accepting its exact runtime type is preferred; otherwise the
    /// value is assigned to <see cref="FrameworkElement.DataContext"/> after parameterless activation.
    /// </param>
    /// <returns>Instance of the <see cref="FrameworkElement"/> object or <see langword="null"/>.</returns>
    public static FrameworkElement? CreateInstance(Type pageType, object? dataContext = null)
    {
        if (!typeof(FrameworkElement).IsAssignableFrom(pageType))
        {
            throw new InvalidCastException(
                $"TargetPageType must be derived from {typeof(FrameworkElement)}. {pageType} is not."
            );
        }

        if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            return new Page
            {
                Content = new TextBlock
                {
                    Text =
                        "Pages are not rendered while using the Designer. Edit the page template directly.",
                },
            };
        }

        FrameworkElement? instance;

        if (dataContext != null)
        {
            instance = InvokeElementConstructor(pageType, dataContext);

            if (instance != null)
            {
                return instance;
            }
        }

        ConstructorInfo emptyConstructor =
            FindParameterlessConstructor(pageType)
            ?? throw new InvalidOperationException(
                $"The {pageType} page does not have a public parameterless constructor. "
                    + "Attach a dependency-injection navigation scope to use constructor injection, "
                    + $"or supply an {typeof(INavigationViewPageProvider)} to {typeof(NavigationScope)}."
            );
        instance = emptyConstructor.Invoke(null) as FrameworkElement;
        SetDataContext(instance, dataContext);

        return instance;
    }

    private static FrameworkElement? InvokeElementConstructor(Type tPage, object? dataContext)
    {
        ConstructorInfo? ctor = dataContext is null
            ? tPage.GetConstructor(Type.EmptyTypes)
            : tPage.GetConstructor(new[] { dataContext.GetType() });

        return ctor?.Invoke(new[] { dataContext }) as FrameworkElement;
    }

    private static ConstructorInfo? FindParameterlessConstructor(Type? tPage)
    {
        return tPage?.GetConstructor(Type.EmptyTypes);
    }

    private static void SetDataContext(FrameworkElement? element, object? dataContext)
    {
        if (element != null && dataContext != null)
        {
            element.DataContext = dataContext;
        }
    }
}
