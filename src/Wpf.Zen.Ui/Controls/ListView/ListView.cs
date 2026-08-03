// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Input;
using NativeListViewItem = System.Windows.Controls.ListViewItem;
using UiListViewItem = Wpf.Zen.Ui.Controls.ListViewItem;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ListView"/> with Fluent item containers, selection
/// visuals, and a data-oriented item double-click contract.
/// </summary>
/// <remarks>
/// <see cref="ItemDoubleClickCommand"/> receives the item from the items source rather than its
/// visual container. This keeps the command directly usable from a view model without an
/// interaction behavior or an element-to-data conversion.
/// </remarks>
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
    /// Identifies the <see cref="ItemDoubleClickCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemDoubleClickCommandProperty =
        DependencyProperty.Register(
            nameof(ItemDoubleClickCommand),
            typeof(ICommand),
            typeof(ListView),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="ItemDoubleClick"/> routed event.
    /// </summary>
    public static readonly RoutedEvent ItemDoubleClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ItemDoubleClick),
            RoutingStrategy.Bubble,
            typeof(TypedEventHandler<ListView, ListViewItemDoubleClickEventArgs>),
            typeof(ListView)
        );

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
    /// Gets or sets the command invoked when the user double-clicks a realized item.
    /// </summary>
    /// <remarks>
    /// The command parameter is always the corresponding value from
    /// <see cref="System.Windows.Controls.ItemsControl.Items"/>.
    /// A double-click on empty space, a group header, or a scroll bar does not invoke the command.
    /// Routed commands use the clicked item container as their command target.
    /// </remarks>
    [Bindable(true)]
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public ICommand? ItemDoubleClickCommand
    {
        get => (ICommand?)GetValue(ItemDoubleClickCommandProperty);
        set => SetValue(ItemDoubleClickCommandProperty, value);
    }

    /// <summary>
    /// Occurs when the user double-clicks an item container in the <see cref="ListView"/>.
    /// </summary>
    public event TypedEventHandler<ListView, ListViewItemDoubleClickEventArgs> ItemDoubleClick
    {
        add => AddHandler(ItemDoubleClickEvent, value);
        remove => RemoveHandler(ItemDoubleClickEvent, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether the selected item displays an accent-colored indicator at its leading edge.
    /// </summary>
    public bool IsSelectionIndicatorVisible
    {
        get => (bool)GetValue(IsSelectionIndicatorVisibleProperty);
        set => SetValue(IsSelectionIndicatorVisibleProperty, value);
    }

    /// <summary>
    /// Raises <see cref="ItemDoubleClick"/> and executes <see cref="ItemDoubleClickCommand"/> for
    /// the activated item.
    /// </summary>
    /// <param name="item">The application-owned item from the items collection or source.</param>
    /// <param name="container">The realized container that received the double-click.</param>
    /// <param name="index">The current index of <paramref name="item"/>.</param>
    /// <param name="changedButton">The mouse button that produced the double-click.</param>
    /// <remarks>
    /// The source item and its container are deliberately separate. Commands receive data while
    /// event consumers that need presentation state can explicitly use the container.
    /// </remarks>
    protected virtual void OnItemDoubleClick(
        object item,
        NativeListViewItem container,
        int index,
        MouseButton changedButton
    )
    {
        var args = new ListViewItemDoubleClickEventArgs(
            ItemDoubleClickEvent,
            this,
            item,
            container,
            index,
            changedButton
        );

        RaiseEvent(args);

        if (ItemDoubleClickCommand is RoutedCommand routedCommand)
        {
            if (routedCommand.CanExecute(item, container))
            {
                routedCommand.Execute(item, container);
            }

            return;
        }

        if (ItemDoubleClickCommand?.CanExecute(item) == true)
        {
            ItemDoubleClickCommand.Execute(item);
        }
    }

    /// <inheritdoc />
    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);

        // MouseDoubleClick is synthesized independently for each Control on the original mouse
        // route. Resolve the original visual at owner level so an activation is reported once and
        // list chrome (including GridView headers and scroll bars) is excluded. Interactive content
        // can still claim its gesture by marking the input event handled before it reaches us.
        if (e.Handled || e.OriginalSource is not DependencyObject originalSource)
        {
            return;
        }

        if (ContainerFromElement(this, originalSource) is not NativeListViewItem container)
        {
            return;
        }

        int index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            return;
        }

        object item = ItemContainerGenerator.ItemFromContainer(container);
        OnItemDoubleClick(item, container, index, e.ChangedButton);
    }

    /// <inheritdoc />
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
