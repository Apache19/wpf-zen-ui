// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines attached properties for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    private const int DefaultTransitionDuration = 200;
    private const NavigationTransition DefaultTransition = NavigationTransition.FadeInWithSlide;

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
    // Page transition attached properties
    // ============================================================

    /// <summary>
    /// Identifies the <see cref="Transition"/> dependency property and the attached
    /// <c>NavigationView.Transition</c> property.
    /// </summary>
    /// <remarks>
    /// Set the instance property on a <see cref="NavigationView"/> to define the default entrance
    /// transition for that navigation scope. Set the attached property on a navigation content root
    /// to override that default for the destination. The attached value is intentionally not
    /// inherited: a declaration applies to that destination only and cannot accidentally alter
    /// nested content or another navigation scope. The global default is
    /// <see cref="NavigationTransition.FadeInWithSlide"/>. This property does not invalidate layout
    /// or rendering; it is consumed only when a destination is committed.
    /// </remarks>
    public static readonly DependencyProperty TransitionProperty =
        DependencyProperty.RegisterAttached(
            nameof(Transition),
            typeof(NavigationTransition),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(DefaultTransition),
            value => Enum.IsDefined(typeof(NavigationTransition), value)
        );

    /// <summary>
    /// Gets or sets the default entrance transition for pages shown by this navigation view.
    /// </summary>
    /// <remarks>
    /// A destination that explicitly declares the attached <c>NavigationView.Transition</c>
    /// property overrides this value for its own navigation. The default is
    /// <see cref="NavigationTransition.FadeInWithSlide"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public NavigationTransition Transition
    {
        get => (NavigationTransition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    /// <summary>Gets the entrance transition declared by <paramref name="element"/>.</summary>
    /// <param name="element">The navigation content root to inspect.</param>
    /// <returns>The transition used when the element becomes the current page.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static NavigationTransition GetTransition(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (NavigationTransition)element.GetValue(TransitionProperty);
    }

    /// <summary>Sets the entrance transition declared by <paramref name="element"/>.</summary>
    /// <param name="element">The navigation content root to configure.</param>
    /// <param name="value">The transition to apply when the element becomes current.</param>
    public static void SetTransition(DependencyObject element, NavigationTransition value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(TransitionProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="TransitionDuration"/> dependency property and the attached
    /// <c>NavigationView.TransitionDuration</c> property.
    /// </summary>
    /// <remarks>
    /// The value is expressed in milliseconds. Set the instance property on a
    /// <see cref="NavigationView"/> to define its default duration, or set the attached property on
    /// a destination to override that value for the destination only. The global default is
    /// <c>200</c>. Durations below ten milliseconds do not start an animation, and durations above
    /// ten seconds are capped at ten seconds. The property is not inherited and does not itself
    /// invalidate layout or rendering. The navigation host snapshots it
    /// immediately before navigation and reads it again after WPF commits the destination, so
    /// constructor assignments, bindings resolved by that point, and changes made by a
    /// <see cref="Navigating"/> handler are respected.
    /// </remarks>
    public static readonly DependencyProperty TransitionDurationProperty =
        DependencyProperty.RegisterAttached(
            nameof(TransitionDuration),
            typeof(int),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(DefaultTransitionDuration)
        );

    /// <summary>
    /// Gets or sets the default page-transition duration, in milliseconds, for this navigation view.
    /// </summary>
    /// <remarks>
    /// A destination that explicitly declares the attached
    /// <c>NavigationView.TransitionDuration</c> property overrides this value for its own navigation.
    /// The default is <c>200</c> milliseconds.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    /// <summary>Gets the entrance-transition duration declared by <paramref name="element"/>.</summary>
    /// <param name="element">The navigation content root to inspect.</param>
    /// <returns>The requested transition duration, in milliseconds.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static int GetTransitionDuration(DependencyObject element)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (int)element.GetValue(TransitionDurationProperty);
    }

    /// <summary>Sets the entrance-transition duration declared by <paramref name="element"/>.</summary>
    /// <param name="element">The navigation content root to configure.</param>
    /// <param name="value">The requested duration, in milliseconds.</param>
    public static void SetTransitionDuration(DependencyObject element, int value)
    {
        if (element is null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(TransitionDurationProperty, value);
    }

    // ============================================================
    // BreadcrumbContent Attached Property
    // ============================================================

    /// <summary>
    /// Identifies the attached <c>NavigationView.BreadcrumbContent</c> dependency property.
    /// </summary>
    /// <remarks>
    /// The property supplies only the active leaf's breadcrumb label. It never mutates a menu
    /// item's content and does not act as a page heading. For richer dynamic paths, implement
    /// <see cref="INavigationLocationSource"/> on the page or its data context.
    /// </remarks>
    public static readonly DependencyProperty BreadcrumbContentProperty = DependencyProperty.RegisterAttached(
        "BreadcrumbContent",
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnBreadcrumbContentChanged)
    );

    private static void OnBreadcrumbContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement frameworkElement)
        {
            return;
        }

        GetNavigationParent(frameworkElement)?.NotifyBreadcrumbDeclarationChanged(frameworkElement);
    }

    /// <summary>Gets the current leaf label declared by <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the label is declared.</param>
    /// <returns>The breadcrumb content, or <see langword="null"/> when no override is declared.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static object? GetBreadcrumbContent(FrameworkElement target) =>
        target.GetValue(BreadcrumbContentProperty);

    /// <summary>Sets the active leaf label declared by <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the label is declared.</param>
    /// <param name="content">The dynamic breadcrumb content.</param>
    public static void SetBreadcrumbContent(FrameworkElement target, object? content) =>
        target.SetValue(BreadcrumbContentProperty, content);

    // ============================================================
    // BreadcrumbParentKey Attached Property
    // ============================================================

    /// <summary>
    /// Identifies the attached <c>NavigationView.BreadcrumbParentKey</c> dependency property.
    /// </summary>
    /// <remarks>
    /// The value selects a node in the host's <see cref="Wpf.Zen.Ui.NavigationMap"/> whose resolved
    /// path is placed before the current page. It is intentionally a route key rather than a page
    /// type because one page type can occupy multiple semantic locations. The property supports
    /// bindings and refreshes the active location without creating journal history. Its default is
    /// <see langword="null"/>; it has no direct layout or rendering effect.
    /// </remarks>
    public static readonly DependencyProperty BreadcrumbParentKeyProperty =
        DependencyProperty.RegisterAttached(
            "BreadcrumbParentKey",
            typeof(string),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, OnBreadcrumbParentKeyChanged),
            value => value is null || !string.IsNullOrWhiteSpace((string)value)
        );

    /// <summary>Gets the map route declared as the semantic parent of <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the parent is declared.</param>
    /// <returns>The parent route key, or <see langword="null"/> when no parent is declared.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static string? GetBreadcrumbParentKey(FrameworkElement target) =>
        (string?)target.GetValue(BreadcrumbParentKeyProperty);

    /// <summary>Sets the map route used as the semantic parent of <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the parent is declared.</param>
    /// <param name="routeKey">The stable key of a node in the host navigation map.</param>
    public static void SetBreadcrumbParentKey(FrameworkElement target, string? routeKey) =>
        target.SetValue(BreadcrumbParentKeyProperty, routeKey);

    // ============================================================
    // BreadcrumbPath Attached Property
    // ============================================================

    private static readonly DependencyProperty BreadcrumbPathChangedHandlerProperty =
        DependencyProperty.RegisterAttached(
            "BreadcrumbPathChangedHandler",
            typeof(EventHandler<EventArgs>),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the attached <c>NavigationView.BreadcrumbPath</c> dependency property.
    /// </summary>
    /// <remarks>
    /// A declared path is complete and therefore takes precedence over parent inference and the
    /// navigation map. <see cref="Wpf.Zen.Ui.BreadcrumbPath"/> is a Freezable collection, so segment
    /// bindings inherit the page data context and automatically refresh the active location. Its
    /// default is <see langword="null"/>; it has no direct layout or rendering effect.
    /// </remarks>
    public static readonly DependencyProperty BreadcrumbPathProperty =
        DependencyProperty.RegisterAttached(
            "BreadcrumbPath",
            typeof(BreadcrumbPath),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, OnBreadcrumbPathChanged)
        );

    /// <summary>Gets the complete path declared by <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the path is declared.</param>
    /// <returns>The declared path, or <see langword="null"/> when map inference should be used.</returns>
    [AttachedPropertyBrowsableForType(typeof(FrameworkElement))]
    public static BreadcrumbPath? GetBreadcrumbPath(FrameworkElement target) =>
        (BreadcrumbPath?)target.GetValue(BreadcrumbPathProperty);

    /// <summary>Sets the complete path declared by <paramref name="target"/>.</summary>
    /// <param name="target">The page root on which the path is declared.</param>
    /// <param name="path">The complete semantic path, including the active leaf.</param>
    public static void SetBreadcrumbPath(FrameworkElement target, BreadcrumbPath? path) =>
        target.SetValue(BreadcrumbPathProperty, path);

    private static void OnBreadcrumbParentKeyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement frameworkElement)
        {
            GetNavigationParent(frameworkElement)?.NotifyBreadcrumbDeclarationChanged(frameworkElement);
        }
    }

    private static void OnBreadcrumbPathChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not FrameworkElement frameworkElement)
        {
            return;
        }

        EventHandler<EventArgs>? handler = (EventHandler<EventArgs>?)frameworkElement.GetValue(
            BreadcrumbPathChangedHandlerProperty
        );
        if (e.OldValue is BreadcrumbPath oldPath && handler is not null)
        {
            WeakEventManager<BreadcrumbPath, EventArgs>.RemoveHandler(
                oldPath,
                nameof(Freezable.Changed),
                handler
            );
        }

        frameworkElement.ClearValue(BreadcrumbPathChangedHandlerProperty);

        if (e.NewValue is BreadcrumbPath newPath)
        {
            // A Freezable propagates child dependency-property changes through Changed. A weak
            // subscription is important when an application places a shared path in resources;
            // that resource must not retain every page instance that has used it.
            handler = (_, _) =>
                GetNavigationParent(frameworkElement)?.NotifyBreadcrumbDeclarationChanged(frameworkElement);
            frameworkElement.SetValue(BreadcrumbPathChangedHandlerProperty, handler);
            WeakEventManager<BreadcrumbPath, EventArgs>.AddHandler(
                newPath,
                nameof(Freezable.Changed),
                handler
            );
        }

        GetNavigationParent(frameworkElement)?.NotifyBreadcrumbDeclarationChanged(frameworkElement);
    }

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
    /// Gets the parent <see cref="NavigationView"/> for its item containers.
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
}