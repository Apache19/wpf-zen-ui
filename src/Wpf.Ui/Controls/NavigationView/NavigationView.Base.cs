// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.navigationview?view=winrt-22621 */

using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a container that enables navigation of app content. It has a header, a view for the main content, and a menu pane for navigation commands.
/// </summary>
public partial class NavigationView : System.Windows.Controls.Control, INavigationView
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

    /// <summary>
    /// Gets a dictionary that maps a string identifier (either <see cref="NavigationViewItem.Id"/> or <see cref="NavigationViewItem.TargetPageTag"/>)
    /// to the corresponding menu item currently present in the navigation tree.
    /// </summary>
    /// <remarks>This dictionary is rebuilt whenever a menu collection changes.</remarks>
    protected Dictionary<string, INavigationViewItem> PageIdOrTargetTagNavigationViewsDictionary { get; } = [];

    /// <summary>
    /// Gets a dictionary that maps a <see cref="Type"/> to the corresponding menu item currently present in the navigation tree.
    /// </summary>
    /// <remarks>This dictionary is rebuilt whenever a menu collection changes.</remarks>
    protected Dictionary<Type, INavigationViewItem> PageTypeNavigationViewsDictionary { get; } = [];

    /// <summary>
    /// Gets a dictionary that maps a rendered page <see cref="FrameworkElement"/> instance back to its originating
    /// <see cref="INavigationViewItem"/>. Used to propagate <c>HeaderContent</c> changes from
    /// the page to the navigation item for breadcrumb updates.
    /// </summary>
    protected Dictionary<FrameworkElement, INavigationViewItem> PageToNavigationItemDictionary { get; } = [];

    private readonly HashSet<INotifyCollectionChanged> _nestedMenuItemCollections = [];

    private readonly ObservableCollection<string>                       _autoSuggestBoxItems = [];
    private readonly ObservableCollection<NavigationViewBreadcrumbItem> _breadcrumbBarItems  = [];

    private static readonly Thickness TitleBarPaneOpenMarginDefault    = new(35, 0, 0, 0);
    private static readonly Thickness TitleBarPaneCompactMarginDefault = new(35, 0, 0, 0);
    private static readonly Thickness AutoSuggestBoxMarginDefault      = new(8, 0, 8, 0);
    private static readonly Thickness FrameMarginDefault               = new(0, 50, 0, 0);

    protected static void UpdateVisualState(NavigationView navigationView)
    {
        // Skip display modes that don't have multiple states
        if (navigationView.PaneDisplayMode is
            NavigationViewPaneDisplayMode.LeftFluent
         or NavigationViewPaneDisplayMode.Top
         or NavigationViewPaneDisplayMode.Bottom)
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
        // TODO: Refresh
        UpdateVisualState((NavigationView)sender);
    }

    /// <summary>
    /// This virtual method is called when this element is detached form a loaded tree.
    /// </summary>
    protected virtual void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Loaded      -= OnLoaded;
        Unloaded    -= OnUnloaded;
        SizeChanged -= OnSizeChanged;

        NavigationStack.CollectionChanged -= NavigationStackOnCollectionChanged;

        // Final cleanup — the control is being detached, release all dictionary references.
        PageIdOrTargetTagNavigationViewsDictionary.Clear();
        PageTypeNavigationViewsDictionary.Clear();
        PageToNavigationItemDictionary.Clear();
        ClearNestedMenuItemCollectionSubscriptions();
        ClearNavigationTargets();

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

        ToggleButton?.Click -= OnToggleButtonClick;

        BackButton?.Click -= OnToggleButtonClick;

        AutoSuggestBoxSymbolButton?.Click -= AutoSuggestBoxSymbolButtonOnClick;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        // Back button
        if (e.ChangedButton is MouseButton.XButton1)
        {
            _ = GoBack();
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
        _ = GoBack();
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
        _ = Dispatcher.BeginInvoke(
            () => AutoSuggestBox?.Focus(),
            System.Windows.Threading.DispatcherPriority.Input
        );
    }

    /// <summary>
    /// This virtual method is called when <see cref="PaneDisplayMode"/> is changed.
    /// </summary>
    protected virtual void OnPaneDisplayModeChanged()
    {
        switch (PaneDisplayMode)
        {
            case NavigationViewPaneDisplayMode.LeftFluent:
                SetCurrentValue(IsBackButtonVisibleProperty, NavigationViewBackButtonVisible.Collapsed);
                SetCurrentValue(IsPaneToggleVisibleProperty, false);
                break;
        }
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

        _ = NavigateInternal(
            CreateNavigationTarget(navigationViewItem.TargetPageType!, navigationViewItem),
            navigationViewItem
        );
    }

    protected virtual void BreadcrumbBarOnItemClicked(BreadcrumbBar sender, BreadcrumbBarItemClickedEventArgs e)
    {
        var item = (NavigationViewBreadcrumbItem)e.Item;
        _ = Navigate(item.PageId);
    }

    private void UpdateAutoSuggestBoxSuggestions()
    {
        if (AutoSuggestBox == null)
        {
            return;
        }

        _autoSuggestBoxItems.Clear();

        AddItemsToAutoSuggestBoxItems();
    }

    /// <summary>
    /// Navigate to the page after its name is selected in <see cref="AutoSuggestBox"/>.
    /// </summary>
    private void AutoSuggestBoxOnSuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
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

        _ = NavigateToMenuItemFromAutoSuggestBox(FooterMenuItems, selectedSuggestBoxItem);
    }

    private void AutoSuggestBoxOnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        var suggestions = new List<string>();
        var querySplit  = args.QueryText.Split(' ');

        foreach (var item in _autoSuggestBoxItems)
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

        _ = NavigateToMenuItemFromAutoSuggestBox(FooterMenuItems, element);
    }

    /// <summary>
    /// Registers navigation items from the given collection into the current UI lookup dictionaries.
    /// </summary>
    /// <param name="list">A collection potentially containing <see cref="NavigationViewItem"/> instances.</param>
    protected virtual void AddItemsToDictionaries(IEnumerable list)
    {
        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
            nvItem.SetValue(NavigationParentProperty, this);

            if (!PageIdOrTargetTagNavigationViewsDictionary.ContainsKey(nvItem.Id))
            {
                PageIdOrTargetTagNavigationViewsDictionary.Add(
                    nvItem.Id,
                    nvItem
                );
            }

            if (!string.IsNullOrWhiteSpace(nvItem.TargetPageTag) &&
                !PageIdOrTargetTagNavigationViewsDictionary.ContainsKey(nvItem.TargetPageTag))
            {
                PageIdOrTargetTagNavigationViewsDictionary.Add(
                    nvItem.TargetPageTag,
                    nvItem
                );
            }

            if (nvItem.TargetPageType != null &&
                !PageTypeNavigationViewsDictionary.ContainsKey(nvItem.TargetPageType))
            {
                PageTypeNavigationViewsDictionary.Add(
                    nvItem.TargetPageType,
                    nvItem
                );
            }

            nvItem.IsMenuElement = true;

            RegisterNavigationTarget(nvItem);

            if (nvItem.MenuItems is INotifyCollectionChanged nestedCollection &&
                _nestedMenuItemCollections.Add(nestedCollection))
            {
                nestedCollection.CollectionChanged += OnMenuItems_CollectionChanged;
            }

            if (nvItem.MenuItems.Count > 0)
            {
                foreach (var childItem in nvItem.MenuItems.OfType<INavigationViewItem>())
                {
                    childItem.NavigationViewItemParent = nvItem;
                }

                AddItemsToDictionaries(nvItem.MenuItems);
            }
        }
    }

    /// <summary>
    /// Rebuilds the menu item lookup dictionaries from <see cref="MenuItems"/> and
    /// <see cref="FooterMenuItems"/>.
    /// </summary>
    // todo: 此方法需要重命名以反映其功能
    protected virtual void AddItemsToDictionaries()
    {
        ClearNestedMenuItemCollectionSubscriptions();

        foreach (var nvItem in PageIdOrTargetTagNavigationViewsDictionary.Values.Distinct())
        {
            nvItem.IsMenuElement            = false;
            nvItem.NavigationViewItemParent = null;

            if (nvItem is DependencyObject dependencyObject)
            {
                dependencyObject.ClearValue(NavigationParentProperty);
            }
        }

        PageIdOrTargetTagNavigationViewsDictionary.Clear();
        PageTypeNavigationViewsDictionary.Clear();

        foreach (var nvItem in MenuItems.OfType<INavigationViewItem>())
        {
            nvItem.NavigationViewItemParent = null;
        }

        foreach (var nvItem in FooterMenuItems.OfType<INavigationViewItem>())
        {
            nvItem.NavigationViewItemParent = null;
        }

        AddItemsToDictionaries(MenuItems);
        AddItemsToDictionaries(FooterMenuItems);

        var currentNavigationItems = new HashSet<INavigationViewItem>(
            PageTypeNavigationViewsDictionary.Values
        );

        var pagesToRemove = PageToNavigationItemDictionary
           .Where(entry => !currentNavigationItems.Contains(entry.Value))
           .Select(entry => entry.Key)
           .ToList();

        foreach (var page in pagesToRemove)
        {
            PageToNavigationItemDictionary.Remove(page);
        }

        SynchronizeCurrentNavigationViewItem();
    }

    internal void RefreshMenuItemDictionaries() => AddItemsToDictionaries();

    private void ClearNestedMenuItemCollectionSubscriptions()
    {
        foreach (var collection in _nestedMenuItemCollections)
        {
            collection.CollectionChanged -= OnMenuItems_CollectionChanged;
        }

        _nestedMenuItemCollections.Clear();
    }

    protected virtual void AddItemsToAutoSuggestBoxItems(IEnumerable list)
    {
        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
            if (nvItem is { Content: string content, TargetPageType: not null } &&
                !string.IsNullOrWhiteSpace(content))
            {
                _autoSuggestBoxItems.Add(content);
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

    protected virtual bool NavigateToMenuItemFromAutoSuggestBox(IEnumerable list, string selectedSuggestBoxItem)
    {
        // todo: 导航菜单项的 Content 可能包含快捷键提示符（AccessText），需调查自动建议框是否原封不动地收集了 Content 字符串。
        //       如果自动建议框原封不动地收集了 Content 字符串，可能无法匹配用户输入的字符串（因为用户输入的字符串不包含快捷键提示符），
        //       需要对 Content 字符串进行预处理（去除快捷键提示符）后再添加到自动建议框的建议列表中。
        //       所以此处也需要对 Content 字符串进行预处理后再进行比较。

        foreach (NavigationViewItem nvItem in list.OfType<NavigationViewItem>())
        {
            if (nvItem.Content is string content && content == selectedSuggestBoxItem)
            {
                _ = NavigateInternal(
                    CreateNavigationTarget(nvItem.TargetPageType!, nvItem),
                    nvItem
                );
                nvItem.BringIntoView();
                _ = nvItem.Focus(); // TODO: Element or content?

                return true;
            }

            if (NavigateToMenuItemFromAutoSuggestBox(nvItem.MenuItems, selectedSuggestBoxItem))
            {
                return true;
            }
        }

        return false;
    }

    protected virtual void CloseNavigationViewItemMenus()
    {
        if (NavigationStack.Count == 0 || IsPaneOpen)
        {
            return;
        }

        DeactivateMenuItems(MenuItems);
        DeactivateMenuItems(FooterMenuItems);

        if (NavigationStack.Count == 0)
        {
            return;
        }

        var currentItem = NavigationStack[^1];

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
            {
                _breadcrumbBarItems.Add(
                    new NavigationViewBreadcrumbItem((INavigationViewItem)e.NewItems![0]!)
                );

                break;
            }

            case NotifyCollectionChangedAction.Remove:
                _breadcrumbBarItems.RemoveAt(e.OldStartingIndex);
                break;

            case NotifyCollectionChangedAction.Replace:
            {
                _breadcrumbBarItems[0] = new NavigationViewBreadcrumbItem(
                    (INavigationViewItem)e.NewItems![0]!
                );
                break;
            }

            case NotifyCollectionChangedAction.Move:
                break;

            case NotifyCollectionChangedAction.Reset:
                _breadcrumbBarItems.Clear();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(e), e.Action, $"Unsupported action: {e.Action}");
        }

        UpdateBreadcrumbContents();
    }

    private void UpdateBreadcrumbContents()
    {
        foreach (var breadcrumbItem in _breadcrumbBarItems)
        {
            breadcrumbItem.UpdateFromSource();
        }
    }
}