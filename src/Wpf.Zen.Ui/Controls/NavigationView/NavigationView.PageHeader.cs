// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Windows.Data;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Exposes the active page's header contribution for application-defined header layouts.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// Identifies the attached <c>NavigationView.PageHeaderContent</c> property.
    /// </summary>
    /// <remarks>
    /// Set this property on a navigation page root. The active element is exposed through
    /// <see cref="CurrentPageHeaderContent"/>. The application decides where to present it,
    /// typically within the navigation view's <see cref="Header"/> layout.
    /// The element must not also appear in the page body. Routed commands follow the visual tree
    /// chosen by the application; use an explicit command target to reach page command bindings.
    /// </remarks>
    public static readonly DependencyProperty PageHeaderContentProperty
        = DependencyProperty.RegisterAttached(
            "PageHeaderContent",
            typeof(FrameworkElement),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, OnPageHeaderContentChanged)
        );

    /// <summary>
    /// Gets the element contributed by a navigation page.
    /// </summary>
    /// <param name="page">The navigation page root.</param>
    /// <returns>The contributed element, or <see langword="null"/>.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static FrameworkElement? GetPageHeaderContent(DependencyObject page)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        return (FrameworkElement?)page.GetValue(PageHeaderContentProperty);
    }

    /// <summary>
    /// Sets the element contributed by a navigation page.
    /// </summary>
    /// <param name="page">The navigation page root.</param>
    /// <param name="content">The contributed element, or <see langword="null"/>.</param>
    public static void SetPageHeaderContent(DependencyObject page, FrameworkElement? content)
    {
        if (page is null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        page.SetValue(PageHeaderContentProperty, content);
    }

    private static readonly DependencyPropertyKey CurrentPageHeaderContentPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CurrentPageHeaderContent),
            typeof(FrameworkElement),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="CurrentPageHeaderContent"/> property.
    /// </summary>
    public static readonly DependencyProperty CurrentPageHeaderContentProperty =
        CurrentPageHeaderContentPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the active page's contributed element for placement by the application.
    /// </summary>
    /// <remarks>
    /// The value changes when WPF commits a navigation or the active page changes its attached
    /// property. An element without its own data context follows the active page's data context,
    /// including later changes, so view-model bindings and commands work in a host layout.
    /// This association remains in place while the element is removed from the host layout.
    /// </remarks>
    public FrameworkElement? CurrentPageHeaderContent
    {
        get => (FrameworkElement?)GetValue(CurrentPageHeaderContentProperty);
    }

    /// <summary>
    /// Tracks the bridge on each contribution so reuse can update its page source while
    /// preserving an explicit context subsequently assigned by the application.
    /// </summary>
    private static readonly DependencyProperty PageHeaderDataContextBindingProperty
        = DependencyProperty.RegisterAttached(
            "PageHeaderDataContextBinding",
            typeof(BindingExpressionBase),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Tracks header ownership for committed Frame pages, including ReplaceContent destinations.
    /// </summary>
    private static readonly DependencyProperty PageHeaderOwnerProperty
        = DependencyProperty.RegisterAttached(
            "PageHeaderOwner",
            typeof(WeakReference<NavigationView>),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Stores the committed page whose attached header declarations notify this navigation view.
    /// </summary>
    private FrameworkElement? _pageHeaderPage;

    /// <summary>
    /// Keeps the exposed contribution current when the active page replaces its element.
    /// </summary>
    private static void OnPageHeaderContentChanged(
        DependencyObject page,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (page is FrameworkElement element)
        {
            var owner = GetPageHeaderOwner(element);
            if (owner is not null)
            {
                owner.UpdatePageHeaderContent(element);
            }
        }
    }

    /// <summary>
    /// Publishes both independent contributions when WPF commits a navigation.
    /// </summary>
    private void UpdatePageHeader(FrameworkElement? page)
    {
        if (!ReferenceEquals(_pageHeaderPage, page))
        {
            if (_pageHeaderPage is not null && ReferenceEquals(GetPageHeaderOwner(_pageHeaderPage), this))
            {
                _pageHeaderPage.ClearValue(PageHeaderOwnerProperty);
            }

            _pageHeaderPage = page;
            page?.SetValue(PageHeaderOwnerProperty, new WeakReference<NavigationView>(this));
        }

        UpdatePageHeaderContent(page);
        UpdatePageHeaderItems(page);
    }

    /// <summary>
    /// Resolves the active declaration owner without retaining a navigation view from cached pages.
    /// </summary>
    private static NavigationView? GetPageHeaderOwner(FrameworkElement page)
    {
        var reference = (WeakReference<NavigationView>?)page.GetValue(PageHeaderOwnerProperty);
        if (reference is not null && reference.TryGetTarget(out var owner)
            && ReferenceEquals(owner._pageHeaderPage, page))
        {
            return owner;
        }

        return null;
    }

    /// <summary>
    /// Publishes a page's single element and bridges its inherited data context to the page.
    /// </summary>
    private void UpdatePageHeaderContent(FrameworkElement? page)
    {
        var content = page is null ? null : GetPageHeaderContent(page);
        if (page is not null && content is not null)
        {
            EnsurePageHeaderDataContext(page, content);
        }

        // A presenter can defer removing the old visual. Keep its local bridge throughout
        // that interval, and prepare the new bridge before publishing the replacement.
        // WPF holds DependencyObject binding sources weakly, so the bridge can follow the
        // element's lifetime without keeping the owning page alive.
        SetValue(CurrentPageHeaderContentPropertyKey, content);

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE contribution published; pageType={page?.GetType().Name ?? "null"}, hasContent={content is not null}."
        );
    }

    /// <summary>
    /// Makes an inherited context follow the page while respecting explicit element contexts.
    /// </summary>
    private static void EnsurePageHeaderDataContext(FrameworkElement page, FrameworkElement element)
    {
        var managedBinding =
            (BindingExpressionBase?)element.GetValue(PageHeaderDataContextBindingProperty);

        var currentBinding = BindingOperations.GetBindingExpressionBase(element, DataContextProperty);

        if (managedBinding is not null && ReferenceEquals(currentBinding, managedBinding))
        {
            if (managedBinding.ParentBindingBase is Binding binding
                && ReferenceEquals(binding.Source, page))
            {
                return;
            }
        }
        else
        {
            element.ClearValue(PageHeaderDataContextBindingProperty);
            var source = DependencyPropertyHelper.GetValueSource(element, DataContextProperty);
            if (source.BaseValueSource is not (BaseValueSource.Default or BaseValueSource.Inherited))
            {
                return;
            }
        }

        var bridge = BindingOperations.SetBinding(
            element,
            DataContextProperty,
            new Binding(nameof(FrameworkElement.DataContext)) { Source = page, Mode = BindingMode.OneWay }
        );

        element.SetValue(PageHeaderDataContextBindingProperty, bridge);
    }

}
