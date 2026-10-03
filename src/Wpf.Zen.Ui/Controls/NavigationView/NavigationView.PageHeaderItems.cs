// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Data;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Publishes page-owned element collections for application-defined header panels.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// Stores each page's header collection behind its lazily initialized CLR attached property.
    /// </summary>
    /// <remarks>
    /// The storage name leaves PageHeaderItems as a CLR attached collection. WPF's XAML loader
    /// calls its lazy getter when filling nested elements. A same-name dependency property would
    /// make WpfMemberInvoker read its null default directly and bypass that initialization.
    /// </remarks>
    private static readonly DependencyProperty PageHeaderItemsStorageProperty
        = DependencyProperty.RegisterAttached(
            "PageHeaderItemsStorage",
            typeof(PageHeaderItemCollection),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, OnPageHeaderItemsChanged)
        );

    /// <summary>
    /// Gets the page's header elements, creating a page-specific empty collection on first access.
    /// </summary>
    /// <remarks>
    /// Declare elements directly inside NavigationView.PageHeaderItems in XAML, or populate the
    /// returned collection in code on the UI thread. This is a CLR attached collection property;
    /// replace it through <see cref="SetPageHeaderItems"/> or a XAML collection resource.
    /// Present <see cref="CurrentPageHeaderItems"/> through <see cref="PanelItems"/>
    /// to create direct children of a host-defined panel. Layout coordinates refer to that panel.
    /// The collection and PageHeaderContent are independent and may both be used on one page with
    /// distinct elements. Contributed elements must not also be page body children. Resources,
    /// ancestor bindings, and routed commands follow the elements' actual host tree.
    /// </remarks>
    /// <param name="page">The navigation page root.</param>
    /// <returns>The page's mutable collection of header elements.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
    public static PageHeaderItemCollection GetPageHeaderItems(DependencyObject page)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        var items = (PageHeaderItemCollection?)page.GetValue(PageHeaderItemsStorageProperty);
        if (items is null)
        {
            items = new PageHeaderItemCollection();
            page.SetValue(PageHeaderItemsStorageProperty, items);
        }

        return items;
    }

    /// <summary>
    /// Replaces a page's header collection, updating its active contribution immediately.
    /// </summary>
    /// <param name="page">The navigation page root.</param>
    /// <param name="items">The new collection, or null to release the current contribution.</param>
    public static void SetPageHeaderItems(DependencyObject page, PageHeaderItemCollection? items)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        page.SetValue(PageHeaderItemsStorageProperty, items);
    }

    private static readonly DependencyPropertyKey CurrentPageHeaderItemsPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CurrentPageHeaderItems),
            typeof(PageHeaderItemCollection),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only collection contributed by the active page.
    /// </summary>
    public static readonly DependencyProperty CurrentPageHeaderItemsProperty =
        CurrentPageHeaderItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the active page's header collection, or null when no collection has been declared.
    /// </summary>
    /// <remarks>
    /// Bind a panel's <see cref="PanelItems.SourceProperty"/> to this property. The original page
    /// collection is published, so additions, removals, replacements, and moves remain observable.
    /// Elements without an explicitly assigned DataContext follow the page's DataContext, including
    /// later changes. An empty declared collection is published as an empty collection.
    /// </remarks>
    public PageHeaderItemCollection? CurrentPageHeaderItems =>
        (PageHeaderItemCollection?)GetValue(CurrentPageHeaderItemsProperty);

    /// <summary>
    /// Tracks only data-context bindings owned by the navigation view for the active collection.
    /// </summary>
    private readonly Dictionary<FrameworkElement, BindingExpressionBase> _pageHeaderItemBindings = [];

    private FrameworkElement? _pageHeaderItemsPage;
    private PageHeaderItemCollection? _activePageHeaderItems;

    /// <summary>
    /// Republishes a replacement collection only when its declaring page is the active page.
    /// </summary>
    private static void OnPageHeaderItemsChanged(
        DependencyObject page,
        DependencyPropertyChangedEventArgs args)
    {
        if (page is FrameworkElement element)
        {
            var owner = GetPageHeaderOwner(element);
            if (owner is not null)
            {
                owner.UpdatePageHeaderItems(element);
            }
        }
    }

    /// <summary>
    /// Releases the previous collection's bridge and publishes the committed page's collection.
    /// </summary>
    private void UpdatePageHeaderItems(FrameworkElement? page)
    {
        // Read the raw property to avoid allocating collections for pages with no declaration.
        var items = (PageHeaderItemCollection?)page?.GetValue(PageHeaderItemsStorageProperty);
        if (ReferenceEquals(_pageHeaderItemsPage, page) && ReferenceEquals(_activePageHeaderItems, items))
        {
            return;
        }

        SetValue(CurrentPageHeaderItemsPropertyKey, null);

        if (_activePageHeaderItems is not null)
        {
            CollectionChangedEventManager.RemoveHandler(
                _activePageHeaderItems,
                OnPageHeaderItemsCollectionChanged
            );
        }

        foreach (var binding in _pageHeaderItemBindings)
        {
            ReleasePageHeaderDataContext(binding.Key, binding.Value);
        }

        _pageHeaderItemBindings.Clear();

        _pageHeaderItemsPage   = page;
        _activePageHeaderItems = items;

        if (page is not null && items is not null)
        {
            // Subscribe before exposing the collection: page contexts are bridged before host
            // collection handlers insert newly contributed elements into a different tree.
            CollectionChangedEventManager.AddHandler(items, OnPageHeaderItemsCollectionChanged);
            SynchronizePageHeaderItemContexts();
            SetValue(CurrentPageHeaderItemsPropertyKey, items);
        }

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE items published; pageType={page?.GetType().Name ?? "null"}, itemCount={items?.Count ?? 0}."
        );
    }

    /// <summary>
    /// Maintains the page-context bridge when the active collection changes in place.
    /// </summary>
    private void OnPageHeaderItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        Dispatcher.VerifyAccess();

        SynchronizePageHeaderItemContexts();

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE items changed; action={args.Action}, itemCount={_activePageHeaderItems?.Count ?? 0}."
        );
    }

    /// <summary>
    /// Bridges new elements and releases removed ones without overwriting page-assigned contexts.
    /// </summary>
    private void SynchronizePageHeaderItemContexts()
    {
        if (_pageHeaderItemsPage is null || _activePageHeaderItems is null)
        {
            return;
        }

        foreach (var element in _pageHeaderItemBindings.Keys.ToArray())
        {
            if (!_activePageHeaderItems.Contains(element))
            {
                ReleasePageHeaderDataContext(element, _pageHeaderItemBindings[element]);
                _pageHeaderItemBindings.Remove(element);
            }
        }

        foreach (var element in _activePageHeaderItems)
        {
            if (!_pageHeaderItemBindings.ContainsKey(element))
            {
                var binding = BindPageHeaderDataContext(_pageHeaderItemsPage, element);
                if (binding is not null)
                {
                    _pageHeaderItemBindings.Add(element, binding);
                }
            }
        }
    }
}
