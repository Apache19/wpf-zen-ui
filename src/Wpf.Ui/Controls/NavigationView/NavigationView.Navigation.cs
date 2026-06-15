// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

/* Based on Windows UI Library */

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Navigation;
using Wpf.Ui.Abstractions;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <content>
/// Defines navigation logic and state management for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    private readonly record struct NavigationTarget(
        NavigationKey Key,
        NavigationCacheMode CacheMode
    );

    private enum JournalNavigationDirection
    {
        New,
        Backward,
        Forward,
    }

    protected List<NavigationKey> Journal { get; } = new(50);

    private readonly List<NavigationKey> _forwardJournal = new(10);

    protected ObservableCollection<INavigationViewItem> NavigationStack { get; } = [];

    private readonly NavigationCache _cache = new();

    private readonly Dictionary<INavigationViewItem, List<INavigationViewItem?[]>> _complexNavigationStackHistory = [];

    private IServiceProvider? _serviceProvider;

    private INavigationViewPageProvider? _pageService;

    private NavigationTarget? _currentNavigationTarget;

    /// <inheritdoc />
    public bool CanGoBack => Journal.Count > 1;

    /// <inheritdoc />
    public void SetPageProviderService(INavigationViewPageProvider pageProvider)
    {
        _pageService = pageProvider;
    }

    /// <inheritdoc />
    public void SetServiceProvider(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public virtual bool Navigate(Type pageType, object? dataContext = null)
    {
        return Navigate(new NavigationKey(pageType), dataContext);
    }

    /// <inheritdoc />
    public virtual bool Navigate(NavigationKey navigationKey, object? dataContext = null)
    {
        NavigationItems.TryGetValue(navigationKey, out var navigationViewItem);
        return NavigateInternal(
            CreateNavigationTarget(navigationKey, navigationViewItem),
            navigationViewItem,
            dataContext
        );
    }

    /// <inheritdoc />
    public virtual bool NavigateWithHierarchy(Type pageType, object? dataContext = null)
    {
        return NavigateWithHierarchy(new NavigationKey(pageType), dataContext);
    }

    /// <inheritdoc />
    public virtual bool NavigateWithHierarchy(NavigationKey navKey, object? dataContext = null)
    {
        NavigationItems.TryGetValue(navKey, out var nvItem);

        return NavigateInternal(
            CreateNavigationTarget(navKey, nvItem),
            nvItem,
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
        if (_forwardJournal.Count == 0)
        {
            return false;
        }

        var navKey = _forwardJournal[^1];
        NavigationItems.TryGetValue(navKey, out var nvItem);

        return NavigateInternal(
            CreateNavigationTarget(navKey, nvItem),
            nvItem,
            direction: JournalNavigationDirection.Forward
        );
    }

    /// <inheritdoc />
    public virtual bool GoBack()
    {
        if (Journal.Count <= 1)
        {
            return false;
        }

        var navKey = Journal[^2];
        NavigationItems.TryGetValue(navKey, out var nvItem);

        OnBackRequested();

        return NavigateInternal(
            CreateNavigationTarget(navKey, nvItem),
            nvItem,
            direction: JournalNavigationDirection.Backward
        );
    }

    /// <inheritdoc />
    public virtual void ClearJournal()
    {
        Journal.Clear();
        _forwardJournal.Clear();
        _currentNavigationTarget = null;
        _complexNavigationStackHistory.Clear();
        SetCurrentValue(IsBackEnabledProperty, false);
    }

    /// <summary>
    /// Removes all cached page instances from the NavigationView.
    /// </summary>
    public virtual void ClearCache()
    {
        _cache.Clear();
    }

    internal void SetCacheEntryMode(INavigationViewItem nvItem, NavigationCacheMode cacheMode)
    {
        if (GetNavigationKey(nvItem) is { } navKey &&
            NavigationItems.TryGetValue(navKey, out var registeredItem) &&
            ReferenceEquals(registeredItem, nvItem))
        {
            _cache.SetMode(navKey, cacheMode);
        }
    }

    private static NavigationKey? GetNavigationKey(INavigationViewItem nvItem)
    {
        return nvItem.TargetPageType is { } type
            ? new NavigationKey(type, nvItem.TargetPageVariant)
            : null;
    }

    private static NavigationTarget CreateNavigationTarget(NavigationKey navKey, INavigationViewItem? nvItem)
    {
        return new NavigationTarget(
            navKey,
            nvItem?.NavigationCacheMode ?? NavigationCacheMode.Disabled
        );
    }

    private void SynchronizeCurrentNavigationViewItem()
    {
        if (_currentNavigationTarget is not { } navigationTarget)
        {
            return;
        }

        NavigationItems.TryGetValue(navigationTarget.Key, out var nvItem);

        if (nvItem is null)
        {
            ClearNavigationStackForUnmappedPage();
            return;
        }

        if (NavigationStack.Contains(nvItem))
        {
            return;
        }

        ClearNavigationStackForUnmappedPage();
        AddToNavigationStack(nvItem, false, false);
        SelectedItem = NavigationStack[0];
        OnSelectionChanged();

        if (NavigationViewContentPresenter?.Content is FrameworkElement page)
        {
            ApplyAttachedProperties(nvItem, page);
        }
    }

    private bool NavigateInternal(
        NavigationTarget target,
        INavigationViewItem? nvItem,
        object? dataContext = null,
        bool addToNavigationStack = false,
        JournalNavigationDirection direction = JournalNavigationDirection.New)
    {
        var isJournalNavigation = direction != JournalNavigationDirection.New;
        var isSameKey = _currentNavigationTarget is { } currTarget && currTarget.Key == target.Key;
        var isSameTarget = _currentNavigationTarget == target;

        if (!isJournalNavigation && isSameTarget && dataContext is null)
        {
            return false;
        }

        var pageInstance = GetNavigationTargetInstance(target);

        if (OnNavigating(pageInstance))
        {
            Debug.WriteLineIf(EnableDebugMessages, "Navigation canceled");

            return false;
        }

        Debug.WriteLineIf(EnableDebugMessages, $"DEBUG | {target.Key} | NAVIGATED");

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
        if (nvItem is not null)
        {
            ApplyAttachedProperties(nvItem, pageInstance);
            AddToNavigationStack(
                nvItem,
                addToNavigationStack,
                direction == JournalNavigationDirection.Backward
            );
        }
        else
        {
            ClearNavigationStackForUnmappedPage();
        }

        AddToJournal(target, direction, isSameKey);
        _currentNavigationTarget = target;

        if (NavigationStack.Count > 0 && SelectedItem != NavigationStack[0] &&
            NavigationStack[0].IsMenuElement)
        {
            SelectedItem = NavigationStack[0];
            OnSelectionChanged();
        }

        return true;
    }

    private void AddToJournal(
        NavigationTarget navigationTarget,
        JournalNavigationDirection direction,
        bool isSameKey)
    {
        switch (direction)
        {
            case JournalNavigationDirection.Backward:
            {
                _forwardJournal.Add(Journal[^1]);
                Journal.RemoveAt(Journal.Count - 1);
                break;
            }

            case JournalNavigationDirection.Forward:
            {
                Journal.Add(_forwardJournal[^1]);
                _forwardJournal.RemoveAt(_forwardJournal.Count - 1);
                break;
            }

            default:
            {
                _forwardJournal.Clear();
                if (!isSameKey)
                {
                    Journal.Add(navigationTarget.Key);
                }

                break;
            }
        }

        SetCurrentValue(IsBackEnabledProperty, CanGoBack);

        Debug.WriteLineIf(EnableDebugMessages, $"JOURNAL INDEX {Journal.Count - 1}");

        if (Journal.Count > 0)
        {
            Debug.WriteLineIf(EnableDebugMessages, $"JOURNAL LAST ELEMENT {Journal[^1]}");
        }
    }

    private object GetNavigationTargetInstance(NavigationTarget target)
    {
        if (_serviceProvider is not null)
        {
            return _serviceProvider.GetService(target.Key.PageType)
                ?? throw new InvalidOperationException(
                       $"{nameof(_serviceProvider)}.{nameof(_serviceProvider.GetService)} returned null for type {target.Key.PageType}."
                   );
        }

        if (_pageService is not null)
        {
            return _pageService.GetPage(target.Key.PageType)
                ?? throw new InvalidOperationException(
                       $"{nameof(_pageService)}.{nameof(_pageService.GetPage)} returned null for type {target.Key.PageType}."
                   );
        }

        return _cache.Remember(
                   target.Key,
                   target.CacheMode,
                   ComputeCachedNavigationInstance
               )
            ?? throw new InvalidOperationException(
                   $"Unable to get or create instance of {target.Key} from cache."
               );

        object? ComputeCachedNavigationInstance() => GetPageInstanceFromCache(target.Key.PageType);
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

    private void ApplyAttachedProperties(INavigationViewItem nvItem, object pageInstance)
    {
        if (pageInstance is FrameworkElement frameworkElement)
        {
            // Store the association between page and navigation item
            PageToNavigationItemDictionary[frameworkElement] = nvItem;

            // Apply HeaderContent if already available
            if (GetHeaderContent(frameworkElement) is { } headerContent)
            {
                nvItem.Content = headerContent;
                UpdateBreadcrumbContents();
            }
        }
    }

    internal void NotifyHeaderContentChanged(FrameworkElement page, object? headerContent)
    {
        if (PageToNavigationItemDictionary.TryGetValue(page, out var navItem))
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

    private void OnNavigationViewContentPresenterNavigated(object sender, NavigationEventArgs e)
    {
        if (sender is not System.Windows.Controls.Frame frame)
        {
            return;
        }

        _ = frame.RemoveBackEntry();

        /*var replaced = 1;
        ((NavigationViewContentPresenter)sender).JournalOwnership =*/
    }

    private void AddToNavigationStack(INavigationViewItem nvItem, bool addToNavigationStack, bool isBackwardsNavigated)
    {
        if (isBackwardsNavigated)
        {
            RecreateNavigationStackFromHistory(nvItem);
        }

        if (addToNavigationStack && !NavigationStack.Contains(nvItem))
        {
            nvItem.Activate(this);
            NavigationStack.Add(nvItem);
        }

        if (!addToNavigationStack)
        {
            UpdateCurrentNavigationStackItem(nvItem);
        }

        ClearNavigationStack(nvItem);
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

    private void UpdateCurrentNavigationStackItem(INavigationViewItem nvItem)
    {
        if (NavigationStack.Contains(nvItem))
        {
            return;
        }

        if (NavigationStack.Count > 1)
        {
            AddToNavigationStackHistory(nvItem);
        }

        if (NavigationStack.Count == 0)
        {
            nvItem.Activate(this);
            NavigationStack.Add(nvItem);
        }
        else
        {
            ReplaceThirstElementInNavigationStack(nvItem);
        }

        ClearNavigationStack(1);
    }

    private void RecreateNavigationStackFromHistory(INavigationViewItem nvItem)
    {
        List<INavigationViewItem?[]>? historyList;
        if (!_complexNavigationStackHistory.TryGetValue(nvItem, out historyList) || historyList.Count == 0)
        {
            return;
        }

        var latestHistory = historyList[^1];
        var startIndex = 0;

        if (latestHistory[0]!.IsMenuElement)
        {
            startIndex = 1;
            ReplaceThirstElementInNavigationStack(latestHistory[0]!);
        }

        for (var i = startIndex; i < latestHistory.Length; i++)
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

        AddToNavigationStack(nvItem, true, false);
    }

    private void AddToNavigationStackHistory(INavigationViewItem nvItem)
    {
        var lastItem = NavigationStack[^1];
        var startIndex = NavigationStack.IndexOf(nvItem);

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

        var arrayLength = NavigationStack.Count - 1 - startIndex;

        INavigationViewItem[] array;

        // OPTIMIZATION: Initializing an array every time well... not an ideal
#if NET6_0_OR_GREATER
        array = System.Buffers.ArrayPool<INavigationViewItem>.Shared.Rent(arrayLength);
#else
        array = new INavigationViewItem[arrayLength];
#endif

        historyList.Add(array);

        var latestHistory = historyList[^1];
        var i = 0;

        for (var j = startIndex; j < NavigationStack.Count - 1; j++)
        {
            latestHistory[i] = NavigationStack[j];
            i++;
        }
    }

    private void ClearNavigationStack(int navigationStackItemIndex)
    {
        var navigationStackCount = NavigationStack.Count;
        var length = navigationStackCount - navigationStackItemIndex;

        if (length == 0)
        {
            return;
        }

        for (var j = navigationStackCount - 1; j >= navigationStackCount - length; j--)
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