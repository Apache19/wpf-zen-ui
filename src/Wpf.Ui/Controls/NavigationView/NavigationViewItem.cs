// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.navigationviewitem?view=winrt-22621 */

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
using WpfToggleButton = System.Windows.Controls.Primitives.ToggleButton;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a hierarchical item container in a <see cref="NavigationView"/>.
/// </summary>
/// <remarks>
/// <para>
/// The item is an <see cref="HeaderedItemsControl"/>, not a button. Its
/// <see cref="HeaderedItemsControl.Header"/> is the
/// navigation row, while <see cref="ItemsControl.Items"/> is the authoritative child collection.
/// This distinction is important: focus, mouse capture and pressed state belong only to
/// <c>PART_HeaderButton</c>; inline descendants and Popup content must never become part of a
/// button's input or focus-scrolling bounds.
/// </para>
/// <para>
/// Built-in templates contain exactly one <see cref="ItemsPresenter"/>. Left/open presentation
/// places it inline; compact and horizontal presentations place it in <c>PART_SubMenuPopup</c>.
/// Switching presentation may replace the template, but it never mirrors the child collection into
/// a second <see cref="ItemsControl"/>. Container generation and logical parenting therefore follow
/// the standard WPF items pipeline.
/// </para>
/// </remarks>
[TemplatePart(Name = "PART_SubMenuPopup", Type = typeof(Popup))]
[TemplatePart(Name = HeaderButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = ExpandButtonPartName, Type = typeof(ButtonBase))]
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationViewItem))]
public class NavigationViewItem : HeaderedItemsControl, INavigationViewItem
{
    private const string HeaderButtonPartName = "PART_HeaderButton";
    private const string ExpandButtonPartName = "PART_ExpandButton";

    #region DP: Indentation

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

    #endregion

    #region DP: IndentationSize

    /// <summary>Identifies the <see cref="IndentationSize"/> dependency property.</summary>
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

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
    internal static readonly DependencyPropertyKey IsActivePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsActive),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsActive"/> dependency property.</summary>
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

    /// <summary>Identifies the read-only <see cref="IsPaneOpen"/> dependency property.</summary>
    /// <remarks>
    /// The public property is read-only. An internal inheritable context receives the owning view's
    /// value and copies it through the private key, so callers cannot override presentation state on
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

    /// <summary>Identifies the read-only <see cref="PaneDisplayMode"/> dependency property.</summary>
    /// <remarks>See <see cref="IsPaneOpenProperty"/> for the inherited context contract.</remarks>
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

    /// <summary>Identifies the <see cref="IsSubItem"/> dependency property.</summary>
    public static readonly DependencyProperty IsSubItemProperty = IsSubItemPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this item belongs to another navigation item.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsSubItem => (bool)GetValue(IsSubItemProperty);

    #endregion

    #region DP: IsExpanded

    /// <summary>Identifies the <see cref="IsExpanded"/> dependency property.</summary>
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
    /// create or remove the inline child presenter. Popup presentations preserve but otherwise
    /// ignore this value and use <see cref="IsSubmenuOpen"/> instead.
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
    /// Identifies the <see cref="IsSubmenuOpen"/> dependency property. The property defaults to
    /// <see langword="false"/> and binds two-way by default so a Popup dismissal updates its owner.
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
    /// Gets or sets a value indicating whether this item's popup submenu is open. The default value
    /// is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// This property represents only transient popup state. The expanded Left pane uses
    /// <see cref="IsExpanded"/> for its inline hierarchy instead. Separating the two states keeps
    /// an inline branch from opening a popup merely because <see cref="NavigationView.PaneDisplayMode"/>
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

    #region DP: Icon

    /// <summary>Identifies the <see cref="Icon"/> dependency property.</summary>
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: TargetPageTag

    /// <summary>Identifies the <see cref="TargetPageTag"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTagProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: TargetPageType

    /// <summary>Identifies the <see cref="TargetPageType"/> dependency property.</summary>
    public static readonly DependencyProperty TargetPageTypeProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: InfoBadge

    /// <summary>Identifies the <see cref="InfoBadge"/> dependency property.</summary>
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

    #region DP: NavigationCacheMode

    /// <summary>Identifies the <see cref="NavigationCacheMode"/> dependency property.</summary>
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

    /// <inheritdoc/>
    public NavigationCacheMode NavigationCacheMode
    {
        get => (NavigationCacheMode)GetValue(NavigationCacheModeProperty);
        set => SetValue(NavigationCacheModeProperty, value);
    }

    #endregion

    /// <inheritdoc />
    public INavigationViewItem? NavigationViewItemParent => _parentItem;

    /// <inheritdoc />
    bool INavigationViewItem.IsMenuElement { get; set; }

    /// <inheritdoc />
    public string Id { get; }

    internal string SearchableContent { get; private set; } = string.Empty;

    private readonly HashSet<NavigationViewItem> _directNavigationChildren = [];
    private NavigationViewItem?                  _parentItem;
    private ButtonBase?                          _headerButton;
    private ButtonBase?                          _expandButton;
    private bool                                 _hadItems;

    // Opening a descendant popup first materializes each required ancestor. Those intermediate
    // IsSubmenuOpen changes must not run branch arbitration independently: an ancestor-only path
    // would treat the descendant currently being opened as a competing branch and close it.
    private bool _isOpeningAsPopupAncestor;

    internal NavigationView? NavigationViewHost
    {
        get
        {
            return NavigationView.GetNavigationParent(this) ?? _parentItem?.NavigationViewHost;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current presentation owns an inline child list rather than a popup.
    /// Left is the only mode with an expandable pane, and it uses inline children only while that
    /// pane is open. Every other presentation uses <see cref="IsSubmenuOpen"/>.
    /// </summary>
    internal bool UsesInlineExpansion =>
        PaneDisplayMode == NavigationViewPaneDisplayMode.Left && IsPaneOpen;

    /// <summary>Gets a value indicating whether the effective child menu is open.</summary>
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

    /// <summary>Gets the realized header input surface for automation and focus coordination.</summary>
    internal ButtonBase? HeaderButton => _headerButton;

    /// <summary>Gets the realized expansion input surface for automation-tree filtering.</summary>
    internal ButtonBase? ExpandButton => _expandButton;

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with an empty header
    /// and child collection.
    /// </summary>
    public NavigationViewItem()
    {
        Id = Guid.NewGuid().ToString("n");

        Unloaded += static (sender, _) => ((NavigationViewItem)sender).CloseSubmenuBranch();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class for the specified page type.
    /// </summary>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(Type targetPageType)
        : this()
    {
        SetValue(TargetPageTypeProperty, targetPageType);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with a text header and target page.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(string name, Type targetPageType)
        : this(targetPageType)
    {
        SetValue(HeaderProperty, name);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class with a text header,
    /// symbol, and target page.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="icon">The Fluent symbol displayed beside the header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    public NavigationViewItem(string name, SymbolRegular icon, Type targetPageType)
        : this(targetPageType)
    {
        SetValue(HeaderProperty, name);
        SetValue(IconProperty, new SymbolIcon
        {
            Symbol = icon
        });
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationViewItem"/> class whose children are
    /// supplied by an enumerable source.
    /// </summary>
    /// <param name="name">The text displayed in the item header.</param>
    /// <param name="icon">The Fluent symbol displayed beside the header.</param>
    /// <param name="targetPageType">The page type activated by the header action.</param>
    /// <param name="itemsSource">The source used by the standard WPF items pipeline to generate children.</param>
    // ReSharper disable once UnusedMember.Global
    public NavigationViewItem(string name, SymbolRegular icon, Type targetPageType, IEnumerable itemsSource)
        : this(name, icon, targetPageType)
    {
        SetValue(ItemsSourceProperty, itemsSource);
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

        SearchableContent = CreateSearchableContent(newHeader);
        NavigationView.GetNavigationParent(this)?.NotifyNavigationViewItemContentChanged(this);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Template buttons are intentionally subscribed here instead of handling mouse input on the
    /// container. Popup model-parent routing can pass input through an owning item, while a Click
    /// raised by the concrete header button unambiguously identifies the intended input surface.
    /// Missing parts leave a non-interactive custom template but do not compromise item generation.
    /// </remarks>
    public override void OnApplyTemplate()
    {
        DetachHeaderParts();
        base.OnApplyTemplate();

        _headerButton = GetTemplateChild(HeaderButtonPartName) as ButtonBase;
        _expandButton = GetTemplateChild(ExpandButtonPartName) as ButtonBase;

        if (_headerButton is not null)
        {
            _headerButton.Click += OnHeaderButtonClick;
        }

        if (_expandButton is not null)
        {
            _expandButton.Click += OnExpandButtonClick;
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// A Popup is a separate presentation source and can survive removal of the template visual
    /// tree. Closing the transient branch before WPF disconnects that tree prevents an orphaned
    /// popup when callers replace <see cref="Control.Template"/> directly.
    /// </remarks>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CloseSubmenuBranch();
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
    /// WPF copies <see cref="ItemsControl.ItemTemplate"/>,
    /// <see cref="ItemsControl.ItemContainerStyle"/> and hierarchical
    /// bindings in the base implementation. Parent attachment is kept separate so both generated
    /// containers and directly supplied <see cref="NavigationViewItem"/> instances share the same
    /// depth, keyboard and Popup ownership model.
    /// </remarks>
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is NavigationViewItem child)
        {
            child.AttachToParent(this);

            if (!ReferenceEquals(element, item))
            {
                NavigationViewHost?.RegisterGeneratedNavigationItem(child);
            }
        }
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationViewItem child && !ReferenceEquals(element, item))
        {
            NavigationViewHost?.UnregisterGeneratedNavigationItem(child);

            if (ReferenceEquals(child._parentItem, this))
            {
                child.DetachFromParent(this);
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
        if (_headerButton is not null)
        {
            _headerButton.Click -= OnHeaderButtonClick;
            _headerButton = null;
        }

        if (_expandButton is not null)
        {
            _expandButton.Click -= OnExpandButtonClick;
            _expandButton = null;
        }
    }

    private void OnHeaderButtonClick(object sender, RoutedEventArgs e)
    {
        _ = InvokePrimaryAction();
        e.Handled = true;
    }

    private void OnExpandButtonClick(object sender, RoutedEventArgs e)
    {
        // ToggleButton updates IsChecked before raising Click. Built-in templates bind that value to
        // the active open-state property, so consume the resulting state instead of toggling twice.
        // Qualify the WPF primitive explicitly. Wpf.Ui.Controls also contains a ToggleButton type;
        // resolving that sibling type here would reject the template part after WPF has already
        // toggled its binding and would immediately reverse every user click.
        bool isOpen = sender is WpfToggleButton toggleButton
            ? toggleButton.IsChecked == true
            : !IsChildMenuOpen;
        SetChildMenuOpen(isOpen, focusFirstItem: false);
        e.Handled = true;
    }

    /// <summary>
    /// Invokes the semantic header action.
    /// </summary>
    /// <remarks>
    /// Navigable parents navigate from the header and expand only from <c>PART_ExpandButton</c>.
    /// Grouping parents, which have no target page, toggle from the header as a convenient single
    /// action. Keeping these actions distinct avoids the historical ambiguity where one click both
    /// changed page and rearranged the hierarchy.
    /// </remarks>
    /// <returns><see langword="true"/> when an action was performed.</returns>
    internal bool InvokePrimaryAction()
    {
        if (TargetPageType is not null && NavigationViewHost is { } navigationView)
        {
            return navigationView.OnNavigationViewItemClick(this);
        }

        if (HasItems)
        {
            SetChildMenuOpen(!IsChildMenuOpen, focusFirstItem: false);
            return true;
        }

        return false;
    }

    private static Visual? ResolveVisualInputSource(object? originalSource)
    {
        DependencyObject? source = originalSource as DependencyObject;

        // Text runs are ContentElements rather than Visuals. Resolve their content parent first,
        // then deliberately stay on the visual axis: following logical/templated parents here would
        // cross Popup's route bridge and incorrectly classify detached popup input as button input.
        while (source is ContentElement contentElement)
        {
            source = ContentOperations.GetParent(contentElement);
            if (source == null)
            {
                source = (contentElement as FrameworkContentElement)?.Parent;
            }
        }

        return source as Visual;
    }

    /// <summary>Sets the effective child-menu state for the active presentation.</summary>
    internal void SetChildMenuOpen(bool isOpen, bool focusFirstItem)
    {
        if (isOpen && !HasItems)
        {
            return;
        }

        if (UsesInlineExpansion)
        {
            SetCurrentValue(IsExpandedProperty, isOpen);
        }
        else
        {
            SetCurrentValue(IsSubmenuOpenProperty, isOpen);
        }

        if (isOpen && focusFirstItem)
        {
            FocusFirstSubmenuItem();
        }
    }

    /// <summary>Closes this popup submenu and every popup below it.</summary>
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

        if (IsSubItem)
        {
            if (key == Key.Right && HasItems)
            {
                SetChildMenuOpen(true, focusFirstItem: true);
                return true;
            }

            if (key == Key.Left && NavigationViewItemParent is NavigationViewItem parentItem)
            {
                parentItem.SetChildMenuOpen(false, focusFirstItem: false);
                _ = parentItem.FocusHeader();
                return true;
            }

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
        for (INavigationViewItem? parent = NavigationViewItemParent; parent is not null; parent = parent.NavigationViewItemParent)
        {
            if (parent is NavigationViewItem { IsSubmenuOpen: true })
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
        // Popup content is created after IsOpen changes. Deferring to input priority lets WPF
        // generate and connect the child containers before focus crosses the HWND boundary.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (!IsChildMenuOpen)
                {
                    return;
                }

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

    /// <summary>
    /// Coordinates popup branch ownership at the dependency-property boundary. Popup dismissal and
    /// external bindings therefore receive the same descendant cleanup and UIA notification as
    /// mouse and keyboard input.
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
            // Popup state is transient presentation state, not a second expansion flag. A leaf or
            // the inline Left presentation has no Popup to own this value, so reject externally
            // supplied values that would otherwise leave automation and template state divergent.
            item.SetCurrentValue(IsSubmenuOpenProperty, false);
            return;
        }

        if (newValue)
        {
            item.OpenPopupAncestors();
            if (!item._isOpeningAsPopupAncestor)
            {
                item.NavigationViewHost?.OnNavigationViewItemSubmenuOpened(item);
            }
        }
        else
        {
            foreach (NavigationViewItem child in item.GetNavigationChildren())
            {
                child.CloseSubmenuBranch();
            }

            item.NavigationViewHost?.OnNavigationViewItemSubmenuClosed();
        }

        if (!item.UsesInlineExpansion && item.HasItems)
        {
            NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
                item,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            );
        }
    }

    /// <summary>
    /// Opens every popup ancestor before this item's popup is presented. A descendant popup cannot
    /// safely outlive the Popup that contains its placement target; doing so leaves a detached HWND
    /// whose routed mouse events still lead back to the descendant item.
    /// </summary>
    private void OpenPopupAncestors()
    {
        var ancestors = new Stack<NavigationViewItem>();
        for (
            var parent = NavigationViewItemParent as NavigationViewItem;
            parent is not null;
            parent = parent.NavigationViewItemParent as NavigationViewItem
        )
        {
            ancestors.Push(parent);
        }

        while (ancestors.Count > 0)
        {
            NavigationViewItem ancestor = ancestors.Pop();
            if (!ancestor.UsesInlineExpansion && ancestor.HasItems)
            {
                ancestor.OpenAsPopupAncestor();
            }
        }
    }

    /// <summary>
    /// Materializes one ancestor without arbitrating branches until the descendant that requested
    /// the operation reaches its own property callback.
    /// </summary>
    private void OpenAsPopupAncestor()
    {
        if (IsSubmenuOpen)
        {
            return;
        }

        _isOpeningAsPopupAncestor = true;

        try
        {
            SetCurrentValue(IsSubmenuOpenProperty, true);
        }
        finally
        {
            _isOpeningAsPopupAncestor = false;
        }
    }

    /// <summary>
    /// Determines whether an already hit-tested input source belongs to this item's open Popup.
    /// </summary>
    /// <remarks>
    /// Popup content lives in a separate HWND and visual tree, so ordinary ancestry from the
    /// NavigationView cannot identify its owning level. Testing against the Popup child before any
    /// branch closes gives a deterministic answer without depending on screen coordinates, layout,
    /// or WPF's private PopupRoot capture-restoration timing.
    /// </remarks>
    internal bool IsInputWithinOpenSubmenu(object? originalSource)
    {
        if (!IsSubmenuOpen || FindSubmenuPopup() is not { IsOpen: true, Child: UIElement popupChild })
        {
            return false;
        }

        return
            ResolveVisualInputSource(originalSource) is { } visual &&
            (
                ReferenceEquals(popupChild, visual) ||
                popupChild.IsAncestorOf(visual)
            );
    }

    /// <summary>Finds the popup owned directly by the active item template.</summary>
    private Popup? FindSubmenuPopup()
    {
        return Template?.FindName("PART_SubMenuPopup", this) as Popup;
    }

    /// <summary>Reports inline state changes only while inline expansion is the active presentation.</summary>
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
    }

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

        // Popup state belongs to the presentation that created it. Inline expansion is retained so
        // reopening the Left pane restores the user's hierarchy state without retaining a detached
        // Popup from the compact presentation.
        item.SetCurrentValue(IsSubmenuOpenProperty, false);
    }

    /// <summary>
    /// Projects the inherited pane mode into the public read-only presentation property.
    /// </summary>
    /// <remarks>
    /// A mode switch replaces the active item template. Closing the transient Popup before that
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

    /// <summary>Associates this container with its semantic parent item.</summary>
    private void AttachToParent(NavigationViewItem parent)
    {
        if (ReferenceEquals(_parentItem, parent))
        {
            return;
        }

        _parentItem = parent;
        SetValue(IsSubItemPropertyKey, true);
        UpdateIndentation();
    }

    /// <summary>Releases parent-owned state when this container leaves an item generator.</summary>
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
    }

    /// <summary>
    /// Enumerates realized child containers without duplicating directly supplied controls.
    /// </summary>
    /// <remarks>
    /// Direct control items are available before layout; data items acquire a container only when
    /// WPF's generator realizes them. Popup arbitration and recursive cleanup operate on containers,
    /// so this method deliberately combines both paths.
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

    /// <summary>Moves keyboard focus to the bounded header input surface.</summary>
    internal bool FocusHeader()
    {
        ApplyTemplate();
        return _headerButton is { IsEnabled: true, Focusable: true } && _headerButton.Focus();
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

    /// <summary>Releases context installed by <see cref="AttachToNavigationViewHost"/>.</summary>
    internal void DetachFromNavigationViewHost(NavigationView navigationView)
    {
        CloseSubmenuBranch();

        BindingOperations.ClearBinding(this, PaneDisplayModeContextProperty);
        BindingOperations.ClearBinding(this, IsPaneOpenContextProperty);

        if (ReferenceEquals(NavigationView.GetNavigationParent(this), navigationView))
        {
            ClearValue(NavigationView.NavigationParentProperty);
        }
    }
}
