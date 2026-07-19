// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.navigationview?view=winrt-22621 */

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a container that enables navigation of app content. It has a header, a view for the main content, and a menu pane for navigation commands.
/// </summary>
public partial class NavigationView : Control, INavigationView
{
    /// <summary>
    /// Notifies navigation-owned template helpers that <see cref="PaneTitle"/> changed. This is an
    /// internal state channel rather than a public control event: consumers observe the dependency
    /// property, while <see cref="NavigationViewButton"/> uses the notification to maintain its
    /// presentation-neutral snapshot without binding or taking ownership of
    /// <see cref="ContentControl.Content"/>.
    /// </summary>
    internal event EventHandler? PaneTitleChangedInternal;

    /// <summary>
    /// Initializes static members of the <see cref="NavigationView"/> class and overrides default property metadata.
    /// </summary>
    static NavigationView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationView),
            new FrameworkPropertyMetadata(typeof(NavigationView))
        );

        MarginProperty.OverrideMetadata(
            typeof(NavigationView),
            new FrameworkPropertyMetadata(new Thickness(0, 0, 0, 0))
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationView"/> class.
    /// </summary>
    public NavigationView()
    {
        NavigationParent = this;
        SetValue(PaneDisplayModeContextProperty, PaneDisplayMode);
        SetValue(IsPaneOpenContextProperty, IsPaneOpen);

        // The navigation frame belongs to the control rather than to a particular template.
        // WPF destroys a control template's generated subtree whenever Template changes; keeping
        // the frame in that subtree would therefore discard the current page while merely changing
        // PaneDisplayMode. The templates expose only a host slot, and OnApplyTemplate moves this
        // stable frame between those slots.

        BindingOperations.SetBinding(
            NavigationViewContentPresenter,
            PaddingProperty,
            new Binding(nameof(Padding))
            {
                Source = this
            }
        );

        BindingOperations.SetBinding(
            NavigationViewContentPresenter,
            NavigationViewContentPresenter.TransitionProperty,
            new Binding(nameof(Transition))
            {
                Source = this
            }
        );

        BindingOperations.SetBinding(
            NavigationViewContentPresenter,
            NavigationViewContentPresenter.TransitionDurationProperty,
            new Binding(nameof(TransitionDuration))
            {
                Source = this
            }
        );

        NavigationViewContentPresenter.Navigated += OnNavigationViewContentPresenterNavigated;

        _paneBackButton = CreateNavigationButton(
            "NavigationBackButton",
            NavigationViewButtonPlacement.Pane,
            NavigationViewButtonRole.Back
        );

        _paneBackButton.Click += OnBackButtonClick;

        BindingOperations.SetBinding(
            _paneBackButton,
            IsEnabledProperty,
            new Binding(nameof(IsBackEnabled))
            {
                Source = this
            }
        );

        _paneToggleButton = CreateNavigationButton(
            "NavigationToggleButton",
            NavigationViewButtonPlacement.Pane,
            NavigationViewButtonRole.PaneToggle
        );

        _paneToggleButton.Click += OnToggleButtonClick;

        // Title-bar commands use separate control instances. WPF elements can have only one
        // visual parent, and ContentPresenter may defer removing its old child until layout;
        // separate instances therefore make visibility changes safe while the window is live.
        _titleBarBackButton = CreateNavigationButton(
            "NavigationBackButton",
            NavigationViewButtonPlacement.TitleBar,
            NavigationViewButtonRole.Back
        );

        _titleBarBackButton.Click += OnBackButtonClick;

        BindingOperations.SetBinding(
            _titleBarBackButton,
            IsEnabledProperty,
            new Binding(nameof(IsBackEnabled))
            {
                Source = this
            }
        );

        _titleBarToggleButton = CreateNavigationButton(
            "NavigationToggleButton",
            NavigationViewButtonPlacement.TitleBar,
            NavigationViewButtonRole.PaneToggle
        );

        _titleBarToggleButton.Click += OnToggleButtonClick;

        BackButton   = _paneBackButton;
        ToggleButton = _paneToggleButton;

        Loaded      += OnLoaded;
        Unloaded    += OnUnloaded;
        SizeChanged += OnSizeChanged;

        // These collections are stable for the lifetime of the control. ItemsSource values are mirrored
        // into them so templates, derived state, navigation indexes, and search all observe one pipeline.
        var menuItems = new ObservableCollection<object>();
        menuItems.CollectionChanged += OnMenuItemsCollectionChanged;
        SetValue(MenuItemsPropertyKey, menuItems);

        var footerMenuItems = new ObservableCollection<object>();
        footerMenuItems.CollectionChanged += OnFooterMenuItemsCollectionChanged;
        SetValue(FooterMenuItemsPropertyKey, footerMenuItems);
    }

    /// <inheritdoc/>
    public INavigationViewItem? SelectedItem { get; protected set; }

    protected Dictionary<string, INavigationViewItem> PageKeyIndex  { get; } = [];
    protected Dictionary<Type, INavigationViewItem>   PageTypeIndex { get; } = [];

    private readonly HashSet<NavigationViewItem>                        _registeredItems  = [];
    private readonly ObservableCollection<string>                       _autoSuggestItems = [];
    private readonly ObservableCollection<NavigationViewBreadcrumbItem> _breadcrumbItems  = [];
    private readonly NavigationViewButton                               _paneBackButton;
    private readonly NavigationViewButton                               _paneToggleButton;
    private readonly NavigationViewButton                               _titleBarBackButton;
    private readonly NavigationViewButton                               _titleBarToggleButton;
    private readonly StackPanel                                         _paneButtonsHost = new();
    private          TitleBar?                                          _titleBarContentOwner;
    private          object?                                            _displacedTitleBarLeftContent;
    private          bool                                               _isLeftPaneWidthInitialized;

    protected static void UpdateVisualState(NavigationView navigationView)
    {
        // Only the Left template owns PaneStates. In particular, changing IsPaneOpen while another
        // template is active must not prime a state that will later animate when Left is restored.
        if (navigationView.PaneDisplayMode != NavigationViewPaneDisplayMode.Left)
        {
            return;
        }

        // IsPaneOpen changes before Loaded are initialization. Update the retained base width
        // directly so the first rendered frame is already final and no storyboard is started.
        if (!navigationView.IsLoaded)
        {
            navigationView.SetValue(
                LeftPaneWidthPropertyKey,
                navigationView.IsPaneOpen
                    ? navigationView.LeftPaneOpenWidth
                    : navigationView.LeftPaneCompactWidth
            );
            navigationView._isLeftPaneWidthInitialized = true;
            return;
        }

        // Capture before replacing the current state storyboard. Width returns the active clock's
        // effective value, so reversing an in-flight animation starts from the pixels now on screen.
        navigationView.CaptureLeftPaneWidth();

        _ = VisualStateManager.GoToState(
            navigationView,
            navigationView.IsPaneOpen ? "PaneOpen" : "PaneCompact",
            true
        );
    }

    /// <inheritdoc />
    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);

        NavigationStack.CollectionChanged += NavigationStackOnCollectionChanged;

        InvalidateArrange();
        InvalidateVisual();
        UpdateLayout();

        UpdateAutoSuggestBoxSuggestions();

        AddItemsToNavigationIndexes();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationStack.CollectionChanged -= NavigationStackOnCollectionChanged;
        NavigationStack.CollectionChanged += NavigationStackOnCollectionChanged;

        UpdatePaneButtonPlacement();
    }

    /// <summary>
    /// This virtual method is called when this element is detached form a loaded tree.
    /// </summary>
    protected virtual void OnUnloaded(object sender, RoutedEventArgs e)
    {
        NavigationStack.CollectionChanged -= NavigationStackOnCollectionChanged;

        // A TitleBar can outlive a NavigationView in the same window. Release the contributed
        // content on detach so the title bar does not retain this control through button handlers.
        ReleaseTitleBarContent();

        ClearRegisteredNavigationItems();

        ClearJournal();

        if (AutoSuggestBox is not null)
        {
            AutoSuggestBox.SuggestionChosen -= AutoSuggestBoxOnSuggestionChosen;
            AutoSuggestBox.QuerySubmitted   -= AutoSuggestBoxOnQuerySubmitted;
        }

        if (Header is BreadcrumbBar breadcrumbBar)
        {
            breadcrumbBar.ItemClicked -= BreadcrumbBarOnItemClicked;
        }

        // Clean up ItemsSource CollectionChanged subscriptions to prevent memory leaks
        if (MenuItemsSource is INotifyCollectionChanged menuItemsSource)
        {
            menuItemsSource.CollectionChanged -= OnMenuItemsSourceCollectionChanged;
        }

        if (FooterMenuItemsSource is INotifyCollectionChanged footerMenuItemsSource)
        {
            footerMenuItemsSource.CollectionChanged -= OnFooterMenuItemsSourceCollectionChanged;
        }

        AutoSuggestBoxSymbolButton?.Click -= AutoSuggestBoxSymbolButtonOnClick;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        // Back button
        if (e.ChangedButton is MouseButton.XButton1)
        {
            GoBack();
            e.Handled = true;
        }

        base.OnMouseDown(e);
    }

    /// <summary>
    /// This virtual method is called when ActualWidth or ActualHeight (or both) changed.
    /// </summary>
    protected virtual void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // TODO: Update reveal
    }

    /// <summary>
    /// This virtual method is called when <see cref="BackButton"/> is clicked.
    /// </summary>
    protected virtual void OnBackButtonClick(object sender, RoutedEventArgs e)
    {
        GoBack();
    }

    /// <summary>
    /// This virtual method is called when <see cref="ToggleButton"/> is clicked.
    /// </summary>
    protected virtual void OnToggleButtonClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsPaneOpenProperty, !IsPaneOpen);
    }

    /// <summary>
    /// This virtual method is called when <see cref="AutoSuggestBoxSymbolButton"/> is clicked.
    /// </summary>
    protected virtual void AutoSuggestBoxSymbolButtonOnClick(object sender, RoutedEventArgs e)
    {
        SetCurrentValue(IsPaneOpenProperty, !IsPaneOpen);

        // Should not call .Focus() immediately.
        Dispatcher.BeginInvoke(() => AutoSuggestBox?.Focus(), DispatcherPriority.Input);
    }

    /// <summary>
    /// This virtual method is called when <see cref="PaneDisplayMode"/> is changed.
    /// </summary>
    protected virtual void OnPaneDisplayModeChanged()
    {
        // PaneDisplayMode selects a template; it must not overwrite independent public settings.
        // The LeftFluent template intentionally omits the pane command host, so its visual contract
        // is achieved without permanently changing button visibility when the caller returns to Left.
        UpdatePaneButtonPlacement();
        RaiseEvent(new RoutedEventArgs(PaneDisplayModeChangedEvent, this));
    }

    /// <summary>
    /// Stops storyboards owned by the current pane template before a display-mode trigger replaces
    /// that template. This must run from the dependency-property callback: WPF evaluates style
    /// triggers immediately after the callback and may clear the old generated subtree before any
    /// later layout or <see cref="OnApplyTemplate"/> notification.
    /// </summary>
    private void PrepareForPaneDisplayModeChange(NavigationViewPaneDisplayMode oldMode)
    {
        if (oldMode != NavigationViewPaneDisplayMode.Left)
        {
            return;
        }

        CaptureLeftPaneWidth();

        // PaneReset has no storyboard. Entering it makes VisualStateManager remove the template-owned
        // width and opacity clocks before StyleHelper.ClearGeneratedSubTree detaches their targets.
        _ = VisualStateManager.GoToState(this, "PaneReset", false);

        // VisualStateManager stops its storyboard, but WPF can retain animation storage until the
        // next property evaluation. Remove the Width clock synchronously while the retained-width
        // TemplateBinding still supplies a finite base value; StyleHelper clears the generated
        // binding immediately after this property callback returns.
        if (Template?.FindName("PaneGrid", this) is FrameworkElement paneGrid)
        {
            paneGrid.BeginAnimation(FrameworkElement.WidthProperty, null);
        }
    }

    /// <summary>
    /// Initializes the retained Left width before WPF generates the first Left template subtree.
    /// Doing this before <see cref="OnApplyTemplate"/> reaches its base implementation means the
    /// PaneGrid TemplateBinding receives its final cold-start value immediately and no storyboard
    /// is needed merely to establish the initial open or compact presentation.
    /// </summary>
    private void EnsureLeftPaneWidthInitialized()
    {
        if (_isLeftPaneWidthInitialized || PaneDisplayMode != NavigationViewPaneDisplayMode.Left)
        {
            return;
        }

        SetValue(
            LeftPaneWidthPropertyKey,
            IsPaneOpen ? LeftPaneOpenWidth : LeftPaneCompactWidth
        );
        _isLeftPaneWidthInitialized = true;
    }

    /// <summary>
    /// Captures the outgoing Left template's effective Width before a PaneDisplayMode style trigger
    /// destroys its generated subtree. Width is the animated dependency property and therefore
    /// exposes the current clock value even between layout passes; ActualWidth is only a fallback for
    /// a custom template that leaves Width on Auto.
    /// </summary>
    private void CaptureLeftPaneWidth()
    {
        if (Template?.FindName("PaneGrid", this) is not FrameworkElement paneGrid)
        {
            return;
        }

        double width = paneGrid.Width;
        if (double.IsNaN(width))
        {
            width = paneGrid.ActualWidth;
        }

        if (double.IsNaN(width) || double.IsInfinity(width) || width < 0D)
        {
            return;
        }

        SetValue(LeftPaneWidthPropertyKey, width);
        _isLeftPaneWidthInitialized = true;
    }

    /// <summary>
    /// Applies a changed open/compact target to a currently matching Left presentation. The active
    /// state is reset first because a completed WPF state storyboard continues to hold its animated
    /// Width value above the TemplateBinding. Configuration changes are therefore immediate and do
    /// not masquerade as pane-toggle animations.
    /// </summary>
    private void UpdateVisibleLeftPaneTargetWidth(DependencyProperty changedProperty)
    {
        if (!_isLeftPaneWidthInitialized || PaneDisplayMode != NavigationViewPaneDisplayMode.Left)
        {
            return;
        }

        bool changesCurrentWidth = IsPaneOpen
            ? changedProperty == LeftPaneOpenWidthProperty
            : changedProperty == LeftPaneCompactWidthProperty;

        if (!changesCurrentWidth)
        {
            return;
        }

        _ = VisualStateManager.GoToState(this, "PaneReset", false);
        if (Template?.FindName("PaneGrid", this) is FrameworkElement paneGrid)
        {
            paneGrid.BeginAnimation(FrameworkElement.WidthProperty, null);
        }

        SetValue(
            LeftPaneWidthPropertyKey,
            IsPaneOpen ? LeftPaneOpenWidth : LeftPaneCompactWidth
        );
    }

    /// <summary>
    /// Creates a navigation-owned command. Commands are intentionally created by
    /// <see cref="NavigationView"/> rather than by a control template because their placement
    /// is selected at runtime and may target <see cref="TitleBar.LeftContent"/>.
    /// </summary>
    /// <param name="automationId">The stable automation identifier shared by both host-specific instances.</param>
    /// <param name="placement">The host in which the button is placed.</param>
    /// <param name="role">The navigation command represented by the button.</param>
    private NavigationViewButton CreateNavigationButton(
        string                        automationId,
        NavigationViewButtonPlacement placement,
        NavigationViewButtonRole      role)
    {
        var button = new NavigationViewButton();

        button.InitializeOwner(this, placement, role);

        AutomationProperties.SetAutomationId(button, automationId);

        // A contributed title-bar button must remain client content. Otherwise the inherited
        // Caption identity would turn a click into a native window drag operation.
        TitleBar.SetElementIdentity(button, TitleBarElementIdentity.UserContent);
        return button;
    }

    /// <summary>
    /// Recomputes the host of the back and pane-toggle commands. At most one command is
    /// contributed to the title bar; the back command wins when both are requested.
    /// </summary>
    private void UpdatePaneButtonPlacement()
    {
        var titleBar = TitleBar;
        if (!ReferenceEquals(_titleBarContentOwner, titleBar))
        {
            ReleaseTitleBarContent();

            if (titleBar is not null)
            {
                _titleBarContentOwner         = titleBar;
                _displacedTitleBarLeftContent = titleBar.LeftContent;
            }
        }

        if (
            _titleBarContentOwner is not null &&
            (
                ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarBackButton) ||
                ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarToggleButton)
            )
        )
        {
            _titleBarContentOwner.SetCurrentValue(TitleBar.LeftContentProperty, null);
        }

        _paneButtonsHost.Children.Clear();

        var showBack   = IsBackButtonVisible != NavigationViewBackButtonVisible.Collapsed;
        var showToggle = IsPaneToggleVisible;
        var titleButton = titleBar is null
            ? null
            : showBack
                ? _titleBarBackButton
                : showToggle
                    ? _titleBarToggleButton
                    : null;

        AddPaneButton(showBack && titleButton is null,                 _paneBackButton);
        AddPaneButton(showToggle && (titleButton is null || showBack), _paneToggleButton);

        _paneButtonsHost.SetCurrentValue(
            StackPanel.OrientationProperty,
            PaneDisplayMode is NavigationViewPaneDisplayMode.Top or NavigationViewPaneDisplayMode.Bottom
                ? Orientation.Horizontal
                : Orientation.Vertical
        );

        // Keep a stable presenter child. Replacing a live ContentPresenter child with the
        // same UIElement during one dispatcher turn can race its deferred template cleanup.
        PaneButtonsPresenter?.SetCurrentValue(ContentPresenter.ContentProperty, _paneButtonsHost);

        // SetCurrentValue preserves a binding placed on LeftContent. NavigationView owns the
        // effective value only while it is associated with this TitleBar.
        titleBar?.SetCurrentValue(TitleBar.LeftContentProperty, titleButton);
    }

    private void AddPaneButton(bool shouldAdd, NavigationViewButton button)
    {
        if (shouldAdd)
        {
            _paneButtonsHost.Children.Add(button);
        }
    }

    /// <summary>
    /// Releases only content still owned by this navigation view. If an application replaced
    /// <see cref="TitleBar.LeftContent"/> while attached, that newer value is left untouched.
    /// </summary>
    private void ReleaseTitleBarContent()
    {
        if (_titleBarContentOwner is null)
        {
            return;
        }

        if (ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarBackButton) ||
            ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarToggleButton) ||
            _titleBarContentOwner.LeftContent is null)
        {
            _titleBarContentOwner.SetCurrentValue(
                TitleBar.LeftContentProperty,
                _displacedTitleBarLeftContent
            );
        }

        _titleBarContentOwner         = null;
        _displacedTitleBarLeftContent = null;
    }

    internal void ToggleAllExpands()
    {
        // TODO: When shift clicked on navigationviewitem
    }

    /// <summary>
    /// Clears the currently selected item.
    /// </summary>
    internal void ClearSelectedItem()
    {
        SelectedItem = null;
        OnSelectionChanged();
    }

    /// <summary>
    /// Invokes a navigation item and closes transient menus only after leaf navigation succeeds. A
    /// canceled navigation intentionally leaves the current branch available to the user. A root
    /// navigation surface may both navigate and open its child ContextMenu from one primary action,
    /// so a successful item with children remains open until that action applies the requested menu
    /// state. Projected MenuItems follow native semantics and invoke this method only for leaves.
    /// </summary>
    internal bool OnNavigationViewItemClick(NavigationViewItem navigationViewItem)
    {
        OnItemInvoked();
        bool navigated = NavigateInternal(navigationViewItem);

        if (navigated && !navigationViewItem.HasItems)
        {
            CloseAllNavigationViewItemSubmenus();
        }

        return navigated;
    }

    /// <summary>Closes every transient navigation-item submenu without altering inline expansion.</summary>
    internal void CloseAllNavigationViewItemSubmenus()
    {
        // Each root owns one native ContextMenu. Closing that root lets MenuBase dismiss every
        // cascading MenuItem Popup and release capture as one atomic menu-mode transition.
        foreach (NavigationViewItem item in GetNavigationViewItemRoots())
        {
            item.CloseSubmenuBranch();
        }
    }

    /// <summary>
    /// Closes every detached root branch except the branch about to become active.
    /// </summary>
    /// <remarks>
    /// An on-screen ContextMenu normally enforces this invariant through WPF's global MenuBase
    /// capture. Keeping the same rule at the semantic state boundary also covers programmatic and
    /// UI Automation expansion before a template has acquired a PresentationSource. It additionally
    /// prevents two roots from briefly reporting Expanded while the new native menu is opening.
    /// </remarks>
    internal void CloseOtherNavigationViewItemSubmenus(NavigationViewItem activeRoot)
    {
        foreach (NavigationViewItem item in GetNavigationViewItemRoots())
        {
            if (!ReferenceEquals(item, activeRoot))
            {
                item.CloseSubmenuBranch();
            }
        }
    }

    private HashSet<NavigationViewItem> GetNavigationViewItemRoots()
    {
        var roots = new HashSet<NavigationViewItem>(MenuItems.OfType<NavigationViewItem>());
        roots.UnionWith(FooterMenuItems.OfType<NavigationViewItem>());
        roots.UnionWith(
            _registeredItems.Where(
                static item => item.NavigationViewItemParent is null
            )
        );

        // The registered set is essential for root data items: their generated NavigationViewItem
        // container is the ContextMenu owner, while the public MenuItems collection still contains
        // only the caller's data object. The HashSet also makes direct roots encountered through
        // both paths close exactly once.
        return roots;
    }

    protected virtual void BreadcrumbBarOnItemClicked(
        BreadcrumbBar sender,
        BreadcrumbBarItemClickedEventArgs e
    )
    {
        var item = (NavigationViewBreadcrumbItem)e.Item;
        Navigate(item.PageId);
    }

    private void UpdateAutoSuggestBoxSuggestions()
    {
        if (AutoSuggestBox == null)
        {
            return;
        }

        _autoSuggestItems.Clear();

        AddItemsToAutoSuggestBoxItems();
    }

    /// <summary>
    /// Navigate to the page after its name is selected in <see cref="AutoSuggestBox"/>.
    /// </summary>
    private void AutoSuggestBoxOnSuggestionChosen(
        AutoSuggestBox sender,
        AutoSuggestBoxSuggestionChosenEventArgs args)
    {
        if (sender.IsSuggestionListOpen)
        {
            return;
        }

        if (args.SelectedItem is not string selectedSuggestBoxItem)
        {
            return;
        }

        if (NavigateToMenuItemFromAutoSuggestBox(MenuItems, selectedSuggestBoxItem))
        {
            return;
        }

        NavigateToMenuItemFromAutoSuggestBox(FooterMenuItems, selectedSuggestBoxItem);
    }

    private void AutoSuggestBoxOnQuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var suggestions = new List<string>();
        var querySplit  = args.QueryText.Split(' ');

        foreach (var item in _autoSuggestItems)
        {
            bool isMatch = true;

            foreach (string queryToken in querySplit)
            {
                if (item.IndexOf(queryToken, StringComparison.CurrentCultureIgnoreCase) < 0)
                {
                    isMatch = false;
                }
            }

            if (isMatch)
            {
                suggestions.Add(item);
            }
        }

        if (suggestions.Count <= 0)
        {
            return;
        }

        var element = suggestions.First();

        if (NavigateToMenuItemFromAutoSuggestBox(MenuItems, element))
        {
            return;
        }

        NavigateToMenuItemFromAutoSuggestBox(FooterMenuItems, element);
    }

    /// <summary>
    /// Registers navigation items from <paramref name="items"/> and their descendants in the page lookup indexes.
    /// </summary>
    /// <param name="items">The navigation items to register.</param>
    /// <remarks>
    /// Registration also attaches descendant collection listeners so later hierarchy changes flow through the
    /// same index and suggestion update pipeline as changes to the two root collections.
    /// </remarks>
    protected virtual void AddItemsToNavigationIndexes(IEnumerable items)
    {
        foreach (NavigationViewItem nvItem in items.OfType<NavigationViewItem>())
        {
            // Direct root controls exist independently of any ItemsPresenter. Associate them at
            // collection registration time so closing a pane or unloading a template cannot make
            // their presentation context fall back to an ancestor NavigationView or to defaults.
            if (
                nvItem.NavigationViewItemParent is null &&
                (MenuItems.Contains(nvItem) || FooterMenuItems.Contains(nvItem))
            )
            {
                nvItem.AttachToNavigationViewHost(this);
            }

            RegisterNavigationViewItem(nvItem);

            if (!PageKeyIndex.ContainsKey(nvItem.Id))
            {
                PageKeyIndex.Add(nvItem.Id, nvItem);
            }

            if (!PageKeyIndex.ContainsKey(nvItem.TargetPageTag))
            {
                PageKeyIndex.Add(nvItem.TargetPageTag, nvItem);
            }

            if (nvItem.TargetPageType != null &&
                !PageTypeIndex.ContainsKey(nvItem.TargetPageType))
            {
                PageTypeIndex.Add(nvItem.TargetPageType, nvItem);
            }

            ((INavigationViewItem)nvItem).IsMenuElement = true;

            if (nvItem.HasItems)
            {
                AddItemsToNavigationIndexes(nvItem.GetNavigationChildren());
            }
        }
    }

    /// <summary>
    /// Registers the current main and footer navigation hierarchies in the page lookup indexes.
    /// </summary>
    protected virtual void AddItemsToNavigationIndexes()
    {
        AddItemsToNavigationIndexes(MenuItems);
        AddItemsToNavigationIndexes(FooterMenuItems);
    }

    /// <summary>
    /// Removes navigation items from <paramref name="items"/> and their descendants from the page lookup indexes.
    /// </summary>
    /// <param name="items">The navigation items to unregister.</param>
    protected virtual void RemoveItemsFromNavigationIndexes(IEnumerable items)
    {
        foreach (NavigationViewItem item in items.OfType<NavigationViewItem>())
        {
            item.CloseSubmenuBranch();
            UnregisterNavigationViewItem(item);

            if (
                item.NavigationViewItemParent is null &&
                !MenuItems.Contains(item) &&
                !FooterMenuItems.Contains(item)
            )
            {
                item.DetachFromNavigationViewHost(this);
            }
        }
    }

    internal void NotifyNavigationViewItemLookupChanged(NavigationViewItem item)
    {
        if (!_registeredItems.Contains(item))
        {
            return;
        }

        RebuildNavigationIndexes();
        UpdateAutoSuggestBoxSuggestions();
    }

    internal void NotifyNavigationViewItemContentChanged(NavigationViewItem item)
    {
        if (!_registeredItems.Contains(item))
        {
            return;
        }

        UpdateAutoSuggestBoxSuggestions();
    }

    private void RegisterNavigationViewItem(NavigationViewItem item)
    {
        if (_registeredItems.Add(item) &&
            item.Items is INotifyCollectionChanged menuItems)
        {
            menuItems.CollectionChanged += OnNavigationViewItemMenuItemsCollectionChanged;
        }
    }

    /// <summary>
    /// Adds a data-generated item container to navigation-wide lookup and search state.
    /// </summary>
    /// <remarks>
    /// Source objects are not navigation containers and may receive their navigation properties
    /// from ItemContainerStyle bindings. Registration therefore occurs only after WPF has prepared
    /// the concrete container. Direct NavigationViewItem objects continue to be registered from the
    /// collection pipeline and are ignored by the HashSet if encountered again.
    /// </remarks>
    internal void RegisterGeneratedNavigationItem(NavigationViewItem item)
    {
        AddItemsToNavigationIndexes(new[] { item });
        UpdateAutoSuggestBoxSuggestions();
    }

    /// <summary>Removes a data-generated container before its generator releases or recycles it.</summary>
    internal void UnregisterGeneratedNavigationItem(NavigationViewItem item)
    {
        RemoveItemsFromNavigationIndexes(new[] { item });
        UpdateAutoSuggestBoxSuggestions();
    }

    private void UnregisterNavigationViewItem(NavigationViewItem item)
    {
        if (_registeredItems.Remove(item) &&
            item.Items is INotifyCollectionChanged menuItems)
        {
            menuItems.CollectionChanged -= OnNavigationViewItemMenuItemsCollectionChanged;
        }

        PageKeyIndex.Remove(item.Id);
        PageKeyIndex.Remove(item.TargetPageTag);

        if (item.TargetPageType is not null)
        {
            PageTypeIndex.Remove(item.TargetPageType);
        }

        if (item.HasItems)
        {
            RemoveItemsFromNavigationIndexes(item.GetNavigationChildren());
        }
    }

    private void ClearRegisteredNavigationItems()
    {
        foreach (var item in _registeredItems)
        {
            // Reset notifications do not expose removed items. Close the previously indexed
            // roots before discarding the index so a detached ContextMenu cannot remain open. Limiting
            // recursion to roots avoids walking every descendant subtree repeatedly.
            if (
                item.NavigationViewItemParent is not NavigationViewItem parentItem ||
                !_registeredItems.Contains(parentItem)
            )
            {
                item.CloseSubmenuBranch();

                if (!MenuItems.Contains(item) && !FooterMenuItems.Contains(item))
                {
                    item.DetachFromNavigationViewHost(this);
                }
            }

            if (item.Items is INotifyCollectionChanged menuItems)
            {
                menuItems.CollectionChanged -= OnNavigationViewItemMenuItemsCollectionChanged;
            }
        }

        _registeredItems.Clear();

        PageKeyIndex.Clear();
        PageTypeIndex.Clear();
    }

    private void OnNavigationViewItemMenuItemsCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        ProcessNavigationItemsCollectionChange(e);
    }

    /// <summary>
    /// Applies the navigation-wide consequences of a change in either root collection or any registered
    /// descendant collection. Root-only state such as <see cref="HasMenuItems"/> is intentionally maintained
    /// by the corresponding root handler before this method is called.
    /// </summary>
    /// <remarks>
    /// Keeping this method independent of the event sender is important: descendant collections participate
    /// in the same page lookup and suggestion model, but must never update either root collection's derived
    /// <c>Has*</c> property.
    /// </remarks>
    private void ProcessNavigationItemsCollectionChange(NotifyCollectionChangedEventArgs e)
    {
        UpdateNavigationIndexes(e);
        UpdateAutoSuggestBoxSuggestions();
    }

    /// <summary>
    /// Applies the portion of a collection notification that affects page lookup and descendant subscriptions.
    /// </summary>
    private void UpdateNavigationIndexes(NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                if (e.NewItems is not null)
                {
                    AddItemsToNavigationIndexes(e.NewItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Remove:
            {
                if (e.OldItems is not null)
                {
                    RemoveItemsFromNavigationIndexes(e.OldItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Replace:
            {
                if (e.OldItems is not null)
                {
                    RemoveItemsFromNavigationIndexes(e.OldItems);
                }

                if (e.NewItems is not null)
                {
                    AddItemsToNavigationIndexes(e.NewItems);
                }

                break;
            }

            case NotifyCollectionChangedAction.Reset:
            {
                // Reset does not describe the removed items, so incremental unregistering is impossible.
                // Rebuilding also restores descendant subscriptions from the two authoritative roots.
                RebuildNavigationIndexes();
                break;
            }

            case NotifyCollectionChangedAction.Move:
                // Page lookup membership is order-independent. Suggestions are refreshed by the caller
                // because their displayed sequence follows the navigation tree order.
                break;
        }
    }

    private void RebuildNavigationIndexes()
    {
        ClearRegisteredNavigationItems();
        AddItemsToNavigationIndexes();
    }

    protected virtual void AddItemsToAutoSuggestBoxItems(IEnumerable list)
    {
        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
            if (nvItem.TargetPageType is not null &&
                !string.IsNullOrWhiteSpace(nvItem.SearchableContent))
            {
                _autoSuggestItems.Add(nvItem.SearchableContent);
            }

            if (nvItem.HasItems)
            {
                AddItemsToAutoSuggestBoxItems(nvItem.GetNavigationChildren());
            }
        }
    }

    protected virtual void AddItemsToAutoSuggestBoxItems()
    {
        AddItemsToAutoSuggestBoxItems(MenuItems);
        AddItemsToAutoSuggestBoxItems(FooterMenuItems);
    }

    protected virtual bool NavigateToMenuItemFromAutoSuggestBox(
        IEnumerable list,
        string selectedSuggestItem)
    {
        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
            if (nvItem.SearchableContent == selectedSuggestItem)
            {
                NavigateInternal(nvItem);

                // The container owns its complete descendant branch and is intentionally not a
                // focus target. Focusing its bounded header lets WPF reveal only the selected row;
                // asking the expanded HeaderedItemsControl itself to come into view would recreate
                // the historical jump that tried to reveal every child at once.
                _ = nvItem.FocusHeader();

                return true;
            }

            if (NavigateToMenuItemFromAutoSuggestBox(nvItem.GetNavigationChildren(), selectedSuggestItem))
            {
                return true;
            }
        }

        return false;
    }

    [DebuggerStepThrough]
    private void NavigationStackOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                _breadcrumbItems.Add(
                    new NavigationViewBreadcrumbItem((INavigationViewItem)e.NewItems![0]!)
                );
                break;

            case NotifyCollectionChangedAction.Remove:
                _breadcrumbItems.RemoveAt(e.OldStartingIndex);
                break;

            case NotifyCollectionChangedAction.Replace:
                _breadcrumbItems[0] = new NavigationViewBreadcrumbItem(
                    (INavigationViewItem)e.NewItems![0]!
                );
                break;

            case NotifyCollectionChangedAction.Move:
                break;

            case NotifyCollectionChangedAction.Reset:
                _breadcrumbItems.Clear();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Action, $"Unsupported action: {e.Action}");
        }

        UpdateBreadcrumbContents();
    }

    private void UpdateBreadcrumbContents()
    {
        foreach (var breadcrumbItem in _breadcrumbItems)
        {
            breadcrumbItem.UpdateFromSource();
        }
    }
}
