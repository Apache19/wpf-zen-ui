// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines navigation logic and state management for <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    // Journal entries retain semantic identity rather than an Id snapshot. INavigationItem.Id is a
    // lookup key and can be supplied by mutable application data; keeping only the string would make
    // a later property change or collection rebuild strand history on a key that no longer exists.
    private List<NavigationEntry> Journal { get; } = [with(50)];

    internal ObservableCollection<NavigationEntry> NavigationStack { get; } = [];

    private readonly NavigationCache _cache = new();

    // Pages that are navigated without a menu item remain valid navigation targets across an
    // unrelated menu-source rebuild. They are deliberately indexed only as fallbacks so a newly
    // added application menu item for the same page type takes precedence.
    private readonly HashSet<NavigationEntry> _unregisteredEntries = [];

    private readonly Dictionary<NavigationEntry, List<NavigationEntry?[]>> _complexNavigationStackHistory = [];

    private IServiceProvider? _serviceProvider;

    private INavigationViewPageProvider? _pageService;

    /// <inheritdoc />
    public bool CanGoBack => Journal.Count > 1;

    /// <inheritdoc />
    public void SetPageProviderService(INavigationViewPageProvider pageProvider)
    {
        _pageService = pageProvider;
    }

    /// <inheritdoc />
    public void SetServiceProvider(IServiceProvider serviceProvider) => _serviceProvider = serviceProvider;

    /// <inheritdoc />
    public virtual bool Navigate(Type pageType, object? dataContext = null)
    {
        if (PageTypeIndex.TryGetValue(pageType, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext);
        }

        return TryNavigateUnregisteredPage(pageType, false, dataContext);
    }

    /// <inheritdoc />
    public virtual bool Navigate(string pageIdOrTargetTag, object? dataContext = null)
    {
        if (PageKeyIndex.TryGetValue(pageIdOrTargetTag, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext);
        }

        return false;
    }

    /// <inheritdoc />
    public virtual bool NavigateWithHierarchy(Type pageType, object? dataContext = null)
    {
        if (PageTypeIndex.TryGetValue(pageType, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext, true);
        }

        return TryNavigateUnregisteredPage(pageType, true, dataContext);
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

        NavigationEntry previousEntry = Journal[^2];

        OnBackRequested();
        return NavigateInternal(previousEntry, null, false, true);
    }

    /// <inheritdoc />
    public virtual void ClearJournal()
    {
        Journal.Clear();
        _complexNavigationStackHistory.Clear();
        UpdateBackNavigationState();
    }

    private bool TryNavigateUnregisteredPage(
        Type pageType,
        bool addToNavigationStack,
        object? dataContext = null
    )
    {
        var container = new NavigationViewItem(pageType);
        var entry = new NavigationEntry(container, null, false) { Container = container };

        if (!NavigateInternal(entry, dataContext, addToNavigationStack))
        {
            return false;
        }

        PageTypeIndex.Add(pageType, entry);
        PageKeyIndex.Add(entry.Id, entry);
        _unregisteredEntries.Add(entry);

        return true;
    }

    private bool NavigateInternal(
        NavigationEntry viewItem,
        object? dataContext = null,
        bool addToNavigationStack = false,
        bool isBackwardsNavigated = false
    )
    {
        if (NavigationStack.Count > 0 && NavigationStack[^1] == viewItem)
        {
            return false;
        }

        var pageInstance = GetNavigationItemInstance(viewItem);

        if (OnNavigating(pageInstance))
        {
            return false;
        }

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
        ApplyAttachedProperties(viewItem, pageInstance);

        AddToNavigationStack(viewItem, addToNavigationStack, isBackwardsNavigated);
        AddToJournal(viewItem, isBackwardsNavigated);

        if (!ReferenceEquals(SelectedItem, NavigationStack[0].Item) && NavigationStack[0].IsMenuElement)
        {
            SetSelectedItem(NavigationStack[0].Item);
            OnSelectionChanged();
        }

        return true;
    }

    private void AddToJournal(NavigationEntry viewItem, bool isBackwardsNavigated)
    {
        if (isBackwardsNavigated)
        {
            // The target is already the penultimate entry. Removing only the current entry keeps
            // the surviving history in order and avoids resolving a mutable Id through PageKeyIndex.
            Journal.RemoveAt(Journal.Count - 1);

            if (Journal.Count == 0 || !ReferenceEquals(Journal[^1], viewItem))
            {
                Journal.Add(viewItem);
            }
        }
        else
        {
            Journal.Add(viewItem);
        }

        UpdateBackNavigationState();
    }

    /// <summary>
    /// Reconciles history and selection after the semantic menu hierarchy has been rebuilt.
    /// </summary>
    /// <remarks>
    /// Observable menu sources may remove an item that is still selected or present in the back
    /// journal. WPF collection changes are valid application operations and must not turn a later
    /// GoBack into a dictionary-index exception. Unregistered pages remain valid because they are
    /// navigation targets independent of the menu hierarchy.
    /// </remarks>
    private void ReconcileNavigationStateAfterIndexRebuild()
    {
        var validEntries = new HashSet<NavigationEntry>(_entriesByItem.Values);
        validEntries.UnionWith(_unregisteredEntries);

        Journal.RemoveAll(entry => !validEntries.Contains(entry));
        UpdateBackNavigationState();

        for (int index = NavigationStack.Count - 1; index >= 0; index--)
        {
            NavigationEntry entry = NavigationStack[index];
            if (validEntries.Contains(entry))
            {
                continue;
            }

            entry.Deactivate(this);
            NavigationStack.RemoveAt(index);
        }

        if (SelectedItem is not null && !_entriesByItem.ContainsKey(SelectedItem))
        {
            SetSelectedItem(null);
            OnSelectionChanged();
        }
    }

    /// <summary>Restores non-menu navigation targets without overriding current menu indexes.</summary>
    private void IndexUnregisteredNavigationEntries()
    {
        foreach (NavigationEntry entry in _unregisteredEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Id))
            {
                if (!PageKeyIndex.ContainsKey(entry.Id))
                {
                    PageKeyIndex.Add(entry.Id, entry);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.TargetPageTag))
            {
                if (!PageKeyIndex.ContainsKey(entry.TargetPageTag!))
                {
                    PageKeyIndex.Add(entry.TargetPageTag!, entry);
                }
            }

            if (entry.TargetPageType is { } targetPageType)
            {
                if (!PageTypeIndex.ContainsKey(targetPageType))
                {
                    PageTypeIndex.Add(targetPageType, entry);
                }
            }
        }
    }

    /// <summary>
    /// Publishes the journal-derived back capability through the read-only dependency property.
    /// Keeping this update behind one method prevents paths such as ClearJournal from leaving the
    /// button enabled after the journal has already been emptied.
    /// </summary>
    private void UpdateBackNavigationState() => SetValue(IsBackEnabledPropertyKey, CanGoBack);

    private object GetNavigationItemInstance(NavigationEntry viewItem)
    {
        if (viewItem.TargetPageType is null)
        {
            throw new InvalidOperationException(
                $"The {nameof(viewItem)}.{nameof(viewItem.TargetPageType)} property cannot be null."
            );
        }

        if (_serviceProvider is not null)
        {
            return _serviceProvider.GetService(viewItem.TargetPageType)
                ?? throw new InvalidOperationException(
                    $"{nameof(_serviceProvider)}.{nameof(_serviceProvider.GetService)} returned null for type {viewItem.TargetPageType}."
                );
        }

        if (_pageService is not null)
        {
            return _pageService.GetPage(viewItem.TargetPageType)
                ?? throw new InvalidOperationException(
                    $"{nameof(_pageService)}.{nameof(_pageService.GetPage)} returned null for type {viewItem.TargetPageType}."
                );
        }

        return _cache.Remember(
                viewItem.TargetPageType,
                viewItem.NavigationCacheMode,
                NavigationCacheSize,
                ComputeCachedNavigationInstance
            )
            ?? throw new InvalidOperationException(
                $"Unable to get or create instance of {viewItem.TargetPageType} from cache."
            );

        object? ComputeCachedNavigationInstance() => GetPageInstanceFromCache(viewItem.TargetPageType);
    }

    internal void NotifyNavigationCacheModeChanged(
        NavigationEntry nvItem,
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode
    )
    {
        _cache.UpdateCacheMode(nvItem.TargetPageType, oldCacheMode, newCacheMode, NavigationCacheSize);
    }

    /// <summary>
    /// Migrates cache state after a data navigation item raises a property-change notification.
    /// </summary>
    /// <remarks>
    /// Unlike a dependency-property callback, INotifyPropertyChanged does not carry the previous
    /// value. NavigationEntry retains that snapshot so Required and Enabled entries can be moved
    /// between the cache partitions instead of leaving two instances for the same page type. A page
    /// type change removes the old type because a Required cache entry would otherwise be retained
    /// indefinitely after the item stopped targeting it.
    /// </remarks>
    private void SynchronizeNavigationEntryCacheState(NavigationEntry entry)
    {
        Type? oldPageType = entry.LastKnownTargetPageType;
        Type? newPageType = entry.TargetPageType;
        NavigationCacheMode oldCacheMode = entry.LastKnownNavigationCacheMode;
        NavigationCacheMode newCacheMode = entry.NavigationCacheMode;

        if (oldPageType == newPageType)
        {
            _cache.UpdateCacheMode(oldPageType, oldCacheMode, newCacheMode, NavigationCacheSize);
        }
        else
        {
            _cache.Remove(oldPageType);
        }

        entry.LastKnownTargetPageType = newPageType;
        entry.LastKnownNavigationCacheMode = newCacheMode;
    }

    private object? GetPageInstanceFromCache(Type? targetPageType)
    {
        if (targetPageType is null)
        {
            return null;
        }

        if (_serviceProvider is not null)
        {
            return _serviceProvider.GetService(targetPageType)
                ?? throw new InvalidOperationException(
                    $"{nameof(_serviceProvider.GetService)} returned null"
                );
        }

        if (_pageService is not null)
        {
            return _pageService.GetPage(targetPageType)
                ?? throw new InvalidOperationException($"{nameof(_pageService.GetPage)} returned null");
        }

        return NavigationViewActivator.CreateInstance(targetPageType)
            ?? throw new InvalidOperationException("Failed to create instance of the page");
    }

    private void ApplyAttachedProperties(NavigationEntry viewItem, object pageInstance)
    {
        if (pageInstance is FrameworkElement frameworkElement)
        {
            // Store the owning navigation item on the page itself — it lives and dies with the page.
            frameworkElement.SetValue(PageNavigationItemProperty, viewItem);

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
        if (page.GetValue(PageNavigationItemProperty) is NavigationEntry navItem)
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

        _ = _navigationFrame.Navigate(content);
    }

    private void AddToNavigationStack(
        NavigationEntry viewItem,
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

    private void UpdateCurrentNavigationStackItem(NavigationEntry viewItem)
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

    private void RecreateNavigationStackFromHistory(NavigationEntry item)
    {
        List<NavigationEntry?[]>? historyList;
        if (!_complexNavigationStackHistory.TryGetValue(item, out historyList) || historyList.Count == 0)
        {
            return;
        }

        NavigationEntry?[] latestHistory = historyList[^1];
        var startIndex = 0;

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
        System.Buffers.ArrayPool<NavigationEntry>.Shared.Return(latestHistory!, true);
#endif

        AddToNavigationStack(item, true, false);
    }

    private void AddToNavigationStackHistory(NavigationEntry viewItem)
    {
        NavigationEntry lastItem = NavigationStack[^1];
        var startIndex = NavigationStack.IndexOf(viewItem);

        if (startIndex < 0)
        {
            startIndex = 0;
        }

        List<NavigationEntry?[]>? historyList;
        if (!_complexNavigationStackHistory.TryGetValue(lastItem, out historyList))
        {
            historyList = new List<NavigationEntry?[]>(5);
            _complexNavigationStackHistory.Add(lastItem, historyList);
        }

        int arrayLength = NavigationStack.Count - 1 - startIndex;

        NavigationEntry[] array;

        // OPTIMIZATION: Initializing an array every time well... not an ideal
#if NET6_0_OR_GREATER
        array = System.Buffers.ArrayPool<NavigationEntry>.Shared.Rent(arrayLength);
#else
        array = new NavigationEntry[arrayLength];
#endif

        historyList.Add(array);

        NavigationEntry?[] latestHistory = historyList[^1];
        int i = 0;

        for (int j = startIndex; j < NavigationStack.Count - 1; j++)
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

        for (int j = navigationStackCount - 1; j >= navigationStackCount - length; j--)
        {
            _ = NavigationStack.Remove(NavigationStack[j]);
        }
    }

    private void ClearNavigationStack(NavigationEntry item)
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

    private void ReplaceThirstElementInNavigationStack(NavigationEntry newItem)
    {
        NavigationStack[0].Deactivate(this);
        NavigationStack[0] = newItem;
        NavigationStack[0].Activate(this);
    }
}
