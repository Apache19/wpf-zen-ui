// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library */

using System.Collections.ObjectModel;
using System.Diagnostics;
using Wpf.Ui.Abstractions;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines navigation logic and state management for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    // 这个重构比原来的设计清晰太多了。你已经做到了：
    // 
    // 搞对了的
    // 
    // - NavigationTarget — 纯数据结构，断开了导航和 UI 的捆绑
    // - _navigationTargets — 独立的注册表，不依赖菜单按钮
    // - GoBack 走 _journalNavigationTargets，不再绕道 UI 词典
    // - _currentNavigationTarget — 导航引擎终于有自己的"当前位置"概念
    // - ClearNavigationStackForUnmappedPage — 没有对应菜单项的页面优雅处理
    // - SynchronizeCurrentNavigationViewItem — UI 同步逻辑集中在导航之后，单向通知
    // - CloseNavigationViewItemMenus              直接用 NavigationStack，不再通过 Journal 查词典
    // - 嵌套集合订阅追踪 — _nestedMenuItemCollections     解决内存泄露
    // 
    // 还在待办里的
    // 
    // - NavigationTarget.Identifier               用 AssemblyQualifiedName 太长了，日志和调试看着累
    // - AddItemsToDictionaries() 名不副实，它现在是全量重建+同步 UI+清理无效映射
    // - _journalNavigationTargets                 和 _navigationTargets                 两个字典是否有合并的可能？
    // - Navigate(string) 仍然依赖                     _navigationTargets 注册——纯代码导航时 string key 从哪里来？
    // 
    // 不过这些问题比你接手时的烂摊子小多了。结构已经理顺，剩下的都是打磨。

    // todo: 导航目标设计得不太好，导航菜单项关联的是 PageType 和 TargetPageTag(string)，考虑结合这两者当作 Key，并且可以从 Key 直接反推 PageType 来创建实例，简化导航目标的设计
    // 导航服务有一个 bool Navigate(string pageIdOrTargetTag); 方法，允许通过 pageId 或 TargetPageTag 导航，但导航目标的设计并没有很好地支持这一点，导致 Navigate(string pageIdOrTargetTag) 方法需要额外的逻辑来查找对应的导航目标
    // 另一个问题：PageId 从何而来？是导航菜单项的 Id 还是 TargetPageTag？如果是前者，那么 Navigate(string pageIdOrTargetTag) 方法就需要先根据 pageId 查找导航菜单项，再从导航菜单项获取 PageType 来创建实例，这样就比较麻烦了。
    // 我认为导航菜单项包含的id没有意义，PageIdOrTargetTagNavigationViewsDictionary等三个词典存有导航菜单的实例引用，菜单项是被找的目标，它不是key。
    private readonly struct NavigationTarget(Type pageType, NavigationCacheMode cacheMode)
    {
        public string Identifier { get; } =
            pageType.AssemblyQualifiedName ?? pageType.FullName ?? pageType.Name;

        public Type PageType { get; } = pageType;

        public NavigationCacheMode CacheMode { get; } = cacheMode;
    }

    protected List<string> Journal { get; } = new(50);

    private readonly Dictionary<string, NavigationTarget> _journalNavigationTargets = [];

    private readonly Dictionary<string, NavigationTarget> _navigationTargets = [];

    protected ObservableCollection<INavigationViewItem> NavigationStack { get; } = [];

    private readonly NavigationCache _cache = new();

    private readonly Dictionary<INavigationViewItem, List<INavigationViewItem?[]>> _complexNavigationStackHistory = [];

    private IServiceProvider? _serviceProvider;

    private INavigationViewPageProvider? _pageService;

    private NavigationTarget? _currentNavigationTarget;

    /// <inheritdoc />
    public bool CanGoBack => Journal.Count > 1;

    /// <inheritdoc />
    public void SetPageProviderService(INavigationViewPageProvider navigationViewPageProvider)
    {
        _pageService = navigationViewPageProvider;
    }

    /// <inheritdoc />
    public void SetServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <inheritdoc />
    public virtual bool Navigate(Type pageType, object? dataContext = null)
    {
        PageTypeNavigationViewsDictionary.TryGetValue(pageType, out var navigationViewItem);

        return NavigateInternal(CreateNavigationTarget(pageType, navigationViewItem), navigationViewItem, dataContext);
    }

    /// <inheritdoc />
    public virtual bool Navigate(string pageIdOrTargetTag, object? dataContext = null)
    {
        if (!_navigationTargets.TryGetValue(pageIdOrTargetTag, out var navigationTarget))
        {
            return false;
        }

        var navigationViewItem = FindNavigationViewItem(pageIdOrTargetTag, navigationTarget.PageType);

        return NavigateInternal(navigationTarget, navigationViewItem, dataContext);
    }

    /// <inheritdoc />
    public virtual bool NavigateWithHierarchy(Type pageType, object? dataContext = null)
    {
        PageTypeNavigationViewsDictionary.TryGetValue(pageType, out var navigationViewItem);

        return NavigateInternal(
            CreateNavigationTarget(pageType, navigationViewItem),
            navigationViewItem,
            dataContext,
            true
        );
    }

    /// <inheritdoc />
    public virtual bool ReplaceContent(Type? pageTypeToEmbed)
    {
        if (pageTypeToEmbed == null)
        {
            return false;
        }

        if (_serviceProvider != null)
        {
            UpdateContent(_serviceProvider.GetService(pageTypeToEmbed));

            return true;
        }

        if (_pageService == null)
        {
            return false;
        }

        UpdateContent(_pageService.GetPage(pageTypeToEmbed));

        return true;
    }

    /// <inheritdoc />
    public virtual bool ReplaceContent(UIElement pageInstanceToEmbed, object? dataContext = null)
    {
        UpdateContent(pageInstanceToEmbed, dataContext);

        return true;
    }

    /// <inheritdoc />
    public virtual bool GoForward()
    {
        throw new NotImplementedException();
    }

    /// <inheritdoc />
    public virtual bool GoBack()
    {
        if (Journal.Count <= 1)
        {
            return false;
        }

        if (!_journalNavigationTargets.TryGetValue(Journal[^2], out var navigationTarget))
        {
            return false;
        }

        PageTypeNavigationViewsDictionary.TryGetValue(
            navigationTarget.PageType,
            out var navigationViewItem
        );

        OnBackRequested();
        return NavigateInternal(navigationTarget, navigationViewItem, null, false, true);
    }

    /// <inheritdoc />
    public virtual void ClearJournal()
    {
        Journal.Clear();
        _journalNavigationTargets.Clear();
        _currentNavigationTarget = null;
        _complexNavigationStackHistory.Clear();
        SetCurrentValue(IsBackEnabledProperty, false);
    }

    private static NavigationTarget CreateNavigationTarget(
        Type pageType,
        INavigationViewItem? navigationViewItem
    )
    {
        return new NavigationTarget(
            pageType,
            navigationViewItem?.NavigationCacheMode ?? NavigationCacheMode.Disabled
        );
    }

    private INavigationViewItem? FindNavigationViewItem(string key, Type pageType)
    {
        if (
            PageIdOrTargetTagNavigationViewsDictionary.TryGetValue(key, out var navigationViewItem)
            && navigationViewItem.TargetPageType == pageType
        )
        {
            return navigationViewItem;
        }

        PageTypeNavigationViewsDictionary.TryGetValue(pageType, out navigationViewItem);
        return navigationViewItem;
    }

    private void RegisterNavigationTarget(INavigationViewItem navigationViewItem)
    {
        if (navigationViewItem.TargetPageType is not { } pageType)
        {
            return;
        }

        var navigationTarget = CreateNavigationTarget(pageType, navigationViewItem);
        _navigationTargets[navigationViewItem.Id] = navigationTarget;

        if (
            !string.IsNullOrWhiteSpace(navigationViewItem.TargetPageTag)
            && PageIdOrTargetTagNavigationViewsDictionary.TryGetValue(
                navigationViewItem.TargetPageTag,
                out var registeredNavigationViewItem
            )
            && ReferenceEquals(registeredNavigationViewItem, navigationViewItem)
        )
        {
            _navigationTargets[navigationViewItem.TargetPageTag] = navigationTarget;
        }
    }

    private void ClearNavigationTargets() => _navigationTargets.Clear();

    private void SynchronizeCurrentNavigationViewItem()
    {
        if (_currentNavigationTarget is not { } navigationTarget)
        {
            return;
        }

        PageTypeNavigationViewsDictionary.TryGetValue(
            navigationTarget.PageType,
            out var navigationViewItem
        );

        if (navigationViewItem is null)
        {
            ClearNavigationStackForUnmappedPage();
            return;
        }

        if (NavigationStack.Contains(navigationViewItem))
        {
            return;
        }

        ClearNavigationStackForUnmappedPage();
        AddToNavigationStack(navigationViewItem, false, false);
        SelectedItem = NavigationStack[0];
        OnSelectionChanged();

        if (NavigationViewContentPresenter?.Content is FrameworkElement page)
        {
            ApplyAttachedProperties(navigationViewItem, page);
        }
    }

    private bool NavigateInternal(
        NavigationTarget navigationTarget,
        INavigationViewItem? viewItem,
        object? dataContext = null,
        bool addToNavigationStack = false,
        bool isBackwardsNavigated = false
    )
    {
        if (
            _currentNavigationTarget is { } currentNavigationTarget
            && currentNavigationTarget.PageType == navigationTarget.PageType
        )
        {
            return false;
        }

        var pageInstance = GetNavigationTargetInstance(navigationTarget);

        if (OnNavigating(pageInstance))
        {
            Debug.WriteLineIf(EnableDebugMessages, "Navigation canceled");

            return false;
        }

        Debug.WriteLineIf(
            EnableDebugMessages,
            $"DEBUG | {navigationTarget.PageType} | NAVIGATED"
        );

        OnNavigated(pageInstance);

        // Set up the association before setting DataContext
        if (pageInstance is FrameworkElement frameworkElement)
        {
            // Set NavigationParent to allow HeaderContent property changes to find this NavigationView
            frameworkElement.SetValue(NavigationParentProperty, this);
        }

        // Set DataContext first so bindings can resolve
        UpdateContent(pageInstance, dataContext);

        // Apply properties after DataContext is set
        if (viewItem is not null)
        {
            ApplyAttachedProperties(viewItem, pageInstance);
            AddToNavigationStack(viewItem, addToNavigationStack, isBackwardsNavigated);
        }
        else
        {
            ClearNavigationStackForUnmappedPage();
        }

        AddToJournal(navigationTarget, isBackwardsNavigated);
        _currentNavigationTarget = navigationTarget;

        if (
            NavigationStack.Count > 0
            && SelectedItem != NavigationStack[0]
            && NavigationStack[0].IsMenuElement
        )
        {
            SelectedItem = NavigationStack[0];
            OnSelectionChanged();
        }

        return true;
    }

    private void AddToJournal(NavigationTarget navigationTarget, bool isBackwardsNavigated)
    {
        if (isBackwardsNavigated)
        {
            Journal.RemoveAt(Journal.Count - 1);
        }
        else
        {
            _journalNavigationTargets[navigationTarget.Identifier] = navigationTarget;
            Journal.Add(navigationTarget.Identifier);
        }

        SetCurrentValue(IsBackEnabledProperty, CanGoBack);

        Debug.WriteLineIf(EnableDebugMessages, $"JOURNAL INDEX {Journal.Count - 1}");

        if (Journal.Count > 0)
        {
            Debug.WriteLineIf(
                EnableDebugMessages,
                $"JOURNAL LAST ELEMENT {Journal[^1]}"
            );
        }
    }

    private object GetNavigationTargetInstance(NavigationTarget navigationTarget)
    {
        if (_serviceProvider is not null)
        {
            return _serviceProvider.GetService(navigationTarget.PageType)
                ?? throw new InvalidOperationException(
                       $"{nameof(_serviceProvider)}.{nameof(_serviceProvider.GetService)} returned null for type {navigationTarget.PageType}."
                   );
        }

        if (_pageService is not null)
        {
            return _pageService.GetPage(navigationTarget.PageType)
                ?? throw new InvalidOperationException(
                       $"{nameof(_pageService)}.{nameof(_pageService.GetPage)} returned null for type {navigationTarget.PageType}."
                   );
        }

        return _cache.Remember(
                   navigationTarget.PageType,
                   navigationTarget.CacheMode,
                   ComputeCachedNavigationInstance
               )
            ?? throw new InvalidOperationException(
                   $"Unable to get or create instance of {navigationTarget.PageType} from cache."
               );

        object? ComputeCachedNavigationInstance() => GetPageInstanceFromCache(navigationTarget.PageType);
    }

    private object? GetPageInstanceFromCache(Type? targetPageType)
    {
        if (targetPageType is null)
        {
            return null;
        }

        if (_serviceProvider is not null)
        {
            Debug.WriteLineIf(
                EnableDebugMessages,
                $"Getting {targetPageType} from cache using IServiceProvider."
            );

            return _serviceProvider.GetService(targetPageType)
                ?? throw new InvalidOperationException(
                       $"{nameof(_serviceProvider.GetService)} returned null"
                   );
        }

        if (_pageService is not null)
        {
            Debug.WriteLineIf(
                EnableDebugMessages,
                $"Getting {targetPageType} from cache using INavigationViewPageProvider."
            );

            return _pageService.GetPage(targetPageType)
                ?? throw new InvalidOperationException($"{nameof(_pageService.GetPage)} returned null");
        }

        Debug.WriteLineIf(
            EnableDebugMessages,
            $"Getting {targetPageType} from cache using reflection."
        );

        return NavigationViewActivator.CreateInstance(targetPageType)
            ?? throw new InvalidOperationException("Failed to create instance of the page");
    }

    private void ApplyAttachedProperties(INavigationViewItem viewItem, object pageInstance)
    {
        if (pageInstance is FrameworkElement frameworkElement)
        {
            // Store the association between page and navigation item
            PageToNavigationItemDictionary[frameworkElement] = viewItem;

            // Apply HeaderContent if already available
            if (GetHeaderContent(frameworkElement) is { } headerContent)
            {
                viewItem.Content = headerContent;
                UpdateBreadcrumbContents();
            }
        }
    }

    internal void NotifyHeaderContentChanged(FrameworkElement page, object? headerContent)
    {
        if (PageToNavigationItemDictionary.TryGetValue(page, out INavigationViewItem? navItem))
        {
            navItem.Content = headerContent;
            UpdateBreadcrumbContents();
        }
    }

    private void UpdateContent(object? content, object? dataContext = null)
    {
        if (dataContext is not null && content is FrameworkElement frameworkViewContent)
        {
            frameworkViewContent.DataContext = dataContext;
        }

        _ = NavigationViewContentPresenter.Navigate(content);
    }

    private void OnNavigationViewContentPresenterNavigated(
        object sender,
        System.Windows.Navigation.NavigationEventArgs e
    )
    {
        if (sender is not System.Windows.Controls.Frame frame)
        {
            return;
        }

        _ = frame.RemoveBackEntry();

        /*var replaced = 1;
        ((NavigationViewContentPresenter)sender).JournalOwnership =*/
    }

    private void AddToNavigationStack(
        INavigationViewItem viewItem,
        bool addToNavigationStack,
        bool isBackwardsNavigated
    )
    {
        if (isBackwardsNavigated)
        {
            RecreateNavigationStackFromHistory(viewItem);
        }

        if (addToNavigationStack && !NavigationStack.Contains(viewItem))
        {
            viewItem.Activate(this);
            NavigationStack.Add(viewItem);
        }

        if (!addToNavigationStack)
        {
            UpdateCurrentNavigationStackItem(viewItem);
        }

        ClearNavigationStack(viewItem);
    }

    private void ClearNavigationStackForUnmappedPage()
    {
        foreach (var navigationViewItem in NavigationStack.Reverse())
        {
            navigationViewItem.Deactivate(this);
        }

        NavigationStack.Clear();

        if (SelectedItem is not null)
        {
            SelectedItem = null;
            OnSelectionChanged();
        }
    }

    private void UpdateCurrentNavigationStackItem(INavigationViewItem viewItem)
    {
        if (NavigationStack.Contains(viewItem))
        {
            return;
        }

        if (NavigationStack.Count > 1)
        {
            AddToNavigationStackHistory(viewItem);
        }

        if (NavigationStack.Count == 0)
        {
            viewItem.Activate(this);
            NavigationStack.Add(viewItem);
        }
        else
        {
            ReplaceThirstElementInNavigationStack(viewItem);
        }

        ClearNavigationStack(1);
    }

    private void RecreateNavigationStackFromHistory(INavigationViewItem item)
    {
        List<INavigationViewItem?[]>? historyList;
        if (!_complexNavigationStackHistory.TryGetValue(item, out historyList) || historyList.Count == 0)
        {
            return;
        }

        INavigationViewItem?[] latestHistory = historyList[^1];
        var                    startIndex    = 0;

        if (latestHistory[0]!.IsMenuElement)
        {
            startIndex = 1;
            ReplaceThirstElementInNavigationStack(latestHistory[0]!);
        }

        for (int i = startIndex; i < latestHistory.Length; i++)
        {
            if (latestHistory[i] is null)
            {
                break;
            }

            AddToNavigationStack(latestHistory[i]!, true, false);
        }

        historyList.Remove(latestHistory);
        /*if (historyList.Count == 0)
            _complexNavigationStackHistory.Remove(item);
        */

        #if NET6_0_OR_GREATER
        System.Buffers.ArrayPool<INavigationViewItem>.Shared.Return(latestHistory!, true);
        #endif

        AddToNavigationStack(item, true, false);
    }

    private void AddToNavigationStackHistory(INavigationViewItem viewItem)
    {
        INavigationViewItem lastItem   = NavigationStack[^1];
        var                 startIndex = NavigationStack.IndexOf(viewItem);

        if (startIndex < 0)
        {
            startIndex = 0;
        }

        List<INavigationViewItem?[]>? historyList;
        if (!_complexNavigationStackHistory.TryGetValue(lastItem, out historyList))
        {
            historyList = new List<INavigationViewItem?[]>(5);
            _complexNavigationStackHistory.Add(lastItem, historyList);
        }

        int arrayLength = NavigationStack.Count - 1 - startIndex;

        INavigationViewItem[] array;

        // OPTIMIZATION: Initializing an array every time well... not an ideal
        #if NET6_0_OR_GREATER
        array = System.Buffers.ArrayPool<INavigationViewItem>.Shared.Rent(arrayLength);
        #else
        array = new INavigationViewItem[arrayLength];
        #endif

        historyList.Add(array);

        INavigationViewItem?[] latestHistory = historyList[^1];
        int                    i             = 0;

        for (int j = startIndex; j < NavigationStack.Count - 1; j++)
        {
            latestHistory[i] = NavigationStack[j];
            i++;
        }
    }

    private void ClearNavigationStack(int navigationStackItemIndex)
    {
        var navigationStackCount = NavigationStack.Count;
        var length               = navigationStackCount - navigationStackItemIndex;

        if (length == 0)
        {
            return;
        }

        for (int j = navigationStackCount - 1; j >= navigationStackCount - length; j--)
        {
            _ = NavigationStack.Remove(NavigationStack[j]);
        }
    }

    private void ClearNavigationStack(INavigationViewItem item)
    {
        var navigationStackCount = NavigationStack.Count;
        if (navigationStackCount <= 1)
        {
            return;
        }

        var index = NavigationStack.IndexOf(item);
        if (index >= navigationStackCount - 1)
        {
            return;
        }

        AddToNavigationStackHistory(item);
        ClearNavigationStack(++index);
    }

    private void ReplaceThirstElementInNavigationStack(INavigationViewItem newItem)
    {
        NavigationStack[0].Deactivate(this);
        NavigationStack[0] = newItem;
        NavigationStack[0].Activate(this);
    }
}
