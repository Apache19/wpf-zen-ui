// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.navigationviewitem?view=winrt-22621 */

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the container for an item in a NavigationView control.
/// When needed, it can be used as a normal button with a <see cref="ButtonBase.Click"/> action.
/// </summary>
public class NavigationViewItem : ButtonBase, INavigationViewItem
{
    // ═══════════════════════════════════════════════════════════════════════
    // Indentation
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly DependencyPropertyKey IndentationPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Indentation),
            typeof(Thickness),
            typeof(NavigationViewItem),
            new PropertyMetadata(default(Thickness))
        );

    /// <summary>Identifies the <see cref="Indentation"/> dependency property.</summary>
    public static readonly DependencyProperty IndentationProperty = IndentationPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the indentation applied to the item content based on its depth in the navigation tree.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public Thickness Indentation => (Thickness)GetValue(IndentationProperty);

    // ═══════════════════════════════════════════════════════════════════════
    // IndentationSize
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="IndentationSize"/> dependency property.</summary>
    public static readonly DependencyProperty IndentationSizeProperty = DependencyProperty.Register(
        nameof(IndentationSize),
        typeof(double),
        typeof(NavigationViewItem),
        new FrameworkPropertyMetadata(
            30d,
            static (d, e) => ((NavigationViewItem)d).UpdateIndentation()
        ),
        static value => value is double size and >= 0 && !double.IsInfinity(size)
    );

    /// <summary>
    /// Gets or sets the indentation added for each level in the navigation tree.
    /// </summary>
    [Category("Layout")]
    public double IndentationSize
    {
        get => (double)GetValue(IndentationSizeProperty);
        set => SetValue(IndentationSizeProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // MenuItems
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly DependencyPropertyKey MenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MenuItems),
        typeof(ObservableCollection<object>),
        typeof(NavigationViewItem),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="MenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

    // ═══════════════════════════════════════════════════════════════════════
    // MenuItemsSource
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="MenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsSourceProperty = DependencyProperty.Register(
        nameof(MenuItemsSource),
        typeof(object),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, OnMenuItemsSourceChanged)
    );

    /// <inheritdoc/>
    [Bindable(true)]
    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HasMenuItems
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    private static readonly DependencyPropertyKey HasMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasMenuItems),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasMenuItemsProperty =
        HasMenuItemsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether MenuItems.Count > 0
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasMenuItems => (bool)GetValue(HasMenuItemsProperty);

    // ═══════════════════════════════════════════════════════════════════════
    // IsActive
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    /// <inheritdoc />
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // IsPaneOpen
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PaneDisplayMode
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly DependencyPropertyKey PaneDisplayModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneDisplayMode),
            typeof(NavigationViewPaneDisplayMode),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(NavigationViewPaneDisplayMode.Left)
        );

    /// <summary>Identifies the <see cref="PaneDisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty PaneDisplayModeProperty =
        PaneDisplayModePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the pane display mode synchronized from the owning <see cref="NavigationView"/>.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public NavigationViewPaneDisplayMode PaneDisplayMode =>
        (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);

    // ═══════════════════════════════════════════════════════════════════════
    // IsSubItem
    // ═══════════════════════════════════════════════════════════════════════

    private static readonly DependencyPropertyKey IsSubItemPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsSubItem),
        typeof(bool),
        typeof(NavigationViewItem),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="IsSubItem"/> dependency property.</summary>
    public static readonly DependencyProperty IsSubItemProperty = IsSubItemPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this item belongs to another navigation item.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsSubItem => (bool)GetValue(IsSubItemProperty);

    // ═══════════════════════════════════════════════════════════════════════
    // IsExpanded
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="IsExpanded"/> dependency property.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    /// <inheritdoc />
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Icon
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, OnIconChanged, IconElement.Coerce)
    );

    /// <inheritdoc />
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TargetPageTag
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="TargetPageTag"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTagProperty = DependencyProperty.Register(
        nameof(TargetPageTag),
        typeof(string),
        typeof(NavigationViewItem),
        new PropertyMetadata(string.Empty, OnNavigationLookupPropertyChanged)
    );

    /// <inheritdoc />
    public string TargetPageTag
    {
        get => (string)GetValue(TargetPageTagProperty);
        set => SetValue(TargetPageTagProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // TargetPageType
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="TargetPageType"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTypeProperty = DependencyProperty.Register(
        nameof(TargetPageType),
        typeof(Type),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, OnNavigationLookupPropertyChanged)
    );

    /// <inheritdoc />
    public Type? TargetPageType
    {
        get => (Type?)GetValue(TargetPageTypeProperty);
        set => SetValue(TargetPageTypeProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // InfoBadge
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="InfoBadge"/> dependency property.</summary>
    public static readonly DependencyProperty InfoBadgeProperty = DependencyProperty.Register(
        nameof(InfoBadge),
        typeof(InfoBadge),
        typeof(NavigationViewItem),
        new PropertyMetadata(null)
    );

    public InfoBadge? InfoBadge
    {
        get => (InfoBadge?)GetValue(InfoBadgeProperty);
        set => SetValue(InfoBadgeProperty, value);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NavigationCacheMode
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>Identifies the <see cref="NavigationCacheMode"/> dependency property.</summary>
    public static readonly DependencyProperty NavigationCacheModeProperty = DependencyProperty.Register(
        nameof(NavigationCacheMode),
        typeof(NavigationCacheMode),
        typeof(NavigationViewItem),
        new FrameworkPropertyMetadata(NavigationCacheMode.Disabled, OnNavigationCacheModeChanged)
    );

    /// <inheritdoc/>
    public NavigationCacheMode NavigationCacheMode
    {
        get => (NavigationCacheMode)GetValue(NavigationCacheModeProperty);
        set => SetValue(NavigationCacheModeProperty, value);
    }

    /// <inheritdoc />
    public INavigationViewItem? NavigationViewItemParent
    {
        get;
        set
        {
            field = value;
            SetValue(IsSubItemPropertyKey, value is not null);

            if (value is NavigationViewItem parentItem)
            {
                SynchronizePaneState(parentItem.PaneDisplayMode, parentItem.IsPaneOpen);
            }

            UpdateIndentation();
        }
    }

    /// <inheritdoc />
    public bool IsMenuElement { get; set; }

    /// <inheritdoc />
    public string Id { get; }

    internal string SearchableContent { get; private set; } = string.Empty;

    private readonly List<INavigationViewItem> _attachedMenuItems = [];

    private NavigationView? _attachedNavigationView;

    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneOpenedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneClosedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneDisplayModeChangedHandler;

    internal NavigationView? NavigationViewHost =>
        _attachedNavigationView
     ?? NavigationView.GetNavigationParent(this)
     ?? (NavigationViewItemParent as NavigationViewItem)?.NavigationViewHost;

    static NavigationViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(typeof(NavigationViewItem))
        );

        // ═══════════════════════════════════════════════════════════════════
        // WARNING: Do NOT remove this coercion. The LeftFluentNavigationViewItemTemplate
        // (NavigationLeftFluent.xaml) contains an IsActive trigger whose EnterActions/
        // ExitActions storyboard animates (Border.BorderBrush).(SolidColorBrush.Opacity).
        // WPF's Storyboard.VerifyPathIsAnimatable requires EVERY intermediate object in a
        // compound property path to be a DependencyObject. If BorderBrush is ever null —
        // because a style setter was commented out, removed, or overridden — the storyboard
        // will throw InvalidOperationException with the message "'BorderBrush' property
        // does not point to a DependencyObject in path '(0).(1)'".
        //
        // This coercion is the LAST LINE OF DEFENSE. It guarantees BorderBrush is never
        // null, no matter what happens to styles, templates, or bindings upstream.
        // ═══════════════════════════════════════════════════════════════════
        Border.BorderBrushProperty.OverrideMetadata(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                Brushes.Transparent,
                null,
                (d, baseValue) => baseValue ?? Brushes.Transparent
            )
        );
    }

    public NavigationViewItem()
    {
        Id = Guid.NewGuid().ToString("n");

        Unloaded += static (sender, _) =>
        {
            var item = (NavigationViewItem)sender;
            item.DetachNavigationViewEvents();
        };

        Loaded += (_, _) => InitializeNavigationViewEvents();

        // Initialize the `Items` collection
        var menuItems = new ObservableCollection<object>();
        menuItems.CollectionChanged += OnMenuItems_CollectionChanged;
        SetValue(MenuItemsPropertyKey, menuItems);
    }

    public NavigationViewItem(Type targetPageType)
        : this()
    {
        SetValue(TargetPageTypeProperty, targetPageType);
    }

    public NavigationViewItem(string name, Type targetPageType)
        : this(targetPageType)
    {
        SetValue(ContentProperty, name);
    }

    public NavigationViewItem(string name, SymbolRegular icon, Type targetPageType)
        : this(targetPageType)
    {
        SetValue(ContentProperty, name);
        SetValue(IconProperty, new SymbolIcon
        {
            Symbol = icon
        });
    }

    // ReSharper disable once UnusedMember.Global
    public NavigationViewItem(string name, SymbolRegular icon, Type targetPageType, IList menuItems)
        : this(name, icon, targetPageType)
    {
        SetValue(MenuItemsSourceProperty, menuItems);
    }

    /// <summary>
    /// Correctly activates
    /// </summary>
    public virtual void Activate(INavigationView navigationView)
    {
        NavigationViewItemTemplateBehavior.SetIsActive(this, navigationView, true);
    }

    /// <summary>
    /// Correctly deactivates
    /// </summary>
    public virtual void Deactivate(INavigationView navigationView)
    {
        NavigationViewItemTemplateBehavior.SetIsActive(this, navigationView, false);
    }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (string.IsNullOrWhiteSpace(TargetPageTag) && Content is not null)
        {
            SetCurrentValue(
                TargetPageTagProperty,
                Content as string ?? Content.ToString()?.ToLower().Trim() ?? string.Empty
            );
        }
    }

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);

        SearchableContent = CreateSearchableContent(newContent);
        NavigationView.GetNavigationParent(this)?.NotifyNavigationViewItemContentChanged(this);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (NavigationView.GetNavigationParent(this) is not { } navigationView)
        {
            return;
        }

        if (HasMenuItems && IsPaneOpen)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }

        if (TargetPageType is not null)
        {
            navigationView.OnNavigationViewItemClick(this);
        }

        base.OnClick();
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NavigationViewItemAutomationPeer(this);
    }

    private void OnMenuItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SetValue(HasMenuItemsPropertyKey, MenuItems.Count > 0);

        var currentItems = MenuItems.OfType<INavigationViewItem>().ToList();

        foreach (var oldItem in _attachedMenuItems)
        {
            if (
                !currentItems.Any(item => ReferenceEquals(item, oldItem))
             && ReferenceEquals(oldItem.NavigationViewItemParent, this)
            )
            {
                oldItem.NavigationViewItemParent = null;
            }
        }

        _attachedMenuItems.Clear();

        foreach (var item in currentItems)
        {
            item.NavigationViewItemParent = this;
            _attachedMenuItems.Add(item);
        }
    }

    private void UpdateIndentation()
    {
        var depth  = 0;
        var parent = NavigationViewItemParent;

        while (parent is not null)
        {
            depth++;
            parent = parent.NavigationViewItemParent;
        }

        SetValue(IndentationPropertyKey, new Thickness(depth * IndentationSize, 0, 0, 0));

        foreach (NavigationViewItem item in MenuItems.OfType<NavigationViewItem>())
        {
            item.UpdateIndentation();
        }
    }

    private static string CreateSearchableContent(object? content)
    {
        if (content is null)
        {
            return string.Empty;
        }

        return RemoveAccessKeyMarkers(content as string ?? content.ToString() ?? string.Empty);
    }

    private static string RemoveAccessKeyMarkers(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        StringBuilder? builder = null;

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];

            if (
                current == '('
             && i + 3 < value.Length
             && IsAccessKeyMarker(value[i + 1])
             && IsAccessKeyCharacter(value[i + 2])
             && value[i + 3] == ')'
            )
            {
                builder ??= new StringBuilder(value.Length).Append(value, 0, i);
                builder.Append(value[i + 2]);
                i += 3;

                continue;
            }

            if (IsAccessKeyMarker(current) && i + 1 < value.Length && IsAccessKeyCharacter(value[i + 1]))
            {
                builder ??= new StringBuilder(value.Length).Append(value, 0, i);
                continue;
            }

            if (IsAccessKeyMarker(current) && i + 1 < value.Length && value[i + 1] == current)
            {
                builder ??= new StringBuilder(value.Length).Append(value, 0, i);
                builder.Append(current);
                i++;

                continue;
            }

            builder?.Append(current);
        }

        return builder?.ToString() ?? value;
    }

    private static bool IsAccessKeyMarker(char value) => value is '_' or '&';

    // AutoSuggestBox displays plain text rather than an AccessText visual. Only remove a marker
    // when its following character can be used as a practical keyboard mnemonic. In particular,
    // punctuation followed by whitespace (for example, "Date & Time") is content, not a marker.
    private static bool IsAccessKeyCharacter(char value) => char.IsLetterOrDigit(value);

    private static void OnNavigationCacheModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        NavigationView
           .GetNavigationParent(navigationViewItem)
          ?.NotifyNavigationCacheModeChanged(
                navigationViewItem,
                (NavigationCacheMode)e.OldValue,
                (NavigationCacheMode)e.NewValue
            );
    }

    private static void OnIconChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is NavigationViewItem navigationViewItem)
        {
            NavigationViewItemTemplateBehavior.UpdateIcon(navigationViewItem, e.OldValue as SymbolIcon);
        }
    }

    private static void OnNavigationLookupPropertyChanged(
        DependencyObject                   d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        NavigationView
           .GetNavigationParent(navigationViewItem)
          ?.NotifyNavigationViewItemLookupChanged(navigationViewItem);
    }

    private static void OnMenuItemsSourceChanged(DependencyObject? d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        if (e.OldValue is INotifyCollectionChanged oldCollection)
        {
            oldCollection.CollectionChanged -= navigationViewItem.OnMenuItemsSource_CollectionChanged;
        }

        navigationViewItem.MenuItems.Clear();

        if (e.NewValue is IEnumerable newItemsSource and not string)
        {
            foreach (var item in newItemsSource)
            {
                _ = navigationViewItem.MenuItems.Add(item);
            }
        }
        else if (e.NewValue != null)
        {
            _ = navigationViewItem.MenuItems.Add(e.NewValue);
        }

        if (e.NewValue is INotifyCollectionChanged newCollection)
        {
            newCollection.CollectionChanged += navigationViewItem.OnMenuItemsSource_CollectionChanged;
        }
    }

    private void OnMenuItemsSource_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NavigationViewItemsSourceSynchronizer.SyncCollectionFromSource(MenuItems, sender, e);
    }

    private void InitializeNavigationViewEvents()
    {
        if (NavigationView.GetNavigationParent(this) is not { } navigationView)
        {
            return;
        }

        // Unsubscribe from previous NavigationView if already attached (e.g., if Loaded fires again)
        DetachNavigationViewEvents();

        _attachedNavigationView = navigationView;
        SynchronizePaneState(navigationView.PaneDisplayMode, navigationView.IsPaneOpen);

        _paneOpenedHandler = (_, _) => SynchronizePaneState(navigationView.PaneDisplayMode, true);
        _paneClosedHandler = (_, _) => SynchronizePaneState(navigationView.PaneDisplayMode, false);
        _paneDisplayModeChangedHandler = (_, _) =>
            SynchronizePaneState(navigationView.PaneDisplayMode, navigationView.IsPaneOpen);

        navigationView.PaneOpened += _paneOpenedHandler;
        navigationView.PaneClosed += _paneClosedHandler;
        navigationView.PaneDisplayModeChanged += _paneDisplayModeChangedHandler;
    }

    private void DetachNavigationViewEvents()
    {
        if (_attachedNavigationView is null)
        {
            return;
        }

        if (_paneOpenedHandler is not null)
        {
            _attachedNavigationView.PaneOpened -= _paneOpenedHandler;
            _paneOpenedHandler                 =  null;
        }

        if (_paneClosedHandler is not null)
        {
            _attachedNavigationView.PaneClosed -= _paneClosedHandler;
            _paneClosedHandler                 =  null;
        }

        if (_paneDisplayModeChangedHandler is not null)
        {
            _attachedNavigationView.PaneDisplayModeChanged -= _paneDisplayModeChangedHandler;
            _paneDisplayModeChangedHandler = null;
        }

        _attachedNavigationView = null;
    }

    internal void SynchronizePaneState(NavigationViewPaneDisplayMode paneDisplayMode, bool isPaneOpen)
    {
        SetValue(PaneDisplayModePropertyKey, paneDisplayMode);
        SetCurrentValue(IsPaneOpenProperty, isPaneOpen);

        foreach (var item in MenuItems.OfType<NavigationViewItem>())
        {
            item.SynchronizePaneState(paneDisplayMode, isPaneOpen);
        }
    }
}
