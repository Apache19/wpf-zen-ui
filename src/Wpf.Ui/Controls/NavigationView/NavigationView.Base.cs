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
            new SymbolIcon(SymbolRegular.ArrowLeft48),
            NavigationViewButtonPlacement.Pane
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
            new SymbolIcon(SymbolRegular.LineHorizontal320),
            NavigationViewButtonPlacement.Pane
        );

        _paneToggleButton.Click += OnToggleButtonClick;

        // Title-bar commands use separate control instances. WPF elements can have only one
        // visual parent, and ContentPresenter may defer removing its old child until layout;
        // separate instances therefore make visibility changes safe while the window is live.
        _titleBarBackButton = CreateNavigationButton(
            "NavigationBackButton",
            new SymbolIcon(SymbolRegular.ArrowLeft48),
            NavigationViewButtonPlacement.TitleBar
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
            new SymbolIcon(SymbolRegular.LineHorizontal320),
            NavigationViewButtonPlacement.TitleBar
        );

        _titleBarToggleButton.Click += OnToggleButtonClick;

        _paneTitle = new TextBlock
        {
            FontWeight = FontWeights.Medium
        };

        BindingOperations.SetBinding(
            _paneTitle,
            System.Windows.Controls.TextBlock.TextProperty,
            new Binding(nameof(PaneTitle))
            {
                Source = this
            }
        );

        BackButton   = _paneBackButton;
        ToggleButton = _paneToggleButton;

        Loaded      += OnLoaded;
        Unloaded    += OnUnloaded;
        SizeChanged += OnSizeChanged;

        // Initialize MenuItems collection
        var menuItems = new ObservableCollection<object>();
        menuItems.CollectionChanged += OnMenuItems_CollectionChanged;
        SetValue(MenuItemsPropertyKey, menuItems);

        var footerMenuItems = new ObservableCollection<object>();
        footerMenuItems.CollectionChanged += OnMenuItems_CollectionChanged;
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
    private readonly TextBlock                                          _paneTitle;
    private readonly StackPanel                                         _paneButtonsHost = new();
    private          TitleBar?                                          _titleBarContentOwner;
    private          object?                                            _displacedTitleBarLeftContent;

    protected static void UpdateVisualState(NavigationView navigationView)
    {
        // Skip display modes that don't have multiple states
        if (navigationView.PaneDisplayMode is
            NavigationViewPaneDisplayMode.LeftFluent or
            NavigationViewPaneDisplayMode.Top or
            NavigationViewPaneDisplayMode.Bottom)
        {
            return;
        }

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

        AddItemsToDictionaries();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigationStack.CollectionChanged -= NavigationStackOnCollectionChanged;
        NavigationStack.CollectionChanged += NavigationStackOnCollectionChanged;

        // TODO: Refresh
        UpdateVisualState((NavigationView)sender);
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

        UnregisterNavigationViewItems();

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
            menuItemsSource.CollectionChanged -= OnMenuItemsSource_CollectionChanged;
        }

        if (FooterMenuItemsSource is INotifyCollectionChanged footerMenuItemsSource)
        {
            footerMenuItemsSource.CollectionChanged -= OnFooterMenuItemsSource_CollectionChanged;
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
    private void PrepareForPaneDisplayModeChange(
        NavigationViewPaneDisplayMode oldMode,
        NavigationViewPaneDisplayMode newMode
    )
    {
        // Left and LeftMinimal deliberately share the compact template and its PaneStates group.
        // Keeping the active state avoids interrupting pane animations when only item layout changes.
        if (UsesCompactPaneTemplate(oldMode) && UsesCompactPaneTemplate(newMode))
        {
            return;
        }

        // PaneReset has no storyboard. Entering it makes VisualStateManager remove the clocks from
        // PaneOpen/PaneCompact before StyleHelper.ClearGeneratedSubTree detaches TemplateBindings.
        // Without this step, Width temporarily falls back to Double.NaN while a DoubleAnimation is
        // still evaluating, which causes WPF to throw during the first cross-template mode switch.
        _ = VisualStateManager.GoToState(this, "PaneReset", false);

        // VisualStateManager stops its storyboard, but WPF can retain the animation storage until
        // the next property evaluation. Remove the Width clock synchronously while its template
        // binding still supplies a finite base value; StyleHelper clears that binding immediately
        // after this property callback returns.
        if (Template?.FindName("PaneGrid", this) is FrameworkElement paneGrid)
        {
            paneGrid.BeginAnimation(FrameworkElement.WidthProperty, null);
        }
    }

    private static bool UsesCompactPaneTemplate(NavigationViewPaneDisplayMode mode) =>
        mode is NavigationViewPaneDisplayMode.Left or NavigationViewPaneDisplayMode.LeftMinimal;

    /// <summary>
    /// Creates a navigation-owned command. Commands are intentionally created by
    /// <see cref="NavigationView"/> rather than by a control template because their placement
    /// is selected at runtime and may target <see cref="TitleBar.LeftContent"/>.
    /// </summary>
    /// <param name="automationId">The stable automation identifier shared by both host-specific instances.</param>
    /// <param name="icon">The icon that identifies the navigation command.</param>
    /// <param name="placement">The host in which the button is placed.</param>
    private NavigationViewButton CreateNavigationButton(
        string                        automationId,
        IconElement                   icon,
        NavigationViewButtonPlacement placement)
    {
        var button = new NavigationViewButton
        {
            Icon = icon
        };

        button.InitializeOwner(this, placement);

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

        _paneToggleButton.SetCurrentValue(
            ContentControl.ContentProperty,
            IsPaneOpen && PaneTitle is not null ? _paneTitle : null
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

    internal void OnNavigationViewItemClick(NavigationViewItem navigationViewItem)
    {
        OnItemInvoked();
        NavigateInternal(navigationViewItem);
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

    protected virtual void AddItemsToDictionaries(IEnumerable list)
    {
        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
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

            nvItem.IsMenuElement = true;

            if (nvItem.HasMenuItems)
            {
                AddItemsToDictionaries(nvItem.MenuItems);
            }
        }
    }

    protected virtual void AddItemsToDictionaries()
    {
        AddItemsToDictionaries(MenuItems);
        AddItemsToDictionaries(FooterMenuItems);
    }

    protected virtual void RemoveItemsFromDictionaries(IEnumerable list)
    {
        foreach (NavigationViewItem item in list.OfType<NavigationViewItem>())
        {
            UnregisterNavigationViewItem(item);
        }
    }

    internal void NotifyNavigationViewItemLookupChanged(NavigationViewItem item)
    {
        if (!_registeredItems.Contains(item))
        {
            return;
        }

        RebuildDictionaries();
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
            item.MenuItems is INotifyCollectionChanged menuItems)
        {
            menuItems.CollectionChanged += OnNavigationViewItemMenuItems_CollectionChanged;
        }
    }

    private void UnregisterNavigationViewItem(NavigationViewItem item)
    {
        if (_registeredItems.Remove(item) &&
            item.MenuItems is INotifyCollectionChanged menuItems)
        {
            menuItems.CollectionChanged -= OnNavigationViewItemMenuItems_CollectionChanged;
        }

        PageKeyIndex.Remove(item.Id);
        PageKeyIndex.Remove(item.TargetPageTag);

        if (item.TargetPageType is not null)
        {
            PageTypeIndex.Remove(item.TargetPageType);
        }

        if (item.HasMenuItems)
        {
            RemoveItemsFromDictionaries(item.MenuItems);
        }
    }

    private void UnregisterNavigationViewItems()
    {
        foreach (var item in _registeredItems)
        {
            if (item.MenuItems is INotifyCollectionChanged menuItems)
            {
                menuItems.CollectionChanged -= OnNavigationViewItemMenuItems_CollectionChanged;
            }
        }

        _registeredItems.Clear();

        PageKeyIndex.Clear();
        PageTypeIndex.Clear();
    }

    private void OnNavigationViewItemMenuItems_CollectionChanged(
        object? sender,
        NotifyCollectionChangedEventArgs e)
    {
        OnMenuItems_CollectionChanged(sender, e);
    }

    private void RebuildDictionaries()
    {
        UnregisterNavigationViewItems();
        AddItemsToDictionaries();
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

            if (nvItem.HasMenuItems)
            {
                AddItemsToAutoSuggestBoxItems(nvItem.MenuItems);
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

                nvItem.BringIntoView();
                nvItem.Focus();

                return true;
            }

            if (NavigateToMenuItemFromAutoSuggestBox(nvItem.MenuItems, selectedSuggestItem))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void CloseNavigationViewItemMenus()
    {
        if (Journal.Count <= 0 || IsPaneOpen)
        {
            return;
        }

        DeactivateMenuItems(MenuItems);
        DeactivateMenuItems(FooterMenuItems);

        INavigationViewItem currentItem = PageKeyIndex[Journal[^1]];

        if (currentItem.NavigationViewItemParent is null)
        {
            currentItem.Activate(this);
            return;
        }

        currentItem.Deactivate(this);
        currentItem.NavigationViewItemParent?.Activate(this);
    }

    protected void DeactivateMenuItems(IEnumerable list)
    {
        foreach (var item in list)
        {
            if (item is NavigationViewItem nvItem)
            {
                nvItem.Deactivate(this);
            }
        }
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