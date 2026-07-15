// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides the interaction behavior used by <see cref="NavigationViewItem"/> control templates.
/// </summary>
public static class NavigationViewItemTemplateBehavior
{
    /// <summary>
    /// Identifies the attached property that lets a template render a symbol icon in its filled state.
    /// </summary>
    public static readonly DependencyProperty IsIconFilledProperty = DependencyProperty.RegisterAttached(
        "IsIconFilled",
        typeof(bool),
        typeof(NavigationViewItemTemplateBehavior),
        new PropertyMetadata(false, OnIsIconFilledChanged)
    );

    /// <summary>
    /// Helper for getting <see cref="IsIconFilledProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="IsIconFilledProperty"/> from.
    /// </param>
    /// <returns>IsIconFilled property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static bool GetIsIconFilled(DependencyObject element)
    {
        return (bool)element.GetValue(IsIconFilledProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="IsIconFilledProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="IsIconFilledProperty"/> on.
    /// </param>
    /// <param name="value">IsIconFilled property value.</param>
    public static void SetIsIconFilled(DependencyObject element, bool value)
    {
        element.SetValue(IsIconFilledProperty, value);
    }

    internal static void SetIsActive(NavigationViewItem item, INavigationView navigationView, bool isActive)
    {
        item.SynchronizePaneState(navigationView.PaneDisplayMode, navigationView.IsPaneOpen);
        item.SetCurrentValue(NavigationViewItem.IsActiveProperty, isActive);

        if (isActive)
        {
            if (item is { IsPaneOpen: false, NavigationViewItemParent: not null })
            {
                item.NavigationViewItemParent.Activate(navigationView);
            }

            item.NavigationViewItemParent?.IsExpanded =
                item.IsPaneOpen
             && item.PaneDisplayMode != NavigationViewPaneDisplayMode.Top;

            return;
        }

        item.NavigationViewItemParent?.Deactivate(navigationView);

        if (item is { IsPaneOpen: false, HasMenuItems: true })
        {
            item.SetCurrentValue(NavigationViewItem.IsExpandedProperty, false);
        }
    }

    internal static void UpdateIcon(NavigationViewItem item, SymbolIcon? oldIcon = null)
    {
        if (oldIcon is not null && !ReferenceEquals(oldIcon, item.Icon))
        {
            oldIcon.SetCurrentValue(SymbolIcon.FilledProperty, false);
        }

        if (item.Icon is SymbolIcon symbolIcon)
        {
            symbolIcon.SetCurrentValue(SymbolIcon.FilledProperty, GetIsIconFilled(item));
        }
    }

    private static void OnIsIconFilledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationViewItem item)
        {
            UpdateIcon(item);
        }
    }

    internal static void OnItemExpanderClick(NavigationViewItemExpander toggleButton)
    {
        if (
            toggleButton.TemplatedParent is
            not NavigationViewItem { NavigationViewHost: { } navigationView } item
        )
        {
            return;
        }

        if (item.MenuItems.OfType<INavigationViewItem>().Any(menuItem => menuItem.IsActive))
        {
            if (toggleButton.IsChecked == true)
            {
                item.Deactivate(navigationView);
            }
            else
            {
                item.Activate(navigationView);
            }
        }
    }
}
