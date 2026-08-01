// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Coordinates positional styles for both WPF UI list controls. Keeping this state outside either
/// control prevents their identical container-lifecycle workaround from drifting over time.
/// </summary>
internal static class ListItemContainerStyleManager
{
    private sealed class PositionStyleSelection(
        ListItemContainerStyleSelector selector,
        Style? appliedStyle
    )
    {
        public Style? AppliedStyle { get; set; } = appliedStyle;

        public ListItemContainerStyleSelector Selector { get; } = selector;
    }

    private static readonly DependencyProperty PositionStyleSelectionProperty =
        DependencyProperty.RegisterAttached(
            "PositionStyleSelection",
            typeof(PositionStyleSelection),
            typeof(ListItemContainerStyleManager),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Records that WPF consulted the selector. Initially explicitly styled containers never receive
    /// this marker, which lets later collection refreshes preserve the caller-owned style.
    /// </summary>
    internal static void RecordSelection(
        DependencyObject container,
        ListItemContainerStyleSelector selector,
        Style? appliedStyle
    ) =>
        container.SetCurrentValue(
            PositionStyleSelectionProperty,
            new PositionStyleSelection(selector, appliedStyle)
        );

    /// <summary>
    /// Re-evaluates only realized containers. Probing every logical index would make collection
    /// changes O(Items.Count) and undermine virtualization for large lists.
    /// </summary>
    internal static void RefreshRealizedContainers(
        ItemsControl owner,
        ListItemContainerStyleSelector selector
    ) => RefreshVisualSubtree(owner, owner, selector);

    private static void RefreshVisualSubtree(
        ItemsControl owner,
        DependencyObject visualParent,
        ListItemContainerStyleSelector selector
    )
    {
        int visualChildCount = VisualTreeHelper.GetChildrenCount(visualParent);
        for (int childIndex = 0; childIndex < visualChildCount; childIndex++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(visualParent, childIndex);
            int itemIndex = owner.ItemContainerGenerator.IndexFromContainer(child);
            if (
                itemIndex >= 0
                && child is FrameworkElement container
                && GetPositionStyleSelection(container) is { } selection
                && ReferenceEquals(selection.Selector, selector)
            )
            {
                object localStyle = container.ReadLocalValue(FrameworkElement.StyleProperty);
                bool stillOwnsStyle =
                    selection.AppliedStyle is null
                        ? localStyle == DependencyProperty.UnsetValue
                        : ReferenceEquals(localStyle, selection.AppliedStyle);
                if (!stillOwnsStyle)
                {
                    // A Style assigned after preparation is a deliberate caller override. Stop
                    // managing it, matching WPF's treatment of an initially styled item container.
                    container.ClearValue(PositionStyleSelectionProperty);
                }
                else
                {
                    Style? style = selector.SelectStyle(itemIndex, owner.Items.Count);
                    if (style is null)
                    {
                        container.ClearValue(FrameworkElement.StyleProperty);
                    }
                    else
                    {
                        // WPF stores selector results as local Style values. Use the same value source
                        // so precedence remains identical to the initial generator preparation.
                        container.SetValue(FrameworkElement.StyleProperty, style);
                    }

                    selection.AppliedStyle = style;
                }
            }

            // Item content may contain nested list controls. IndexFromContainer verifies ownership,
            // so traversal cannot accidentally restyle a descendant control's containers.
            RefreshVisualSubtree(owner, child, selector);
        }
    }

    /// <summary>
    /// Removes bookkeeping before recycling. This also clears a style applied during a later refresh
    /// when the selector originally returned null and WPF therefore has no generator-style flag.
    /// </summary>
    internal static void ClearContainer(DependencyObject container)
    {
        if (GetPositionStyleSelection(container) is not { } selection)
        {
            return;
        }

        object localStyle = container.ReadLocalValue(FrameworkElement.StyleProperty);
        if (
            selection.AppliedStyle is null
                ? localStyle == DependencyProperty.UnsetValue
                : ReferenceEquals(localStyle, selection.AppliedStyle)
        )
        {
            container.ClearValue(FrameworkElement.StyleProperty);
        }

        container.ClearValue(PositionStyleSelectionProperty);
    }

    private static PositionStyleSelection? GetPositionStyleSelection(
        DependencyObject container
    ) => (PositionStyleSelection?)container.GetValue(PositionStyleSelectionProperty);
}