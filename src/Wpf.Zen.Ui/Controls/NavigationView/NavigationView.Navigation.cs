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

    // Kept parallel to Journal so its long-standing semantic-entry representation remains stable
    // for diagnostics while each visit can still restore distinct request state.
    private List<NavigationJournalState> JournalState { get; } = [with(50)];

    internal ObservableCollection<NavigationEntry> NavigationStack { get; } = [];

    private readonly NavigationCache _cache = new();

    // Pages that are navigated without a menu item remain valid navigation targets across an
    // unrelated menu-source rebuild. They are deliberately indexed only as fallbacks so a newly
    // added application menu item for the same page type takes precedence.
    private readonly HashSet<NavigationEntry> _unregisteredEntries = [];

    private readonly Dictionary<NavigationEntry, List<NavigationEntry?[]>> _complexNavigationStackHistory = [];

    private INavigationViewPageProvider? _pageService;

    // Navigation state belongs to this control, while page and view-model lifetimes belong to the
    // externally owned navigation scope. Keeping the contextual service here allows the current
    // page tree to discover its own navigator without any application-global registration.
    private INavigationService? _navigationService;

    private DependencyObject? _navigationContextOwner;

    /// <summary>Gets a value indicating whether the journal contains an entry to navigate back to.</summary>
    public bool CanGoBack => Journal.Count > 1;

    /// <summary>Navigates to a page identified by its CLR type.</summary>
    /// <param name="pageType">The page type to activate.</param>
    /// <param name="dataContext">The optional data context assigned to the page.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool Navigate(Type pageType, object? dataContext = null)
    {
        if (PageTypeIndex.TryGetValue(pageType, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext);
        }

        return TryNavigateUnregisteredPage(pageType, false, dataContext);
    }

    /// <summary>Navigates to the item whose page identifier or target tag matches the supplied value.</summary>
    /// <param name="pageIdOrTargetTag">The page identifier or navigation-item target tag.</param>
    /// <param name="dataContext">The optional data context assigned to the page.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool Navigate(string pageIdOrTargetTag, object? dataContext = null)
    {
        if (PageKeyIndex.TryGetValue(pageIdOrTargetTag, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext);
        }

        return false;
    }

    /// <summary>Navigates to a page and activates the matching hierarchy of menu items.</summary>
    /// <param name="pageType">The page type to activate.</param>
    /// <param name="dataContext">The optional data context assigned to the page.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool NavigateWithHierarchy(Type pageType, object? dataContext = null)
    {
        if (PageTypeIndex.TryGetValue(pageType, out var nvItem))
        {
            return NavigateInternal(nvItem, dataContext, true);
        }

        return TryNavigateUnregisteredPage(pageType, true, dataContext);
    }

    /// <summary>Replaces the frame content without changing selection or raising <see cref="SelectionChanged"/>.</summary>
    /// <param name="pageTypeToEmbed">The page type to create and display.</param>
    /// <returns><see langword="true"/> when the content is replaced; otherwise, <see langword="false"/>.</returns>
    public virtual bool ReplaceContent(Type? pageTypeToEmbed)
    {
        if (pageTypeToEmbed == null)
        {
            return false;
        }

        if (_pageService == null)
        {
            return false;
        }

        UpdateContent(_pageService.GetPage(pageTypeToEmbed));

        return true;
    }

    /// <summary>Replaces the frame content with an existing element without changing selection.</summary>
    /// <param name="pageInstanceToEmbed">The element to display.</param>
    /// <param name="dataContext">The optional data context assigned to the element.</param>
    /// <returns><see langword="true"/> when the content is replaced; otherwise, <see langword="false"/>.</returns>
    public virtual bool ReplaceContent(UIElement pageInstanceToEmbed, object? dataContext = null)
    {
        UpdateContent(pageInstanceToEmbed, dataContext);

        return true;
    }

    /// <summary>Navigates to the next journal entry.</summary>
    /// <returns><see langword="true"/> when forward navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool GoForward()
    {
        throw new NotImplementedException();
    }

    /// <summary>Navigates to the previous journal entry.</summary>
    /// <returns><see langword="true"/> when back navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool GoBack()
    {
        if (Journal.Count <= 1)
        {
            return false;
        }

        NavigationEntry previousEntry = Journal[^2];
        NavigationJournalState previousState = JournalState[^2];

        OnBackRequested();
        return NavigateInternal(
            previousEntry,
            previousState.DataContext,
            false,
            true,
            previousState.Parameter
        );
    }

    /// <summary>Clears the navigation journal while retaining the current page.</summary>
    public virtual void ClearJournal()
    {
        Journal.Clear();
        JournalState.Clear();
        _complexNavigationStackHistory.Clear();
        UpdateBackNavigationState();
    }

    /// <inheritdoc />
    NavigationResult INavigationHost.Navigate(NavigationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        bool navigated;
        if (request.PageType is { } pageType)
        {
            bool useHierarchy = request.Mode == NavigationMode.Hierarchical;
            if (PageTypeIndex.TryGetValue(pageType, out NavigationEntry? entry))
            {
                navigated = NavigateInternal(
                    entry,
                    addToNavigationStack: useHierarchy,
                    navigationParameter: request.Parameter
                );
            }
            else
            {
                navigated = TryNavigateUnregisteredPage(
                    pageType,
                    useHierarchy,
                    navigationParameter: request.Parameter
                );
            }

            return navigated ? NavigationResult.Success : NavigationResult.Rejected;
        }

        if (
            request.PageIdOrTargetTag is not { } pageIdOrTargetTag
            || !PageKeyIndex.TryGetValue(pageIdOrTargetTag, out NavigationEntry? taggedEntry)
        )
        {
            return NavigationResult.TargetNotFound;
        }

        navigated = NavigateInternal(
            taggedEntry,
            addToNavigationStack: request.Mode == NavigationMode.Hierarchical,
            navigationParameter: request.Parameter
        );
        return navigated ? NavigationResult.Success : NavigationResult.Rejected;
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "Scope attachment is infrastructure-only; derived controls customize public navigation operations instead of replacing scope ownership."
    )]
    NavigationResult INavigationHost.RequestGoBack() =>
        GoBack() ? NavigationResult.Success : NavigationResult.Rejected;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "ClearJournal is the public and overridable-facing semantic operation."
    )]
    void INavigationHost.ClearNavigationHistory() => ClearJournal();

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "Only a navigation-scope factory may establish the page-provider lifetime boundary."
    )]
    void INavigationHost.AttachNavigationScope(
        INavigationService navigationService,
        INavigationViewPageProvider pageProvider
    )
    {
        if (navigationService is null)
        {
            throw new ArgumentNullException(nameof(navigationService));
        }

        if (pageProvider is null)
        {
            throw new ArgumentNullException(nameof(pageProvider));
        }

        if (_navigationService is not null || _pageService is not null)
        {
            throw new InvalidOperationException(
                "This NavigationView is already attached to a navigation scope. Dispose that scope before attaching another one."
            );
        }

        _navigationService = navigationService;
        _pageService = pageProvider;
        SetNavigationContextOwner(_navigationFrame.Content as DependencyObject);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "Only the owning navigation scope may remove its contextual service and page provider."
    )]
    Task INavigationHost.DetachNavigationScopeAsync(INavigationService navigationService)
    {
        if (!ReferenceEquals(_navigationService, navigationService))
        {
            return Task.CompletedTask;
        }

        object? navigationAwareContent = _navigationAwareContent;
        _navigationAwareContent = null;
        Task exitNotificationTask = QueueNavigationNotificationAsync(
            () => NotifyContentAboutNavigationExitedAsync(navigationAwareContent)
        );

        if (_navigationContextOwner is not null)
        {
            Navigation.ClearContext(_navigationContextOwner);
            _navigationContextOwner = null;
        }

        // A navigation scope owns every page and scoped dependency created through its provider.
        // Remove all control-held page references before that provider is disposed; otherwise a
        // later scope attached to the same NavigationView could display an object whose services
        // belong to the already-disposed scope.
        _ = _navigationFrame.Navigate(null);
        foreach (NavigationEntry entry in NavigationStack)
        {
            entry.Deactivate(this);
        }

        NavigationStack.Clear();
        Journal.Clear();
        JournalState.Clear();
        _complexNavigationStackHistory.Clear();
        _unregisteredEntries.Clear();
        _cache.Clear();
        SetSelectedItem(null);
        RebuildNavigationIndexes();
        UpdateBackNavigationState();

        _navigationService = null;
        _pageService = null;

        return exitNotificationTask;
    }

    private bool TryNavigateUnregisteredPage(
        Type pageType,
        bool addToNavigationStack,
        object? dataContext = null,
        object? navigationParameter = null
    )
    {
        var container = new NavigationViewItem(pageType);
        var entry = new NavigationEntry(container, null, false) { Container = container };

        if (
            !NavigateInternal(
                entry,
                dataContext,
                addToNavigationStack,
                navigationParameter: navigationParameter
            )
        )
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
        bool isBackwardsNavigated = false,
        object? navigationParameter = null
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
        UpdateContent(pageInstance, dataContext, navigationParameter);

        // Apply properties after DataContext is set
        ApplyAttachedProperties(viewItem, pageInstance);

        AddToNavigationStack(viewItem, addToNavigationStack, isBackwardsNavigated);
        AddToJournal(viewItem, dataContext, navigationParameter, isBackwardsNavigated);

        if (!ReferenceEquals(SelectedItem, NavigationStack[0].Item) && NavigationStack[0].IsMenuElement)
        {
            SetSelectedItem(NavigationStack[0].Item);
            OnSelectionChanged();
        }

        return true;
    }

    private void AddToJournal(
        NavigationEntry viewItem,
        object? dataContext,
        object? navigationParameter,
        bool isBackwardsNavigated
    )
    {
        if (isBackwardsNavigated)
        {
            // The target is already the penultimate entry. Removing only the current entry keeps
            // the surviving history in order and avoids resolving a mutable Id through PageKeyIndex.
            Journal.RemoveAt(Journal.Count - 1);
            JournalState.RemoveAt(JournalState.Count - 1);

            if (Journal.Count == 0 || !ReferenceEquals(Journal[^1], viewItem))
            {
                Journal.Add(viewItem);
                JournalState.Add(new NavigationJournalState(dataContext, navigationParameter));
            }
        }
        else
        {
            Journal.Add(viewItem);
            JournalState.Add(new NavigationJournalState(dataContext, navigationParameter));
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

        for (int index = Journal.Count - 1; index >= 0; index--)
        {
            if (!validEntries.Contains(Journal[index]))
            {
                Journal.RemoveAt(index);
                JournalState.RemoveAt(index);
            }
        }

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

        // The provider creates pages, but NavigationView continues to own cache policy. Bypassing
        // this cache for DI-created pages would make NavigationCacheMode silently depend on the
        // application's service registration lifetime and could share singleton UIElements across
        // hosts. Disabled mode still asks the scoped provider for a fresh instance every time.
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

    private void UpdateContent(
        object? content,
        object? dataContext = null,
        object? navigationParameter = null
    )
    {
        if (dataContext is not null && content is FrameworkElement frameworkViewContent)
        {
            frameworkViewContent.DataContext = dataContext;
        }

        SetNavigationContextOwner(content as DependencyObject, navigationParameter);

        _ = _navigationFrame.Navigate(content);
    }

    /// <summary>
    /// Moves scope context from the page being exited to the destination page root.
    /// </summary>
    /// <remarks>
    /// Frame sets an inheritance boundary, so publishing on NavigationView itself would not reach
    /// page descendants. The old root is cleared first because cached pages can later be attached
    /// to another navigation host and must not retain the previous host or request parameter.
    /// </remarks>
    private void SetNavigationContextOwner(
        DependencyObject? newOwner,
        object? navigationParameter = null
    )
    {
        if (!ReferenceEquals(_navigationContextOwner, newOwner))
        {
            if (_navigationContextOwner is not null)
            {
                Navigation.ClearContext(_navigationContextOwner);
            }

            _navigationContextOwner = newOwner;
        }

        if (_navigationContextOwner is not null && _navigationService is not null)
        {
            Navigation.SetContext(
                _navigationContextOwner,
                _navigationService,
                this,
                navigationParameter
            );
        }
    }

    /// <summary>
    /// Retains per-visit state separately from the semantic navigation-item identity.
    /// </summary>
    /// <remarks>
    /// The same page type or menu item may be visited repeatedly with different parameters. Keeping
    /// request state on the journal occurrence ensures GoBack restores the parameter and any legacy
    /// direct-navigation DataContext associated with that visit instead of silently replacing it
    /// with null or with data from a later visit.
    /// </remarks>
    private sealed class NavigationJournalState(object? dataContext, object? parameter)
    {
        public object? DataContext { get; } = dataContext;

        public object? Parameter { get; } = parameter;
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
