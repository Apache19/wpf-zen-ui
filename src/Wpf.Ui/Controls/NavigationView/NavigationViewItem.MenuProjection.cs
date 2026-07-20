// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Ui.Converters;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Contains the native ContextMenu projection contract for <see cref="NavigationViewItem"/>.
/// </summary>
[StyleTypedProperty(Property = nameof(SubmenuContextMenuStyle), StyleTargetType = typeof(ContextMenu))]
[StyleTypedProperty(Property = nameof(SubmenuMenuItemStyle), StyleTargetType = typeof(MenuItem))]
public partial class NavigationViewItem
{
    private const string SubmenuContextMenuPartName = "PART_SubMenuContextMenu";

    private static readonly IValueConverter MenuHeaderConverter = new ProjectedHeaderConverter();
    private static readonly IValueConverter MenuIconConverter   = new ProjectedIconConverter();

    private readonly Dictionary<NavigationViewItem, MenuItem> _projectedMenuItems = [];
    private ContextMenu?                                      _submenuContextMenu;
    private DispatcherOperation?                              _projectionRefreshOperation;
    private PlacementMode                                     _placementBeforeContextGesture;
    private bool                                              _isBuildingSubmenuProjection;
    private bool                                              _isSubmenuProjectionDirty = true;
    private bool                                              _shouldRestoreContextGesturePlacement;

    #region DP: SubmenuContextMenuStyle

    /// <summary>
    /// Identifies the <see cref="SubmenuContextMenuStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SubmenuContextMenuStyleProperty =
        DependencyProperty.Register(
            nameof(SubmenuContextMenuStyle),
            typeof(Style),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Gets or sets the style applied to the root <see cref="ContextMenu"/> used by compact and
    /// horizontal navigation presentations.
    /// </summary>
    /// <remarks>
    /// The built-in item style supplies the library's ordinary ContextMenu appearance. Replacing
    /// this value changes only the root menu surface; cascading windows remain native MenuItem
    /// submenus and automatically follow the selected MenuItem style.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Style? SubmenuContextMenuStyle
    {
        get => (Style?)GetValue(SubmenuContextMenuStyleProperty);
        set => SetValue(SubmenuContextMenuStyleProperty, value);
    }

    #endregion

    #region DP: SubmenuMenuItemStyle

    /// <summary>
    /// Identifies the <see cref="SubmenuMenuItemStyle"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SubmenuMenuItemStyleProperty =
        DependencyProperty.Register(
            nameof(SubmenuMenuItemStyle),
            typeof(Style),
            typeof(NavigationViewItem),
            new FrameworkPropertyMetadata(
                null,
                FrameworkPropertyMetadataOptions.Inherits
            )
        );

    /// <summary>
    /// Gets or sets the style applied to every native <see cref="MenuItem"/> projected from this
    /// navigation branch.
    /// </summary>
    /// <remarks>
    /// Each projected MenuItem uses its source <see cref="NavigationViewItem"/> as
    /// <see cref="FrameworkElement.DataContext"/>. A custom style can therefore bind to properties
    /// such as <see cref="HeaderedContentControl.Header"/>, <see cref="Icon"/>, <see cref="IsActive"/> or
    /// <see cref="TargetPageType"/> without requiring a second public item model. The default
    /// projection converts an arbitrary UIElement Header to accessible text because WPF forbids one
    /// visual from belonging to both the navigation and menu trees; a custom style can render an
    /// alternative representation directly from the source DataContext.
    /// </remarks>
    [Bindable(true)]
    [Category("Appearance")]
    public Style? SubmenuMenuItemStyle
    {
        get => (Style?)GetValue(SubmenuMenuItemStyleProperty);
        set => SetValue(SubmenuMenuItemStyleProperty, value);
    }

    #endregion

    /// <summary>
    /// Attaches the template-owned native menu and schedules its MenuItem projection to be warmed
    /// while the dispatcher is idle. The resulting tree remains attached while the menu is closed,
    /// so opening it does not normally allocate controls, bindings, or icon clones.
    /// </summary>
    private void AttachSubmenuContextMenu(ContextMenu? contextMenu)
    {
        _submenuContextMenu = contextMenu;

        if (_submenuContextMenu is not null)
        {
            // ContextMenuService assigns its owner as PlacementTarget only when it opens a menu
            // through the ordinary right-click service path. NavigationViewItem also opens this
            // menu by changing IsSubmenuOpen/ContextMenu.IsOpen, so that implicit assignment never
            // runs. An ElementName binding declared inside Button.ContextMenu is not a safe
            // substitute: ContextMenu establishes a separate namescope/presentation source and the
            // binding can resolve to null, which makes Popup position itself at screen (0, 0).
            // Assign the concrete template part before IsOpen can change so mouse, keyboard,
            // automation and programmatic opens all use exactly the same anchor.
            _submenuContextMenu.SetCurrentValue(
                ContextMenu.PlacementTargetProperty,
                _headerButton
            );
            _submenuContextMenu.Opened += OnSubmenuContextMenuOpened;
            _submenuContextMenu.Closed += OnSubmenuContextMenuClosed;

            if (ReferenceEquals(GetRootNavigationItem(), this))
            {
                // ContextMenu.IsOpen changes synchronously when MenuBase loses capture, before its
                // Closed event is dispatched. Keep a TwoWay binding for that exact native timing,
                // but install it only on the semantic root. Declaring the binding in every item
                // template also binds source descendants whose templates were instantiated during
                // an earlier inline presentation; mirroring a projected submenu open would then
                // open a second root ContextMenu and immediately destroy the active cascade.
                if (IsSubmenuOpen)
                {
                    PrepareSubmenuProjectionForOpen();
                }

                BindingOperations.SetBinding(
                    _submenuContextMenu,
                    ContextMenu.IsOpenProperty,
                    new Binding(nameof(IsSubmenuOpen))
                    {
                        Source = this,
                        Mode = BindingMode.TwoWay,
                    }
                );

                ScheduleSubmenuProjectionRefresh(DispatcherPriority.ContextIdle);
            }
        }

        if (_headerButton is not null)
        {
            // ContextMenuService opens Button.ContextMenu directly for a right-click and therefore
            // bypasses IsSubmenuOpen until the menu is already opening. This routed event is the
            // public WPF hook that lets the owner populate the native menu before layout begins.
            _headerButton.ContextMenuOpening += OnHeaderContextMenuOpening;
        }
    }

    /// <summary>
    /// Detaches events and projection bindings before a template part can outlive its owner in a
    /// separate Popup presentation source.
    /// </summary>
    private void DetachSubmenuContextMenu()
    {
        if (_headerButton is not null)
        {
            _headerButton.ContextMenuOpening -= OnHeaderContextMenuOpening;
        }

        if (_submenuContextMenu is not null)
        {
            _submenuContextMenu.Opened -= OnSubmenuContextMenuOpened;
            _submenuContextMenu.Closed -= OnSubmenuContextMenuClosed;
            _submenuContextMenu.SetCurrentValue(ContextMenu.IsOpenProperty, false);
            RestoreContextGesturePlacement();
            BindingOperations.ClearBinding(_submenuContextMenu, ContextMenu.IsOpenProperty);
            _submenuContextMenu.SetCurrentValue(ContextMenu.PlacementTargetProperty, null);
        }

        if (_projectionRefreshOperation is { Status: DispatcherOperationStatus.Pending })
        {
            _projectionRefreshOperation.Abort();
        }

        _projectionRefreshOperation = null;
        ClearSubmenuProjection();
        _submenuContextMenu = null;
        _shouldRestoreContextGesturePlacement = false;
    }

    private void OnHeaderContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (UsesInlineExpansion || !HasItems || _submenuContextMenu is null)
        {
            // A leaf must not display an empty internal ContextMenu, and the open Left pane already
            // exposes the same children inline. Applications can still place a separate right-click
            // menu on a surrounding element when either behavior is desired.
            e.Handled = true;
            return;
        }

        PrepareSubmenuProjectionForOpen();

        // PopupControlService normally opens Button.ContextMenu after this routed event returns.
        // Its mouse path assigns ContextMenu.Placement = MousePoint with SetValue and never restores
        // the former value when the menu closes. Because this same menu is also opened from the
        // header Click path, that framework assignment would permanently replace the template's
        // Right/Top/Bottom placement (and can even remove a custom Placement binding). Handle the
        // service event ourselves and use SetCurrentValue for the transient gesture placement so
        // the original value source remains intact. Cursor coordinates of -1 are WPF's sentinel
        // for the keyboard context-menu gesture; matching the framework's Center choice preserves
        // native Shift+F10/Menu-key behavior.
        if (!_shouldRestoreContextGesturePlacement)
        {
            _placementBeforeContextGesture = _submenuContextMenu.Placement;
            _shouldRestoreContextGesturePlacement = true;
        }

        _submenuContextMenu.SetCurrentValue(
            ContextMenu.PlacementProperty,
            e.CursorLeft == -1d && e.CursorTop == -1d
                ? PlacementMode.Center
                : PlacementMode.MousePoint
        );

        e.Handled = true;
        SetCurrentValue(IsSubmenuOpenProperty, true);
    }

    private void OnSubmenuContextMenuOpened(object sender, RoutedEventArgs e)
    {
        PrepareSubmenuProjectionForOpen();

        if (!IsSubmenuOpen)
        {
            SetCurrentValue(IsSubmenuOpenProperty, true);
        }
    }

    private void OnSubmenuContextMenuClosed(object sender, RoutedEventArgs e)
    {
        if (IsSubmenuOpen)
        {
            SetCurrentValue(IsSubmenuOpenProperty, false);
        }

        // MenuBase has already closed every native descendant Popup. Resetting the source states
        // before dropping bindings keeps programmatic/UIA observation aligned with that native end
        // state even if WPF did not update an unrealized MenuItem binding during teardown.
        foreach (NavigationViewItem child in GetNavigationChildren())
        {
            child.CloseSubmenuBranch();
        }

        RestoreContextGesturePlacement();
    }

    /// <summary>
    /// Restores the placement selected by the active template after a native context-menu gesture.
    /// </summary>
    /// <remarks>
    /// <see cref="DependencyObject.SetCurrentValue(DependencyProperty, object)"/> is intentional:
    /// custom templates may bind <see cref="ContextMenu.PlacementProperty"/>, and replacing that
    /// binding merely because the same menu was opened once by right-click would reproduce the WPF
    /// framework behavior this method isolates.
    /// </remarks>
    private void RestoreContextGesturePlacement()
    {
        if (!_shouldRestoreContextGesturePlacement || _submenuContextMenu is null)
        {
            return;
        }

        _submenuContextMenu.SetCurrentValue(
            ContextMenu.PlacementProperty,
            _placementBeforeContextGesture
        );
        _shouldRestoreContextGesturePlacement = false;
    }

    /// <summary>
    /// Applies the semantic root item's open state to its template-owned ContextMenu without
    /// exposing the same binding on descendant source containers.
    /// </summary>
    /// <remarks>
    /// Descendant <see cref="NavigationViewItem"/> controls can keep an applied template while no
    /// longer belonging to the active visual tree, for example after the Left pane changes from
    /// expanded to compact. Their <see cref="IsSubmenuOpen"/> values describe projected native
    /// <see cref="MenuItem"/> popups only. Opening a second ContextMenu from those stale templates
    /// would start a competing <see cref="MenuBase"/> capture session and WPF would immediately
    /// collapse the intended native submenu. Restricting this method to the semantic root preserves
    /// one ContextMenu and lets its MenuItems own every lower Popup.
    /// </remarks>
    private void SynchronizeRootContextMenuState(bool isOpen)
    {
        if (
            _submenuContextMenu is null ||
            !ReferenceEquals(GetRootNavigationItem(), this)
        )
        {
            return;
        }

        if (isOpen)
        {
            PrepareSubmenuProjectionForOpen();
        }

        if (_submenuContextMenu.IsOpen != isOpen)
        {
            _submenuContextMenu.SetCurrentValue(ContextMenu.IsOpenProperty, isOpen);
        }
    }

    /// <summary>
    /// Ensures the root native menu has a current MenuItem projection before IsOpen is observed by
    /// ContextMenu. Descendant requests are redirected to the root because only the root owns a
    /// ContextMenu; all lower Popup windows are private implementation details of MenuItem.
    /// </summary>
    private void PrepareSubmenuProjectionForOpen()
    {
        NavigationViewItem root = GetRootNavigationItem();
        if (!ReferenceEquals(root, this))
        {
            root.PrepareSubmenuProjectionForOpen();
            return;
        }

        if (UsesInlineExpansion || !HasItems)
        {
            return;
        }

        if (_submenuContextMenu is null)
        {
            ApplyTemplate();
        }

        bool hasItems = _submenuContextMenu is not null && _submenuContextMenu.Items.Count > 0;
        if (
            _submenuContextMenu is null ||
            _isBuildingSubmenuProjection ||
            (!_isSubmenuProjectionDirty && hasItems)
        )
        {
            return;
        }

        RebuildSubmenuProjection();
    }

    /// <summary>
    /// Marks the owning root projection stale after an ItemsControl collection change and updates
    /// the cached tree after the current generator notification completes. This applies while the
    /// menu is closed as well as while it is open, keeping the next opening allocation-free.
    /// </summary>
    private void InvalidateSubmenuProjectionCache()
    {
        NavigationViewItem root = GetRootNavigationItem();
        root._isSubmenuProjectionDirty = true;
        root.ScheduleSubmenuProjectionRefresh(DispatcherPriority.DataBind);
    }

    /// <summary>
    /// Schedules a projection refresh. Template setup uses an idle priority so normal layout and
    /// rendering can finish before cache warming; collection changes use DataBind priority so the
    /// generator has completed its notification before the cached hierarchy is reconciled.
    /// </summary>
    private void ScheduleSubmenuProjectionRefresh(DispatcherPriority priority)
    {
        if (_projectionRefreshOperation is { Status: DispatcherOperationStatus.Pending } pendingOperation)
        {
            if (pendingOperation.Priority < priority)
            {
                pendingOperation.Priority = priority;
            }

            return;
        }

        if (_submenuContextMenu is null || UsesInlineExpansion)
        {
            return;
        }

        _projectionRefreshOperation = Dispatcher.BeginInvoke(
            priority,
            new Action(() =>
            {
                _projectionRefreshOperation = null;
                if (_submenuContextMenu is not null && !UsesInlineExpansion)
                {
                    RebuildSubmenuProjection();
                }
            })
        );
    }

    /// <summary>
    /// Reconciles the cached native menu hierarchy with the source navigation containers. Existing
    /// MenuItems are retained by source identity, so a collection mutation clones only newly added
    /// branches and releases only removed branches. Property values remain current through Binding.
    /// </summary>
    private void RebuildSubmenuProjection()
    {
        if (_submenuContextMenu is null || _isBuildingSubmenuProjection)
        {
            return;
        }

        _isBuildingSubmenuProjection = true;

        try
        {
            ReconcileProjectedChildren(_submenuContextMenu, this);

            _isSubmenuProjectionDirty = false;
        }
        finally
        {
            _isBuildingSubmenuProjection = false;
        }
    }

    private void ReconcileProjectedChildren(
        ItemsControl projectedParent,
        NavigationViewItem sourceParent)
    {
        IReadOnlyList<NavigationViewItem> sourceChildren =
            sourceParent.GetOrGenerateNavigationChildren();
        var retainedSources = new HashSet<NavigationViewItem>(sourceChildren);

        foreach (MenuItem oldItem in projectedParent.Items.OfType<MenuItem>().ToArray())
        {
            if (
                oldItem.DataContext is not NavigationViewItem oldSource ||
                !retainedSources.Contains(oldSource)
            )
            {
                projectedParent.Items.Remove(oldItem);
                ReleaseProjectedMenuItem(oldItem);
            }
        }

        for (int index = 0; index < sourceChildren.Count; index++)
        {
            NavigationViewItem source = sourceChildren[index];
            if (!_projectedMenuItems.TryGetValue(source, out MenuItem? menuItem))
            {
                menuItem = CreateProjectedMenuItem(source);
            }

            int currentIndex = projectedParent.Items.IndexOf(menuItem);
            if (currentIndex != index)
            {
                if (currentIndex >= 0)
                {
                    projectedParent.Items.RemoveAt(currentIndex);
                }

                projectedParent.Items.Insert(index, menuItem);
            }

            ReconcileProjectedChildren(menuItem, source);
            UpdateProjectedMenuItemRole(menuItem, source);
        }
    }

    private MenuItem CreateProjectedMenuItem(NavigationViewItem source)
    {
        var menuItem = new MenuItem
        {
            DataContext = source,
        };

        BindingOperations.SetBinding(
            menuItem,
            MenuItem.HeaderProperty,
            new Binding(nameof(Header))
            {
                Source = source,
                Mode = BindingMode.OneWay,
                Converter = MenuHeaderConverter,
            }
        );
        SetOneWayBinding(menuItem, MenuItem.HeaderTemplateProperty, source, HeaderTemplateProperty);
        SetOneWayBinding(menuItem, MenuItem.HeaderTemplateSelectorProperty, source, HeaderTemplateSelectorProperty);
        SetOneWayBinding(menuItem, MenuItem.HeaderStringFormatProperty, source, HeaderStringFormatProperty);
        SetOneWayBinding(menuItem, MenuItem.IsEnabledProperty, source, IsEnabledProperty);
        SetOneWayBinding(menuItem, MenuItem.VisibilityProperty, source, VisibilityProperty);
        // The root property styles the complete native cascade as one visual unit. Source child
        // containers also receive their own default NavigationViewItem style, whose setter would
        // otherwise mask an explicit root override because style setters outrank inherited values.
        SetOneWayBinding(
            menuItem,
            MenuItem.StyleProperty,
            GetRootNavigationItem(),
            SubmenuMenuItemStyleProperty
        );

        BindingOperations.SetBinding(
            menuItem,
            MenuItem.IconProperty,
            new Binding(nameof(Icon))
            {
                Source = source,
                Mode = BindingMode.OneWay,
                Converter = MenuIconConverter,
            }
        );

        string automationName = AutomationProperties.GetName(source);
        if (!string.IsNullOrWhiteSpace(automationName))
        {
            AutomationProperties.SetName(menuItem, automationName);
        }

        _projectedMenuItems[source] = menuItem;

        return menuItem;
    }

    private void UpdateProjectedMenuItemRole(MenuItem menuItem, NavigationViewItem source)
    {
        menuItem.SubmenuOpened -= OnProjectedMenuItemSubmenuOpened;
        menuItem.SubmenuClosed -= OnProjectedMenuItemSubmenuClosed;

        if (menuItem.HasItems)
        {
            // IsSubmenuOpen is deliberately not data-bound. WPF's MenuItem changes this property
            // through SetCurrentValueInternal while running its hover timers and branch-selection
            // state machine. A TwoWay Binding inserts the source NavigationViewItem into that
            // control-owned transition and can restore false before the nested Popup is shown.
            // Routed open/close events mirror the final native state without participating in the
            // transition itself; programmatic source changes use SynchronizeProjectedSubmenuState.
            menuItem.SubmenuOpened += OnProjectedMenuItemSubmenuOpened;
            menuItem.SubmenuClosed += OnProjectedMenuItemSubmenuClosed;
            menuItem.SetCurrentValue(MenuItem.CommandProperty, null);

            if (source.IsSubmenuOpen)
            {
                menuItem.SetCurrentValue(MenuItem.IsSubmenuOpenProperty, true);
            }
        }
        else
        {
            // WPF invokes MenuItem.Command through its normal PreviewClick/Click pipeline after the
            // complete ContextMenu cascade has been dismissed. Routing activation through a command
            // preserves that native sequencing and avoids synthesizing input on the source control.
            menuItem.SetCurrentValue(MenuItem.IsSubmenuOpenProperty, false);
            if (menuItem.Command is null)
            {
                menuItem.SetCurrentValue(
                    MenuItem.CommandProperty,
                    new NavigationViewItemMenuCommand(source)
                );
            }
        }
    }

    private void ReleaseProjectedMenuItem(MenuItem menuItem)
    {
        foreach (MenuItem child in menuItem.Items.OfType<MenuItem>().ToArray())
        {
            ReleaseProjectedMenuItem(child);
        }

        menuItem.SubmenuOpened -= OnProjectedMenuItemSubmenuOpened;
        menuItem.SubmenuClosed -= OnProjectedMenuItemSubmenuClosed;

        if (menuItem.DataContext is NavigationViewItem source)
        {
            _projectedMenuItems.Remove(source);
        }

        menuItem.Items.Clear();
    }

    private void OnProjectedMenuItemSubmenuOpened(object sender, RoutedEventArgs e)
    {
        // SubmenuOpened bubbles through ancestor MenuItems. Only the item that originated the
        // event may update its source; otherwise opening a deep level would mark every ancestor
        // from the routed event alone rather than from WPF's actual native state.
        if (
            sender is MenuItem menuItem &&
            ReferenceEquals(e.OriginalSource, menuItem) &&
            menuItem.DataContext is NavigationViewItem source &&
            !source.IsSubmenuOpen
        )
        {
            source.SetCurrentValue(IsSubmenuOpenProperty, true);
        }
    }

    private void OnProjectedMenuItemSubmenuClosed(object sender, RoutedEventArgs e)
    {
        if (
            sender is MenuItem menuItem &&
            ReferenceEquals(e.OriginalSource, menuItem) &&
            menuItem.DataContext is NavigationViewItem source &&
            source.IsSubmenuOpen
        )
        {
            source.SetCurrentValue(IsSubmenuOpenProperty, false);
        }
    }

    /// <summary>
    /// Applies an external or automation-driven source state change to an already projected native
    /// MenuItem. Native pointer/keyboard changes arrive through SubmenuOpened/SubmenuClosed instead,
    /// keeping WPF's own IsSubmenuOpen transition free from a binding feedback loop.
    /// </summary>
    private void SynchronizeProjectedSubmenuState(NavigationViewItem source, bool isOpen)
    {
        if (
            _projectedMenuItems.TryGetValue(source, out MenuItem? menuItem) &&
            menuItem.HasItems &&
            menuItem.IsSubmenuOpen != isOpen
        )
        {
            menuItem.SetCurrentValue(MenuItem.IsSubmenuOpenProperty, isOpen);
        }
    }

    private static void SetOneWayBinding(
        DependencyObject target,
        DependencyProperty targetProperty,
        DependencyObject source,
        DependencyProperty sourceProperty)
    {
        BindingOperations.SetBinding(
            target,
            targetProperty,
            new Binding
            {
                Source = source,
                Path = new PropertyPath(sourceProperty),
                Mode = BindingMode.OneWay,
            }
        );
    }

    /// <summary>
    /// Returns every child NavigationViewItem container, generating data-item containers when no
    /// inline ItemsPresenter currently exists.
    /// </summary>
    /// <remarks>
    /// WPF's ItemsControl semantics live on prepared containers: ItemContainerStyle may bind
    /// TargetPageType, Header, Icon or a descendant ItemsSource. Projecting raw data directly into
    /// MenuItem would bypass those semantics. The public <see cref="IItemContainerGenerator"/> is
    /// therefore used in the same StartAt/GenerateNext/PrepareItemContainer sequence as WPF Panel;
    /// generated containers stay associated with this ItemsControl and can later be reused by the
    /// inline presenter after a pane-mode change.
    /// </remarks>
    private IReadOnlyList<NavigationViewItem> GetOrGenerateNavigationChildren()
    {
        var result = new List<NavigationViewItem>(Items.Count);
        var generator = (IItemContainerGenerator)ItemContainerGenerator;

        using (
            generator.StartAt(
                new GeneratorPosition(-1, 0),
                GeneratorDirection.Forward,
                allowStartAtRealizedItem: true
            )
        )
        {
            for (int index = 0; index < Items.Count; index++)
            {
                DependencyObject container = generator.GenerateNext(out bool isNewlyRealized);
                if (container is null)
                {
                    break;
                }

                if (isNewlyRealized)
                {
                    generator.PrepareItemContainer(container);
                }

                if (container is NavigationViewItem navigationItem)
                {
                    result.Add(navigationItem);
                }
            }
        }

        return result;
    }

    private void FocusFirstProjectedMenuItem()
    {
        NavigationViewItem root = GetRootNavigationItem();
        root.PrepareSubmenuProjectionForOpen();

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(() =>
            {
                if (root._submenuContextMenu is not { IsOpen: true })
                {
                    return;
                }

                MenuItem? firstItem;
                if (ReferenceEquals(root, this))
                {
                    firstItem = root._submenuContextMenu.Items.OfType<MenuItem>().FirstOrDefault();
                }
                else if (root._projectedMenuItems.TryGetValue(this, out MenuItem? projectedOwner))
                {
                    firstItem = projectedOwner.Items.OfType<MenuItem>().FirstOrDefault();
                }
                else
                {
                    firstItem = null;
                }

                _ = firstItem?.Focus();
            })
        );
    }

    private void ClearSubmenuProjection()
    {
        foreach (MenuItem menuItem in _projectedMenuItems.Values)
        {
            menuItem.SubmenuOpened -= OnProjectedMenuItemSubmenuOpened;
            menuItem.SubmenuClosed -= OnProjectedMenuItemSubmenuClosed;
        }

        _submenuContextMenu?.Items.Clear();
        _projectedMenuItems.Clear();
        _isSubmenuProjectionDirty = true;
    }

    /// <summary>
    /// Adapts native MenuItem command invocation to the source navigation action. A parent
    /// MenuItem never receives this command because WPF treats items with children as submenu
    /// headers; this deliberately establishes native menu semantics where only leaves activate.
    /// </summary>
    private sealed class NavigationViewItemMenuCommand(NavigationViewItem source) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            _ = source.InvokePrimaryAction();
        }
    }

    /// <summary>
    /// Keeps Header projection data-only. Ordinary strings and view-model values are safe to share;
    /// DependencyObjects have WPF parentage and are reduced to useful text so opening a menu can
    /// never steal a custom header visual from the source item.
    /// </summary>
    private sealed class ProjectedHeaderConverter : IValueConverter
    {
        public object? Convert(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            if (value is not DependencyObject dependencyObject)
            {
                return value;
            }

            string automationName = AutomationProperties.GetName(dependencyObject);
            if (!string.IsNullOrWhiteSpace(automationName))
            {
                return automationName;
            }

            return value switch
            {
                AccessText accessText => accessText.Text,
                System.Windows.Controls.TextBlock textBlock => textBlock.Text,
                ContentControl { Content: not DependencyObject } contentControl => contentControl.Content,
                _ => value.ToString(),
            };
        }

        public object ConvertBack(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException(
                $"{nameof(ProjectedHeaderConverter)} only creates detached menu header values."
            );
        }
    }

    /// <summary>
    /// Uses the library's icon clone converter and rejects an unsupported converter fallback that
    /// returns the original FrameworkElement. A missing custom icon is preferable to reparenting it
    /// out of the source NavigationViewItem; custom menu styles can supply another representation.
    /// </summary>
    private sealed class ProjectedIconConverter : IValueConverter
    {
        private readonly IconElementCloneConverter _cloneConverter = new();

        public object? Convert(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            object? clone = _cloneConverter.Convert(value, targetType, parameter, culture);
            return ReferenceEquals(value, clone) && value is FrameworkElement ? null : clone;
        }

        public object ConvertBack(
            object? value,
            Type targetType,
            object? parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException(
                $"{nameof(ProjectedIconConverter)} only creates detached menu icon values."
            );
        }
    }
}
