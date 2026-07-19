// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines attached properties for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    /// <summary>
    /// Inheritable presentation context consumed by <see cref="NavigationViewItem"/> containers.
    /// </summary>
    /// <remarks>
    /// This is intentionally separate from the public <see cref="PaneDisplayModeProperty"/>. Making
    /// the public property inheritable would cause a nested NavigationView with no local assignment
    /// to adopt its ancestor's mode. Every NavigationView writes a local context value instead, so
    /// nested controls form explicit scope boundaries while item descendants inherit normally,
    /// including through Popup inheritance contexts.
    /// </remarks>
    internal static readonly DependencyProperty PaneDisplayModeContextProperty =
        DependencyProperty.RegisterAttached(
            "PaneDisplayModeContext",
            typeof(NavigationViewPaneDisplayMode),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(
                NavigationViewPaneDisplayMode.Left,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Inheritable pane-open context paired with <see cref="PaneDisplayModeContextProperty"/>.
    /// </summary>
    internal static readonly DependencyProperty IsPaneOpenContextProperty =
        DependencyProperty.RegisterAttached(
            "IsPaneOpenContext",
            typeof(bool),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits)
        );

    // ============================================================
    // HeaderContent Attached Property
    // ============================================================

    /// <summary>Registers attached property NavigationView.HeaderContent</summary>
    public static readonly DependencyProperty HeaderContentProperty = DependencyProperty.RegisterAttached(
        "HeaderContent",
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnHeaderContentChanged)
    );

    private static void OnHeaderContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement frameworkElement)
        {
            return;
        }

        GetNavigationParent(frameworkElement)?.NotifyHeaderContentChanged(frameworkElement, e.NewValue);
    }

    /// <summary>Helper for getting <see cref="HeaderContentProperty"/> from <paramref name="target"/>.</summary>
    /// <param name="target"><see cref="FrameworkElement"/> to read <see cref="HeaderContentProperty"/> from.</param>
    /// <returns>HeaderContent property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static object? GetHeaderContent(FrameworkElement target) => target.GetValue(HeaderContentProperty);

    /// <summary>Helper for setting <see cref="HeaderContentProperty"/> on <paramref name="target"/>.</summary>
    /// <param name="target"><see cref="FrameworkElement"/> to set <see cref="HeaderContentProperty"/> on.</param>
    /// <param name="headerContent">HeaderContent property value.</param>
    public static void SetHeaderContent(FrameworkElement target, object? headerContent) =>
        target.SetValue(HeaderContentProperty, headerContent);

    // ============================================================
    // NavigationParent Attached Property
    // ============================================================

    /// <summary>Identifies the <see cref="NavigationParent"/> dependency property.</summary>
    internal static readonly DependencyProperty NavigationParentProperty =
        DependencyProperty.RegisterAttached(
            nameof(NavigationParent),
            typeof(NavigationView),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <summary>
    /// Gets the parent <see cref="NavigationView"/> for its <see cref="INavigationViewItem"/> children.
    /// </summary>
    internal NavigationView? NavigationParent
    {
        get => (NavigationView?)GetValue(NavigationParentProperty);
        private set => SetValue(NavigationParentProperty, value);
    }

    /// <summary>Helper for getting <see cref="NavigationParentProperty"/> from <paramref name="navigationItem"/>.</summary>
    /// <param name="navigationItem"><see cref="DependencyObject"/> to read <see cref="NavigationParentProperty"/> from.</param>
    /// <returns>NavigationParent property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    internal static NavigationView? GetNavigationParent(DependencyObject navigationItem) =>
        navigationItem.GetValue(NavigationParentProperty) as NavigationView;

    // ============================================================
    // PageNavigationItem Internal Attached Property
    // ============================================================

    /// <summary>
    /// Internal attached property that stores the owning <see cref="INavigationViewItem"/>
    /// directly on the page's <see cref="FrameworkElement"/>, avoiding a separate
    /// dictionary lookup when <see cref="HeaderContentProperty"/> changes.
    /// The value lives and dies with the page — no leak.
    /// </summary>
    internal static readonly DependencyProperty PageNavigationItemProperty =
        DependencyProperty.RegisterAttached(
            "PageNavigationItem",
            typeof(INavigationViewItem),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );
}
