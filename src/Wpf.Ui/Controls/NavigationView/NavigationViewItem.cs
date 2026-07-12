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
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the container for an item in a NavigationView control.
/// When needed, it can be used as a normal button with a <see cref="ButtonBase.Click"/> action.
/// </summary>
[TemplatePart(Name = TemplateElementChevronGrid, Type = typeof(Grid))]
public class NavigationViewItem : ButtonBase, INavigationViewItem
{
    protected const string TemplateElementChevronGrid = "PART_ChevronGrid";

    private static readonly DependencyPropertyKey IndentationPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(Indentation),
        typeof(Thickness),
        typeof(NavigationViewItem),
        new PropertyMetadata(default(Thickness))
    );

    private static readonly DependencyPropertyKey MenuItemsPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(MenuItems),
        typeof(ObservableCollection<object>),
        typeof(NavigationViewItem),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="Indentation"/> dependency property.</summary>
    public static readonly DependencyProperty IndentationProperty = IndentationPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="IndentationSize"/> dependency property.</summary>
    public static readonly DependencyProperty IndentationSizeProperty = DependencyProperty.Register(
        nameof(IndentationSize),
        typeof(double),
        typeof(NavigationViewItem),
        new FrameworkPropertyMetadata(30d, OnIndentationSizeChanged),
        static value => value is double size && size >= 0 && !double.IsInfinity(size)
    );

    /// <summary>Identifies the <see cref="MenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="MenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsSourceProperty = DependencyProperty.Register(
        nameof(MenuItemsSource),
        typeof(object),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, OnMenuItemsSourceChanged)
    );

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    internal static readonly DependencyPropertyKey HasMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasMenuItems),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty HasMenuItemsProperty =
        HasMenuItemsPropertyKey.DependencyProperty;

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(
        nameof(IsActive),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty = DependencyProperty.Register(
        nameof(IsPaneOpen),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="IsExpanded"/> dependency property.</summary>
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded),
        typeof(bool),
        typeof(NavigationViewItem),
        new PropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, null, IconElement.Coerce)
    );

    /// <summary>Identifies the <see cref="TargetPageTag"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTagProperty = DependencyProperty.Register(
        nameof(TargetPageTag),
        typeof(string),
        typeof(NavigationViewItem),
        new PropertyMetadata(string.Empty, OnNavigationLookupPropertyChanged)
    );

    /// <summary>Identifies the <see cref="TargetPageType"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTypeProperty = DependencyProperty.Register(
        nameof(TargetPageType),
        typeof(Type),
        typeof(NavigationViewItem),
        new PropertyMetadata(null, OnNavigationLookupPropertyChanged)
    );

    /// <summary>Identifies the <see cref="InfoBadge"/> dependency property.</summary>
    public static readonly DependencyProperty InfoBadgeProperty = DependencyProperty.Register(
        nameof(InfoBadge),
        typeof(InfoBadge),
        typeof(NavigationViewItem),
        new PropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="NavigationCacheMode"/> dependency property.</summary>
    public static readonly DependencyProperty NavigationCacheModeProperty = DependencyProperty.Register(
        nameof(NavigationCacheMode),
        typeof(NavigationCacheMode),
        typeof(NavigationViewItem),
        new FrameworkPropertyMetadata(NavigationCacheMode.Disabled, OnNavigationCacheModeChanged)
    );

    /// <summary>
    /// Gets the indentation applied to the item content based on its depth in the navigation tree.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public Thickness Indentation => (Thickness)GetValue(IndentationProperty);

    /// <summary>
    /// Gets or sets the indentation added for each level in the navigation tree.
    /// </summary>
    [Category("Layout")]
    public double IndentationSize
    {
        get => (double)GetValue(IndentationSizeProperty);
        set => SetValue(IndentationSizeProperty, value);
    }

    /// <inheritdoc/>
    public IList MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

    /// <inheritdoc/>
    [Bindable(true)]
    public object? MenuItemsSource
    {
        get => GetValue(MenuItemsSourceProperty);
        set => SetValue(MenuItemsSourceProperty, value);
    }

    /// <summary>
    /// Gets a value indicating whether MenuItems.Count > 0
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool HasMenuItems
    {
        get => (bool)GetValue(HasMenuItemsProperty);
    }

    /// <inheritdoc />
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsActive
    {
        get => (bool)GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    /// <inheritdoc />
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen
    {
        get => (bool)GetValue(IsPaneOpenProperty);
        set => SetValue(IsPaneOpenProperty, value);
    }

    /// <inheritdoc />
    [Bindable(true)]
    [Category("Appearance")]
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <inheritdoc />
    public string TargetPageTag
    {
        get => (string)GetValue(TargetPageTagProperty);
        set => SetValue(TargetPageTagProperty, value);
    }

    /// <inheritdoc />
    public Type? TargetPageType
    {
        get => (Type?)GetValue(TargetPageTypeProperty);
        set => SetValue(TargetPageTypeProperty, value);
    }

    public InfoBadge? InfoBadge
    {
        get => (InfoBadge?)GetValue(InfoBadgeProperty);
        set => SetValue(InfoBadgeProperty, value);
    }

    /// <inheritdoc/>
    public NavigationCacheMode NavigationCacheMode
    {
        get => (NavigationCacheMode)GetValue(NavigationCacheModeProperty);
        set => SetValue(NavigationCacheModeProperty, value);
    }

    /// <inheritdoc />
    public INavigationViewItem? NavigationViewItemParent
    {
        get => _navigationViewItemParent;
        set
        {
            _navigationViewItemParent = value;
            UpdateIndentation();
        }
    }

    /// <inheritdoc />
    public bool IsMenuElement { get; set; }

    /// <inheritdoc />
    public string Id { get; }

    internal string SearchableContent { get; private set; } = string.Empty;

    protected Grid? ChevronGrid { get; set; }

    private INavigationViewItem? _navigationViewItemParent;

    private NavigationView? _attachedNavigationView;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneOpenedHandler;
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneClosedHandler;

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
                CoerceBorderBrush
            )
        );
    }

    /// <summary>
    /// Ensures <see cref="System.Windows.Controls.Border.BorderBrush"/> is never null,
    /// so the <c>LeftFluentNavigationViewItemTemplate</c> storyboard always has a valid
    /// DependencyObject to animate. See the static constructor for the full backstory.
    /// </summary>
    private static object CoerceBorderBrush(DependencyObject d, object baseValue) =>
        baseValue ?? Brushes.Transparent;

    public NavigationViewItem()
    {
        Id = Guid.NewGuid().ToString("n");

        Unloaded += static (sender, _) =>
        {
            var item = (NavigationViewItem)sender;
            item.NavigationViewItemParent = null;
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
        SetCurrentValue(IsActiveProperty, true);

        if (!navigationView.IsPaneOpen && NavigationViewItemParent is not null)
        {
            NavigationViewItemParent.Activate(navigationView);
        }

        if (NavigationViewItemParent is not null)
        {
            if (
                navigationView.IsPaneOpen
             && navigationView.PaneDisplayMode != NavigationViewPaneDisplayMode.Top
            )
            {
                NavigationViewItemParent.IsExpanded = true;
            }
            else
            {
                NavigationViewItemParent.IsExpanded = false;
            }
        }

        if (
            Icon is SymbolIcon symbolIcon
         && navigationView.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftFluent
        )
        {
            symbolIcon.Filled = true;
        }
    }

    /// <summary>
    /// Correctly deactivates
    /// </summary>
    public virtual void Deactivate(INavigationView navigationView)
    {
        SetCurrentValue(IsActiveProperty, false);
        NavigationViewItemParent?.Deactivate(navigationView);

        if (!navigationView.IsPaneOpen && HasMenuItems)
        {
            SetCurrentValue(IsExpandedProperty, false);
        }

        if (
            Icon is SymbolIcon symbolIcon
         && navigationView.PaneDisplayMode == NavigationViewPaneDisplayMode.LeftFluent
        )
        {
            symbolIcon.Filled = false;
        }
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        if (GetTemplateChild(TemplateElementChevronGrid) is Grid chevronGrid)
        {
            ChevronGrid = chevronGrid;
        }
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

        if (HasMenuItems && navigationView.IsPaneOpen)
        {
            SetCurrentValue(IsExpandedProperty, !IsExpanded);
        }

        if (TargetPageType is not null)
        {
            navigationView.OnNavigationViewItemClick(this);
        }

        base.OnClick();
    }

    /// <summary>
    /// Is called when mouse is clicked down.
    /// </summary>
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        if (!HasMenuItems || e.LeftButton != MouseButtonState.Pressed)
        {
            base.OnMouseDown(e);
            return;
        }

        if (NavigationView.GetNavigationParent(this) is not { } navigationView)
        {
            return;
        }

        if (!navigationView.IsPaneOpen || ChevronGrid is null ||
            navigationView.PaneDisplayMode != NavigationViewPaneDisplayMode.Left)
        {
            base.OnMouseDown(e);
            return;
        }

        var mouseOverChevron = ActualWidth < e.GetPosition(this).X + ChevronGrid.ActualWidth;
        if (!mouseOverChevron)
        {
            base.OnMouseDown(e);
            return;
        }

        SetCurrentValue(IsExpandedProperty, !IsExpanded);

        foreach (var menuItem in MenuItems)
        {
            if (menuItem is not INavigationViewItem { IsActive: true })
            {
                continue;
            }

            if (IsExpanded)
            {
                Deactivate(navigationView);
            }
            else
            {
                Activate(navigationView);
            }

            break;
        }

        e.Handled = true;
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NavigationViewItemAutomationPeer(this);
    }

    private void OnMenuItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SetValue(HasMenuItemsPropertyKey, MenuItems.Count > 0);

        foreach (INavigationViewItem item in MenuItems.OfType<INavigationViewItem>())
        {
            item.NavigationViewItemParent = this;
        }
    }

    private void UpdateIndentation()
    {
        var depth = 0;
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

    private static void OnIndentationSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((NavigationViewItem)d).UpdateIndentation();
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

            if (current == '(' &&
                i + 3 < value.Length &&
                IsAccessKeyMarker(value[i + 1]) &&
                IsAccessKeyCharacter(value[i + 2]) &&
                value[i + 3] == ')')
            {
                builder ??= new StringBuilder(value.Length).Append(value, 0, i);
                builder.Append(value[i + 2]);
                i += 3;

                continue;
            }

            if (IsAccessKeyMarker(current) &&
                i + 1 < value.Length &&
                IsAccessKeyCharacter(value[i + 1]))
            {
                builder ??= new StringBuilder(value.Length).Append(value, 0, i);
                continue;
            }

            if (IsAccessKeyMarker(current) &&
                i + 1 < value.Length &&
                value[i + 1] == current)
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

        NavigationView.GetNavigationParent(navigationViewItem)?.NotifyNavigationCacheModeChanged(
            navigationViewItem,
            (NavigationCacheMode)e.OldValue,
            (NavigationCacheMode)e.NewValue
        );
    }

    private static void OnNavigationLookupPropertyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        NavigationView.GetNavigationParent(navigationViewItem)?.NotifyNavigationViewItemLookupChanged(
            navigationViewItem
        );
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
        SetCurrentValue(IsPaneOpenProperty, navigationView.IsPaneOpen);

        _paneOpenedHandler = (_, _) => SetCurrentValue(IsPaneOpenProperty, true);
        _paneClosedHandler = (_, _) => SetCurrentValue(IsPaneOpenProperty, false);

        navigationView.PaneOpened += _paneOpenedHandler;
        navigationView.PaneClosed += _paneClosedHandler;
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
            _paneOpenedHandler = null;
        }

        if (_paneClosedHandler is not null)
        {
            _attachedNavigationView.PaneClosed -= _paneClosedHandler;
            _paneClosedHandler = null;
        }

        _attachedNavigationView = null;
    }
}
