// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>
/// Defines navigation history, semantic-location resolution, and page activation for
/// <see cref="NavigationView"/>.
/// </content>
public partial class NavigationView
{
    // The two journal lists are deliberately sequences of completed navigation occurrences. They
    // do not model parent/child relationships: each entry carries the request that originally
    // described its semantic location, plus a resolved snapshot that can recover a useful path
    // when a dynamic map no longer contains that route during back or forward navigation.
    private List<NavigationJournalEntry> Journal { get; } = [with(50)];
    private List<NavigationJournalEntry> ForwardJournal { get; } = [with(20)];

    private readonly NavigationCache _cache = new();
    private readonly HashSet<NavigationEntry> _unregisteredEntries = [];
    private INavigationViewPageProvider? _pageService;
    private INavigationService? _navigationService;
    private DependencyObject? _navigationContextOwner;
    private NavigationEntry? _currentEntry;
    private NavigationJournalEntry? _currentOccurrence;
    private object? _currentPage;
    private FrameworkElement? _currentLocationElement;
    private INavigationLocationSource? _currentLocationSource;
    private IReadOnlyList<BreadcrumbSegment>? _oneTimeBreadcrumbFallback;
    private int _isLocationRefreshPending;
    private bool _isNavigationRequestInProgress;

    /// <summary>Gets a value indicating whether the journal contains an earlier occurrence.</summary>
    public bool CanGoBack => Journal.Count > 1;

    /// <summary>Gets a value indicating whether the journal contains a later occurrence.</summary>
    public bool CanGoForward => ForwardJournal.Count > 0;

    /// <inheritdoc />
    public INavigationState NavigationState => this;

    /// <summary>Navigates to a page identified by its CLR type.</summary>
    /// <param name="pageType">The page type to activate.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool Navigate(Type pageType) =>
        PerformRequest(NavigationRequest.To(pageType)).Succeeded;

    /// <summary>Navigates to a route, menu-item id, or target tag.</summary>
    /// <param name="routeKey">The exact semantic route or host lookup key.</param>
    /// <returns><see langword="true"/> when navigation succeeds; otherwise, <see langword="false"/>.</returns>
    public virtual bool Navigate(string routeKey) =>
        PerformRequest(NavigationRequest.To(routeKey)).Succeeded;

    /// <summary>Replaces frame content without changing semantic location, selection, or history.</summary>
    /// <param name="pageTypeToEmbed">The page type to create and display.</param>
    /// <returns><see langword="true"/> when content is replaced.</returns>
    public virtual bool ReplaceContent(Type? pageTypeToEmbed)
    {
        if (pageTypeToEmbed is null)
        {
            return false;
        }

        UpdateContent(CreatePageInstance(pageTypeToEmbed));
        return true;
    }

    /// <summary>Replaces frame content with an existing element without changing semantic state.</summary>
    /// <param name="pageInstanceToEmbed">The element to display.</param>
    /// <returns><see langword="true"/> when content is replaced.</returns>
    public virtual bool ReplaceContent(UIElement pageInstanceToEmbed)
    {
        UpdateContent(pageInstanceToEmbed);
        return true;
    }

    /// <summary>Navigates to the next journal occurrence.</summary>
    /// <returns><see langword="true"/> when forward navigation succeeds.</returns>
    public virtual bool GoForward()
    {
        if (ForwardJournal.Count == 0)
        {
            return false;
        }

        var next = ForwardJournal[^1];
        return NavigateInternal(
            next.Entry,
            next.Request,
            isForwardsNavigated: true,
            restoredOccurrence: next
        );
    }

    /// <summary>Navigates to the preceding journal occurrence.</summary>
    /// <returns><see langword="true"/> when back navigation succeeds.</returns>
    public virtual bool GoBack()
    {
        if (Journal.Count <= 1)
        {
            return false;
        }

        var previous = Journal[^2];
        return NavigateInternal(
            previous.Entry,
            previous.Request,
            isBackwardsNavigated: true,
            restoredOccurrence: previous
        );
    }

    /// <summary>Clears navigation history while retaining the current page and semantic location.</summary>
    public virtual void ClearJournal()
    {
        Journal.Clear();
        ForwardJournal.Clear();
        if (_currentOccurrence is not null)
        {
            // Retaining the current occurrence gives the next navigation a valid back baseline only
            // after it is added; CanGoBack correctly remains false while this is the sole entry.
            Journal.Add(_currentOccurrence);
        }

        UpdateBackNavigationState();
    }

    /// <inheritdoc />
    NavigationResult INavigationHost.Navigate(NavigationRequest request) => PerformRequest(request);

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "RequestGoBack has the public overridable GoBack counterpart."
    )]
    NavigationResult INavigationHost.RequestGoBack() =>
        GoBack() ? NavigationResult.Success : NavigationResult.Rejected;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "RequestGoForward has the public overridable GoForward counterpart."
    )]
    NavigationResult INavigationHost.RequestGoForward() =>
        GoForward() ? NavigationResult.Success : NavigationResult.Rejected;

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "ClearJournal is the public overridable counterpart."
    )]
    void INavigationHost.ClearNavigationHistory() => ClearJournal();

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "Only navigation-scope infrastructure establishes the page-provider lifetime boundary."
    )]
    void INavigationHost.AttachNavigationScope(
        INavigationService navigationService,
        INavigationViewPageProvider pageProvider)
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
        Justification = "Only the owning navigation scope may remove contextual services and pages."
    )]
    Task INavigationHost.DetachNavigationScope(INavigationService navigationService)
    {
        if (!ReferenceEquals(_navigationService, navigationService))
        {
            return Task.CompletedTask;
        }

        Task entryCompletion = CancelCurrentNavigationEnter();
        AbortPendingNavigationTransition();

        var navigationAwareContent = _navigationAwareContent;
        var navigationAwareTarget  = _navigationAwareTarget;

        _navigationAwareContent = null;
        _navigationAwareTarget  = null;

        var callbackFailures = new List<NavigationCallbackException>();
        NotifyContentAboutNavigationExited(
            navigationAwareContent,
            navigationAwareTarget,
            callbackFailures
        );

        DetachCurrentLocationSource();
        ClearNavigationParent(_currentPage as FrameworkElement);

        if (_navigationContextOwner is not null)
        {
            Navigation.ClearContext(_navigationContextOwner);
            _navigationContextOwner = null;
        }

        _ = _navigationFrame.Navigate(null);

        _currentEntry?.Deactivate(this);
        _currentEntry = null;
        _currentPage = null;
        _currentOccurrence = null;

        Journal.Clear();
        ForwardJournal.Clear();
        _unregisteredEntries.Clear();
        _cache.Clear();

        SetSelectedItem(null);
        SetValue(CurrentLocationPropertyKey, null);

        _breadcrumbs.Clear();

        RebuildNavigationIndexes();
        UpdateBackNavigationState();

        CurrentLocationChanged?.Invoke(this, EventArgs.Empty);

        _navigationService = null;
        _pageService = null;

        ReportNavigationCallbackFailures(callbackFailures);
        return entryCompletion;
    }

    private NavigationResult PerformRequest(NavigationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var mappedNode = NavigationMap.FindNode(request.Target);
        if (request.Target.RouteKey is null && mappedNode is not null)
        {
            request = request.WithTarget(
                new NavigationTarget(
                    request.Target.PageType ?? mappedNode.Target?.PageType,
                    mappedNode.Key,
                    request.Target.Parameter
                )
            );
        }

        var entry = ResolveNavigationEntry(request.Target);
        if (entry is not null)
        {
            if (entry.TargetPageType is null)
            {
                // Structural menu entries participate in route lookup so maps and breadcrumbs can
                // refer to them, but they are not navigation destinations. Preserve the public
                // result contract instead of passing an absent page type to page activation.
                return NavigationResult.TargetNotFound;
            }

            var targetIsMenuLookup = request.Target.RouteKey == entry.Id
                                     || request.Target.RouteKey == entry.TargetPageTag;

            if (mappedNode is null && targetIsMenuLookup && entry.TargetPageType is { } entryPageType)
            {
                mappedNode = NavigationMap.FindNode(NavigationTarget.ForPage(entryPageType));
                if (mappedNode is not null)
                {
                    request = request.WithTarget(
                        new NavigationTarget(entryPageType, mappedNode.Key, request.Target.Parameter)
                    );
                }
            }

            return NavigateInternal(entry, request)
                ? NavigationResult.Success
                : NavigationResult.Rejected;
        }

        var pageType = request.Target.PageType ?? mappedNode?.Target?.PageType;
        if (pageType is null)
        {
            return NavigationResult.TargetNotFound;
        }

        return TryNavigateUnregisteredPage(pageType, request)
            ? NavigationResult.Success
            : NavigationResult.Rejected;
    }

    private NavigationEntry? ResolveNavigationEntry(NavigationTarget target)
    {
        if (target.RouteKey is { } routeKey && PageKeyIndex.TryGetValue(routeKey, out var keyed))
        {
            return keyed;
        }

        if (target.PageType is { } pageType && PageTypeIndex.TryGetValue(pageType, out var typed))
        {
            return typed;
        }

        var mappedTarget = NavigationMap.FindNode(target)?.Target;
        if (mappedTarget?.RouteKey is { } mappedRoute
            && PageKeyIndex.TryGetValue(mappedRoute, out var mappedByRoute))
        {
            return mappedByRoute;
        }

        return mappedTarget?.PageType is { } mappedPageType
            && PageTypeIndex.TryGetValue(mappedPageType, out var mappedByType)
                ? mappedByType
                : null;
    }

    private NavigationRequest CreateRequestForEntry(NavigationEntry entry, object? parameter = null)
    {
        var mapNode = entry.TargetPageType is { } pageType
            ? NavigationMap.FindNode(NavigationTarget.ForPage(pageType))
            : null;

        return NavigationRequest.To(
            new NavigationTarget(entry.TargetPageType, mapNode?.Key ?? entry.Id, parameter)
        );
    }

    private bool TryNavigateUnregisteredPage(Type pageType, NavigationRequest request)
    {
        // Code-only targets need semantic lookup state, not a WPF container. Retaining a fabricated
        // NavigationViewItem for every encountered page type would keep an unnecessary dispatcher-
        // affine control graph alive for the complete scope. The data model supplies the same page
        // identity and generated route id without any visual or template state.
        var entry = new NavigationEntry(new NavigationItem(pageType.Name, pageType), false);

        if (!NavigateInternal(entry, request))
        {
            return false;
        }

        if (!PageTypeIndex.ContainsKey(pageType))
        {
            PageTypeIndex.Add(pageType, entry);
        }

        if (!PageKeyIndex.ContainsKey(entry.Id))
        {
            PageKeyIndex.Add(entry.Id, entry);
        }

        _unregisteredEntries.Add(entry);
        return true;
    }

    private bool NavigateInternal(
        NavigationEntry entry,
        NavigationRequest? request = null,
        bool isBackwardsNavigated = false,
        bool isForwardsNavigated = false,
        NavigationJournalEntry? restoredOccurrence = null)
    {
        // Routed callbacks are synchronous and application code may request another navigation
        // from Navigating, BackRequested, or Navigated. Reject that nested transaction so it cannot
        // commit a journal occurrence that the outer request immediately overwrites.
        if (_isNavigationRequestInProgress)
        {
            return false;
        }

        _isNavigationRequestInProgress = true;
        try
        {
            return NavigateInternalCore(
                entry,
                request,
                isBackwardsNavigated,
                isForwardsNavigated,
                restoredOccurrence
            );
        }
        finally
        {
            _isNavigationRequestInProgress = false;
        }
    }

    private bool NavigateInternalCore(
        NavigationEntry entry,
        NavigationRequest? request,
        bool isBackwardsNavigated,
        bool isForwardsNavigated,
        NavigationJournalEntry? restoredOccurrence)
    {
        request ??= CreateRequestForEntry(entry);
        if (!isBackwardsNavigated
            && !isForwardsNavigated
            && IsRedundantRequest(entry, request))
        {
            return false;
        }

        if (isBackwardsNavigated)
        {
            OnBackRequested();
        }

        var pageInstance = GetNavigationItemInstance(entry);
        if (OnNavigating(pageInstance))
        {
            return false;
        }

        if (!ReferenceEquals(_currentPage, pageInstance))
        {
            ClearNavigationParent(_currentPage as FrameworkElement);
        }

        if (pageInstance is FrameworkElement frameworkElement)
        {
            // Frame content does not inherit NavigationParent from the host. Setting it on the page
            // root lets the BreadcrumbContent attached property notify exactly this host.
            frameworkElement.SetValue(NavigationParentProperty, this);
        }

        UpdateContent(pageInstance, request.Target.Parameter, request.Target);

        _currentEntry?.Deactivate(this);
        _currentEntry = entry;
        _currentEntry.Activate(this);

        _currentPage = pageInstance;

        AttachCurrentLocationSource(pageInstance);

        if (isBackwardsNavigated)
        {
            var departedOccurrence = _currentOccurrence
                ?? throw new InvalidOperationException("Back navigation requires a current journal occurrence.");

            Journal.RemoveAt(Journal.Count - 1);
            ForwardJournal.Add(departedOccurrence);

            _currentOccurrence = restoredOccurrence
                ?? throw new InvalidOperationException("Back navigation requires a journal occurrence.");

            _oneTimeBreadcrumbFallback = _currentOccurrence.BreadcrumbSnapshot;
        }
        else if (isForwardsNavigated)
        {
            _currentOccurrence = restoredOccurrence
                ?? throw new InvalidOperationException("Forward navigation requires a journal occurrence.");

            ForwardJournal.RemoveAt(ForwardJournal.Count - 1);
            Journal.Add(_currentOccurrence);

            _oneTimeBreadcrumbFallback = _currentOccurrence.BreadcrumbSnapshot;
        }
        else
        {
            _currentOccurrence = new NavigationJournalEntry(entry, request);
            Journal.Add(_currentOccurrence);
            ForwardJournal.Clear();
        }

        RefreshCurrentLocation();
        _currentOccurrence.BreadcrumbSnapshot = CurrentLocation?.Breadcrumbs ?? [];
        UpdateBackNavigationState();

        var isLiveMenuEntry = entry.IsMenuElement && _entriesByItem.Values.Contains(entry);
        var selectedItem = isLiveMenuEntry ? entry.Item : null;
        if (!ReferenceEquals(SelectedItem, selectedItem))
        {
            SetSelectedItem(selectedItem);
            OnSelectionChanged();
        }

        OnNavigated(pageInstance);
        QueueActiveNavigationBranchIntoView();
        return true;
    }

    private bool IsRedundantRequest(NavigationEntry entry, NavigationRequest request)
    {
        if (!ReferenceEquals(_currentEntry, entry) || _currentOccurrence is null)
        {
            return false;
        }

        var current = _currentOccurrence.Request;
        return current.Target.RouteKey == request.Target.RouteKey
            && current.Target.PageType == request.Target.PageType
            && Equals(current.Target.Parameter, request.Target.Parameter)
            && request.Breadcrumbs is null
            && request.BreadcrumbParent is null
            && request.BreadcrumbContent is null;
    }

    /// <summary>
    /// Reconciles menu selection after lookup indexes change without rewriting history or paths.
    /// </summary>
    /// <remarks>
    /// Menu collections are shortcut surfaces. Removing a shortcut cannot invalidate a page that
    /// is already displayed or an occurrence retained by the journal. Only the menu projection is
    /// cleared; back navigation can still activate the retained entry or its page type.
    /// </remarks>
    private void ReconcileNavigationStateAfterIndexRebuild()
    {
        var currentIsLiveMenuEntry = _currentEntry is not null
            && _currentEntry.IsMenuElement
            && _entriesByItem.Values.Contains(_currentEntry);

        if (!currentIsLiveMenuEntry && SelectedItem is not null)
        {
            SetSelectedItem(null);
            OnSelectionChanged();
        }

        UpdateBackNavigationState();
    }

    /// <summary>Restores code-only targets as fallback lookup entries after a menu rebuild.</summary>
    private void IndexUnregisteredNavigationEntries()
    {
        foreach (var entry in _unregisteredEntries)
        {
            if (!string.IsNullOrWhiteSpace(entry.Id) && !PageKeyIndex.ContainsKey(entry.Id))
            {
                PageKeyIndex.Add(entry.Id, entry);
            }

            if (entry.TargetPageTag is { Length: > 0 } tag && !PageKeyIndex.ContainsKey(tag))
            {
                PageKeyIndex.Add(tag, entry);
            }

            if (entry.TargetPageType is { } pageType && !PageTypeIndex.ContainsKey(pageType))
            {
                PageTypeIndex.Add(pageType, entry);
            }
        }
    }

    private void UpdateBackNavigationState() => SetValue(IsBackEnabledPropertyKey, CanGoBack);

    private object GetNavigationItemInstance(NavigationEntry entry)
    {
        var targetPageType = entry.TargetPageType;
        if (targetPageType is null)
        {
            throw new InvalidOperationException(
                $"The {nameof(entry)}.{nameof(entry.TargetPageType)} property cannot be null."
            );
        }

        return _cache.Remember(
                targetPageType,
                entry.NavigationCacheMode,
                NavigationCacheSize,
                () => CreatePageInstance(targetPageType)
            )
            ?? throw new InvalidOperationException(
                $"Unable to get or create instance of {targetPageType} from cache."
            );
    }

    internal void NotifyNavigationCacheModeChanged(
        NavigationEntry entry,
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode) =>
        _cache.UpdateCacheMode(entry.TargetPageType, oldCacheMode, newCacheMode, NavigationCacheSize);

    /// <summary>Migrates cache state after mutable menu metadata changes.</summary>
    private void SynchronizeNavigationEntryCacheState(NavigationEntry entry)
    {
        var oldPageType  = entry.LastKnownTargetPageType;
        var newPageType  = entry.TargetPageType;
        var oldCacheMode = entry.LastKnownNavigationCacheMode;
        var newCacheMode = entry.NavigationCacheMode;

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

    /// <summary>
    /// Resolves a page instance for the given type via the attached page provider, or the
    /// container-free activator when no provider is attached. The navigation cache is consulted by
    /// <see cref="GetNavigationItemInstance"/> before this factory is invoked.
    /// </summary>
    private object CreatePageInstance(Type targetPageType)
    {
        // Every page must derive from FrameworkElement: the navigation host relies on DataContext,
        // attached properties, and breadcrumb declarations that only a FrameworkElement root can
        // carry. Enforce the invariant once at this chokepoint so the DI provider, the container-free
        // activator, and custom providers all fail with the same diagnostic instead of rendering
        // template-less content inside the frame.
        if (!typeof(FrameworkElement).IsAssignableFrom(targetPageType))
        {
            throw new InvalidCastException(
                $"TargetPageType must be derived from {typeof(FrameworkElement)}. {targetPageType} is not."
            );
        }

        if (_pageService is not null)
        {
            // The provider contract declares a non-nullable result, but a custom implementation
            // may still violate it. Reject null here, before the navigation cache can store it:
            // a cached null would poison the entry and reject every later navigation to this type
            // without re-invoking the provider.
            return _pageService.GetPage(targetPageType)
                ?? throw new InvalidOperationException(
                    $"The page provider {_pageService.GetType().Name} returned null for {targetPageType}."
                );
        }

        return ActivatorNavigationViewPageProvider.Instance.GetPage(targetPageType);
    }

    internal void NotifyBreadcrumbDeclarationChanged(FrameworkElement page)
    {
        if (ReferenceEquals(page, _currentPage))
        {
            RefreshCurrentLocation();
        }
    }

    /// <summary>
    /// Removes only the host reference written by this navigation view from a page that is no
    /// longer current.
    /// </summary>
    /// <remarks>
    /// Page providers may retain page instances after a navigation scope is disposed. Leaving the
    /// local inherited-property value behind would then keep the former NavigationView alive and
    /// would route later breadcrumb declarations to a stale host. The identity check matters when
    /// an application deliberately moves one page instance between hosts: a previous host must not
    /// clear a newer host's ownership value.
    /// </remarks>
    private void ClearNavigationParent(FrameworkElement? page)
    {
        if (page is not null
            && ReferenceEquals(page.ReadLocalValue(NavigationParentProperty), this))
        {
            page.ClearValue(NavigationParentProperty);
        }
    }

    private void UpdateContent(
        object? content,
        object? navigationParameter = null,
        NavigationTarget? navigationTarget = null
    )
    {
        // Select the host template before Frame receives the destination. Waiting for Navigated
        // would briefly arrange the new page using the previous page's scrolling policy.
        UpdateNavigationFrameScrollState(content);
        SetNavigationContextOwner(content as DependencyObject, navigationParameter);

        // Read page-owned transition declarations only after activation has produced the concrete
        // instance and Navigating has had a chance to configure it. WPF object navigation may commit
        // asynchronously, so carrying this snapshot with the request also gives the commit handler
        // a coherent fallback if a future navigation path supplies non-DependencyObject content.
        var transitionSettings = GetNavigationTransitionSettings(content);
        _ = _navigationFrame.Navigate(
            content,
            new NavigationFrameState(navigationTarget, transitionSettings)
        );
    }

    /// <summary>Moves scope context from the exited page root to the destination page root.</summary>
    private void SetNavigationContextOwner(
        DependencyObject? newOwner,
        object? navigationParameter = null)
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
            Navigation.SetContext(_navigationContextOwner, _navigationService, this, navigationParameter);
        }
    }

    /// <summary>Moves map notifications and resolves the active occurrence against the new graph.</summary>
    private void ChangeNavigationMap(NavigationMap? oldMap, NavigationMap? newMap)
    {
        if (oldMap is not null)
        {
            WeakEventManager<NavigationMap, EventArgs>.RemoveHandler(
                oldMap,
                nameof(NavigationMap.Changed),
                OnNavigationMapChanged
            );
        }

        if (newMap is not null)
        {
            WeakEventManager<NavigationMap, EventArgs>.RemoveHandler(
                newMap,
                nameof(NavigationMap.Changed),
                OnNavigationMapChanged
            );
            WeakEventManager<NavigationMap, EventArgs>.AddHandler(
                newMap,
                nameof(NavigationMap.Changed),
                OnNavigationMapChanged
            );
        }

        if (newMap is not null)
        {
            RefreshCurrentLocation();
        }
    }

    private void OnNavigationMapChanged(object? sender, EventArgs e) =>
        RefreshCurrentLocationOnDispatcher();

    /// <summary>
    /// Resolves the current occurrence using deterministic request, page, map, and fallback priority.
    /// </summary>
    /// <remarks>
    /// The resolver never infers hierarchy from the previous journal entry or from menu nesting.
    /// Anonymous pages therefore produce a single leaf unless a caller, page source, or map supplies
    /// more information. This prevents ordinary A → D navigation from being misrepresented as a
    /// semantic A → D relationship.
    /// </remarks>
    internal void RefreshCurrentLocation()
    {
        if (_currentOccurrence is null || _currentPage is null)
        {
            return;
        }

        var request = _currentOccurrence.Request;
        var context = new NavigationLocationContext(request.Target, _currentPage);
        var descriptor = _currentLocationSource?.GetNavigationLocation(context);

        IReadOnlyList<BreadcrumbSegment> resolved;

        if (request.Breadcrumbs is { Count: > 0 } explicitRequestPath)
        {
            resolved = explicitRequestPath;
        }
        else if (descriptor?.Breadcrumbs is { Count: > 0 } explicitPagePath)
        {
            resolved = explicitPagePath;
        }
        else if (_currentPage is FrameworkElement pathElement
                 && GetBreadcrumbPath(pathElement) is { } declaredPath)
        {
            resolved = declaredPath.CreateSnapshot();
        }
        else
        {
            var leafContentOverride = request.BreadcrumbContent
                ?? descriptor?.Content
                ?? (_currentPage is FrameworkElement element ? GetBreadcrumbContent(element) : null);

            var fallbackLeafContent = leafContentOverride
                ?? GetSafeEntryContent(_currentEntry)
                ?? request.Target.PageType?.Name
                ?? _currentEntry?.TargetPageType?.Name
                ?? request.Target.RouteKey;

            var declaredParentKey = _currentPage is FrameworkElement parentElement
                ? GetBreadcrumbParentKey(parentElement)
                : null;

            var parent = request.BreadcrumbParent
                ?? descriptor?.Parent
                ?? (declaredParentKey is null
                    ? null
                    : NavigationTarget.ForRoute(declaredParentKey));

            if (parent is not null)
            {
                var combined = new List<BreadcrumbSegment>(NavigationMap.ResolvePath(parent, context));
                var leaf = CreateLeafSegment(request.Target, fallbackLeafContent);
                var mappedLeaf = NavigationMap.FindNode(request.Target);
                if (mappedLeaf?.IsBreadcrumbVisible != false
                    && (combined.Count == 0 || combined[^1].Key != leaf.Key))
                {
                    combined.Add(leaf);
                }

                resolved = combined;
            }
            else
            {
                var mappedLeaf = NavigationMap.FindNode(request.Target);
                resolved = NavigationMap.ResolvePath(request.Target, context, leafContentOverride);
                if (resolved.Count == 0 && mappedLeaf is null)
                {
                    resolved = _oneTimeBreadcrumbFallback is { Count: > 0 } fallback
                        ? fallback
                        : [CreateLeafSegment(request.Target, fallbackLeafContent)];
                }
            }
        }

        _oneTimeBreadcrumbFallback = null;

        var immutablePath = SnapshotResolvedPath(resolved);
        var location = new NavigationLocation(request.Target, _currentPage, immutablePath);
        SetValue(CurrentLocationPropertyKey, location);

        _breadcrumbs.ReplaceWith(immutablePath);

        if (_currentOccurrence is not null)
        {
            _currentOccurrence.BreadcrumbSnapshot = immutablePath;
        }

        CurrentLocationChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Defensively snapshots extension-provided paths and turns a latent null-reference failure in
    /// BreadcrumbBar into an immediate contract error at the source boundary.
    /// </summary>
    private static IReadOnlyList<BreadcrumbSegment> SnapshotResolvedPath(
        IReadOnlyList<BreadcrumbSegment> path)
    {
        var snapshot = new BreadcrumbSegment[path.Count];
        for (var index = 0; index < path.Count; index++)
        {
            snapshot[index] = path[index]
                ?? throw new InvalidOperationException(
                    "A resolved breadcrumb path cannot contain null segments."
                );
        }

        return Array.AsReadOnly(snapshot);
    }

    private BreadcrumbSegment CreateLeafSegment(NavigationTarget requestedTarget, object? content)
    {
        var pageType = requestedTarget.PageType ?? _currentEntry?.TargetPageType;

        var key = requestedTarget.RouteKey
            ?? pageType?.FullName
            ?? _currentEntry?.Id
            ?? Guid.NewGuid().ToString("n");

        var target = new NavigationTarget(pageType, requestedTarget.RouteKey, requestedTarget.Parameter);
        return new BreadcrumbSegment(key, content, target);
    }

    private static object? GetSafeEntryContent(NavigationEntry? entry)
    {
        var content = entry?.Content;
        return content is UIElement ? entry?.SearchText : content;
    }

    private void AttachCurrentLocationSource(object page)
    {
        DetachCurrentLocationSource();

        _currentLocationElement = page as FrameworkElement;
        if (_currentLocationElement is not null)
        {
            _currentLocationElement.DataContextChanged += OnCurrentPageDataContextChanged;
        }

        _currentLocationSource = page as INavigationLocationSource
            ?? _currentLocationElement?.DataContext as INavigationLocationSource;

        if (_currentLocationSource is not null)
        {
            _currentLocationSource.LocationChanged += OnCurrentLocationSourceChanged;
        }
    }

    private void DetachCurrentLocationSource()
    {
        if (_currentLocationElement is not null)
        {
            _currentLocationElement.DataContextChanged -= OnCurrentPageDataContextChanged;
            _currentLocationElement = null;
        }

        if (_currentLocationSource is not null)
        {
            _currentLocationSource.LocationChanged -= OnCurrentLocationSourceChanged;
            _currentLocationSource = null;
        }
    }

    private void OnCurrentPageDataContextChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (_currentPage is not null)
        {
            AttachCurrentLocationSource(_currentPage);
            RefreshCurrentLocation();
        }
    }

    private void OnCurrentLocationSourceChanged(object? sender, EventArgs e) =>
        RefreshCurrentLocationOnDispatcher();

    /// <summary>
    /// Marshals model notifications because maps and view models are ordinary .NET objects and may
    /// raise their change events from a worker thread. Each host resolves on its own Dispatcher, so
    /// the same read-only map can safely notify navigation surfaces in different windows.
    /// </summary>
    private void RefreshCurrentLocationOnDispatcher()
    {
        if (Dispatcher.CheckAccess())
        {
            RefreshCurrentLocation();
        }
        else if (!Dispatcher.HasShutdownStarted
                 && !Dispatcher.HasShutdownFinished)
        {
            // Notifications can arrive concurrently from unrelated view models. Interlocked owns
            // the pending bit across those producers and the dispatcher consumer without relying
            // on an otherwise unsynchronized Boolean read/write pair.
            if (Interlocked.CompareExchange(ref _isLocationRefreshPending, 1, 0) != 0)
            {
                return;
            }

            _ = Dispatcher.BeginInvoke(
                DispatcherPriority.DataBind,
                new Action(() =>
                {
                    Volatile.Write(ref _isLocationRefreshPending, 0);
                    RefreshCurrentLocation();
                })
            );
        }
    }

    private sealed class NavigationJournalEntry(NavigationEntry entry, NavigationRequest request)
    {
        internal NavigationEntry Entry { get; } = entry;

        internal NavigationRequest Request { get; } = request;

        internal IReadOnlyList<BreadcrumbSegment> BreadcrumbSnapshot { get; set; } = [];
    }
}
