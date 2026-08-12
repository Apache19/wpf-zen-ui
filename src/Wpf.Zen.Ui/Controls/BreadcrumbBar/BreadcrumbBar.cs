// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library
   Copyright(c) Microsoft Corporation.All rights reserved. */

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// The <see cref="BreadcrumbBar"/> control provides the direct path of pages or folders to the current location.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:BreadcrumbBar x:Name="BreadcrumbBar" /&gt;
/// </code>
/// </example>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(BreadcrumbBarItem))]
public class BreadcrumbBar : ItemsControl
{
    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(BreadcrumbBar),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets custom command executed after selecting the item.
    /// </summary>
    [Bindable(true)]
    [Category("Action")]
    [Localizability(LocalizationCategory.NeverLocalize)]
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Identifies the <see cref="ItemClicked"/> routed event.</summary>
    public static readonly RoutedEvent ItemClickedEvent = EventManager.RegisterRoutedEvent(
        nameof(ItemClicked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs>),
        typeof(BreadcrumbBar)
    );

    /// <summary>
    /// Occurs when an item is clicked in the <see cref="BreadcrumbBar"/>.
    /// </summary>
    public event TypedEventHandler<BreadcrumbBar, BreadcrumbBarItemClickedEventArgs> ItemClicked
    {
        add => AddHandler(ItemClickedEvent, value);
        remove => RemoveHandler(ItemClickedEvent, value);
    }

    static BreadcrumbBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(BreadcrumbBar),
            new FrameworkPropertyMetadata(typeof(BreadcrumbBar))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="BreadcrumbBar"/> class.
    /// </summary>
    public BreadcrumbBar()
    {
        // BreadcrumbBarItem translates only its bounded template button into this event. Listening
        // for the container-owned event therefore excludes separator clicks while still identifying
        // the exact generated container without an ancestor binding or template command.
        AddHandler(BreadcrumbBarItem.ClickEvent, new RoutedEventHandler(OnItemContainerClick));
        ItemContainerGenerator.ItemsChanged += ItemContainerGeneratorOnItemsChanged;
        ItemContainerGenerator.StatusChanged += ItemContainerGeneratorOnStatusChanged;
    }

    /// <summary>
    /// Raises <see cref="ItemClicked"/> and executes <see cref="Command"/> for an activated item.
    /// </summary>
    /// <param name="item">The original item supplied through the items collection or source.</param>
    /// <param name="container">The realized container that received the input.</param>
    /// <param name="index">The current index of <paramref name="item"/>.</param>
    /// <remarks>
    /// The source item is deliberately kept separate from the container so derived controls can
    /// customize invocation without leaking presentation objects into view-model commands.
    /// </remarks>
    protected virtual void OnItemClicked(object item, BreadcrumbBarItem container, int index)
    {
        var args = new BreadcrumbBarItemClickedEventArgs(ItemClickedEvent, this, item, container, index);
        RaiseEvent(args);

        if (Command?.CanExecute(item) == true)
        {
            Command.Execute(item);
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is BreadcrumbBarItem;
    }

    protected override DependencyObject GetContainerForItemOverride()
    {
        return new BreadcrumbBarItem();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);
        UpdateContainerPositions();
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is BreadcrumbBarItem container)
        {
            container.SetIsLast(false);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    private void ItemContainerGeneratorOnStatusChanged(object? sender, EventArgs e)
    {
        if (ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
        {
            return;
        }

        UpdateContainerPositions();
    }

    private void ItemContainerGeneratorOnItemsChanged(object sender, ItemsChangedEventArgs e)
    {
        UpdateContainerPositions();
    }

    private void OnItemContainerClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is not BreadcrumbBarItem container || container.IsLast)
        {
            return;
        }

        int index = ItemContainerGenerator.IndexFromContainer(container);
        if (index < 0)
        {
            return;
        }

        object item = ItemContainerGenerator.ItemFromContainer(container);
        OnItemClicked(item, container, index);
    }

    /// <summary>
    /// Recomputes positional state for every realized container after generator changes.
    /// </summary>
    /// <remarks>
    /// Breadcrumb bars are short, non-virtualized paths. Updating all realized containers avoids
    /// stale IsLast values after Move, Replace, Reset, or source replacement without coupling the
    /// algorithm to the shape of a particular collection notification.
    /// </remarks>
    private void UpdateContainerPositions()
    {
        int lastIndex = Items.Count - 1;
        for (int index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is BreadcrumbBarItem container)
            {
                container.SetIsLast(index == lastIndex);
            }
        }
    }
}
