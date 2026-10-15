// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Diagnostics;

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
    /// later changes, even while removed from the host. Reused elements follow their new page.
    /// An empty declared collection is published as an empty collection.
    /// </remarks>
    public PageHeaderItemCollection? CurrentPageHeaderItems
    {
        get => (PageHeaderItemCollection?)GetValue(CurrentPageHeaderItemsProperty);
    }

    /// <summary>
    /// Stores the page supplying the active collection's data context.
    /// </summary>
    private FrameworkElement? _pageHeaderItemsPage;

    /// <summary>
    /// Stores the collection whose changes are observed while its page is active.
    /// </summary>
    private PageHeaderItemCollection? _activePageHeaderItems;

    /// <summary>
    /// Republishes a replacement collection only when its declaring page is the active page.
    /// </summary>
    private static void OnPageHeaderItemsChanged(
        DependencyObject page, DependencyPropertyChangedEventArgs args)
    {
        if (page is FrameworkElement element)
        {
            var owner = GetPageHeaderOwner(element);
            if (owner is not null)
            {
                owner.UpdatePageHeader(element);
            }
        }
    }

    /// <summary>
    /// Transfers the collection subscription and publishes the committed page's collection.
    /// </summary>
    private void UpdatePageHeaderItems(FrameworkElement? page, long revision)
    {
        // Read the raw property to avoid allocating collections for pages with no declaration.
        var items = (PageHeaderItemCollection?)page?.GetValue(PageHeaderItemsStorageProperty);
        if (ReferenceEquals(_pageHeaderItemsPage, page) && ReferenceEquals(_activePageHeaderItems, items))
        {
            return;
        }

        var previous = _activePageHeaderItems;

        _activePageHeaderItems = null;
        _pageHeaderItemsPage   = null;

        var failures = new NavigationFailures();

        if (previous is not null)
        {
            failures.Try(() => CollectionChangedEventManager.RemoveHandler(
                previous, OnPageHeaderItemsCollectionChanged)
            );
        }

        failures.Try(() => SetValue(CurrentPageHeaderItemsPropertyKey, null));

        if (!IsPageHeaderPublicationCurrent(page, revision))
        {
            failures.ThrowIfAny();
            return;
        }

        _pageHeaderItemsPage   = page;
        _activePageHeaderItems = items;

        if (page is not null && items is not null)
        {
            // Subscribe before exposing the collection: page contexts are bridged before host
            // collection handlers insert newly contributed elements into a different tree.
            failures.Try(
                () => CollectionChangedEventManager.AddHandler(
                    items,
                    OnPageHeaderItemsCollectionChanged
                )
            );

            failures.Try(SynchronizePageHeaderItemContexts);

            if (IsPageHeaderPublicationCurrent(page, revision))
            {
                failures.Try(() => SetValue(CurrentPageHeaderItemsPropertyKey, items));
            }
        }

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE items published; pageType={page?.GetType().Name ?? "null"}, itemCount={items?.Count ?? 0}."
        );

        failures.ThrowIfAny();
    }

    /// <summary>
    /// Maintains the page-context bridge when the active collection changes in place.
    /// </summary>
    private void OnPageHeaderItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        Dispatcher.VerifyAccess();

        if (!ReferenceEquals(sender, _activePageHeaderItems))
        {
            return;
        }

        var session = _navigationSession;

        NavigationWorkRegistry.Lease? work = null;

        if (session is { IsStopping: false })
        {
            work = session.Work.Acquire();
        }

        using var publication = work;

        SynchronizePageHeaderItemContexts();

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE items changed; action={args.Action}, itemCount={_activePageHeaderItems?.Count ?? 0}."
        );
    }

    /// <summary>
    /// Associates active elements with their page while preserving explicitly assigned contexts.
    /// </summary>
    private void SynchronizePageHeaderItemContexts()
    {
        var page     = _pageHeaderItemsPage;
        var items    = _activePageHeaderItems;
        var revision = _pageHeaderRevision;

        if (page is null || items is null)
        {
            return;
        }

        // Collection notifications reach us before the host removes old children. Their
        // bridges remain attached to the elements throughout removal and subsequent reuse.
        var snapshot = items.ToArray();
        var failures = new NavigationFailures();

        foreach (var element in snapshot)
        {
            if (!IsPageHeaderPublicationCurrent(page, revision))
            {
                break;
            }

            failures.Try(() => EnsurePageHeaderDataContext(page, element));
        }

        failures.ThrowIfAny();
    }
}