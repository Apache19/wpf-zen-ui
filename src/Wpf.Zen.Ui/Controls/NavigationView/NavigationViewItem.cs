// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a hierarchical item container in a <see cref="NavigationView"/>.
/// </summary>
/// <remarks>
/// <para>
/// The item is an <see cref="HeaderedItemsControl"/>, not a button. Its
/// <see cref="HeaderedItemsControl.Header"/> is the
/// navigation row, while <see cref="ItemsControl.Items"/> is the authoritative child collection.
/// This distinction is important: focus, mouse capture and pressed state belong only to the bounded
/// <c>PART_HeaderButton</c>. The Chevron rendered by built-in templates is a non-interactive state
/// indicator; inline descendants and detached submenu content must never become part of the
/// button's
/// input or focus-scrolling bounds.
/// </para>
/// <para>
/// The open Left pane contains the ordinary <see cref="ItemsPresenter"/> and therefore displays
/// child <see cref="NavigationViewItem"/> containers inline. Compact and horizontal presentations
/// deliberately use a different projection: <c>PART_SubMenuContextMenu</c> receives a recursively
/// generated tree of native <see cref="MenuItem"/> controls. The projected controls bind back to
/// the source navigation containers but never reuse them as visuals, allowing WPF's menu system to
/// own cascading Popup windows, capture, focus, keyboard navigation and dismissal from root to
/// leaf.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_SubMenuContextMenu", Type = typeof(ContextMenu))]
[TemplatePart(Name = HeaderButtonPartName, Type = typeof(ButtonBase))]
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public partial class NavigationViewItem : HeaderedItemsControl
{
    private const string HeaderButtonPartName = "PART_HeaderButton";

    #region DP: Icon

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(NavigationViewItem),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>
    /// Identifies the <see cref="IconMargin"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(NavigationViewItem),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

    #region DP: Indentation

    private static readonly DependencyPropertyKey IndentationPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Indentation),
            typeof(Thickness),
            typeof(NavigationViewItem),
            new PropertyMetadata(default(Thickness))
        );

    /// <summary>
    /// Identifies the <see cref="Indentation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IndentationProperty = IndentationPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the indentation applied to the item content based on its depth in the navigation tree.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public Thickness Indentation => (Thickness)GetValue(IndentationProperty);

    #endregion

    #region DP: IndentationSize

    /// <summary>
    /// Identifies the <see cref="IndentationSize"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IndentationSizeProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: IsActive

    /// <summary>
    /// Identifies the <see cref="IsActive"/> dependency property.
    /// </summary>
    internal static readonly DependencyPropertyKey IsActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsActive),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false, OnIsActiveChanged)
        );

    /// <summary>
    /// Identifies the <see cref="IsActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsActiveProperty =
        IsActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this item is currently selected in the owning
    /// <see cref="NavigationView"/>.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsActive => (bool)GetValue(IsActiveProperty);

    #endregion

    #region DP: IsDescendantActive

    private static readonly DependencyPropertyKey IsDescendantActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsDescendantActive),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsDescendantActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsDescendantActiveProperty =
        IsDescendantActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether any descendant of this item is active. The default value is
    /// <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This state is independent of <see cref="IsActive"/> and includes every declared level below
    /// the current item, not only its direct children. Built-in Left templates use it to retain an
    /// active indicator on a collapsed branch while avoiding a second indicator when the active
    /// descendant is visible inside an expanded branch. The property is maintained by the semantic
    /// navigation-item tree, so it also remains meaningful while descendants are presented through
    /// a detached native menu or have not generated containers yet. Its metadata does not directly
    /// invalidate measure, arrange, or rendering; templates opt into the state with triggers.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsDescendantActive => (bool)GetValue(IsDescendantActiveProperty);

    #endregion

    #region DP: IsBranchActive

    private static readonly DependencyPropertyKey IsBranchActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsBranchActive),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsBranchActive"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsBranchActiveProperty =
        IsBranchActivePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this item or any of its descendants is active. The default
    /// value is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This property is equivalent to <see cref="IsActive"/> or
    /// <see cref="IsDescendantActive"/>. It provides templates with one stable branch-level state
    /// while preserving the distinct meanings of the two source properties.
    /// </remarks>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsBranchActive => (bool)GetValue(IsBranchActiveProperty);

    #endregion

    #region DP: IsPaneOpen

    /// <summary>
    /// Identifies the <see cref="IsPaneOpen"/> dependency property.
    /// </summary>
    private static readonly DependencyPropertyKey IsPaneOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPaneOpen),
            typeof(bool),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(true)
        );

    /// <summary>
    /// Identifies the <see cref="IsPaneOpen"/> dependency property.
    /// </summary>
    /// <remarks>
    /// The public property is read-only. An internal inheritable context receives the owning view's
    /// value and copies it through the private key, so callers cannot override presentation state
    /// on
    /// individual items and every item avoids per-instance event subscriptions.
    /// </remarks>
    public static readonly DependencyProperty IsPaneOpenProperty =
        IsPaneOpenPropertyKey.DependencyProperty;

    private static readonly DependencyProperty IsPaneOpenContextProperty =
        NavigationView.IsPaneOpenContextProperty.AddOwner(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                true,
                FrameworkPropertyMetadataOptions.Inherits,
                OnIsPaneOpenContextChanged
            )
        );

    /// <summary>
    /// Gets a value indicating whether the owning <see cref="NavigationView"/> pane is open.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen => (bool)GetValue(IsPaneOpenProperty);

    #endregion

    #region DP: PaneDisplayMode

    /// <summary>
    /// Identifies the <see cref="PaneDisplayMode"/> dependency property.
    /// </summary>
    private static readonly DependencyPropertyKey PaneDisplayModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PaneDisplayMode),
            typeof(NavigationViewPaneDisplayMode),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(NavigationViewPaneDisplayMode.Left)
        );

    /// <summary>
    /// Identifies the <see cref="PaneDisplayMode"/> dependency property.
    /// </summary>
    /// <remarks>
    /// See <see cref="IsPaneOpenProperty"/> for the inherited context contract.
    /// </remarks>
    public static readonly DependencyProperty PaneDisplayModeProperty =
        PaneDisplayModePropertyKey.DependencyProperty;

    private static readonly DependencyProperty PaneDisplayModeContextProperty =
        NavigationView.PaneDisplayModeContextProperty.AddOwner(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                NavigationViewPaneDisplayMode.Left,
                FrameworkPropertyMetadataOptions.Inherits,
                OnPaneDisplayModeContextChanged
            )
        );

    /// <summary>
    /// Gets the pane display mode synchronized from the owning <see cref="NavigationView"/>.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public NavigationViewPaneDisplayMode PaneDisplayMode =>
        (NavigationViewPaneDisplayMode)GetValue(PaneDisplayModeProperty);

    #endregion

    #region DP: IsSubItem

    private static readonly DependencyPropertyKey IsSubItemPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsSubItem),
            typeof(bool),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="IsSubItem"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSubItemProperty = IsSubItemPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this item belongs to another navigation item.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsSubItem => (bool)GetValue(IsSubItemProperty);

    #endregion

    #region DP: IsExpanded

    /// <summary>
    /// Identifies the <see cref="IsExpanded"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(false, OnIsExpandedChanged)
        );

    /// <summary>
    /// Gets or sets a value indicating whether children are expanded inline in the open Left pane.
    /// The default value is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This property does not carry layout metadata itself; the Left item template observes it to
    /// create or remove the inline child presenter. Detached menu presentations preserve but
    /// otherwise ignore this value and use <see cref="IsSubmenuOpen"/> instead.
    /// </remarks>
    [Browsable(false)]
    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    #endregion

    #region DP: IsSubmenuOpen

    /// <summary>
    /// Identifies the <see cref="IsSubmenuOpen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSubmenuOpenProperty =
        DependencyProperty.Register(
            nameof(IsSubmenuOpen),
            typeof(bool),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                false,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnIsSubmenuOpenChanged
            )
        );

    /// <summary>
    /// Gets or sets a value indicating whether this item's detached submenu is open. The default
    /// value is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This property represents only transient detached-menu state. The expanded Left pane uses
    /// <see cref="IsExpanded"/> for its inline hierarchy instead. Separating the two states keeps
    /// an inline branch from opening a ContextMenu merely because <see
    /// cref="NavigationView.PaneDisplayMode"/>
    /// selected another control template.
    /// </remarks>
    [Bindable(true)]
    [Browsable(false)]
    public bool IsSubmenuOpen
    {
        get => (bool)GetValue(IsSubmenuOpenProperty);
        set => SetValue(IsSubmenuOpenProperty, value);
    }

    #endregion

    #region DP: TargetPageTag

    /// <summary>
    /// Identifies the <see cref="TargetPageTag"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetPageTagProperty =
        DependencyProperty.Register(
            nameof(TargetPageTag),
            typeof(string),
            typeof(NavigationViewItem),
            new PropertyMetadata(string.Empty, OnNavigationLookupPropertyChanged)
        );

    /// <summary>
    /// Gets or sets the navigation tag used by directly supplied container items.
    /// </summary>
    public string TargetPageTag
    {
        get => (string)GetValue(TargetPageTagProperty);
        set => SetValue(TargetPageTagProperty, value);
    }

    #endregion

    #region DP: TargetPageType

    /// <summary>
    /// Identifies the <see cref="TargetPageType"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetPageTypeProperty =
        DependencyProperty.Register(
            nameof(TargetPageType),
            typeof(Type),
            typeof(NavigationViewItem),
            new PropertyMetadata(null, OnNavigationLookupPropertyChanged)
        );

    /// <summary>
    /// Gets or sets the page type used by directly supplied container items.
    /// </summary>
    public Type? TargetPageType
    {
        get => (Type?)GetValue(TargetPageTypeProperty);
        set => SetValue(TargetPageTypeProperty, value);
    }

    #endregion

    #region DP: InfoBadge

    /// <summary>
    /// Identifies the <see cref="InfoBadge"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty InfoBadgeProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: SearchText

    /// <summary>
    /// Identifies the <see cref="SearchText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(
            nameof(SearchText),
            typeof(string),
            typeof(NavigationViewItem),
            new PropertyMetadata(
                null,
                (d, e) => ((NavigationViewItem)d).UpdateSearchableContent()
            )
        );

    /// <summary>
    /// Gets or sets the text used to locate this item through the navigation search box.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value derives the searchable text from
    /// <see cref="HeaderedItemsControl.Header"/>. Set
    /// an empty string to exclude the item from search, or bind an explicit value when the header
    /// is a visual or a view-model whose textual representation is not suitable for search.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public string? SearchText
    {
        get => (string?)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    #endregion

    #region DP: NavigationCacheMode

    /// <summary>
    /// Identifies the <see cref="NavigationCacheMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty NavigationCacheModeProperty =
        DependencyProperty.Register(
            nameof(NavigationCacheMode),
            typeof(NavigationCacheMode),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                NavigationCacheMode.Disabled,
                OnNavigationCacheModeChanged
            )
        );

    /// <summary>
    /// Gets or sets the page caching policy used by this direct container item.
    /// </summary>
    public NavigationCacheMode NavigationCacheMode
    {
        get => (NavigationCacheMode)GetValue(NavigationCacheModeProperty);
        set => SetValue(NavigationCacheModeProperty, value);
    }

    #endregion

    /// <summary>
    /// Gets the realized parent container in the visual menu hierarchy.
    /// </summary>
    public NavigationViewItem? NavigationViewItemParent => _parentItem;

    /// <summary>
    /// Gets or sets the presentation-independent entry represented by this realized container.
    /// </summary>
    /// <remarks>
    /// Generated containers receive this association from their owner and release it when cleared.
    /// Keeping it internal prevents application code from treating container lifetime as data
    /// lifetime.
    /// </remarks>
    internal NavigationEntry? NavigationEntry { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the owner projected
    /// <see cref="INavigationItem.Children"/> into this
    /// container rather than receiving an explicit ItemsSource value from application styling.
    /// </summary>
    internal bool UsesNavigationDataChildren { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the owner projected the current icon from
    /// <see cref="INavigationItem.Icon"/>.
    /// </summary>
    internal bool UsesNavigationDataIcon { get; set; }

    /// <summary>
    /// Gets the stable identifier used when this container is supplied directly.
    /// </summary>
    public string Id { get; }

    internal string SearchableContent { get; private set; } = string.Empty;

    private readonly HashSet<NavigationViewItem> _directNavigationChildren = [];
    private NavigationViewItem?                  _parentItem;
    private ButtonBase?                          _headerButton;
    private BindingBase?                         _inheritedItemContainerStyleBinding;
    private BindingBase?                         _inheritedItemContainerStyleSelectorBinding;
    private ItemsControl?                        _itemContainerPresentationOwner;
    private bool                                 _hadItems;

    private NavigationView? NavigationViewHost =>
        NavigationView.GetNavigationParent(this) ?? _parentItem?.NavigationViewHost;

    /// <summary>
    /// Gets a value indicating whether the current navigation branch owns an inline child list
    /// rather than a ContextMenu.
    /// Left is the only mode with an expandable pane, and it uses inline children only while that
    /// pane is open. Every detached presentation uses <see cref="IsSubmenuOpen"/>.
    /// </summary>
    /// <remarks>
    /// The semantic root is authoritative for the complete branch. Descendants supplied through
    /// <see cref="ItemsControl.ItemsSource"/> may never enter the NavigationView visual tree before
    /// they are projected into a native menu, so their inheritable presentation context can still
    /// contain its default Left/open values. Likewise, a descendant removed from an inline
    /// presenter can temporarily retain its former inherited values. Reading those values from the
    /// descendant would reject a native <see cref="MenuItem"/> submenu transition even while the
    /// root's detached <see cref="ContextMenu"/> is open. Resolving through the semantic root keeps
    /// input, dependency-property validation, and UI Automation on one presentation model.
    /// </remarks>
    internal bool UsesInlineExpansion
    {
        get
        {
            NavigationViewItem root = GetRootNavigationItem();
            return root.PaneDisplayMode == NavigationViewPaneDisplayMode.Left && root.IsPaneOpen;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the effective child menu is open.
    /// </summary>
    internal bool IsChildMenuOpen => UsesInlineExpansion ? IsExpanded : IsSubmenuOpen;

    static NavigationViewItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(typeof(NavigationViewItem))
        );

        // The container owns descendants and can be much taller than its row. Only the template's
        // header button participates in focus navigation; focusing the container itself would make
        // FrameworkElement request that the complete expanded branch be scrolled into view.

        FocusableProperty.OverrideMetadata(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(false)
        );

        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Gets the realized header input surface for automation and focus coordination.
    /// </summary>
    internal ButtonBase? HeaderButton => _headerButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with an empty
    /// header
    /// and child collection.
    /// </summary>
    public NavigationViewItem()
    {
        Id = Guid.NewGuid().ToString("n");

        Unloaded += static (sender, _) => ((NavigationViewItem)sender).CloseSubmenuBranch();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class from the complete
    /// set of commonly configured content, navigation, hierarchy, badge, and caching values.
    /// </summary>
    /// <param name="header">The content displayed in the item header.</param>
    /// <param name="icon">The optional icon displayed beside the header.</param>
    /// <param name="targetPageType">
    /// The optional page type activated by the header action. A <see langword="null"/> value
    /// creates
    /// a grouping item whose header only controls its child presentation.
    /// </param>
    /// <param name="itemsSource">
    /// The optional source used by the standard WPF items pipeline to generate child items.
    /// </param>
    /// <param name="targetPageTag">
    /// The optional explicit navigation tag. When omitted, initialization derives a tag from a
    /// non-null header using the same rules as property-based construction.
    /// </param>
    /// <param name="infoBadge">The optional badge displayed in the item header.</param>
    /// <param name="navigationCacheMode">
    /// The caching behavior used for the target page. The default is
    /// <see cref="NavigationCacheMode.Disabled"/>.
    /// </param>
    /// <remarks>
    /// Only non-null optional values are assigned locally. This preserves style and binding
    /// precedence for omitted properties while allowing named arguments to express combinations
    /// such as a grouping item with children or an icon-only navigation target without an object
    /// initializer.
    /// </remarks>
    public NavigationViewItem(
        object? header,
        object? icon = null,
        Type? targetPageType = null,
        IEnumerable? itemsSource = null,
        string? targetPageTag = null,
        InfoBadge? infoBadge = null,
        NavigationCacheMode navigationCacheMode = NavigationCacheMode.Disabled)
        : this()
    {
        if (header is not null)
        {
            SetValue(HeaderProperty, header);
        }

        if (icon is not null)
        {
            SetValue(IconProperty, icon);
        }

        if (targetPageType is not null)
        {
            SetValue(TargetPageTypeProperty, targetPageType);
        }

        if (itemsSource is not null)
        {
            SetValue(ItemsSourceProperty, itemsSource);
        }

        if (targetPageTag is not null)
        {
            SetValue(TargetPageTagProperty, targetPageTag);
        }

        if (infoBadge is not null)
        {
            SetValue(InfoBadgeProperty, infoBadge);
        }

        if (navigationCacheMode != NavigationCacheMode.Disabled)
        {
            SetValue(NavigationCacheModeProperty, navigationCacheMode);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with a Fluent
    /// symbol and optional header, target page, children, tag, badge, and caching behavior.
    /// </summary>
    /// <param name="header">The optional content displayed in the item header.</param>
    /// <param name="icon">The Fluent symbol displayed beside the header.</param>
    /// <param name="targetPageType">The optional page type activated by the header action.</param>
    /// <param name="itemsSource">The optional source used to generate child items.</param>
    /// <param name="targetPageTag">The optional explicit navigation tag.</param>
    /// <param name="infoBadge">The optional badge displayed in the item header.</param>
    /// <param name="navigationCacheMode">The caching behavior used for the target page.</param>
    public NavigationViewItem(
        object?             header,
        FluentSymbolRegular icon,
        Type?               targetPageType = null,
        IEnumerable?        itemsSource = null,
        string?             targetPageTag = null,
        InfoBadge?          infoBadge = null,
        NavigationCacheMode navigationCacheMode = NavigationCacheMode.Disabled)
        : this(
            header,
            new FluentIconSource(icon),
            targetPageType,
            itemsSource,
            targetPageTag,
            infoBadge,
            navigationCacheMode
        )
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class without a header
    /// and with an icon and optional target page and children.
    /// </summary>
    /// <param name="icon">The icon displayed by the item.</param>
    /// <param name="targetPageType">The optional page type activated by the item.</param>
    /// <param name="itemsSource">The optional source used to generate child items.</param>
    public NavigationViewItem(
        object       icon,
        Type?        targetPageType,
        IEnumerable? itemsSource = null)
        : this(null, icon, targetPageType, itemsSource)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class without a header
    /// and with a Fluent symbol and optional target page and children.
    /// </summary>
    /// <param name="icon">The Fluent symbol displayed by the item.</param>
    /// <param name="targetPageType">The optional page type activated by the item.</param>
    /// <param name="itemsSource">The optional source used to generate child items.</param>
    public NavigationViewItem(
        FluentSymbolRegular icon,
        Type?               targetPageType = null,
        IEnumerable?        itemsSource    = null) : this((object?)null, icon, targetPageType, itemsSource)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class for the specified
    /// page type.
    /// </summary>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(Type targetPageType)
        : this(null, icon: null, targetPageType)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with a text header
    /// and target page.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(string name, Type targetPageType)
        : this(name, icon: null, targetPageType)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with a text header,
    /// symbol, and target page.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="icon">The Fluent symbol displayed beside the header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(string name, FluentSymbolRegular icon, Type targetPageType)
        : this((object?)name, icon, targetPageType)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class whose children are
    /// supplied by an enumerable source.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="icon">The Fluent symbol displayed beside the header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    /// <param name="itemsSource">
    /// The source used by the standard WPF items pipeline to generate children.
    /// </param>
    // ReSharper disable once UnusedMember.Global
    public NavigationViewItem(string name, FluentSymbolRegular icon, Type targetPageType, IEnumerable itemsSource)
        : this((object?)name, icon, targetPageType, itemsSource)
    {
    }

    /// <summary>
    /// Marks this item active while preserving inline expansion and submenu-open state, then
    /// invokes
    /// <see cref="OnActivated"/>.
    /// </summary>
    /// <remarks>
    /// Registered items update their navigation entry; unregistered items own local state. This
    /// method changes activation only and does not navigate, change SelectedItem, or clear other
    /// items.
    /// </remarks>
    public void Activate(NavigationView navigationView)
    {
        if (NavigationEntry is { } entry)
        {
            entry.Activate(navigationView);
        }
        else
        {
            ApplyActiveState(navigationView, true);
        }
    }

    /// <summary>
    /// Marks this item inactive without changing the active state of its ancestors, then invokes
    /// <see cref="OnDeactivated"/>.
    /// </summary>
    /// <remarks>
    /// Registered items clear their entry's activation, including ancestor summaries. The current
    /// page and navigation history are unaffected, and container recreation preserves this state.
    /// </remarks>
    public void Deactivate(NavigationView navigationView)
    {
        if (NavigationEntry is { } entry)
        {
            entry.Deactivate(navigationView);
        }
        else
        {
            ApplyActiveState(navigationView, false);
        }
    }

    /// <summary>
    /// Projects activation without writing back to the owning navigation entry.
    /// </summary>
    /// <remarks>
    /// Container generation and release use this path so presentation lifetime cannot activate or
    /// deactivate a semantic item. Hooks observe the completed dependency-property transition.
    /// </remarks>
    internal void ApplyActiveState(NavigationView navigationView, bool isActive)
    {
        object? session = navigationView.NavigationSessionIdentity;
        SetValue(IsActivePropertyKey, isActive);
        if (IsActive != isActive || !ReferenceEquals(session, navigationView.NavigationSessionIdentity))
        {
            return;
        }

        if (isActive)
        {
            OnActivated(navigationView);
        }
        else
        {
            OnDeactivated(navigationView);
        }
    }

    /// <summary>
    /// Called after this item and its semantic ancestors have observed the active state.
    /// </summary>
    /// <param name="navigationView">The navigation view that activated the item.</param>
    protected virtual void OnActivated(NavigationView navigationView) { }

    /// <summary>
    /// Called after this item and its semantic ancestors have observed the inactive state.
    /// </summary>
    /// <param name="navigationView">The navigation view that deactivated the item.</param>
    protected virtual void OnDeactivated(NavigationView navigationView) { }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        if (string.IsNullOrWhiteSpace(TargetPageTag) && Header is not null)
        {
            SetCurrentValue(
                TargetPageTagProperty,
                Header as string ?? Header.ToString()?.ToLower().Trim() ?? string.Empty
            );
        }
    }

    /// <inheritdoc />
    protected override void OnHeaderChanged(object oldHeader, object newHeader)
    {
        base.OnHeaderChanged(oldHeader, newHeader);

        UpdateSearchableContent();
    }

    /// <inheritdoc />
    /// <remarks>
    /// The template button is intentionally subscribed here instead of handling mouse input on the
    /// container. A detached ContextMenu has its own presentation source, while a Click
    /// raised by the concrete header button unambiguously identifies the intended input surface.
    /// A missing part leaves a non-interactive custom template but does not compromise item
    /// generation.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachSubmenuContextMenu();
        DetachHeaderParts();
        base.OnApplyTemplate();

        _headerButton = GetTemplateChild(HeaderButtonPartName) as ButtonBase;
        _headerButton?.Click += OnHeaderButtonClick;

        AttachSubmenuContextMenu(
            GetTemplateChild(SubmenuContextMenuPartName) as ContextMenu
        );
    }

    /// <inheritdoc />
    /// <remarks>
    /// A ContextMenu creates a separate presentation source that can survive removal of the
    /// template visual tree. Closing and detaching the native menu before WPF disconnects that tree
    /// prevents an orphaned menu window and releases the projected MenuItem bindings promptly.
    /// </remarks>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CloseSubmenuBranch();
        DetachSubmenuContextMenu();
        DetachHeaderParts();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item)
    {
        return item is NavigationViewItem;
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride()
    {
        return new NavigationViewItem();
    }

    /// <inheritdoc />
    /// <remarks>
    /// WPF copies <see cref="ItemsControl.ItemTemplate"/> and container-presentation properties in
    /// the base implementation only when a data item receives a generated container. A directly
    /// supplied <see cref="NavigationViewItem"/> is its own container, so the framework
    /// deliberately
    /// skips that copy. <see cref="InheritItemContainerPresentationFrom"/> fills that gap for the
    /// child generator without changing the style already selected for the child itself.
    /// </remarks>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is NavigationViewItem child)
        {
            if (ReferenceEquals(element, item))
            {
                child.InheritItemContainerPresentationFrom(this);
            }

            NavigationViewList.PrepareDataContainer(child, item);
            child.AttachToParent(this);

            if (!ReferenceEquals(element, item))
            {
                NavigationViewHost?.RegisterGeneratedNavigationItem(child, item);
            }
        }
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationViewItem child)
        {
            if (ReferenceEquals(element, item))
            {
                child.ClearInheritedItemContainerPresentation(this);
            }
            else
            {
                NavigationViewHost?.UnregisterGeneratedNavigationItem(child);

                NavigationViewList.ClearDataContainer(child);

                if (ReferenceEquals(child._parentItem, this))
                {
                    child.DetachFromParent(this);
                }
            }
        }

        base.ClearContainerForItemOverride(element, item);
    }

    /// <inheritdoc />
    protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);

        bool hasItems = HasItems;
        if (_hadItems != hasItems)
        {
            OnHasItemsChanged(_hadItems, hasItems);
            _hadItems = hasItems;
        }

        // Reset notifications do not include OldItems. Retain the previous direct-control set so a
        // Clear or ItemsSource replacement can release semantic parents just as reliably as an
        // individual Remove. Generated data containers use ClearContainerForItemOverride instead.
        var currentDirectChildren = new HashSet<NavigationViewItem>(
            Items.OfType<NavigationViewItem>()
        );

        foreach (NavigationViewItem child in _directNavigationChildren)
        {
            if (!currentDirectChildren.Contains(child))
            {
                child.DetachFromParent(this);
            }
        }

        foreach (NavigationViewItem child in currentDirectChildren)
        {
            child.AttachToParent(this);
        }

        _directNavigationChildren.Clear();
        _directNavigationChildren.UnionWith(currentDirectChildren);

        if (!HasItems)
        {
            SetCurrentValue(IsExpandedProperty, false);
            SetCurrentValue(IsSubmenuOpenProperty, false);
        }

        // Each source item owns collection change notification through ItemsControl. Rebuilding the
        // currently open root projection here keeps dynamic menus exact without a parallel web of
        // collection subscriptions between projected MenuItems and every descendant ItemCollection.
        InvalidateOpenSubmenuProjection();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (HandleChildMenuKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NavigationViewItemAutomationPeer(this);
    }

    private void DetachHeaderParts()
    {
        _headerButton?.Click -= OnHeaderButtonClick;
        _headerButton = null;
    }

    private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
    {
        _ = InvokePrimaryAction();
        e.Handled = true;
    }

    /// <summary>
    /// Executes every action represented by the navigation-item surface.
    /// </summary>
    /// <remarks>
    /// Navigation and child presentation are deliberately one semantic operation. An item that has
    /// both a target and children navigates and toggles its children from the same bounded header
    /// surface. The Chevron is only a visual projection of the resulting open state and therefore
    /// never participates in input, focus, capture, or UI Automation as a second control.
    /// </remarks>
    /// <returns><see langword="true"/> when an action was performed.</returns>
    internal bool InvokePrimaryAction()
    {
        // Snapshot the child action before navigation. Navigation can synchronously update
        // selection, content, bindings, or menu state; the physical click must still apply the
        // state that was requested when the operation began.
        bool hasItems = HasItems;
        bool shouldOpenChildMenu = hasItems && !IsChildMenuOpen;
        bool actionPerformed = false;

        if (
            NavigationViewHost is { } navigationView
            && navigationView.TryGetNavigationEntry(this, out NavigationEntry? entry)
            && entry?.TargetPageType is not null
        )
        {
            actionPerformed = navigationView.OnNavigationViewItemClick(this);
        }

        if (hasItems)
        {
            SetChildMenuOpen(shouldOpenChildMenu, focusFirstItem: false);
            actionPerformed = true;
        }

        OnInvoked();
        return actionPerformed;
    }

    /// <summary>
    /// Called after the primary action has applied navigation and child-menu state.
    /// </summary>
    /// <remarks>
    /// Override this method to append item-specific behavior without replacing the input,
    /// navigation, expansion, or native-menu state machines owned by the control.
    /// </remarks>
    protected virtual void OnInvoked() { }

    /// <summary>
    /// Sets the effective child-menu state for the active presentation.
    /// </summary>
    internal void SetChildMenuOpen(bool isOpen, bool focusFirstItem)
    {
        if (isOpen && !HasItems)
        {
            return;
        }

        SetCurrentValue(UsesInlineExpansion ? IsExpandedProperty : IsSubmenuOpenProperty, isOpen);

        if (isOpen && focusFirstItem)
        {
            FocusFirstSubmenuItem();
        }
    }

    /// <summary>
    /// Closes this detached submenu and every cascading submenu below it.
    /// </summary>
    internal void CloseSubmenuBranch()
    {
        SetCurrentValue(IsSubmenuOpenProperty, false);

        foreach (NavigationViewItem child in GetNavigationChildren())
        {
            child.CloseSubmenuBranch();
        }
    }

    private bool HandleChildMenuKey(Key key)
    {
        if (key == Key.Escape && (IsSubmenuOpen || HasOpenSubmenuAncestor()))
        {
            NavigationViewItem root = GetRootNavigationItem();
            NavigationViewHost?.CloseAllNavigationViewItemSubmenus();
            _ = root.FocusHeader();
            return true;
        }

        if (UsesInlineExpansion)
        {
            if (key == Key.Right && HasItems)
            {
                SetChildMenuOpen(true, focusFirstItem: false);
                return true;
            }

            if (key == Key.Left)
            {
                if (IsExpanded)
                {
                    SetChildMenuOpen(false, focusFirstItem: false);
                }
                else if (NavigationViewItemParent is NavigationViewItem parentItem)
                {
                    _ = parentItem.FocusHeader();
                }

                return true;
            }

            return false;
        }

        // Once focus enters the projected ContextMenu tree, WPF's MenuItem implementation owns all
        // submenu arrows, Escape handling and sibling selection. Source NavigationViewItem
        // descendants are not visual menu containers and must not run a second keyboard model.
        if (IsSubItem)
        {
            return false;
        }

        Key openKey = PaneDisplayMode switch
        {
            NavigationViewPaneDisplayMode.Top => Key.Down,
            NavigationViewPaneDisplayMode.Bottom => Key.Up,
            _ => Key.Right,
        };

        Key closeKey = PaneDisplayMode switch
        {
            NavigationViewPaneDisplayMode.Top => Key.Up,
            NavigationViewPaneDisplayMode.Bottom => Key.Down,
            _ => Key.Left,
        };

        if (key == openKey && HasItems)
        {
            SetChildMenuOpen(true, focusFirstItem: true);
            return true;
        }

        if (key == closeKey && IsSubmenuOpen)
        {
            SetChildMenuOpen(false, focusFirstItem: false);
            return true;
        }

        return false;
    }

    private bool HasOpenSubmenuAncestor()
    {
        for (NavigationViewItem? parent = NavigationViewItemParent; parent is not null; parent = parent.NavigationViewItemParent)
        {
            if (parent is { IsSubmenuOpen: true })
            {
                return true;
            }
        }

        return false;
    }

    private NavigationViewItem GetRootNavigationItem()
    {
        var root = this;
        while (root.NavigationViewItemParent is NavigationViewItem parentItem)
        {
            root = parentItem;
        }

        return root;
    }

    private void FocusFirstSubmenuItem()
    {
        if (UsesInlineExpansion)
        {
            // Inline children remain ordinary NavigationViewItem containers.
            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.Input,
                new Action(() =>
                {
                    for (int index = 0; index < Items.Count; index++)
                    {
                        if (
                            ItemContainerGenerator.ContainerFromIndex(index) is NavigationViewItem child &&
                            child.FocusHeader()
                        )
                        {
                            return;
                        }
                    }
                })
            );
            return;
        }

        FocusFirstProjectedMenuItem();
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

        foreach (NavigationViewItem item in GetNavigationChildren())
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

    private static bool IsAccessKeyMarker(char value)
    {
        return value is '_' or '&';
    }

    // AutoSuggestBox displays plain text rather than an AccessText visual. Only remove a marker
    // when its following character can be used as a practical keyboard mnemonic. In particular,
    // punctuation followed by whitespace (for example, "Date & Time") is content, not a marker.
    private static bool IsAccessKeyCharacter(char value)
    {
        return char.IsLetterOrDigit(value);
    }

    private void UpdateSearchableContent()
    {
        SearchableContent = SearchText ?? CreateSearchableContent(Header);
        NavigationViewHost?.NotifyNavigationViewItemContentChanged(this);
    }

    private static void OnNavigationCacheModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        if (
            navigationViewItem is
            {
                NavigationViewHost: { } navigationView,
                NavigationEntry: { } entry
            }
        )
        {
            navigationView.NotifyNavigationCacheModeChanged(
                entry,
                (NavigationCacheMode)e.OldValue,
                (NavigationCacheMode)e.NewValue
            );
        }
    }

    /// <summary>
    /// Propagates active-state changes through semantic parents rather than the visual tree.
    /// </summary>
    /// <remarks>
    /// Compact and horizontal modes project descendants into independent MenuItems hosted by
    /// Popup presentation sources, so routed events, inherited properties, and visual ancestry
    /// cannot reliably describe whether a branch contains the active item. Registered ancestors
    /// read entry summaries; standalone ancestors summarize their semantic child containers.
    /// </remarks>
    private static void OnIsActiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;
        item.UpdateBranchActiveState();
        item._parentItem?.UpdateDescendantActiveState();
        NavigationViewItemAutomationPeer.RaiseSelectionAutomationEvent(
            item,
            (bool)e.OldValue,
            (bool)e.NewValue
        );
    }

    /// <summary>
    /// Coordinates the native menu projection at the dependency-property boundary. External
    /// bindings therefore use the same root ContextMenu and receive the same descendant cleanup and
    /// UIA notification as pointer and keyboard input.
    /// </summary>
    private static void OnIsSubmenuOpenChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;
        bool oldValue = (bool)e.OldValue;
        bool newValue = (bool)e.NewValue;

        if (newValue && (!item.HasItems || item.UsesInlineExpansion))
        {
            // Detached-menu state is transient presentation state, not a second expansion flag. A
            // leaf or the inline Left presentation has no menu surface to own this value, so reject
            // externally supplied values that would leave automation and template state divergent.
            item.SetCurrentValue(IsSubmenuOpenProperty, false);
            return;
        }

        NavigationViewItem root = item.GetRootNavigationItem();

        if (newValue)
        {
            item.OpenSubmenuAncestors();

            if (ReferenceEquals(item, root))
            {
                // WPF's MenuBase capture closes other visible ContextMenus, but programmatic/UIA
                // state can change before either template is connected to a presentation source.
                // Preserve the same single-root invariant independently of realization timing.
                item.NavigationViewHost?.CloseOtherNavigationViewItemSubmenus(root);
            }

            root.PrepareSubmenuProjectionForOpen();
        }
        else
        {
            foreach (NavigationViewItem child in item.GetNavigationChildren())
            {
                child.CloseSubmenuBranch();
            }
        }

        // Only the semantic root may start the ContextMenu session. A descendant can retain an
        // instantiated NavigationViewItem template after an earlier inline presentation; binding
        // its IsSubmenuOpen value to that template's ContextMenu would create a competing root menu
        // precisely when the projected native MenuItem tries to open its submenu.
        if (ReferenceEquals(item, root))
        {
            root.SynchronizeRootContextMenuState(newValue);
        }
        else
        {
            // Descendants are independent native MenuItems. Explicit source/UIA requests flow into
            // those controls here; native pointer and keyboard transitions flow back through
            // SubmenuOpened/SubmenuClosed without participating in WPF's internal state transition.
            root.SynchronizeProjectedSubmenuState(item, newValue);
        }

        if (item is { UsesInlineExpansion: false, HasItems: true })
        {
            NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
                item,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            );
        }

        item.OnIsSubmenuOpenChanged(oldValue, newValue);
    }

    /// <summary>
    /// Opens every semantic ancestor before a projected MenuItem is asked to expose its submenu.
    /// WPF still owns the actual cascade; these values only make programmatic expansion and UIA
    /// requests converge on the same source state mirrored by the projected MenuItems.
    /// </summary>
    private void OpenSubmenuAncestors()
    {
        var ancestors = new Stack<NavigationViewItem>();
        for (
            var parent = NavigationViewItemParent;
            parent is not null;
            parent = parent.NavigationViewItemParent
        )
        {
            ancestors.Push(parent);
        }

        while (ancestors.Count > 0)
        {
            NavigationViewItem ancestor = ancestors.Pop();
            if (!ancestor.UsesInlineExpansion && ancestor.HasItems)
            {
                ancestor.SetCurrentValue(IsSubmenuOpenProperty, true);
            }
        }
    }

    /// <summary>
    /// Reports inline state changes only while inline expansion is the active presentation.
    /// </summary>
    private static void OnIsExpandedChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;

        if (item.UsesInlineExpansion && item.HasItems)
        {
            NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
                item,
                (bool)e.OldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                (bool)e.NewValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            );
        }

        item.OnIsExpandedChanged((bool)e.OldValue, (bool)e.NewValue);
    }

    /// <summary>
    /// This method is invoked when the <see cref="IsExpandedProperty"/> changes.
    /// </summary>
    /// <param name="oldValue">
    /// The old value of <see cref="IsExpandedProperty"/>.
    /// </param>
    /// <param name="newValue">
    /// The new value of <see cref="IsExpandedProperty"/>.
    /// </param>
    protected virtual void OnIsExpandedChanged(bool oldValue, bool newValue) { }

    /// <summary>
    /// Called after detached submenu state, projection, and automation have been synchronized.
    /// </summary>
    protected virtual void OnIsSubmenuOpenChanged(bool oldValue, bool newValue) { }

    /// <summary>
    /// Reports transitions to and from UI Automation's LeafNode state. This callback runs before
    /// collection cleanup resets an open-state property, preventing a misleading Expanded-to-
    /// Collapsed notification when the item actually became a leaf.
    /// </summary>
    private void OnHasItemsChanged(bool oldValue, bool newValue)
    {
        ExpandCollapseState oldState = oldValue
            ? IsChildMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            : ExpandCollapseState.LeafNode;

        ExpandCollapseState newState = newValue
            ? IsChildMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            : ExpandCollapseState.LeafNode;

        NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
            this,
            oldState,
            newState
        );
    }

    private static void OnNavigationLookupPropertyChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e
    )
    {
        if (d is not NavigationViewItem navigationViewItem)
        {
            return;
        }

        navigationViewItem.NavigationViewHost?.NotifyNavigationViewItemLookupChanged(
            navigationViewItem
        );
    }

    /// <summary>
    /// Projects the inherited pane-open context into the public read-only presentation property.
    /// </summary>
    /// <remarks>
    /// The context property is deliberately distinct from <see cref="IsPaneOpenProperty"/>. Making
    /// the public property inheritable would let an application override presentation state on an
    /// individual item, while making it writable would create contradictory states inside one
    /// navigation view. A private key keeps the public contract observational and still allows the
    /// template to use an ordinary dependency-property trigger.
    /// </remarks>
    private static void OnIsPaneOpenContextChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;
        item.SetValue(IsPaneOpenPropertyKey, (bool)e.NewValue);

        // Detached-menu state belongs to the presentation that created it. Inline expansion is
        // retained so reopening the Left pane restores the user's hierarchy state without retaining
        // a ContextMenu from the compact presentation.
        item.SetCurrentValue(IsSubmenuOpenProperty, false);
    }

    /// <summary>
    /// Projects the inherited pane mode into the public read-only presentation property.
    /// </summary>
    /// <remarks>
    /// A mode switch replaces the active item template. Closing the transient menu before that
    /// replacement prevents its independent presentation source from outliving the old template;
    /// <see cref="IsExpanded"/> remains untouched because it represents persistent tree state.
    /// </remarks>
    private static void OnPaneDisplayModeContextChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;

        item.SetValue(
            PaneDisplayModePropertyKey,
            (NavigationViewPaneDisplayMode)e.NewValue
        );

        item.SetCurrentValue(IsSubmenuOpenProperty, false);
    }

    /// <summary>
    /// Associates this container with its semantic parent item.
    /// </summary>
    private void AttachToParent(NavigationViewItem parent)
    {
        if (ReferenceEquals(_parentItem, parent))
        {
            return;
        }

        _parentItem = parent;
        SetValue(IsSubItemPropertyKey, true);
        UpdateIndentation();
        parent.UpdateDescendantActiveState();
    }

    /// <summary>
    /// Releases parent-owned state when this container leaves an item generator.
    /// </summary>
    private void DetachFromParent(NavigationViewItem parent)
    {
        if (!ReferenceEquals(_parentItem, parent))
        {
            return;
        }

        CloseSubmenuBranch();
        _parentItem = null;
        SetValue(IsSubItemPropertyKey, false);
        UpdateIndentation();
        parent.UpdateDescendantActiveState();
    }

    /// <summary>
    /// Projects the owning entry's branch state, or summarizes children of an unregistered control.
    /// </summary>
    /// <remarks>
    /// Registered items use the complete source hierarchy regardless of realization. Standalone
    /// controls propagate changes through their local parents. Attachment and detachment use the
    /// same boundary so container generation cannot create a competing source of branch state.
    /// </remarks>
    internal void UpdateDescendantActiveState()
    {
        // Registered containers project the source tree exclusively, including unrealized data.
        // Only standalone control trees derive state from child containers; combining these two
        // sources would let a stale visual projection override a committed entry transition.
        bool isDescendantActive;
        if (NavigationEntry is { } entry)
        {
            isDescendantActive = entry.IsDescendantActive;
        }
        else
        {
            isDescendantActive = false;
            foreach (NavigationViewItem child in GetNavigationChildren())
            {
                // A generator can briefly expose a container while it is being cleared.
                if (ReferenceEquals(child._parentItem, this) && (child.IsActive || child.IsDescendantActive))
                {
                    isDescendantActive = true;
                    break;
                }
            }
        }

        if (IsDescendantActive == isDescendantActive)
        {
            return;
        }

        SetValue(IsDescendantActivePropertyKey, isDescendantActive);
        UpdateBranchActiveState();
        _parentItem?.UpdateDescendantActiveState();
    }

    /// <summary>
    /// Keeps the template-facing branch state synchronized with its source states.
    /// </summary>
    private void UpdateBranchActiveState()
    {
        bool isBranchActive = IsActive || IsDescendantActive;

        if (IsBranchActive != isBranchActive)
        {
            SetValue(IsBranchActivePropertyKey, isBranchActive);
        }
    }

    /// <summary>
    /// Enumerates realized child containers without duplicating directly supplied controls.
    /// </summary>
    /// <remarks>
    /// Direct control items are available before layout; data items acquire a container only when
    /// WPF's generator realizes them. Submenu arbitration and recursive cleanup operate on
    /// containers, so this method deliberately combines both paths.
    /// </remarks>
    internal IEnumerable<NavigationViewItem> GetNavigationChildren()
    {
        var seen = new HashSet<NavigationViewItem>();

        foreach (NavigationViewItem child in Items.OfType<NavigationViewItem>())
        {
            if (seen.Add(child))
            {
                yield return child;
            }
        }

        for (int index = 0; index < Items.Count; index++)
        {
            if (
                ItemContainerGenerator.ContainerFromIndex(index) is NavigationViewItem child &&
                seen.Add(child)
            )
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Moves keyboard focus to the bounded header input surface.
    /// </summary>
    internal bool FocusHeader()
    {
        ApplyTemplate();
        return _headerButton is { IsEnabled: true, Focusable: true } && _headerButton.Focus();
    }

    /// <summary>
    /// Reveals the header row without moving keyboard focus or opening child menus.
    /// </summary>
    /// <remarks>
    /// An expanded container includes its entire subtree and can exceed the viewport. Requesting
    /// only the header lets WPF's ScrollViewer.MakeVisible choose the minimum useful scroll. A
    /// custom template without the header part falls back to the container's bounds.
    /// </remarks>
    internal bool TryBringHeaderIntoView()
    {
        if (!IsVisible)
        {
            return false;
        }

        ApplyTemplate();
        FrameworkElement header = _headerButton ?? (FrameworkElement)this;
        if (!header.IsVisible)
        {
            return false;
        }

        header.BringIntoView();
        return true;
    }

    /// <summary>
    /// Extends WPF's item-container presentation inheritance to a navigation item that is supplied
    /// directly rather than generated from data.
    /// </summary>
    /// <param name="owner">
    /// The list or parent navigation item whose children include this item.
    /// </param>
    /// <remarks>
    /// <para>
    /// WPF's internal <c>ItemsControl.PrepareItemsControl</c> copies
    /// <see cref="ItemsControl.ItemContainerStyle"/> and
    /// <see cref="ItemsControl.ItemContainerStyleSelector"/> into a generated
    /// <see cref="HeaderedItemsControl"/> so the same presentation continues through deeper data
    /// levels. It intentionally does nothing when the item is already its own container. Without
    /// this complement, a NavigationView-level container style reaches a directly declared root
    /// item but not that item's directly declared descendants.
    /// </para>
    /// <para>
    /// Each non-null owner value is bound only while its target property still has the default
    /// value.
    /// Consequently, an <see cref="ItemsControl.ItemContainerStyle"/> or selector declared directly
    /// on this item, including one supplied by its own Style, remains a branch-local override. A
    /// binding rather than a copied value also keeps the default chain synchronized when the owning
    /// NavigationView changes its menu or footer container presentation at runtime.
    /// </para>
    /// </remarks>
    internal void InheritItemContainerPresentationFrom(ItemsControl owner)
    {
        if (ReferenceEquals(_itemContainerPresentationOwner, owner))
        {
            return;
        }

        ClearInheritedItemContainerPresentation();

        _inheritedItemContainerStyleBinding = BindDefaultContainerPresentationProperty(
            owner,
            ItemContainerStyleProperty
        );
        _inheritedItemContainerStyleSelectorBinding = BindDefaultContainerPresentationProperty(
            owner,
            ItemContainerStyleSelectorProperty
        );

        if (
            _inheritedItemContainerStyleBinding is not null
            || _inheritedItemContainerStyleSelectorBinding is not null
        )
        {
            _itemContainerPresentationOwner = owner;
        }
    }

    /// <summary>
    /// Releases container-presentation bindings owned by the specified former items owner.
    /// </summary>
    internal void ClearInheritedItemContainerPresentation(ItemsControl owner)
    {
        if (ReferenceEquals(_itemContainerPresentationOwner, owner))
        {
            ClearInheritedItemContainerPresentation();
        }
    }

    /// <summary>
    /// Creates a one-way fallback binding only when application code or this item's Style has not
    /// already configured the target property.
    /// </summary>
    private BindingBase? BindDefaultContainerPresentationProperty(
        ItemsControl owner,
        DependencyProperty property)
    {
        // Match WPF's copy rule by leaving a null source value completely unset. Installing a
        // null-valued local Binding would unnecessarily outrank an ItemContainerStyle supplied by
        // an implicit or subsequently applied Style on the direct navigation item.
        if (
            owner.GetValue(property) is null
            || DependencyPropertyHelper.GetValueSource(this, property).BaseValueSource
                != BaseValueSource.Default
        )
        {
            return null;
        }

        var binding = new Binding
        {
            Source = owner,
            Path = new PropertyPath(property),
            Mode = BindingMode.OneWay,
        };
        BindingOperations.SetBinding(this, property, binding);
        return binding;
    }

    /// <summary>
    /// Clears only bindings that are still owned by this inheritance mechanism. If application
    /// code replaced either binding with a local value, that explicit branch configuration must
    /// survive removal and reparenting.
    /// </summary>
    private void ClearInheritedItemContainerPresentation()
    {
        ClearInheritedItemContainerPresentationBinding(
            ItemContainerStyleProperty,
            _inheritedItemContainerStyleBinding
        );
        ClearInheritedItemContainerPresentationBinding(
            ItemContainerStyleSelectorProperty,
            _inheritedItemContainerStyleSelectorBinding
        );

        _inheritedItemContainerStyleBinding = null;
        _inheritedItemContainerStyleSelectorBinding = null;
        _itemContainerPresentationOwner = null;
    }

    /// <summary>
    /// Clears an inherited binding without disturbing an application replacement.
    /// </summary>
    private void ClearInheritedItemContainerPresentationBinding(
        DependencyProperty property,
        BindingBase? inheritedBinding)
    {
        if (
            inheritedBinding is not null
            && ReferenceEquals(
                BindingOperations.GetBindingBase(this, property),
                inheritedBinding
            )
        )
        {
            BindingOperations.ClearBinding(this, property);
        }
    }

    /// <summary>
    /// Establishes the ownership and presentation context of a root item container.
    /// </summary>
    /// <remarks>
    /// Ordinary dependency-property inheritance is sufficient below this item, but a root items
    /// presenter can be disconnected when a pane closes or a template changes. Source bindings keep
    /// that root associated with its owning NavigationView while it is unrealized; descendants then
    /// inherit from the root without allocating one binding or event subscription per item. This
    /// also forms an explicit scope boundary for NavigationViews nested inside another pane.
    /// </remarks>
    internal void AttachToNavigationViewHost(NavigationView navigationView)
    {
        SetCurrentValue(NavigationView.NavigationParentProperty, navigationView);

        BindingOperations.SetBinding(
            this,
            PaneDisplayModeContextProperty,
            new Binding(nameof(NavigationView.PaneDisplayMode))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );

        BindingOperations.SetBinding(
            this,
            IsPaneOpenContextProperty,
            new Binding(nameof(NavigationView.IsPaneOpen))
            {
                Source = navigationView,
                Mode = BindingMode.OneWay,
            }
        );
    }

    /// <summary>
    /// Releases context installed by <see cref="AttachToNavigationViewHost"/>.
    /// </summary>
    internal void DetachFromNavigationViewHost(NavigationView navigationView)
    {
        CloseSubmenuBranch();

        BindingOperations.ClearBinding(this, PaneDisplayModeContextProperty);
        BindingOperations.ClearBinding(this, IsPaneOpenContextProperty);

        if (
            ReferenceEquals(
                ReadLocalValue(NavigationView.NavigationParentProperty),
                navigationView
            )
        )
        {
            ClearValue(NavigationView.NavigationParentProperty);
        }
    }
}
