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

    /// <summary>Gets whether the navigation item icon should use its filled variant.</summary>
    public static bool GetIsIconFilled(DependencyObject element) =>
        (bool)element.GetValue(IsIconFilledProperty);

    /// <summary>Sets whether the navigation item icon should use its filled variant.</summary>
    public static void SetIsIconFilled(DependencyObject element, bool value) =>
        element.SetValue(IsIconFilledProperty, value);

    internal static void SetIsActive(NavigationViewItem item, INavigationView navigationView, bool isActive)
    {
        item.SynchronizePaneState(navigationView.PaneDisplayMode, navigationView.IsPaneOpen);
        item.SetCurrentValue(NavigationViewItem.IsActiveProperty, isActive);

        if (isActive)
        {
            if (!item.IsPaneOpen && item.NavigationViewItemParent is not null)
            {
                item.NavigationViewItemParent.Activate(navigationView);
            }

            if (item.NavigationViewItemParent is not null)
            {
                item.NavigationViewItemParent.IsExpanded =
                    item.IsPaneOpen && item.PaneDisplayMode != NavigationViewPaneDisplayMode.Top;
            }

            return;
        }

        item.NavigationViewItemParent?.Deactivate(navigationView);

        if (!item.IsPaneOpen && item.HasMenuItems)
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
            toggleButton.TemplatedParent is not NavigationViewItem item
            || item.NavigationViewHost is not { } navigationView
        )
        {
            return;
        }

        foreach (var menuItem in item.MenuItems.OfType<INavigationViewItem>())
        {
            if (!menuItem.IsActive)
            {
                continue;
            }

            if (toggleButton.IsChecked == true)
            {
                item.Deactivate(navigationView);
            }
            else
            {
                item.Activate(navigationView);
            }

            break;
        }
    }
}
