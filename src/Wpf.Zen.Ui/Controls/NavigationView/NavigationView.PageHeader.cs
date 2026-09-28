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
    public static readonly DependencyProperty PageHeaderContentProperty =
        DependencyProperty.RegisterAttached(
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

    private static readonly DependencyPropertyKey CurrentPageHeaderContentPropertyKey =
        DependencyProperty.RegisterReadOnly(
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
    /// </remarks>
    public FrameworkElement? CurrentPageHeaderContent =>
        (FrameworkElement?)GetValue(CurrentPageHeaderContentProperty);

    /// <summary>
    /// Tracks the data-context binding installed on the active contribution so it can be removed
    /// without disturbing a value assigned by the page while the element was visible.
    /// </summary>
    private BindingExpressionBase? _pageHeaderDataContextBinding;

    /// <summary>
    /// Keeps the exposed contribution current when the active page replaces its element.
    /// </summary>
    private static void OnPageHeaderContentChanged(
        DependencyObject page,
        DependencyPropertyChangedEventArgs eventArgs)
    {
        if (page is FrameworkElement element)
        {
            var owner = GetNavigationParent(element);
            if (owner is not null && ReferenceEquals(owner._navigationAwareContent, element))
            {
                owner.UpdatePageHeader(element);
            }
        }
    }

    /// <summary>
    /// Publishes a committed page's element and bridges its inherited data context to the page.
    /// </summary>
    private void UpdatePageHeader(FrameworkElement? page)
    {
        var previous = CurrentPageHeaderContent;
        // The contributed element is hosted outside the page's visual tree. Remove our
        // binding before releasing the old contribution so its explicit source no longer
        // retains the previous page. Leave a binding installed later by the page intact.
        if (previous is not null && _pageHeaderDataContextBinding is not null)
        {
            var binding = BindingOperations.GetBindingExpressionBase(previous, DataContextProperty);
            if (ReferenceEquals(binding, _pageHeaderDataContextBinding))
            {
                BindingOperations.ClearBinding(previous, DataContextProperty);
            }
        }

        _pageHeaderDataContextBinding = null;
        SetValue(CurrentPageHeaderContentPropertyKey, null);

        var content = page is null ? null : GetPageHeaderContent(page);
        if (page is not null && content is not null)
        {
            var source = DependencyPropertyHelper.GetValueSource(
                content,
                DataContextProperty
            );

            // An explicitly assigned DataContext belongs to the page and takes precedence.
            if (source.BaseValueSource is BaseValueSource.Default or BaseValueSource.Inherited)
            {
                // Presentation in the application's header changes DataContext inheritance to
                // the header host. Bind to the page instead so the contributed element uses the
                // page's view model and follows later changes to the page's DataContext.
                _pageHeaderDataContextBinding = BindingOperations.SetBinding(
                    content,
                    DataContextProperty,
                    new Binding(nameof(FrameworkElement.DataContext)) { Source = page }
                );
            }

            SetValue(CurrentPageHeaderContentPropertyKey, content);
        }

        Debug.WriteLine(
            $"[NavigationView.Header] PAGE contribution published; pageType={page?.GetType().Name ?? "null"}, hasContent={content is not null}."
        );
    }
}
