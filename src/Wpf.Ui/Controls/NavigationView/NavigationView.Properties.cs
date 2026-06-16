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

        // Unsubscribe from the old source to prevent memory leaks when the source is replaced
        if (e.OldValue is INotifyCollectionChanged oldOc)
        {
            oldOc.CollectionChanged -= navigationView.OnMenuItemsSource_CollectionChanged;
        }

        navigationView.MenuItems.Clear();

        if (e.NewValue is IEnumerable newItemsSource and not string)
        {
            foreach (var item in newItemsSource)
            {
                _ = navigationView.MenuItems.Add(item);
            }
        }
        else if (e.NewValue != null)
        {
            _ = navigationView.MenuItems.Add(e.NewValue);
        }

        if (e.NewValue is INotifyCollectionChanged oc)
        {
            oc.CollectionChanged += navigationView.OnMenuItemsSource_CollectionChanged;
        }
    }

    private void OnMenuItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NavigationViewItemsSourceSynchronizer.SyncCollectionFromSource(MenuItems, sender, e);
    }

    private void OnFooterMenuItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NavigationViewItemsSourceSynchronizer.SyncCollectionFromSource(FooterMenuItems, sender, e);
    }

    private void OnMenuItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                if (e.NewItems is not null)
                {
                    AddItemsToDictionaries(e.NewItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Remove:
            {
                if (e.OldItems is not null)
                {
                    RemoveItemsFromDictionaries(e.OldItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Replace:
            {
                if (e.OldItems is not null)
                {
                    RemoveItemsFromDictionaries(e.OldItems);
                }

                if (e.NewItems is not null)
                {
                    AddItemsToDictionaries(e.NewItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Reset:
            {
                RebuildDictionaries();
                break;
            }

            case NotifyCollectionChangedAction.Move:
                break;
        }

        UpdateAutoSuggestBoxSuggestions();
    }

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

        // Unsubscribe from the old source to prevent memory leaks when the source is replaced
        if (e.OldValue is INotifyCollectionChanged oldOc)
        {
            oldOc.CollectionChanged -= navigationView.OnFooterMenuItemsSource_CollectionChanged;
        }

        navigationView.FooterMenuItems.Clear();

        if (e.NewValue is IEnumerable newItemsSource and not string)
        {
            foreach (var item in newItemsSource)
            {
                _ = navigationView.FooterMenuItems.Add(item);
            }
        }
        else if (e.NewValue != null)
        {
            _ = navigationView.FooterMenuItems.Add(e.NewValue);
        }

        if (e.NewValue is INotifyCollectionChanged oc)
        {
            oc.CollectionChanged += navigationView.OnFooterMenuItemsSource_CollectionChanged;
        }
    }

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

    /// <summary>Identifies the <see cref="NavigationCacheSize"/> dependency property.</summary>
    public static readonly DependencyProperty NavigationCacheSizeProperty = DependencyProperty.Register(
        nameof(NavigationCacheSize),
        typeof(int),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(10, OnNavigationCacheSizeChanged),
        static value => value is int cacheSize && cacheSize >= 0
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

    /// <summary>Identifies the <see cref="IsBackButtonVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsBackButtonVisibleProperty = DependencyProperty.Register(
        nameof(IsBackButtonVisible),
        typeof(NavigationViewBackButtonVisible),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(NavigationViewBackButtonVisible.Auto)
    );

    /// <inheritdoc/>
    public NavigationViewBackButtonVisible IsBackButtonVisible
    {
        get => (NavigationViewBackButtonVisible)GetValue(IsBackButtonVisibleProperty);
        set => SetValue(IsBackButtonVisibleProperty, value);
    }

    /// <summary>Identifies the <see cref="IsPaneToggleVisible"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneToggleVisibleProperty = DependencyProperty.Register(
        nameof(IsPaneToggleVisible),
        typeof(bool),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(true)
    );

    /// <inheritdoc/>
    public bool IsPaneToggleVisible
    {
        get => (bool)GetValue(IsPaneToggleVisibleProperty);
        set => SetValue(IsPaneToggleVisibleProperty, value);
    }

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

        if (navigationView.IsPaneOpen)
        {
            navigationView.OnPaneOpened();
        }
        else
        {
            navigationView.OnPaneClosed();
        }

        navigationView.CloseNavigationViewItemMenus();

        navigationView.TitleBar?.SetCurrentValue(
            MarginProperty,
            navigationView.IsPaneOpen ? TitleBarPaneOpenMarginDefault : TitleBarPaneCompactMarginDefault
        );

        UpdateVisualState(navigationView);
    }

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

    /// <summary>Identifies the <see cref="OpenPaneLength"/> dependency property.</summary>
    public static readonly DependencyProperty OpenPaneLengthProperty = DependencyProperty.Register(
        nameof(OpenPaneLength),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D)
    );

    /// <inheritdoc/>
    public double OpenPaneLength
    {
        get => (double)GetValue(OpenPaneLengthProperty);
        set => SetValue(OpenPaneLengthProperty, value);
    }

    /// <summary>Identifies the <see cref="CompactPaneLength"/> dependency property.</summary>
    public static readonly DependencyProperty CompactPaneLengthProperty = DependencyProperty.Register(
        nameof(CompactPaneLength),
        typeof(double),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(0D)
    );

    /// <inheritdoc/>
    public double CompactPaneLength
    {
        get => (double)GetValue(CompactPaneLengthProperty);
        set => SetValue(CompactPaneLengthProperty, value);
    }

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

    /// <summary>Identifies the <see cref="PaneTitle"/> dependency property.</summary>
    public static readonly DependencyProperty PaneTitleProperty = DependencyProperty.Register(
        nameof(PaneTitle),
        typeof(string),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null)
    );

    /// <inheritdoc/>
    public string? PaneTitle
    {
        get => (string?)GetValue(PaneTitleProperty);
        set => SetValue(PaneTitleProperty, value);
    }

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

        navigationView.OnPaneDisplayModeChanged();
    }

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

        if (navigationView.TitleBar?.Margin == TitleBarPaneOpenMarginDefault
         && autoSuggestBox.Margin == default)
        {
            autoSuggestBox.Margin = AutoSuggestBoxMarginDefault;
        }
    }

    /// <summary>Identifies the <see cref="TitleBar"/> dependency property.</summary>
    public static readonly DependencyProperty TitleBarProperty = DependencyProperty.Register(
        nameof(TitleBar),
        typeof(TitleBar),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(null, OnTitleBarChanged)
    );

    /// <inheritdoc/>
    public TitleBar? TitleBar
    {
        get => (TitleBar?)GetValue(TitleBarProperty);
        set => SetValue(TitleBarProperty, value);
    }

    // todo: 工具栏和自动建议框的边距调整逻辑似乎不应该硬编码，可以考虑使用Style触发器或者VisualState来实现
    private static void OnTitleBarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationView navigationView)
        {
            return;
        }

        if (e.NewValue is null && e.OldValue is TitleBar oldValue)
        {
            navigationView.FrameMargin = new Thickness(0);
            oldValue.Margin            = new Thickness(0);

            if (navigationView.AutoSuggestBox?.Margin == AutoSuggestBoxMarginDefault)
            {
                navigationView.AutoSuggestBox.SetCurrentValue(MarginProperty, new Thickness(0));
            }

            return;
        }

        if (e.NewValue is not TitleBar titleBar)
        {
            return;
        }

        navigationView.FrameMargin = FrameMarginDefault;
        titleBar.Margin            = TitleBarPaneOpenMarginDefault;

        if (navigationView.AutoSuggestBox?.Margin is { Bottom: 0, Left: 0, Right: 0, Top: 0 })
        {
            navigationView.AutoSuggestBox.SetCurrentValue(MarginProperty, AutoSuggestBoxMarginDefault);
        }
    }

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

    /// <summary>Identifies the <see cref="ItemContainerTemplate"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerTemplateProperty = DependencyProperty.RegisterAttached(
        nameof(ItemContainerTemplate),
        typeof(ControlTemplate),
        typeof(NavigationView),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.Inherits
        )
    );

    /// <inheritdoc/>
    public ControlTemplate? ItemContainerTemplate
    {
        get => (ControlTemplate?)GetValue(ItemContainerTemplateProperty);
        set => SetValue(ItemContainerTemplateProperty, value);
    }

    /// <summary>
    /// Helper for getting <see cref="ItemContainerTemplateProperty"/> from <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to read <see cref="ItemContainerTemplateProperty"/> from.
    /// </param>
    /// <returns>ItemContainerTemplate property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static ControlTemplate? GetItemContainerTemplate(DependencyObject element)
    {
        return (ControlTemplate?)element.GetValue(ItemContainerTemplateProperty);
    }

    /// <summary>
    /// Helper for setting <see cref="ItemContainerTemplateProperty"/> on <paramref name="element"/>.
    /// </summary>
    /// <param name="element">
    /// <see cref="DependencyObject"/> to set <see cref="ItemContainerTemplateProperty"/> on.
    /// </param>
    /// <param name="value">ItemContainerTemplate property value.</param>
    public static void SetItemContainerTemplate(DependencyObject element, ControlTemplate? value)
    {
        element.SetValue(ItemContainerTemplateProperty, value);
    }

    /// <summary>Identifies the <see cref="ItemContainerStyle"/> dependency property.</summary>
    public static readonly DependencyProperty ItemContainerStyleProperty = DependencyProperty.RegisterAttached(
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

    /// <summary>Identifies the <see cref="ItemTemplateSelector"/> dependency property.</summary>
    public static readonly DependencyProperty ItemTemplateSelectorProperty = DependencyProperty.RegisterAttached(
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
}