// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Input;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;
using UiListBoxItem = Wpf.Zen.Ui.Controls.ListBoxItem;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ListBox"/> with Fluent item containers and a
/// data-oriented item double-click contract.
/// </summary>
/// <remarks>
/// <see cref="ItemDoubleClickCommand"/> receives the item from the items source rather than its
/// visual container. This keeps the command directly usable from a view model without an
/// interaction behavior or an element-to-data conversion.
/// </remarks>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NativeListBoxItem))]
public class ListBox : System.Windows.Controls.ListBox
{
    /// <summary>
    /// Identifies the <see cref="ItemDoubleClickCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ItemDoubleClickCommandProperty =
        DependencyProperty.Register(
            nameof(ItemDoubleClickCommand),
            typeof(ICommand),
            typeof(ListBox),
            new PropertyMetadata(null)
        );

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
    /// Identifies the <see cref="ItemDoubleClick"/> routed event.
    /// </summary>
    public static readonly RoutedEvent ItemDoubleClickEvent =
        EventManager.RegisterRoutedEvent(
            nameof(ItemDoubleClick),
            RoutingStrategy.Bubble,
            typeof(TypedEventHandler<ListBox, ListBoxItemDoubleClickEventArgs>),
            typeof(ListBox)
        );

    /// <summary>
    /// Occurs when the user double-clicks an item container in the <see cref="ListBox"/>.
    /// </summary>
    public event TypedEventHandler<ListBox, ListBoxItemDoubleClickEventArgs> ItemDoubleClick
    {
        add => AddHandler(ItemDoubleClickEvent, value);
        remove => RemoveHandler(ItemDoubleClickEvent, value);
    }

    static ListBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ListBox),
            new FrameworkPropertyMetadata(typeof(ListBox))
        );
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
        NativeListBoxItem container,
        int index,
        MouseButton changedButton
    )
    {
        var args = new ListBoxItemDoubleClickEventArgs(
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

        // WPF synthesizes MouseDoubleClick as a Direct event for each Control on the original
        // mouse route. Resolving OriginalSource through the item-container generator lets the
        // ListBox react exactly once at owner level and excludes empty chrome automatically.
        // Respecting Handled also preserves the normal WPF escape hatch for interactive content
        // inside an item that owns its double-click gesture.
        if (e.Handled || e.OriginalSource is not DependencyObject originalSource)
        {
            return;
        }

        if (ContainerFromElement(this, originalSource) is not NativeListBoxItem container)
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
        return new UiListBoxItem();
    }

    /// <summary>
    /// Re-evaluates position-dependent styles for realized containers after an item collection
    /// insertion, removal, replacement, move, or reset.
    /// </summary>
    /// <remarks>
    /// WPF does not call an item-container style selector again for containers that survive a
    /// collection change. Delegating to the shared manager keeps first, last, single, and alternating
    /// rules correct while retaining virtualization and caller-assigned local styles.
    /// </remarks>
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        if (
            ItemContainerStyle is null
            && ItemContainerStyleSelector is ListItemContainerStyleSelector selector
        )
        {
            ListItemContainerStyleManager.RefreshRealizedContainers(this, selector);
        }
    }

    /// <summary>
    /// Clears positional-selector bookkeeping before WPF reuses an item container.
    /// </summary>
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        ListItemContainerStyleManager.ClearContainer(element);
        base.ClearContainerForItemOverride(element, item);
    }
}