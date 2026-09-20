// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Keeps position-dependent item templates synchronized for any WPF <see cref="ItemsControl"/>.
/// </summary>
/// <remarks>
/// Unlike container styles, templates are selected by a <see cref="ContentPresenter"/> that may be
/// nested inside the generated item container. The manager is consequently registered from the
/// selector itself and listens to the public <see cref="ItemContainerGenerator.ItemsChanged"/>
/// event, so native and WPF Zen UI controls share the same behavior.
/// </remarks>
internal static class ListItemTemplateManager
{
    private sealed class OwnerRegistration
    {
        private readonly ItemsControl _owner;

        internal OwnerRegistration(ItemsControl owner, ListItemTemplateSelector selector)
        {
            _owner = owner;
            Selector = selector;
            owner.ItemContainerGenerator.ItemsChanged += OnItemsChanged;
        }

        internal ListItemTemplateSelector Selector { get; }

        internal void Detach()
        {
            _owner.ItemContainerGenerator.ItemsChanged -= OnItemsChanged;
        }

        private void OnItemsChanged(object sender, ItemsChangedEventArgs e)
        {
            if (
                _owner.ItemTemplate is not null
                || !ReferenceEquals(_owner.ItemTemplateSelector, Selector)
            )
            {
                // ItemTemplate has precedence over ItemTemplateSelector, and a replacement selector
                // owns subsequent refreshes. Stop observing as soon as this registration is stale.
                Detach();
                Registrations.Remove(_owner);
                return;
            }

            RefreshRealizedContainers(_owner, Selector);
        }
    }

    private static readonly ConditionalWeakTable<ItemsControl, OwnerRegistration> Registrations =
        new();

    /// <summary>
    /// Registers an owner when WPF first asks the selector for one of its item templates. The
    /// weak table makes registration idempotent without keeping an otherwise unreachable owner
    /// alive; its value may safely reference the owner because conditional-table entries use
    /// ephemeron lifetime semantics.
    /// </summary>
    internal static void RegisterOwner(ItemsControl owner, ListItemTemplateSelector selector)
    {
        if (
            Registrations.TryGetValue(owner, out OwnerRegistration? current)
            && ReferenceEquals(current.Selector, selector)
        )
        {
            return;
        }

        current?.Detach();
        Registrations.Remove(owner);
        Registrations.Add(owner, new OwnerRegistration(owner, selector));
    }

    /// <summary>
    /// Finds the owning control and direct generated container even when WPF invokes the selector
    /// from a ContentPresenter inside a ListBoxItem or ListViewItem control template.
    /// </summary>
    internal static bool TryFindOwner(
        DependencyObject origin,
        [NotNullWhen(true)] out ItemsControl? owner,
        [NotNullWhen(true)] out DependencyObject? itemContainer
    )
    {
        DependencyObject? current = origin;
        while (current is not null)
        {
            if (ItemsControl.ItemsControlFromItemContainer(current) is { } currentOwner)
            {
                owner = currentOwner;
                itemContainer = current;
                return true;
            }

            DependencyObject? templatedParent =
                (current as FrameworkElement)?.TemplatedParent
                ?? (current as FrameworkContentElement)?.TemplatedParent;
            current = templatedParent ?? GetVisualOrLogicalParent(current);
        }

        owner         = null;
        itemContainer = null;
        return false;
    }

    /// <summary>
    /// Re-evaluates only realized containers. WPF caches the result inside ContentPresenter, so the
    /// public and supported invalidation path is to change the selector value. Clearing and restoring
    /// the selector keeps ContentTemplate unset, preserving implicit template lookup when a positional
    /// rule returns null.
    /// </summary>
    private static void RefreshRealizedContainers(
        ItemsControl owner,
        ListItemTemplateSelector selector
    ) => RefreshVisualSubtree(owner, owner, selector);

    private static void RefreshVisualSubtree(
        ItemsControl owner,
        DependencyObject visualParent,
        ListItemTemplateSelector selector
    )
    {
        int visualChildCount = VisualTreeHelper.GetChildrenCount(visualParent);
        for (int childIndex = 0; childIndex < visualChildCount; childIndex++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(visualParent, childIndex);
            if (owner.ItemContainerGenerator.IndexFromContainer(child) >= 0)
            {
                RefreshContainer(child, selector);
            }

            // Item content can host nested item controls. IndexFromContainer above limits mutation
            // to containers generated by this owner, while traversal still reaches virtualizing and
            // non-virtualizing panels regardless of their concrete visual structure.
            RefreshVisualSubtree(owner, child, selector);
        }
    }

    private static void RefreshContainer(
        DependencyObject container,
        ListItemTemplateSelector selector
    )
    {
        if (container is ContentPresenter presenter)
        {
            if (
                presenter.ContentTemplate is null
                && ReferenceEquals(presenter.ContentTemplateSelector, selector)
            )
            {
                presenter.SetCurrentValue(ContentPresenter.ContentTemplateSelectorProperty, null);
                presenter.SetCurrentValue(
                    ContentPresenter.ContentTemplateSelectorProperty,
                    selector
                );
            }

            return;
        }

        if (
            container is ContentControl contentControl
            && contentControl.ContentTemplate is null
            && ReferenceEquals(contentControl.ContentTemplateSelector, selector)
        )
        {
            // Generated ContentControls receive this selector as a local value from ItemsControl.
            // Their template ContentPresenter aliases the property, so changing the container is
            // sufficient and avoids replacing bindings inside an application-defined template.
            contentControl.SetCurrentValue(ContentControl.ContentTemplateSelectorProperty, null);
            contentControl.SetCurrentValue(
                ContentControl.ContentTemplateSelectorProperty,
                selector
            );
        }
    }

    private static DependencyObject? GetVisualOrLogicalParent(DependencyObject element)
    {
        if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
        {
            return VisualTreeHelper.GetParent(element) ?? LogicalTreeHelper.GetParent(element);
        }

        return LogicalTreeHelper.GetParent(element);
    }
}
