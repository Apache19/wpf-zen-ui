// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using NativeListViewItem = System.Windows.Controls.ListViewItem;
using UiListViewItem = Wpf.Ui.Controls.ListViewItem;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ListView"/> with Fluent item containers and selection visuals.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:ListView ItemsSource="{Binding ...}" &gt;
///     &lt;ui:ListView.View&gt;
///         &lt;ui:GridView&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding FirstName}"
///                 Header="First Name" /&gt;
///             &lt;GridViewColumn
///                 DisplayMemberBinding="{Binding LastName}"
///                 Header="Last Name" /&gt;
///         &lt;/ui:GridView&gt;
///     &lt;/ui:ListView.View&gt;
/// &lt;/ui:ListView&gt;
/// </code>
/// </example>
public class ListView : System.Windows.Controls.ListView
{
    /// <summary>
    /// Identifies the <see cref="IsSelectionIndicatorVisible"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSelectionIndicatorVisibleProperty =
        SelectionIndicator.IsSelectionIndicatorVisibleProperty.AddOwner(
            typeof(ListView),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender
            )
        );

    static ListView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ListView),
            new FrameworkPropertyMetadata(typeof(ListView))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selected item displays an accent-colored indicator at its leading edge.
    /// </summary>
    public bool IsSelectionIndicatorVisible
    {
        get => (bool)GetValue(IsSelectionIndicatorVisibleProperty);
        set => SetValue(IsSelectionIndicatorVisibleProperty, value);
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new UiListViewItem();
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        // Match the native ListView contract: callers may supply either the Fluent container or a
        // native ListViewItem explicitly. Rejecting the native base type makes WPF wrap that item in
        // another container, which breaks routed input, selection ownership, and item styling.
        return item is NativeListViewItem;
    }

    /// <summary>
    /// Re-evaluates position-dependent container styles after the item collection changes.
    /// </summary>
    /// <remarks>
    /// WPF invokes the active <see cref="System.Windows.Controls.StyleSelector"/> while preparing a container, but it does
    /// not invoke the selector again for surviving containers after an insertion, removal, or move.
    /// Every realized index may change (including alternating rules), so all realized containers are
    /// updated. Unrealized containers are handled by the normal preparation path when virtualization
    /// brings them into view.
    /// </remarks>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        if (
            ItemContainerStyle is not null
            || ItemContainerStyleSelector is not ListItemContainerStyleSelector selector
        )
        {
            return;
        }

        ListItemContainerStyleManager.RefreshRealizedContainers(this, selector);
    }

    /// <summary>
    /// Removes selector bookkeeping before a container is recycled. This is especially important
    /// when an earlier rule returned no style: WPF then has no generator-style flag with which to
    /// clear a style that a later collection mutation may have applied.
    /// </summary>
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        ListItemContainerStyleManager.ClearContainer(element);
        base.ClearContainerForItemOverride(element, item);
    }
}