// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.CompilerServices;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents a container that enables navigation of app content. It has a header, a view for the main content, and a menu pane for navigation commands.
/// </summary>
/// <remarks>
/// Navigation infrastructure should depend on <see cref="Wpf.Zen.Ui.INavigationHost"/>, which is
/// intentionally limited to host-level navigation operations. The visual surface remains a
/// concrete control contract: mirroring its dependency properties, routed events, and template
/// integration in a second interface would create another API that must change in lockstep without
/// providing an independently implementable WPF control abstraction.
///
/// Navigated content is hosted by a stable WPF <see cref="Frame"/> so changing
/// <see cref="PaneDisplayMode"/> does not recreate the current page. The frame does not add a
/// <see cref="ScrollViewer"/> around the page: each page owns its scrolling policy and may use a
/// standard <see cref="ScrollViewer"/>, <see cref="DynamicScrollViewer"/>, or no scrolling host at
/// all. Pages that want the library's full-page dynamic scrolling can explicitly apply the
/// <see cref="Theming.StyleKeys.DynamicScrollPageStyle"/> resource, normally through
/// <see cref="Markup.DynamicStyleExtension"/>. In particular,
/// <see cref="ScrollViewer.CanContentScroll"/> retains its WPF-defined meaning of selecting logical
/// versus pixel scrolling inside the page's own scroll viewer.
/// A custom page template that also keeps its scroll viewer at the navigation edge should set
/// <see cref="IsContentPaddingHandledProperty"/> and apply the inherited
/// <see cref="ContentPaddingProperty"/> inside that viewer. Ordinary content requires no opt-in and
/// continues to receive <see cref="Control.Padding"/> from the native frame.
///
/// The internal frame's browser-era command bindings are removed, and mouse side buttons do not
/// implicitly change this control's journal. Applications that also want to stop the corresponding
/// raw input and routed commands at the navigation boundary can configure
/// <see cref="SuppressedNavigationInput"/>.
/// </remarks>
public partial class NavigationView : Control, INavigationHost, INavigationState
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

        // Control.Padding normally invalidates the parent measure, which remains appropriate for
        // NavigationView. The callback additionally republishes the value to the current page: a
        // native Frame is an inheritance boundary, so page-owned scrolling cannot obtain this
        // content inset through ordinary property inheritance or an ancestor binding.
        PaddingProperty.OverrideMetadata(
            typeof(NavigationView),
            new FrameworkPropertyMetadata(
                default(Thickness),
                FrameworkPropertyMetadataOptions.AffectsParentMeasure,
                OnPaddingChanged
            )
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

        // The native WPF frame belongs to the control rather than to a particular template. WPF
        // destroys a template's generated subtree whenever Template changes; keeping the frame in
        // that subtree would discard the current page while merely changing PaneDisplayMode. The
        // templates expose only a host slot, and OnApplyTemplate moves this stable frame between
        // those slots.

        _navigationFrame.Navigated += OnNavigationFrameNavigated;
        _navigationFrame.Unloaded += OnNavigationFrameUnloaded;

        InitializeNavigationInputSuppression();

        _paneBackButton = CreateNavigationButton(
            "NavigationBackButton",
            NavigationViewButtonPlacement.Pane,
            NavigationViewButtonRole.Back
        );

        _paneBackButton.Click += OnBackButtonClick;

        BindingOperations.SetBinding(
            _paneBackButton,
            IsEnabledProperty,
            new Binding(nameof(IsBackEnabled)) { Source = this }
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
            new Binding(nameof(IsBackEnabled)) { Source = this }
        );

        _titleBarToggleButton = CreateNavigationButton(
            "NavigationToggleButton",
            NavigationViewButtonPlacement.TitleBar,
            NavigationViewButtonRole.PaneToggle
        );

        _titleBarToggleButton.Click += OnToggleButtonClick;

        BackButton = _paneBackButton;
        ToggleButton = _paneToggleButton;

        // Auto is the default and the initial journal is empty. Apply the derived visibility before
        // either button can be measured in a pane or contributed to a title bar.
        UpdateBackButtonVisibility();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;

        // These collections are the direct-content alternative to ItemsSource, matching the two
        // mutually exclusive item paths exposed by WPF ItemsControl. ItemsSource is never copied
        // into them; preserving source identity is required for menu selection and invocation.
        MenuItems.CollectionChanged += OnMenuItemsCollectionChanged;
        FooterMenuItems.CollectionChanged += OnFooterMenuItemsCollectionChanged;

        Breadcrumbs = new ReadOnlyObservableCollection<BreadcrumbSegment>(_breadcrumbs);
        SetCurrentValue(NavigationMapProperty, new NavigationMap());
    }

    /// <summary>
    /// Gets the current semantic path as presentation-safe, non-visual breadcrumb segments.
    /// </summary>
    /// <remarks>
    /// The collection is stable for the lifetime of the control and can be assigned directly to a
    /// <see cref="ItemsControl.ItemsSource"/> on a <see cref="BreadcrumbBar"/>. The collection is a
    /// projection of <see cref="CurrentLocation"/> and never contains menu items or live controls.
    /// A BreadcrumbBar remains an ordinary independent presenter: the NavigationView neither owns
    /// it nor modifies its templates, visibility, source, or event subscriptions.
    /// </remarks>
    public ReadOnlyObservableCollection<BreadcrumbSegment> Breadcrumbs { get; }

    internal Dictionary<string, NavigationEntry> PageKeyIndex { get; } = [];
    internal Dictionary<Type, NavigationEntry> PageTypeIndex { get; } = [];

    private readonly HashSet<NavigationViewItem> _registeredItems = [];
    // Source objects are identities, not values. View models may implement value equality for
    // persistence or comparison; using that equality here would accidentally share selection,
    // parentage and generated containers between two distinct menu entries.
    private readonly Dictionary<object, NavigationEntry> _entriesByItem = new(
        ReferenceComparer<object>.Instance
    );
    private readonly HashSet<object> _entriesSeenDuringRebuild = new(
        ReferenceComparer<object>.Instance
    );
    private readonly HashSet<INotifyCollectionChanged> _navigationCollections = new(
        ReferenceComparer<INotifyCollectionChanged>.Instance
    );
    private readonly HashSet<INotifyPropertyChanged> _navigationItemsWithNotifications = new(
        ReferenceComparer<INotifyPropertyChanged>.Instance
    );
    private readonly ObservableCollection<string> _autoSuggestItems = [];
    private readonly BreadcrumbCollection _breadcrumbs = [];
    private readonly NavigationViewButton _paneBackButton;
    private readonly NavigationViewButton _paneToggleButton;
    private readonly NavigationViewButton _titleBarBackButton;
    private readonly NavigationViewButton _titleBarToggleButton;
    private readonly StackPanel _paneButtonsHost = new();
    private TitleBar? _titleBarContentOwner;
    private object? _displacedTitleBarLeftContent;
    private bool _isTitleBarContentContributed;
    private bool _isTitleBarContentContributionSuppressed;
    private bool _isLeftPaneWidthInitialized;

    /// <summary>
    /// Replaces a resolved path with one reset notification. Ordinary Clear/Add projection would
    /// expose transient partial paths to bindings and make one dynamic label change produce many
    /// layout passes in BreadcrumbBar.
    /// </summary>
    private sealed class BreadcrumbCollection : ObservableCollection<BreadcrumbSegment>
    {
        internal void ReplaceWith(IReadOnlyList<BreadcrumbSegment> items)
        {
            if (Count == items.Count && this.SequenceEqual(items))
            {
                return;
            }

            CheckReentrancy();
            bool countChanged = Count != items.Count;
            Items.Clear();
            foreach (BreadcrumbSegment item in items)
            {
                Items.Add(item);
            }

            if (countChanged)
            {
                OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
            }

            OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
            OnCollectionChanged(
                new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset)
            );
        }
    }

    /// <inheritdoc />
    public event EventHandler? CurrentLocationChanged;

    /// <summary>
    /// Compares source items by object identity so navigation state remains associated with the
    /// exact object supplied by the application.
    /// </summary>
    private sealed class ReferenceComparer<T> : IEqualityComparer<T>
        where T : class
    {
        internal static ReferenceComparer<T> Instance { get; } = new();

        private ReferenceComparer() { }

        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
    }

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

        InvalidateArrange();
        InvalidateVisual();
        UpdateLayout();

        RebuildNavigationIndexes();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new NavigationViewAutomationPeer(this);
    }

    /// <summary>Gets the realized container representing the current single selection.</summary>
    internal NavigationViewItem? GetSelectedNavigationContainer() => _currentEntry?.Container;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RebuildNavigationIndexes();

        if (AutoSuggestBox is { } autoSuggestBox)
        {
            // A NavigationView can leave and re-enter a loaded tree without its dependency
            // properties changing. Remove before add so manually raised or repeated Loaded events
            // cannot multiply the handlers.
            autoSuggestBox.SuggestionChosen -= AutoSuggestBoxOnSuggestionChosen;
            autoSuggestBox.QuerySubmitted -= AutoSuggestBoxOnQuerySubmitted;
            autoSuggestBox.SuggestionChosen += AutoSuggestBoxOnSuggestionChosen;
            autoSuggestBox.QuerySubmitted += AutoSuggestBoxOnQuerySubmitted;
        }

        UpdatePaneButtonPlacement();
    }

    /// <summary>
    /// This virtual method is called when this element is detached form a loaded tree.
    /// </summary>
    protected virtual void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // A TitleBar can outlive a NavigationView in the same window. Release the contributed
        // content on detach so the title bar does not retain this control through button handlers.
        ReleaseTitleBarContent();

        ClearRegisteredNavigationItems();

        if (AutoSuggestBox is not null)
        {
            AutoSuggestBox.SuggestionChosen -= AutoSuggestBoxOnSuggestionChosen;
            AutoSuggestBox.QuerySubmitted -= AutoSuggestBoxOnQuerySubmitted;
        }

        // Descendant sources can outlive this control. Release every recursively discovered
        // subscription; OnLoaded rebuilds the semantic model if this instance is reused.
        foreach (INotifyCollectionChanged collection in _navigationCollections)
        {
            collection.CollectionChanged -= OnNavigationItemsSourceCollectionChanged;
        }

        _navigationCollections.Clear();

        foreach (INotifyPropertyChanged item in _navigationItemsWithNotifications)
        {
            item.PropertyChanged -= OnNavigationItemPropertyChanged;
        }

        _navigationItemsWithNotifications.Clear();

        AutoSuggestBoxSymbolButton?.Click -= AutoSuggestBoxSymbolButtonOnClick;
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
        RaiseEvent(new NavigationViewRoutedEventArgs(PaneDisplayModeChangedEvent, this));
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

        SetValue(LeftPaneWidthPropertyKey, IsPaneOpen ? LeftPaneOpenWidth : LeftPaneCompactWidth);
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

        SetValue(LeftPaneWidthPropertyKey, IsPaneOpen ? LeftPaneOpenWidth : LeftPaneCompactWidth);
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
        string automationId,
        NavigationViewButtonPlacement placement,
        NavigationViewButtonRole role
    )
    {
        var button = new NavigationViewButton();

        button.InitializeOwner(this, placement, role);

        AutomationProperties.SetAutomationId(button, automationId);
        AutomationProperties.SetName(
            button,
            role == NavigationViewButtonRole.Back ? "Back" : "Toggle navigation pane"
        );

        // A contributed title-bar button must remain client content. Otherwise the inherited
        // Caption role would turn a click into a native window drag operation.
        TitleBar.SetHitTestRole(button, TitleBarHitTestRole.Client);
        return button;
    }

    /// <summary>
    /// Applies the public back-button policy to both host-specific button instances. The title-bar
    /// instance cannot use a NavigationView ancestor binding because it is contributed to
    /// TitleBar.LeftContent and therefore leaves this control's visual tree.
    /// </summary>
    private void UpdateBackButtonVisibility()
    {
        Visibility visibility = IsBackButtonVisible switch
        {
            NavigationViewBackButtonVisible.Visible => Visibility.Visible,
            NavigationViewBackButtonVisible.Collapsed => Visibility.Collapsed,
            NavigationViewBackButtonVisible.Auto when IsBackEnabled => Visibility.Visible,
            NavigationViewBackButtonVisible.Auto => Visibility.Collapsed,
            _ => Visibility.Collapsed,
        };

        _paneBackButton.SetCurrentValue(VisibilityProperty, visibility);
        _titleBarBackButton.SetCurrentValue(VisibilityProperty, visibility);
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
                _titleBarContentOwner = titleBar;
                _displacedTitleBarLeftContent = titleBar.LeftContent;
                _isTitleBarContentContributionSuppressed = false;
            }
        }
        else if (
            _isTitleBarContentContributed
            && _titleBarContentOwner is not null
            && !ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarBackButton)
            && !ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarToggleButton)
            && _titleBarContentOwner.LeftContent is not null
        )
        {
            // Once an application replaces the contributed value, its newer value owns the slot.
            // Do not reacquire it during an unrelated pane or visibility update.
            _isTitleBarContentContributed = false;
            _isTitleBarContentContributionSuppressed = true;
        }

        if (
            _titleBarContentOwner is not null
            && (
                ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarBackButton)
                || ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarToggleButton)
            )
        )
        {
            _titleBarContentOwner.SetCurrentValue(TitleBar.LeftContentProperty, null);
            _isTitleBarContentContributed = false;
        }

        _paneButtonsHost.Children.Clear();

        var showBack = IsBackButtonVisible != NavigationViewBackButtonVisible.Collapsed;
        var showToggle = IsPaneToggleVisible;
        var titleButton =
            titleBar is null ? null
            : showBack ? _titleBarBackButton
            : showToggle ? _titleBarToggleButton
            : null;

        AddPaneButton(showBack && titleButton is null, _paneBackButton);
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
        // effective value only until the application explicitly replaces it.
        if (titleBar is not null && !_isTitleBarContentContributionSuppressed)
        {
            titleBar.SetCurrentValue(TitleBar.LeftContentProperty, titleButton);
            _isTitleBarContentContributed = true;
        }
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

        if (_isTitleBarContentContributed
            && (ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarBackButton)
                || ReferenceEquals(_titleBarContentOwner.LeftContent, _titleBarToggleButton)
                || _titleBarContentOwner.LeftContent is null))
        {
            _titleBarContentOwner.SetCurrentValue(
                TitleBar.LeftContentProperty,
                _displacedTitleBarLeftContent
            );
        }

        _titleBarContentOwner = null;
        _displacedTitleBarLeftContent = null;
        _isTitleBarContentContributed = false;
        _isTitleBarContentContributionSuppressed = false;
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
        _currentEntry?.Deactivate(this);

        SetSelectedItem(null);
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
        if (!TryGetNavigationEntry(navigationViewItem, out NavigationEntry entry))
        {
            return false;
        }

        OnItemInvoked();
        bool navigated = NavigateInternal(entry);

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
        var roots = new HashSet<NavigationViewItem>(GetEffectiveMenuItems().OfType<NavigationViewItem>());
        roots.UnionWith(GetEffectiveFooterMenuItems().OfType<NavigationViewItem>());
        roots.UnionWith(_registeredItems.Where(static item => item.NavigationViewItemParent is null));

        // The registered set is essential for root data items: their generated NavigationViewItem
        // container is the ContextMenu owner, while the public MenuItems collection still contains
        // only the caller's data object. The HashSet also makes direct roots encountered through
        // both paths close exactly once.
        return roots;
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
        AutoSuggestBoxSuggestionChosenEventArgs args
    )
    {
        if (sender.IsSuggestionListOpen)
        {
            return;
        }

        if (args.SelectedItem is not string selectedSuggestBoxItem)
        {
            return;
        }

        _ = NavigateToMenuItemFromAutoSuggestBox(selectedSuggestBoxItem);
    }

    private void AutoSuggestBoxOnQuerySubmitted(
        AutoSuggestBox sender,
        AutoSuggestBoxQuerySubmittedEventArgs args
    )
    {
        var suggestions = new List<string>();
        var querySplit = args.QueryText.Split(' ');

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

        _ = NavigateToMenuItemFromAutoSuggestBox(element);
    }

    /// <summary>
    /// Rebuilds presentation-independent entries from an item source and its declared descendants.
    /// </summary>
    private void AddItemsToNavigationIndexes(IEnumerable items, bool isMenuElement = true)
    {
        SubscribeToNavigationCollection(items);

        foreach (object? item in items)
        {
            if (item is null)
            {
                continue;
            }

            if (item is not INavigationItem && item is not NavigationViewItem)
            {
                // Arbitrary objects remain valid presentation data, but NavigationView cannot
                // infer page identity or hierarchy from property names. Such items can acquire a
                // realization-scoped fallback entry from ItemContainerStyle bindings. Preserve an
                // existing fallback while its source object is still present; otherwise an
                // unrelated collection notification would invalidate a currently realized item.
                if (_entriesByItem.TryGetValue(item, out NavigationEntry? fallbackEntry))
                {
                    fallbackEntry.IsMenuElement = isMenuElement;
                    _entriesSeenDuringRebuild.Add(item);
                    IndexNavigationEntry(fallbackEntry);
                }

                continue;
            }

            if (!_entriesByItem.TryGetValue(item, out NavigationEntry? entry))
            {
                entry = new NavigationEntry(item, isMenuElement);
                _entriesByItem[item] = entry;
            }
            else
            {
                entry.IsMenuElement = isMenuElement;
            }

            _entriesSeenDuringRebuild.Add(item);

            if (
                item is INotifyPropertyChanged observableItem
                && _navigationItemsWithNotifications.Add(observableItem)
            )
            {
                observableItem.PropertyChanged += OnNavigationItemPropertyChanged;
            }

            if (item is NavigationViewItem directContainer)
            {
                entry.Container = directContainer;
                directContainer.NavigationEntry = entry;
                directContainer.AttachToNavigationViewHost(this);
                RegisterNavigationViewItem(directContainer);
            }

            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                PageKeyIndex[entry.Id] = entry;
            }

            if (!string.IsNullOrWhiteSpace(entry.TargetPageTag))
            {
                PageKeyIndex[entry.TargetPageTag!] = entry;
            }

            if (entry.TargetPageType is { } targetPageType)
            {
                PageTypeIndex[targetPageType] = entry;
            }

            if (entry.Children is { } children)
            {
                AddItemsToNavigationIndexes(children, isMenuElement);
            }
        }
    }

    /// <summary>
    /// Registers the current main and footer menu hierarchies in the page lookup indexes.
    /// </summary>
    protected virtual void AddItemsToNavigationIndexes()
    {
        AddItemsToNavigationIndexes(GetEffectiveMenuItems());
        AddItemsToNavigationIndexes(GetEffectiveFooterMenuItems());
    }

    private void SubscribeToNavigationCollection(IEnumerable items)
    {
        if (
            items is INotifyCollectionChanged observable
            && !ReferenceEquals(items, MenuItems)
            && !ReferenceEquals(items, FooterMenuItems)
            && _navigationCollections.Add(observable)
        )
        {
            observable.CollectionChanged += OnNavigationItemsSourceCollectionChanged;
        }
    }

    internal void NotifyNavigationViewItemLookupChanged(NavigationViewItem item)
    {
        if (item.NavigationEntry is null)
        {
            return;
        }

        RebuildNavigationIndexes();
        UpdateAutoSuggestBoxSuggestions();
    }

    internal void NotifyNavigationViewItemContentChanged(NavigationViewItem item)
    {
        if (item.NavigationEntry is null)
        {
            return;
        }

        UpdateAutoSuggestBoxSuggestions();
    }

    private void RegisterNavigationViewItem(NavigationViewItem item)
    {
        _registeredItems.Add(item);
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
    internal void RegisterGeneratedNavigationItem(NavigationViewItem item, object sourceItem)
    {
        if (!_entriesByItem.TryGetValue(sourceItem, out NavigationEntry? entry))
        {
            entry = new NavigationEntry(sourceItem, true)
            {
                Container = item,
            };
            _entriesByItem[sourceItem] = entry;
        }

        entry.Container = item;
        item.NavigationEntry = entry;
        RegisterNavigationViewItem(item);

        // A style may provide navigation metadata for an otherwise arbitrary data object. This
        // fallback is necessarily realization-scoped; INavigationItem remains the path that allows
        // navigation before layout and through collapsed branches.
        IndexNavigationEntry(entry);

        if (ReferenceEquals(_currentEntry, entry))
        {
            entry.Activate(this);
        }

        UpdateAutoSuggestBoxSuggestions();
    }

    /// <summary>Removes a data-generated container before its generator releases or recycles it.</summary>
    internal void UnregisterGeneratedNavigationItem(NavigationViewItem item)
    {
        _registeredItems.Remove(item);
        if (item.NavigationEntry is { } entry && ReferenceEquals(entry.Container, item))
        {
            entry.Container = null;
        }

        item.NavigationEntry = null;
    }

    private void ClearRegisteredNavigationItems()
    {
        foreach (var item in _registeredItems)
        {
            // Reset notifications do not expose removed items. Close the previously indexed
            // roots before discarding the index so a detached ContextMenu cannot remain open. Limiting
            // recursion to roots avoids walking every descendant subtree repeatedly.
            if (
                item.NavigationViewItemParent is not NavigationViewItem parentItem
                || !_registeredItems.Contains(parentItem)
            )
            {
                item.CloseSubmenuBranch();

                if (!MenuItems.Contains(item) && !FooterMenuItems.Contains(item))
                {
                    item.DetachFromNavigationViewHost(this);
                }
            }

            item.NavigationEntry = null;
        }

        _registeredItems.Clear();
    }

    private void OnNavigationItemsSourceCollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        RebuildNavigationIndexes();
    }

    private void OnNavigationItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        bool allPropertiesChanged = string.IsNullOrEmpty(e.PropertyName);

        if (
            sender is INavigationItem sourceItem
            && _entriesByItem.TryGetValue(sourceItem, out NavigationEntry? changedEntry)
            && changedEntry.Container is { } container
            && (allPropertiesChanged
                || e.PropertyName == nameof(INavigationItem.Children)
                || e.PropertyName == nameof(INavigationItem.Icon))
        )
        {
            NavigationViewList.SynchronizeDataContainer(container, sourceItem);
        }

        if (sender is INavigationItem changedItem
            && _entriesByItem.TryGetValue(changedItem, out NavigationEntry? cacheEntry)
            && (allPropertiesChanged
                || e.PropertyName == nameof(INavigationItem.NavigationCacheMode)
                || e.PropertyName == nameof(INavigationItem.TargetPageType)))
        {
            SynchronizeNavigationEntryCacheState(cacheEntry);
        }

        if (allPropertiesChanged || e.PropertyName == nameof(INavigationItem.Children))
        {
            RebuildNavigationIndexes();
            return;
        }

        PageKeyIndex.Clear();
        PageTypeIndex.Clear();
        foreach (NavigationEntry entry in _entriesByItem.Values)
        {
            IndexNavigationEntry(entry);
        }

        UpdateAutoSuggestBoxSuggestions();
        RefreshCurrentLocation();
    }

    private void RebuildNavigationIndexes()
    {
        foreach (INotifyCollectionChanged collection in _navigationCollections)
        {
            collection.CollectionChanged -= OnNavigationItemsSourceCollectionChanged;
        }

        _navigationCollections.Clear();
        foreach (INotifyPropertyChanged item in _navigationItemsWithNotifications)
        {
            item.PropertyChanged -= OnNavigationItemPropertyChanged;
        }

        _navigationItemsWithNotifications.Clear();
        PageKeyIndex.Clear();
        PageTypeIndex.Clear();
        _entriesSeenDuringRebuild.Clear();
        AddItemsToNavigationIndexes();

        foreach (object removedItem in _entriesByItem.Keys
                     .Where(item => !_entriesSeenDuringRebuild.Contains(item))
                     .ToArray())
        {
            if (_entriesByItem[removedItem].Container is { } container)
            {
                container.CloseSubmenuBranch();
                container.NavigationEntry = null;
                _registeredItems.Remove(container);

                if (container.NavigationViewItemParent is null)
                {
                    container.DetachFromNavigationViewHost(this);
                }
            }

            _entriesByItem.Remove(removedItem);
        }

        IndexUnregisteredNavigationEntries();
        ReconcileNavigationStateAfterIndexRebuild();

        UpdateRootItemState();
        UpdateAutoSuggestBoxSuggestions();
    }

    private void IndexNavigationEntry(NavigationEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.Id))
        {
            PageKeyIndex[entry.Id] = entry;
        }

        if (!string.IsNullOrWhiteSpace(entry.TargetPageTag))
        {
            PageKeyIndex[entry.TargetPageTag!] = entry;
        }

        if (entry.TargetPageType is { } targetPageType)
        {
            PageTypeIndex[targetPageType] = entry;
        }
    }

    protected virtual void AddItemsToAutoSuggestBoxItems()
    {
        foreach (NavigationEntry entry in _entriesByItem.Values)
        {
            if (entry.TargetPageType is not null && !string.IsNullOrWhiteSpace(entry.SearchText))
            {
                _autoSuggestItems.Add(entry.SearchText);
            }
        }
    }

    protected virtual bool NavigateToMenuItemFromAutoSuggestBox(string selectedSuggestItem)
    {
        foreach (NavigationEntry entry in _entriesByItem.Values)
        {
            if (entry.SearchText == selectedSuggestItem && entry.TargetPageType is not null)
            {
                NavigateInternal(entry);

                _ = entry.Container?.FocusHeader();

                return true;
            }
        }

        return false;
    }

    internal bool TryGetNavigationEntry(
        NavigationViewItem container,
        out NavigationEntry entry)
    {
        if (container.NavigationEntry is { } containerEntry)
        {
            entry = containerEntry;
            return true;
        }

        object? sourceItem = container.Header;
        if (sourceItem is null)
        {
            entry = null!;
            return false;
        }

        if (_entriesByItem.TryGetValue(sourceItem, out NavigationEntry? existingEntry))
        {
            entry = existingEntry;
            return true;
        }

        if (ReferenceEquals(sourceItem, container) || container.TargetPageType is not null)
        {
            entry = new NavigationEntry(container, true)
            {
                Container = container,
            };
            container.NavigationEntry = entry;
            _entriesByItem[container] = entry;
            IndexNavigationEntry(entry);
            return true;
        }

        entry = null!;
        return false;
    }

    /// <summary>
    /// Determines whether a directly supplied root container still belongs to the semantic menu.
    /// </summary>
    internal bool ContainsNavigationItemSource(NavigationViewItem item) =>
        _entriesByItem.TryGetValue(item, out NavigationEntry? entry) && entry.IsMenuElement;

    /// <summary>Returns the authoritative main-menu sequence without copying source items.</summary>
    internal IEnumerable GetEffectiveMenuItems() => GetEffectiveItems(MenuItemsSource, MenuItems);

    /// <summary>Returns the authoritative footer-menu sequence without copying source items.</summary>
    internal IEnumerable GetEffectiveFooterMenuItems() =>
        GetEffectiveItems(FooterMenuItemsSource, FooterMenuItems);

    private static IEnumerable GetEffectiveItems(object? source, IList directItems)
    {
        if (source is IEnumerable enumerable and not string)
        {
            return enumerable;
        }

        return source is null ? directItems : new[] { source };
    }

    /// <summary>
    /// Assigns the authoritative sources to the realized root lists. The assignment is separate
    /// from semantic indexing because a NavigationView must support Navigate before ApplyTemplate.
    /// </summary>
    internal void UpdateMenuItemsHosts()
    {
        if (MenuItemsList is not null)
        {
            MenuItemsList.SetCurrentValue(
                ItemsControl.ItemsSourceProperty,
                GetEffectiveMenuItems()
            );
        }

        if (FooterMenuItemsList is not null)
        {
            FooterMenuItemsList.SetCurrentValue(
                ItemsControl.ItemsSourceProperty,
                GetEffectiveFooterMenuItems()
            );
        }
    }

    /// <summary>Updates template-facing root state from the effective, non-copied sources.</summary>
    private void UpdateRootItemState()
    {
        SetValue(HasMenuItemsPropertyKey, GetEffectiveMenuItems().Cast<object>().Any());
        SetValue(HasFooterMenuItemsPropertyKey, GetEffectiveFooterMenuItems().Cast<object>().Any());
        UpdateMenuItemsHosts();
    }
}
