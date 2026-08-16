// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains template, item-container generation, and item-collection lifecycle integration for
/// <see cref="NavigationMapView"/>.
/// </summary>
public partial class NavigationMapView
{
    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        // A template replacement invalidates both the coordinate space used by the gesture and
        // the ScrollViewer receiving offsets. End the gesture before replacing that reference so
        // a captured mouse cannot continue driving a detached template part.
        EndMiddleButtonPanning();

        base.OnApplyTemplate();

        _scrollViewer = GetTemplateChild(ScrollViewerPartName) as ScrollViewer;
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>Raises invocation and then executes <see cref="Command"/>.</summary>
    /// <param name="item">The item owned by WPF's item generator.</param>
    /// <param name="container">The activated visual container.</param>
    protected virtual void OnNodeInvoked(object item, NavigationMapItem container)
    {
        var args = new NavigationMapNodeInvokedEventArgs(NodeInvokedEvent, this, item, container);

        RaiseEvent(args);

        if (!args.Handled && Command?.CanExecute(item) == true)
        {
            Command.Execute(item);
        }
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is NavigationMapItem;
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new NavigationMapItem();
    }

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is not NavigationMapItem container)
        {
            return;
        }

        if (item is NavigationMapNode node)
        {
            BindContainer(container, NavigationMapItem.NodeKeyProperty, node, nameof(NavigationMapNode.Key));
            BindContainer(container, NavigationMapItem.ParentKeyProperty, node, nameof(NavigationMapNode.ParentKey));
            BindContainer(container, NavigationMapItem.TargetProperty, node, nameof(NavigationMapNode.Target));
            BindContainer(container, NavigationMapItem.DescriptionProperty, node, nameof(NavigationMapNode.Description));
            BindContainer(container, AutomationProperties.NameProperty, node, nameof(NavigationMapNode.Content));
        }

        UpdateContainerStates(container, item);
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationMapItem container && !ReferenceEquals(element, item))
        {
            container.SetLayoutSelected(false, false);
            container.SetSearchMatch(false, false);

            BindingOperations.ClearBinding(container, NavigationMapItem.NodeKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.ParentKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.TargetProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.DescriptionProperty);
            BindingOperations.ClearBinding(container, AutomationProperties.NameProperty);

            container.SetLayoutPosition(0, true, 1, 1);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                RefreshGeneratedState();
                if (CanEditSelection() && SnapToGrid)
                {
                    // Loaded priority is already after item generation. Normalizing here avoids a
                    // second dispatcher hop that could cancel the edit-entry alignment while a
                    // template is still being established.
                    UpdateLayout();

                    _ = AlignContainersToGrid(
                        GetGeneratedContainers().ToList(),
                        recordHistory: false
                    );
                }
            })
        );
    }
}
