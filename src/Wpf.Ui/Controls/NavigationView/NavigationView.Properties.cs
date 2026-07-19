// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Controls;
using Wpf.Ui.Animations;
using Wpf.Ui.Theming;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines the dependency properties and dp callbacks for <see cref="NavigationView"/> control
/// </content>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public partial class NavigationView
{
    #region DP: AlwaysShowHeader

    /// <summary>Identifies the <see cref="AlwaysShowHeader"/> dependency property.</summary>
    public static readonly DependencyProperty AlwaysShowHeaderProperty = DependencyProperty.Register(
        nameof(AlwaysShowHeader),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <inheritdoc/>
    public bool AlwaysShowHeader
    {
        get => (bool)GetValue(AlwaysShowHeaderProperty);
        set => SetValue(AlwaysShowHeaderProperty, value);
    }

    #endregion

    #region DP: AutoSuggestBox

    /// <summary>Identifies the <see cref="AutoSuggestBox"/> dependency property.</summary>
    public static readonly DependencyProperty AutoSuggestBoxProperty = DependencyProperty.Register(
        nameof(AutoSuggestBox),
        typeof(AutoSuggestBox),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnAutoSuggestBoxChanged)
    );

    /// <inheritdoc/>
    public AutoSuggestBox? AutoSuggestBox
    {
        get => (AutoSuggestBox?)GetValue(AutoSuggestBoxProperty);
        set => SetValue(AutoSuggestBoxProperty, value);
    }

    private static void OnAutoSuggestBoxChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if (e.NewValue is null && e.OldValue is AutoSuggestBox oldValue)
        {
            oldValue.SuggestionChosen -= navigationView.AutoSuggestBoxOnSuggestionChosen;
            oldValue.QuerySubmitted   -= navigationView.AutoSuggestBoxOnQuerySubmitted;
            return;
        }

        if (e.NewValue is not AutoSuggestBox autoSuggestBox)
        {
            return;
        }

        autoSuggestBox.OriginalItemsSource =  navigationView._autoSuggestItems;
        navigationView.UpdateAutoSuggestBoxSuggestions();

        autoSuggestBox.SuggestionChosen    += navigationView.AutoSuggestBoxOnSuggestionChosen;
        autoSuggestBox.QuerySubmitted      += navigationView.AutoSuggestBoxOnQuerySubmitted;
    }

    #endregion

    #region DP: BreadcrumbBar

    /// <summary>Identifies the <see cref="BreadcrumbBar"/> dependency property.</summary>
    public static readonly DependencyProperty BreadcrumbBarProperty = DependencyProperty.Register(
        nameof(BreadcrumbBar),
        typeof(BreadcrumbBar),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnBreadcrumbBarChanged)
    );

    /// <inheritdoc/>
    public BreadcrumbBar? BreadcrumbBar
    {
        get => (BreadcrumbBar?)GetValue(BreadcrumbBarProperty);
        set => SetValue(BreadcrumbBarProperty, value);
    }

    private static void OnBreadcrumbBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if (e.NewValue is null && e.OldValue is BreadcrumbBar oldValue)
        {
            oldValue.ItemClicked -= navigationView.BreadcrumbBarOnItemClicked;
            {
                return;
            }
        }

        if (e.NewValue is not BreadcrumbBar breadcrumbBar)
        {
            return;
        }

        breadcrumbBar.ItemsSource = navigationView._breadcrumbItems;

        breadcrumbBar.ItemTemplate ??= navigationView.TryFindResource(StyleKeys.NavigationViewItemDataTemplate) as DataTemplate;

        // todo 考察一下使用路由事件是否比强绑定要好一点
        breadcrumbBar.ItemClicked += navigationView.BreadcrumbBarOnItemClicked;
    }

    #endregion

    #region DP: LeftPaneCompactWidth

    /// <summary>
    /// Identifies the <see cref="LeftPaneCompactWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftPaneCompactWidthProperty = DependencyProperty.Register(
        nameof(LeftPaneCompactWidth),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D, OnLeftPaneTargetWidthChanged)
    );

    /// <summary>
    /// Gets or sets the width used by the built-in Left pane while <see cref="IsPaneOpen"/> is
    /// <see langword="false"/>. The value is also used as the fixed width of the current
    /// LeftMinimal template. Changing it while the compact Left pane is visible updates the pane
    /// immediately rather than replaying an open/close animation.
    /// </summary>
    public double LeftPaneCompactWidth
    {
        get => (double)GetValue(LeftPaneCompactWidthProperty);
        set => SetValue(LeftPaneCompactWidthProperty, value);
    }

    #endregion

    #region DP: ContentOverlay

    /// <summary>Identifies the <see cref="ContentOverlay"/> dependency property.</summary>
    public static readonly DependencyProperty ContentOverlayProperty = DependencyProperty.Register(
        nameof(ContentOverlay),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public object? ContentOverlay
    {
        get => GetValue(ContentOverlayProperty);
        set => SetValue(ContentOverlayProperty, value);
    }

    #endregion

    #region DP: EnableDebugMessages

    /// <summary>Identifies the <see cref="EnableDebugMessages"/> dependency property.</summary>
    public static readonly DependencyProperty EnableDebugMessagesProperty = DependencyProperty.Register(
        nameof(EnableDebugMessages),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether debugging messages for this control are enabled
    /// </summary>
    public bool EnableDebugMessages
    {
        get => (bool)GetValue(EnableDebugMessagesProperty);
        set => SetValue(EnableDebugMessagesProperty, value);
    }

    #endregion

    #region DP: FooterMenuItems

    private static readonly DependencyPropertyKey FooterMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(FooterMenuItems),
            typeof(ObservableCollection<object>),
            typeof(NavigationView),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="FooterMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemsProperty =
        FooterMenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList FooterMenuItems => (ObservableCollection<object>)GetValue(FooterMenuItemsProperty);

    #endregion

    #region DP: FooterMenuItemsSource

    /// <summary>Identifies the <see cref="FooterMenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty FooterMenuItemsSourceProperty = DependencyProperty.Register(
        nameof(FooterMenuItemsSource),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnFooterMenuItemsSourceChanged)
    );

    /// <inheritdoc/>
    [Bindable(true)]
    public object? FooterMenuItemsSource
    {
        get => GetValue(FooterMenuItemsSourceProperty);
        set => SetValue(FooterMenuItemsSourceProperty, value);
    }

    private static void OnFooterMenuItemsSourceChanged(
        DependencyObject?                  d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.ReplaceMenuItemsSource(
            navigationView.FooterMenuItems,
            e.OldValue,
            e.NewValue,
            navigationView.OnFooterMenuItemsSourceCollectionChanged
        );
    }

    private void OnFooterMenuItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NavigationViewItemsSourceSynchronizer.SyncCollectionFromSource(FooterMenuItems, sender, e);
    }

    #endregion

    #region DP: FrameMargin

    /// <summary>Identifies the <see cref="FrameMargin"/> dependency property.</summary>
    public static readonly DependencyProperty FrameMarginProperty = DependencyProperty.Register(
        nameof(FrameMargin),
        typeof(Thickness),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(default(Thickness))
    );

    /// <inheritdoc/>
    public Thickness FrameMargin
    {
        get => (Thickness)GetValue(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    #endregion

    #region DP: HasFooterMenuItems

    private static readonly DependencyPropertyKey HasFooterMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasFooterMenuItems),
            typeof(bool),
            typeof(NavigationView),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="HasFooterMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasFooterMenuItemsProperty =
        HasFooterMenuItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether <see cref="FooterMenuItems"/> contains at least one item.
    /// </summary>
    /// <remarks>
    /// The value is maintained by the control whenever the internal footer collection changes, including
    /// changes synchronized from <see cref="FooterMenuItemsSource"/>. As a read-only dependency property,
    /// it can be consumed by bindings and template triggers without allowing callers to create a state that
    /// disagrees with the collection.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasFooterMenuItems => (bool)GetValue(HasFooterMenuItemsProperty);

    #endregion

    #region DP: HasMenuItems

    private static readonly DependencyPropertyKey HasMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasMenuItems),
            typeof(bool),
            typeof(NavigationView),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="HasMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasMenuItemsProperty =
        HasMenuItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether <see cref="MenuItems"/> contains at least one item.
    /// </summary>
    /// <remarks>
    /// The value is maintained by the control whenever the internal menu collection changes, including
    /// changes synchronized from <see cref="MenuItemsSource"/>. As a read-only dependency property, it can
    /// be consumed by bindings and template triggers without allowing callers to create a state that
    /// disagrees with the collection.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasMenuItems => (bool)GetValue(HasMenuItemsProperty);

    #endregion

    #region DP: Header

    /// <summary>Identifies the <see cref="Header"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    #endregion

    #region DP: HeaderVisibility

    /// <summary>Identifies the <see cref="HeaderVisibility"/> dependency property.</summary>
    public static readonly DependencyProperty HeaderVisibilityProperty = DependencyProperty.Register(
        nameof(HeaderVisibility),
        typeof(Visibility),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(Visibility.Visible)
    );

    /// <inheritdoc/>
    public Visibility HeaderVisibility
    {
        get => (Visibility)GetValue(HeaderVisibilityProperty);
        set => SetValue(HeaderVisibilityProperty, value);
    }

    #endregion

    #region DP: IsBackButtonVisible

    /// <summary>Identifies the <see cref="IsBackButtonVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsBackButtonVisibleProperty = DependencyProperty.Register(
        nameof(IsBackButtonVisible),
        typeof(NavigationViewBackButtonVisible),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewBackButtonVisible.Auto, OnPaneButtonVisibilityChanged)
    );

    /// <inheritdoc/>
    public NavigationViewBackButtonVisible IsBackButtonVisible
    {
        get => (NavigationViewBackButtonVisible)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    #endregion

    #region DP: IsBackEnabled

    /// <summary>Identifies the <see cref="IsBackEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsBackEnabledProperty = DependencyProperty.Register(
        nameof(IsBackEnabled),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <inheritdoc/>
    public bool IsBackEnabled
    {
        get => (bool)GetValue(IsBackEnabledProperty);
        protected set => SetValue(IsBackEnabledProperty, value);
    }

    #endregion

    #region DP: IsFooterSeparatorVisible

    /// <summary>Identifies the <see cref="IsFooterSeparatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsFooterSeparatorVisibleProperty = DependencyProperty.Register(
        nameof(IsFooterSeparatorVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true)
    );

    /// <inheritdoc/>
    public bool IsFooterSeparatorVisible
    {
        get => (bool)GetValue(IsFooterSeparatorVisibleProperty);
        set => SetValue(IsFooterSeparatorVisibleProperty, value);
    }

    #endregion

    #region DP: IsPaneOpen

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true, OnIsPaneOpenChanged)
    );

    /// <inheritdoc/>
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    private static void OnIsPaneOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if ((bool)e.NewValue == (bool)e.OldValue)
        {
            return;
        }

        navigationView.SetValue(IsPaneOpenContextProperty, (bool)e.NewValue);
        navigationView.CloseAllNavigationViewItemSubmenus();

        if (navigationView.IsPaneOpen)
        {
            navigationView.OnPaneOpened();
        }
        else
        {
            navigationView.OnPaneClosed();
        }

        navigationView.CloseNavigationViewItemMenus();
        navigationView.RefreshNavigationItemActiveState();

        navigationView.UpdatePaneButtonPlacement();
        UpdateVisualState(navigationView);
    }

    #endregion

    #region DP: IsPaneToggleVisible

    /// <summary>Identifies the <see cref="IsPaneToggleVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneToggleVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneToggleVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true, OnPaneButtonVisibilityChanged)
    );

    /// <inheritdoc/>
    public bool IsPaneToggleVisible
    {
        get => (bool)GetValue(IsPaneToggleVisibleProperty);
        set => SetValue(IsPaneToggleVisibleProperty, value);
    }

    /// <summary>
    /// Shared callback for <see cref="IsBackButtonVisibleProperty"/> and
    /// <see cref="IsPaneToggleVisibleProperty"/>.
    /// </summary>
    private static void OnPaneButtonVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.UpdatePaneButtonPlacement();
        }
    }

    #endregion

    #region DP: IsPaneVisible

    /// <summary>Identifies the <see cref="IsPaneVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(false)
    );

    /// <inheritdoc/>
    public bool IsPaneVisible
    {
        get => (bool)GetValue(IsPaneVisibleProperty);
        set => SetValue(IsPaneVisibleProperty, value);
    }

    #endregion

    #region DP: IsTopSeparatorVisible

    /// <summary>Identifies the <see cref="IsTopSeparatorVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsTopSeparatorVisibleProperty = DependencyProperty.Register(
        nameof(IsTopSeparatorVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true)
    );

    /// <inheritdoc/>
    public bool IsTopSeparatorVisible
    {
        get => (bool)GetValue(IsTopSeparatorVisibleProperty);
        set => SetValue(IsTopSeparatorVisibleProperty, value);
    }

    #endregion

    #region DP: ItemContainerStyle

    /// <summary>Identifies the <see cref="ItemContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerStyleProperty =
        DependencyProperty.RegisterAttached(
            nameof(ItemContainerStyle),
            typeof(Style),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <inheritdoc/>
    public Style? ItemContainerStyle
    {
        get => (Style?)GetValue(ItemContainerStyleProperty);
        set => SetValue(ItemContainerStyleProperty, value);
    }

    // ReSharper disable UnusedMember.Global

    /// <summary>
    /// Helper for getting <see cref="ItemContainerStyleProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ItemContainerStyleProperty"/> from.
    /// </param>
    /// <returns>ItemContainerStyle property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static Style? GetItemContainerStyle(DependencyObject element)
    {
        return (Style?)element.GetValue(ItemContainerStyleProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ItemContainerStyleProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ItemContainerStyleProperty"/> on.
    /// </param>
    /// <param name="value">ItemContainerStyle property value.</param>
    public static void SetItemContainerStyle(DependencyObject element, Style? value)
    {
        element.SetValue(ItemContainerStyleProperty, value);
    }

    // ReSharper restore UnusedMember.Global

    #endregion

    #region DP: ItemContainerStyleSelector

    /// <summary>Identifies the <see cref="ItemContainerStyleSelector"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerStyleSelectorProperty =
        DependencyProperty.RegisterAttached(
            nameof(ItemContainerStyleSelector),
            typeof(StyleSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <inheritdoc/>
    public StyleSelector? ItemContainerStyleSelector
    {
        get => (StyleSelector?)GetValue(ItemContainerStyleSelectorProperty);
        set => SetValue(ItemContainerStyleSelectorProperty, value);
    }

    // ReSharper disable UnusedMember.Global

    /// <summary>
    /// Helper for getting <see cref="ItemContainerStyleSelectorProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ItemContainerStyleSelectorProperty"/> from.
    /// </param>
    /// <returns>ItemContainerStyleSelector property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static StyleSelector? GetItemContainerStyleSelector(DependencyObject element)
    {
        return (StyleSelector?)element.GetValue(ItemContainerStyleSelectorProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ItemContainerStyleSelectorProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ItemContainerStyleSelectorProperty"/> on.
    /// </param>
    /// <param name="value">ItemContainerStyleSelector property value.</param>
    public static void SetItemContainerStyleSelector(DependencyObject element, StyleSelector? value)
    {
        element.SetValue(ItemContainerStyleSelectorProperty, value);
    }

    // ReSharper restore UnusedMember.Global

    #endregion

    #region DP: ItemTemplate

    /// <summary>Identifies the <see cref="ItemTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateProperty = DependencyProperty.RegisterAttached(
        nameof(ItemTemplate),
        typeof(DataTemplate),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <inheritdoc/>
    public DataTemplate? ItemTemplate
    {
        get => (DataTemplate?)GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    // ReSharper disable UnusedMember.Global

    /// <summary>
    /// Helper for getting <see cref="ItemTemplateProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ItemTemplateProperty"/> from.
    /// </param>
    /// <returns>ItemTemplate property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static DataTemplate? GetItemTemplate(DependencyObject element)
    {
        return (DataTemplate?)element.GetValue(ItemTemplateProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ItemTemplateProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ItemTemplateProperty"/> on.
    /// </param>
    /// <param name="value">ItemTemplate property value.</param>
    public static void SetItemTemplate(DependencyObject element, DataTemplate? value)
    {
        element.SetValue(ItemTemplateProperty, value);
    }

    // ReSharper restore UnusedMember.Global

    #endregion

    #region DP: ItemTemplateSelector

    /// <summary>Identifies the <see cref="ItemTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateSelectorProperty =
        DependencyProperty.RegisterAttached(
            nameof(ItemTemplateSelector),
            typeof(DataTemplateSelector),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits)
        );

    /// <inheritdoc/>
    public DataTemplateSelector? ItemTemplateSelector
    {
        get => (DataTemplateSelector?)GetValue(ItemTemplateSelectorProperty);
        set => SetValue(ItemTemplateSelectorProperty, value);
    }

    // ReSharper disable UnusedMember.Global

    /// <summary>
    /// Helper for getting <see cref="ItemTemplateSelectorProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ItemTemplateSelectorProperty"/> from.
    /// </param>
    /// <returns>ItemTemplateSelector property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static DataTemplateSelector? GetItemTemplateSelector(DependencyObject element)
    {
        return (DataTemplateSelector?)element.GetValue(ItemTemplateSelectorProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ItemTemplateSelectorProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ItemTemplateSelectorProperty"/> on.
    /// </param>
    /// <param name="value">ItemTemplateSelector property value.</param>
    public static void SetItemTemplateSelector(DependencyObject element, DataTemplateSelector? value)
    {
        element.SetValue(ItemTemplateSelectorProperty, value);
    }

    // ReSharper restore UnusedMember.Global

    #endregion

    #region DP: MenuItems

    private static readonly DependencyPropertyKey MenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MenuItems),
        typeof(ObservableCollection<object>),
        typeof(NavigationView),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="MenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

    private void OnFooterMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ProcessRootMenuItemsCollectionChange(FooterMenuItems, HasFooterMenuItemsPropertyKey, e);
    }

    private void OnMenuItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ProcessRootMenuItemsCollectionChange(MenuItems, HasMenuItemsPropertyKey, e);
    }

    /// <summary>
    /// Maintains state owned by a root collection, then forwards the change to the shared navigation-data
    /// pipeline used by both roots and all descendant menu collections.
    /// </summary>
    private void ProcessRootMenuItemsCollectionChange(
        IList rootItems,
        DependencyPropertyKey hasItemsPropertyKey,
        NotifyCollectionChangedEventArgs e)
    {
        SetValue(hasItemsPropertyKey, rootItems.Count > 0);
        ProcessNavigationItemsCollectionChange(e);
    }

    #endregion

    #region DP: MenuItemsSource

    /// <summary>Identifies the <see cref="MenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsSourceProperty = DependencyProperty.Register(
        nameof(MenuItemsSource),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnMenuItemsSourceChanged)
    );

    /// <inheritdoc/>
    [Bindable(true)]
    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    private static void OnMenuItemsSourceChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.ReplaceMenuItemsSource(
            navigationView.MenuItems,
            e.OldValue,
            e.NewValue,
            navigationView.OnMenuItemsSourceCollectionChanged
        );
    }

    private void OnMenuItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NavigationViewItemsSourceSynchronizer.SyncCollectionFromSource(MenuItems, sender, e);
    }

    /// <summary>
    /// Replaces the external source mirrored by one of the control's stable root menu collections.
    /// </summary>
    /// <remarks>
    /// Both roots deliberately use the same replacement order. Detaching first prevents stale sources from
    /// mutating the control during replacement; populating the stable collection then sends every item through
    /// the normal root collection pipeline before live change tracking is attached to the new source.
    /// </remarks>
    private void ReplaceMenuItemsSource(
        IList targetCollection,
        object? oldSource,
        object? newSource,
        NotifyCollectionChangedEventHandler sourceCollectionChangedHandler)
    {
        if (oldSource is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= sourceCollectionChangedHandler;
        }

        targetCollection.Clear();

        if (newSource is IEnumerable newItemsSource and not string)
        {
            foreach (var item in newItemsSource)
            {
                _ = targetCollection.Add(item);
            }
        }
        else if (newSource is not null)
        {
            _ = targetCollection.Add(newSource);
        }

        if (newSource is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += sourceCollectionChangedHandler;
        }
    }

    #endregion

    #region DP: NavigationCacheSize

    /// <summary>Identifies the <see cref="NavigationCacheSize"/> dependency property.</summary>
    public static readonly DependencyProperty NavigationCacheSizeProperty = DependencyProperty.Register(
        nameof(NavigationCacheSize),
        typeof(int),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(10, OnNavigationCacheSizeChanged),
        static value => value is >= 0
    );

    /// <summary>
    /// Gets or sets the number of pages with <see cref="NavigationCacheMode.Enabled"/> kept in cache.
    /// Pages with <see cref="NavigationCacheMode.Required"/> do not count toward this size.
    /// <para>The default value is <c>10</c>.</para>
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public int NavigationCacheSize
    {
        get => (int)GetValue(NavigationCacheSizeProperty);
        set => SetValue(NavigationCacheSizeProperty, value);
    }

    private static void OnNavigationCacheSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView._cache.Trim((int)e.NewValue);
        }
    }

    #endregion

    #region DP: LeftPaneOpenWidth

    /// <summary>
    /// Identifies the <see cref="LeftPaneOpenWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftPaneOpenWidthProperty = DependencyProperty.Register(
        nameof(LeftPaneOpenWidth),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D, OnLeftPaneTargetWidthChanged)
    );

    /// <summary>
    /// Gets or sets the width used by the built-in Left pane while <see cref="IsPaneOpen"/> is
    /// <see langword="true"/>. This property is intentionally scoped to the Left template; the
    /// other display modes size their panes from their own content and layout contracts.
    /// </summary>
    public double LeftPaneOpenWidth
    {
        get => (double)GetValue(LeftPaneOpenWidthProperty);
        set => SetValue(LeftPaneOpenWidthProperty, value);
    }

    #endregion

    #region Read-only DP: LeftPaneWidth

    private static readonly DependencyPropertyKey LeftPaneWidthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(LeftPaneWidth),
            typeof(double),
            typeof(NavigationView),
            new FrameworkPropertyMetadata(0D)
        );

    /// <summary>
    /// Identifies the read-only <see cref="LeftPaneWidth"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LeftPaneWidthProperty =
        LeftPaneWidthPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the width retained for the built-in Left pane.
    /// </summary>
    /// <remarks>
    /// The value is initialized from <see cref="LeftPaneOpenWidth"/> or
    /// <see cref="LeftPaneCompactWidth"/> on the first Left template application and captures the
    /// pane's effective width before that template is replaced. Binding the Left template back to
    /// this retained value lets a later mode switch restore the pane without replaying its
    /// open/close animation.
    /// </remarks>
    public double LeftPaneWidth => (double)GetValue(LeftPaneWidthProperty);

    private static void OnLeftPaneTargetWidthChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.UpdateVisibleLeftPaneTargetWidth(e.Property);
        }
    }

    #endregion

    #region DP: PaneDisplayMode

    /// <summary>Identifies the <see cref="PaneDisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty PaneDisplayModeProperty = DependencyProperty.Register(
        nameof(PaneDisplayMode),
        typeof(NavigationViewPaneDisplayMode),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewPaneDisplayMode.Left, OnPaneDisplayModeChanged)
    );

    /// <inheritdoc/>
    public NavigationViewPaneDisplayMode PaneDisplayMode
    {
        get => (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);
        set => SetValue(PaneDisplayModeProperty, value);
    }

    private static void OnPaneDisplayModeChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        navigationView.SetValue(
            PaneDisplayModeContextProperty,
            (NavigationViewPaneDisplayMode)e.NewValue
        );

        // Close ContextMenu-owned state before the style trigger clears the outgoing template subtree.
        // IsExpanded is intentionally left untouched so an inline Left hierarchy survives a
        // presentation round trip.
        navigationView.CloseAllNavigationViewItemSubmenus();
        navigationView.PrepareForPaneDisplayModeChange(
            (NavigationViewPaneDisplayMode)e.OldValue
        );
        navigationView.OnPaneDisplayModeChanged();
    }

    #endregion

    #region DP: PaneFooter

    /// <summary>Identifies the <see cref="PaneFooter"/> dependency property.</summary>
    public static readonly DependencyProperty PaneFooterProperty = DependencyProperty.Register(
        nameof(PaneFooter),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public object? PaneFooter
    {
        get => GetValue(PaneFooterProperty);
        set => SetValue(PaneFooterProperty, value);
    }

    #endregion

    #region DP: PaneHeader

    /// <summary>Identifies the <see cref="PaneHeader"/> dependency property.</summary>
    public static readonly DependencyProperty PaneHeaderProperty = DependencyProperty.Register(
        nameof(PaneHeader),
        typeof(object),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public object? PaneHeader
    {
        get => GetValue(PaneHeaderProperty);
        set => SetValue(PaneHeaderProperty, value);
    }

    #endregion

    #region DP: PaneTitle

    /// <summary>Identifies the <see cref="PaneTitle"/> dependency property.</summary>
    public static readonly DependencyProperty PaneTitleProperty = DependencyProperty.Register(
        nameof(PaneTitle),
        typeof(string),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnPaneTitleChanged)
    );

    /// <inheritdoc/>
    public string? PaneTitle
    {
        get => (string?)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

    private static void OnPaneTitleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationView navigationView)
        {
            navigationView.PaneTitleChangedInternal?.Invoke(navigationView, EventArgs.Empty);
        }
    }

    #endregion

    #region DP: TitleBar

    /// <summary>
    /// Identifies the <see cref="TitleBar"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TitleBarProperty = DependencyProperty.Register(
        nameof(TitleBar),
        typeof(TitleBar),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnTitleBarChanged)
    );

    /// <summary>
    /// Gets or sets the title bar whose <see cref="Wpf.Ui.Controls.TitleBar.LeftContent"/> slot hosts
    /// the highest-priority visible navigation command.
    /// </summary>
    /// <remarks>
    /// Assigning a title bar grants this navigation view temporary ownership of that slot.
    /// The prior value is restored when the association is removed, provided the application
    /// has not replaced the contributed content in the meantime. No margins or other layout
    /// properties on either control are modified.
    /// </remarks>
    public TitleBar? TitleBar
    {
        get => (TitleBar?)GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    private static void OnTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        // TitleBar exposes a content contract, so coordination no longer requires either
        // control to know or mutate the other's margins or template geometry.
        navigationView.UpdatePaneButtonPlacement();
    }

    #endregion

    #region DP: Transition

    /// <summary>Identifies the <see cref="Transition"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionProperty = DependencyProperty.Register(
        nameof(Transition),
        typeof(Transition),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(Transition.FadeInWithSlide)
    );

    /// <inheritdoc/>
    public Transition Transition
    {
        get => (Transition)GetValue(TransitionProperty);
        set => SetValue(TransitionProperty, value);
    }

    #endregion

    #region DP: TransitionDuration

    /// <summary>Identifies the <see cref="TransitionDuration"/> dependency property.</summary>
    public static readonly DependencyProperty TransitionDurationProperty = DependencyProperty.Register(
        nameof(TransitionDuration),
        typeof(int),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(200)
    );

    /// <inheritdoc/>
    [Bindable(true)]
    [Category("Appearance")]
    public int TransitionDuration
    {
        get => (int)GetValue(TransitionDurationProperty);
        set => SetValue(TransitionDurationProperty, value);
    }

    #endregion
}
