// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.navigationviewitem?view=winrt-22621 */

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents the container for an item in a NavigationView control.
/// When needed, it can be used as a normal button with a <see cref="ButtonBase.Click"/> action.
/// </summary>
/// <remarks>
/// <c>PART_SubMenuPopup</c> is used by popup presentations and is intentionally absent from the
/// expanded Left presentation, which owns one inline child list instead. Missing optional parts
/// degrade to property-driven behavior without preventing navigation or expansion.
/// </remarks>
[TemplatePart(Name = "PART_SubMenuPopup", Type = typeof(Popup))]
public class NavigationViewItem : ButtonBase, INavigationViewItem
{
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

    #region DP: MenuItems

    private static readonly DependencyPropertyKey MenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(MenuItems),
            typeof(ObservableCollection<object>),
            typeof(NavigationViewItem),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the <see cref="MenuItems"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsProperty = MenuItemsPropertyKey.DependencyProperty;

    /// <inheritdoc/>
    public IList MenuItems => (ObservableCollection<object>)GetValue(MenuItemsProperty);

    #endregion

    #region DP: MenuItemsSource

    /// <summary>Identifies the <see cref="MenuItemsSource"/> dependency property.</summary>
    public static readonly DependencyProperty MenuItemsSourceProperty =
        DependencyProperty.Register(
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

    #endregion

    #region DP: HasMenuItems

    /// <summary>Identifies the <see cref="HasMenuItems"/> dependency property.</summary>
    private static readonly DependencyPropertyKey HasMenuItemsPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasMenuItems),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false, OnHasMenuItemsChanged)
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

    private static readonly DependencyPropertyKey IsPaneOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPaneOpen),
            typeof(bool),
            typeof(NavigationViewItem),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the <see cref="IsPaneOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsPaneOpenProperty =
        IsPaneOpenPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the owning <see cref="NavigationView"/> pane is open.
    /// </summary>
    [Browsable(false)]
    [ReadOnly(true)]
    public bool IsPaneOpen => (bool)GetValue(IsPaneOpenProperty);

    #endregion

    #region DP: PaneDisplayMode

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
    private TypedEventHandler<NavigationView, RoutedEventArgs>? _paneModeChangedHandler;

    internal NavigationView? NavigationViewHost
    {
        get
        {
            return _attachedNavigationView
                ?? NavigationView.GetNavigationParent(this)
                ?? (NavigationViewItemParent as NavigationViewItem)?.NavigationViewHost;
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
            item.CloseSubmenuBranch();
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
    /// <remarks>
    /// A Popup is a separate presentation source and can survive removal of the template visual
    /// tree. Closing the transient branch before WPF disconnects that tree prevents an orphaned
    /// popup when callers replace <see cref="Control.Template"/> directly.
    /// </remarks>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CloseSubmenuBranch();
        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        _ = InvokePrimaryAction();

        base.OnClick();
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
                if (oldItem is NavigationViewItem oldNavigationItem)
                {
                    oldNavigationItem.CloseSubmenuBranch();
                }

                oldItem.NavigationViewItemParent = null;
            }
        }

        _attachedMenuItems.Clear();

        foreach (var item in currentItems)
        {
            item.NavigationViewItemParent = this;
            _attachedMenuItems.Add(item);
        }

        if (!HasMenuItems)
        {
            SetCurrentValue(IsExpandedProperty, false);
            SetCurrentValue(IsSubmenuOpenProperty, false);
        }
    }

    /// <summary>
    /// Executes every action represented by the item surface. Navigation and child presentation are
    /// intentionally independent: an item that has both a target and children navigates and toggles
    /// its children from the same click, matching the fact that the template exposes one button.
    /// </summary>
    /// <returns><see langword="true"/> when an action was performed.</returns>
    internal bool InvokePrimaryAction()
    {
        // Capture the desired child state before navigation. Successful navigation closes all
        // transient Popup branches, so deriving the toggle state afterwards would always reopen an
        // already-open menu instead of honoring the user's request to close it.
        bool hasMenuItems = HasMenuItems;
        bool shouldOpenChildMenu = hasMenuItems && !IsChildMenuOpen;
        bool actionPerformed = false;

        if (TargetPageType is not null && NavigationViewHost is { } navigationView)
        {
            actionPerformed = navigationView.OnNavigationViewItemClick(this);
        }

        if (hasMenuItems)
        {
            SetChildMenuOpen(shouldOpenChildMenu, focusFirstItem: false);
            actionPerformed = true;
        }

        return actionPerformed;
    }

    /// <summary>Sets the effective child-menu state for the active presentation.</summary>
    internal void SetChildMenuOpen(bool isOpen, bool focusFirstItem)
    {
        if (isOpen && !HasMenuItems)
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

        foreach (NavigationViewItem child in MenuItems.OfType<NavigationViewItem>())
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
            _ = root.Focus();
            return true;
        }

        if (UsesInlineExpansion)
        {
            if (key == Key.Right && HasMenuItems)
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
                    _ = parentItem.Focus();
                }

                return true;
            }

            return false;
        }

        if (IsSubItem)
        {
            if (key == Key.Right && HasMenuItems)
            {
                SetChildMenuOpen(true, focusFirstItem: true);
                return true;
            }

            if (key == Key.Left && NavigationViewItemParent is NavigationViewItem parentItem)
            {
                parentItem.SetChildMenuOpen(false, focusFirstItem: false);
                _ = parentItem.Focus();
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

        if (key == openKey && HasMenuItems)
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

                foreach (UIElement child in MenuItems.OfType<UIElement>())
                {
                    if (child is { Focusable: true, IsEnabled: true } && child.Focus())
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

        if (newValue && !item.HasMenuItems)
        {
            item.SetCurrentValue(IsSubmenuOpenProperty, false);
            return;
        }

        if (newValue)
        {
            item.NavigationViewHost?.OnNavigationViewItemSubmenuOpened(item);
        }
        else
        {
            foreach (NavigationViewItem child in item.MenuItems.OfType<NavigationViewItem>())
            {
                child.CloseSubmenuBranch();
            }
        }

        if (!item.UsesInlineExpansion && item.HasMenuItems)
        {
            NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
                item,
                oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            );
        }
    }

    /// <summary>Reports inline state changes only while inline expansion is the active presentation.</summary>
    private static void OnIsExpandedChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;

        if (item.UsesInlineExpansion && item.HasMenuItems)
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
    private static void OnHasMenuItemsChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var item = (NavigationViewItem)d;
        bool oldValue = (bool)e.OldValue;
        bool newValue = (bool)e.NewValue;

        ExpandCollapseState oldState = oldValue
            ? item.IsChildMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            : ExpandCollapseState.LeafNode;
        ExpandCollapseState newState = newValue
            ? item.IsChildMenuOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed
            : ExpandCollapseState.LeafNode;

        NavigationViewItemAutomationPeer.RaiseExpandCollapseAutomationEvent(
            item,
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

        _paneOpenedHandler      = (_, _) => SynchronizePaneState(navigationView.PaneDisplayMode, true);
        _paneClosedHandler      = (_, _) => SynchronizePaneState(navigationView.PaneDisplayMode, false);
        _paneModeChangedHandler = (_, _) => SynchronizePaneState(navigationView.PaneDisplayMode, navigationView.IsPaneOpen);

        navigationView.PaneOpened             += _paneOpenedHandler;
        navigationView.PaneClosed             += _paneClosedHandler;
        navigationView.PaneDisplayModeChanged += _paneModeChangedHandler;
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

        if (_paneModeChangedHandler is not null)
        {
            _attachedNavigationView.PaneDisplayModeChanged -= _paneModeChangedHandler;
            _paneModeChangedHandler                        =  null;
        }

        _attachedNavigationView = null;
    }

    internal void SynchronizePaneState(NavigationViewPaneDisplayMode paneDisplayMode, bool isPaneOpen)
    {
        bool presentationChanged =
            PaneDisplayMode != paneDisplayMode || IsPaneOpen != isPaneOpen;

        SetValue(PaneDisplayModePropertyKey, paneDisplayMode);
        SetValue(IsPaneOpenPropertyKey, isPaneOpen);

        if (presentationChanged)
        {
            SetCurrentValue(IsSubmenuOpenProperty, false);
        }

        foreach (var item in MenuItems.OfType<NavigationViewItem>())
        {
            item.SynchronizePaneState(paneDisplayMode, isPaneOpen);
        }
    }
}
