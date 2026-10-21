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
    private List<NavigationJournalEntry> Journal        { get; } = [with(50)];
    private List<NavigationJournalEntry> ForwardJournal { get; } = [with(20)];

    private readonly NavigationCache          _cache               = new(ReleaseCachedInstance);
    private readonly HashSet<NavigationEntry> _unregisteredEntries = [];

    private INavigationViewPageProvider? _pageService;
    private INavigationService?          _navigationService;

    private NavigationSession?                _navigationSession;
    private NavigationPageInstance?           _currentInstance;
    private DependencyObject?                 _navigationContextOwner;
    private NavigationEntry?                  _currentEntry;
    private NavigationJournalEntry?           _currentOccurrence;
    private object?                           _currentPage;
    private FrameworkElement?                 _currentLocationElement;
    private INavigationLocationSource?        _currentLocationSource;
    private EventHandler?                     _currentLocationSourceHandler;
    private IReadOnlyList<BreadcrumbSegment>? _oneTimeBreadcrumbFallback;
    private int                               _isLocationRefreshPending;
    private NavigationRequestCoordinator?     _requests;
    private Task                              _detachedSessionCompletion = Task.CompletedTask;
    private long                              _locationRevision;
    private NavigationProjectionRetirement?   _retiringProjection;

    internal object? NavigationSessionIdentity => _navigationSession;

    /// <summary>
    /// Gets a value indicating whether the journal contains an earlier occurrence.
    /// </summary>
    public bool CanGoBack => Journal.Count > 1;

    /// <summary>
    /// Gets a value indicating whether the journal contains a later occurrence.
    /// </summary>
    public bool CanGoForward => ForwardJournal.Count > 0;

    /// <inheritdoc />
    public INavigationState NavigationState => this;

    /// <summary>
    /// Stops the current navigation session and awaits all work, error delivery, and owned resource
    /// disposal.
    /// </summary>
    /// <remarks>
    /// Host owners using control navigation directly should await this method before final
    /// shutdown.
    /// Callers with an explicit scope may instead await that scope's DisposeAsync.
    /// Call on the UI thread. Lifecycle callbacks should use their scope's RequestStop instead.
    /// </remarks>
    /// <returns>
    /// The stable current session completion task, or a completed task when detached.
    /// </returns>
    public Task StopNavigationAsync()
    {
        Dispatcher.VerifyAccess();

        var session = _navigationSession;
        if (session is null)
        {
            NavigationCallbackContext.VerifyCanWait(_detachedSessionCompletion);
            return _detachedSessionCompletion;
        }

        session.VerifyCanWait();
        session.RequestStop();

        return session.Completion;
    }

    /// <summary>
    /// Navigates to a page identified by its CLR type.
    /// </summary>
    /// <param name="pageType">The page type to activate.</param>
    /// <returns>
    /// <see langword="true"/> when the request is accepted; otherwise, <see langword="false"/>.
    /// </returns>
    public virtual bool Navigate(Type pageType)
    {
        var request = NavigationRequest.To(pageType);
        return PerformRequest(request).WasAccepted;
    }

    /// <summary>
    /// Navigates to a route, menu-item id, or target tag.
    /// </summary>
    /// <param name="routeKey">The exact semantic route or host lookup key.</param>
    /// <returns>
    /// <see langword="true"/> when the request is accepted; otherwise, <see langword="false"/>.
    /// </returns>
    public virtual bool Navigate(string routeKey)
    {
        var request = NavigationRequest.To(routeKey);
        return PerformRequest(request).WasAccepted;
    }

    /// <summary>
    /// Replaces frame content without changing semantic location, selection, or history.
    /// </summary>
    /// <param name="pageTypeToEmbed">The page type to create and display.</param>
    /// <returns><see langword="true"/> when the replacement request is accepted.</returns>
    public virtual bool ReplaceContent(Type? pageTypeToEmbed)
    {
        if (pageTypeToEmbed is null)
        {
            return false;
        }

        _ = EnsureNavigationSession();

        var result = _requests!.Submit(intent: NavigationIntent.Replace, contentType: pageTypeToEmbed);

        return result.WasAccepted;
    }

    /// <summary>
    /// Replaces frame content with an existing element without changing semantic state.
    /// </summary>
    /// <param name="pageInstanceToEmbed">The element to display.</param>
    /// <returns><see langword="true"/> when the replacement request is accepted.</returns>
    public virtual bool ReplaceContent(UIElement pageInstanceToEmbed)
    {
        _ = EnsureNavigationSession();

        var result = _requests!.Submit(intent: NavigationIntent.Replace, content: pageInstanceToEmbed);

        return result.WasAccepted;
    }

    /// <summary>
    /// Navigates to the next journal occurrence.
    /// </summary>
    /// <returns><see langword="true"/> when the forward request is accepted.</returns>
    public virtual bool GoForward()
    {
        _ = EnsureNavigationSession();
        return _requests!.Submit(intent: NavigationIntent.Forward).WasAccepted;
    }

    /// <summary>
    /// Navigates to the preceding journal occurrence.
    /// </summary>
    /// <returns><see langword="true"/> when the back request is accepted.</returns>
    public virtual bool GoBack()
    {
        _ = EnsureNavigationSession();

        return _requests!.Submit(intent: NavigationIntent.Back).WasAccepted;
    }

    /// <summary>
    /// Clears navigation history while retaining the current page and semantic location.
    /// </summary>
    public virtual void ClearJournal()
    {
        if (_requests is { } requests)
        {
            requests.Submit(intent: NavigationIntent.ClearHistory);
            return;
        }

        ClearJournalCore();
    }

    /// <summary>
    /// Clears history through the request queue so a pending back request cannot retain a deleted
    /// history position.
    /// </summary>
    private void ClearJournalCore()
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
    NavigationResult INavigationHost.Navigate(NavigationRequest request, CancellationToken cancellationToken)
    {
        _ = EnsureNavigationSession();
        return _requests!.Submit(request, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "RequestGoBack has the public overridable GoBack counterpart."
    )]
    NavigationResult INavigationHost.RequestGoBack(CancellationToken cancellationToken)
    {
        _ = EnsureNavigationSession();
        return _requests!.Submit(intent: NavigationIntent.Back, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "RequestGoForward has the public overridable GoForward counterpart."
    )]
    NavigationResult INavigationHost.RequestGoForward(CancellationToken cancellationToken)
    {
        _ = EnsureNavigationSession();
        return _requests!.Submit(intent: NavigationIntent.Forward, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "ClearJournal is the public overridable counterpart."
    )]
    void INavigationHost.ClearNavigationHistory()
    {
        ClearJournal();
    }

    /// <inheritdoc />
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1033:Interface methods should be callable by child types",
        Justification = "Only navigation-scope infrastructure establishes the page-provider lifetime boundary."
    )]
    void INavigationHost.AttachNavigationScope(
        INavigationService          navigationService,
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

        _navigationService         = navigationService;
        _detachedSessionCompletion = Task.CompletedTask;
        _pageService               = pageProvider;

        _navigationSession = (navigationService as NavigationService)?.Session
            ?? throw new InvalidOperationException(
                "The navigator must belong to a navigation session."
            );

        _requests = new NavigationRequestCoordinator(
            _navigationSession, Dispatcher, BeginNavigationRequest, _navigationFrame.StopLoading
        );

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

        _detachedSessionCompletion = _navigationSession!.Completion;

        var failures = new NavigationFailures();
        failures.Try(() => _requests?.Stop());
        failures.Try(RetirePreviousProjection);

        var visualDetach = _visualDetachOperation;
        _visualDetachOperation = null;

        failures.Try(() => visualDetach?.Abort());

        var reveal = _bringActiveBranchIntoViewOperation;
        _bringActiveBranchIntoViewOperation = null;

        failures.Try(() => reveal?.Abort());
        failures.Try(StopCurrentActivation);
        failures.Try(_navigationFrame.StopLoading);
        failures.Try(DetachCurrentLocationSource);

        var currentPage = _currentPage as FrameworkElement;
        failures.Try(() => ClearNavigationParent(currentPage));

        var contextOwner = _navigationContextOwner;
        _navigationContextOwner = null;
        if (contextOwner is not null)
        {
            failures.Try(() => Navigation.ClearContext(contextOwner));
        }

        var entry = _currentEntry;
        _currentEntry      = null;
        _currentPage       = null;
        _currentOccurrence = null;
        if (entry is not null)
        {
            failures.Try(() => entry.Deactivate(this));
        }

        Journal.Clear();
        ForwardJournal.Clear();
        _unregisteredEntries.Clear();

        failures.Try(_cache.Clear);

        var instance = _currentInstance;
        _currentInstance = null;

        failures.Try(() => instance?.Release());
        failures.Try(() => _navigationFrame.Navigate(null));
        failures.Try(() => SetSelectedItem(null));
        failures.Try(() => SetValue(CurrentLocationPropertyKey, null));
        failures.Try(_breadcrumbs.Clear);
        failures.Try(RebuildNavigationIndexes);
        failures.Try(UpdateBackNavigationState);
        failures.Try(() => UpdatePageHeader(null));

        // Transfers the host after clearing old projections. Notifications below may attach a new
        // session; old cleanup no longer writes host fields.
        _navigationService = null;
        _pageService       = null;
        _navigationSession = null;
        _requests          = null;

        Volatile.Write(ref _isLocationRefreshPending, 0);

        failures.Try(() => CurrentLocationChanged?.Invoke(this, EventArgs.Empty));

        var failure = failures.Exception;

        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }

    /// <summary>
    /// Creates the same session boundary for direct control callers; explicit scopes use the
    /// factory-supplied identity.
    /// </summary>
    private NavigationSession EnsureNavigationSession()
    {
        if (_navigationSession is null)
        {
            _ = NavigationScope.Attach(this);
        }

        return _navigationSession
            ?? throw new InvalidOperationException("The navigation host did not attach a session.");
    }

    /// <summary>
    /// Accepts a request and returns awaitable commit completion; the queue serializes commits
    /// without waiting for page initialization.
    /// </summary>
    private NavigationResult PerformRequest(NavigationRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        _ = EnsureNavigationSession();

        return _requests!.Submit(request);
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

    /// <summary>
    /// Menu and service entry points share the request queue, preventing callback requests from
    /// modifying history recursively.
    /// </summary>
    private bool NavigateInternal(NavigationEntry entry)
    {
        _ = EnsureNavigationSession();
        var result = _requests!.Submit(entry: entry);
        return result.WasAccepted;
    }

    /// <summary>
    /// Prepares candidate content and history intent without changing page context, location,
    /// history, or selection.
    /// </summary>
    private void BeginNavigationRequest(NavigationTransaction transaction)
    {
        if (transaction.Intent == NavigationIntent.ClearHistory)
        {
            ClearJournalCore();
            transaction.Finish(NavigationResult.Success);
            return;
        }

        if (transaction.Intent is NavigationIntent.Back or NavigationIntent.Forward)
        {
            var backwards = transaction.Intent == NavigationIntent.Back;
            if ((backwards && Journal.Count < 2) || (!backwards && ForwardJournal.Count == 0))
            {
                transaction.Finish(NavigationResult.Rejected);
                return;
            }

            var occurrence = backwards ? Journal[^2] : ForwardJournal[^1];
            transaction.Occurrence = occurrence;
            transaction.Entry      = occurrence.Entry;
            transaction.Request    = occurrence.Request;

            if (backwards)
            {
                OnBackRequested();
            }
        }
        else if (transaction.Intent != NavigationIntent.Replace)
        {
            ResolveRequestTarget(transaction);
        }

        if (!IsCurrentRequest(transaction))
        {
            return;
        }

        if (transaction.Intent == NavigationIntent.Replace)
        {
            if (transaction.Content is null && transaction.ContentType is { } contentType)
            {
                transaction.Instance = CreatePageInstance(contentType, transaction);
            }
            else
            {
                var borrowed = transaction.Content as FrameworkElement
                    ?? throw new InvalidCastException(
                        "Navigation content must derive from FrameworkElement."
                    );

                var lease = NavigationPageLease.Borrowed(borrowed);
                var work  = transaction.AcquireCleanupWork();

                transaction.Instance = transaction.Session.RetainPage(lease, work);
            }

            transaction.Content = transaction.Instance.Page;
        }
        else
        {
            var entry   = transaction.Entry;
            var request = transaction.Request;

            if (entry?.TargetPageType is null || request is null)
            {
                transaction.Finish(NavigationResult.TargetNotFound);
                return;
            }

            if (transaction.Intent == NavigationIntent.Navigate && IsRedundantRequest(entry, request))
            {
                transaction.Finish(NavigationResult.Rejected);
                return;
            }

            transaction.Instance = GetNavigationItemInstance(entry, transaction);
            transaction.Content  = transaction.Instance.Page;
        }

        if (!IsCurrentRequest(transaction))
        {
            return;
        }

        var content = transaction.Content
            ?? throw new InvalidOperationException("A navigation request requires content.");

        var cancelled = OnNavigating(content);
        if (!IsCurrentRequest(transaction))
        {
            return;
        }

        if (cancelled || transaction.CancellationToken.IsCancellationRequested)
        {
            transaction.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
            return;
        }

        transaction.TransitionSettings = GetNavigationTransitionSettings(content);

        var accepted = _navigationFrame.Navigate(content, transaction);
        if (!accepted && !transaction.IsCommitted)
        {
            transaction.Finish(new NavigationResult(NavigationResultStatus.Cancelled));
        }
    }

    /// <summary>
    /// Resolves route and menu identity; temporary targets enter the host index only after commit.
    /// </summary>
    private void ResolveRequestTarget(NavigationTransaction transaction)
    {
        var request = transaction.Request;
        if (request is null && transaction.Entry is { } suppliedEntry)
        {
            request = CreateRequestForEntry(suppliedEntry);
        }

        if (request is null)
        {
            throw new InvalidOperationException("A semantic navigation request requires a target.");
        }

        var mappedNode = NavigationMap.FindNode(request.Target);
        if (request.Target.RouteKey is null && mappedNode is not null)
        {
            var mappedTarget = new NavigationTarget(
                request.Target.PageType ?? mappedNode.Target?.PageType,
                mappedNode.Key,
                request.Target.Parameter
            );

            request = request.WithTarget(mappedTarget);
        }

        var entry = transaction.Entry ?? ResolveNavigationEntry(request.Target);
        if (entry is not null)
        {
            var menuLookup = request.Target.RouteKey == entry.Id
                || request.Target.RouteKey == entry.TargetPageTag;

            if (mappedNode is null && menuLookup && entry.TargetPageType is { } entryPageType)
            {
                var pageTarget = NavigationTarget.ForPage(entryPageType);

                mappedNode = NavigationMap.FindNode(pageTarget);

                if (mappedNode is not null)
                {
                    var mappedTarget = new NavigationTarget(entryPageType, mappedNode.Key, request.Target.Parameter);
                    request = request.WithTarget(mappedTarget);
                }
            }
        }
        else
        {
            var pageType = request.Target.PageType ?? mappedNode?.Target?.PageType;
            if (pageType is not null)
            {
                entry = new NavigationEntry(new NavigationItem(pageType.Name, pageType), false);
            }
        }

        transaction.Entry   = entry;
        transaction.Request = request;
    }

    /// <summary>
    /// Checks session and request identity after each external callback so a stopped outer request
    /// cannot affect a new session.
    /// </summary>
    private bool IsCurrentRequest(NavigationTransaction transaction)
    {
        return ReferenceEquals(_navigationSession, transaction.Session)
            && !transaction.Session.IsStopping
            && ReferenceEquals(_requests?.Current, transaction)
            && !transaction.Completion.IsCompleted;
    }

    /// <summary>
    /// Establishes complete internal state after Frame commit, then publishes UI projections and
    /// external notifications in stages.
    /// </summary>
    private void CommitNavigationRequest(NavigationTransaction transaction)
    {
        transaction.IsCommitted = true;

        var page             = transaction.Content!;
        var previousInstance = _currentInstance;

        _currentInstance = transaction.Instance!;
        _currentInstance.Retain();

        var previousPage  = _currentPage;
        var previousEntry = _currentEntry;

        var replacement = transaction.Intent == NavigationIntent.Replace;

        _currentPage = page;

        if (!replacement)
        {
            var entry   = transaction.Entry!;
            var request = transaction.Request!;

            _currentEntry = entry;

            if (transaction.Intent == NavigationIntent.Back)
            {
                ForwardJournal.Add(_currentOccurrence!);

                Journal.RemoveAt(Journal.Count - 1);

                _currentOccurrence         = transaction.Occurrence!;
                _oneTimeBreadcrumbFallback = _currentOccurrence.BreadcrumbSnapshot;
            }
            else if (transaction.Intent == NavigationIntent.Forward)
            {
                _currentOccurrence = transaction.Occurrence!;

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

            if (!_entriesByItem.Values.Contains(entry))
            {
                _unregisteredEntries.Add(entry);
                IndexUnregisteredNavigationEntries();
            }
        }

        // The current record now belongs to the new page; retain cleanup obligations for old
        // projections separately.
        // If exit or property notifications reenter stop, detachment takes over remaining steps
        // before a new session may attach.
        _retiringProjection = new NavigationProjectionRetirement();

        if (!replacement)
        {
            _retiringProjection.Add(() => previousEntry?.Deactivate(this));
        }

        if (!ReferenceEquals(previousPage, page))
        {
            _retiringProjection.Add(() => ClearNavigationParent(previousPage as FrameworkElement));
        }

        var failures = new NavigationFailures();
        failures.Try(StopCurrentActivation);
        failures.Try(RetirePreviousProjection);
        failures.Try(() => RememberCommittedInstance(transaction));

        // After Frame commit, attempt every independent projection update even if a user
        // notification throws.
        void Publish(Action action)
        {
            if (IsCurrentRequest(transaction))
            {
                failures.Try(action);
            }
        }

        if (!replacement)
        {
            Publish(() => transaction.Entry!.Activate(this));
        }

        if (page is FrameworkElement element)
        {
            Publish(() => element.SetValue(NavigationParentProperty, this));
        }

        var parameter = _currentOccurrence?.Request.Target.Parameter;

        Publish(() => SetNavigationContextOwner(page as DependencyObject, parameter));
        Publish(() => AttachCurrentLocationSource(page));
        Publish(RefreshCurrentLocation);

        if (_currentOccurrence is { } committedOccurrence
            && (!ReferenceEquals(CurrentLocation?.Page, page)
                || !ReferenceEquals(CurrentLocation?.Target, committedOccurrence.Request.Target)))
        {
            // A location extension failure must still reflect committed Frame state; the request
            // result preserves the failure.
            var unresolved = new NavigationLocation(committedOccurrence.Request.Target, page, []);

            Publish(() => SetValue(CurrentLocationPropertyKey, unresolved));
            Publish(_breadcrumbs.Clear);
            Publish(() => CurrentLocationChanged?.Invoke(this, EventArgs.Empty));
        }

        Publish(UpdateBackNavigationState);

        if (!replacement)
        {
            var entry         = transaction.Entry!;
            var liveMenuEntry = entry.IsMenuElement && _entriesByItem.Values.Contains(entry);
            var selected      = liveMenuEntry ? entry.Item : null;

            if (!ReferenceEquals(SelectedItem, selected))
            {
                Publish(() => SetSelectedItem(selected));
                Publish(OnSelectionChanged);
            }
        }

        Publish(() => OnNavigated(page));
        Publish(QueueActiveNavigationBranchIntoView);
        failures.Try(() => previousInstance?.Release());
        failures.ThrowIfAny();
    }

    /// <summary>
    /// Drains transferred projection cleanup, dequeuing each step first so reentrant detachment can
    /// finish the remainder.
    /// </summary>
    private void RetirePreviousProjection()
    {
        var retirement = _retiringProjection;
        if (retirement is null)
        {
            return;
        }

        try
        {
            retirement.Drain();
        }
        finally
        {
            if (ReferenceEquals(retirement, _retiringProjection))
            {
                _retiringProjection = null;
            }
        }
    }

    /// <summary>
    /// Retains old page and menu projection cleanup independently of the new page publication
    /// identity.
    /// </summary>
    private sealed class NavigationProjectionRetirement
    {
        private readonly Queue<Action> _steps = new();

        internal void Add(Action step)
        {
            _steps.Enqueue(step);
        }

        internal void Drain()
        {
            var failures = new NavigationFailures();
            while (_steps.Count > 0)
            {
                var step = _steps.Dequeue();
                failures.Try(step);
            }

            failures.ThrowIfAny();
        }
    }

    /// <summary>
    /// Compares resolved destinations while preserving occurrence parameters and explicit path
    /// overrides. Menu lookup aliases do not create a new occurrence of the current destination.
    /// </summary>
    private bool IsRedundantRequest(NavigationEntry entry, NavigationRequest request)
    {
        if (!ReferenceEquals(_currentEntry, entry) || _currentOccurrence is null)
        {
            return false;
        }

        var current         = _currentOccurrence.Request;
        var currentPageType = current.Target.PageType ?? entry.TargetPageType;
        var requestedType   = request.Target.PageType ?? entry.TargetPageType;
        var currentRoute    = GetDestinationRouteKey(current.Target, entry);
        var requestedRoute  = GetDestinationRouteKey(request.Target, entry);

        return currentRoute == requestedRoute
            && currentPageType == requestedType
            && Equals(current.Target.Parameter, request.Target.Parameter)
            && request.Breadcrumbs is null
            && request.BreadcrumbParent is null
            && request.BreadcrumbContent is null;
    }

    /// <summary>
    /// Resolves route identity for duplicate detection without changing the request retained by
    /// location, breadcrumbs, or history.
    /// </summary>
    /// <remarks>
    /// A map placement is semantic identity, even when its key also matches a menu id or tag.
    /// Otherwise, an entry's id and tag are only lookup aliases for its page type. Type-only
    /// requests and menu invocations therefore compare equally after resolving to the same entry.
    /// Explicit route keys that are not menu aliases remain distinct destinations.
    /// </remarks>
    private string? GetDestinationRouteKey(NavigationTarget target, NavigationEntry entry)
    {
        var mappedNode = NavigationMap.FindNode(target);
        if (mappedNode is not null)
        {
            return mappedNode.Key;
        }

        var routeKey = target.RouteKey;
        if (routeKey == entry.Id || routeKey == entry.TargetPageTag)
        {
            return null;
        }

        return routeKey;
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

    /// <summary>
    /// Restores code-only targets as fallback lookup entries after a menu rebuild.
    /// </summary>
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

    private void UpdateBackNavigationState()
    {
        SetValue(IsBackEnabledPropertyKey, CanGoBack);
    }

    private NavigationPageInstance GetNavigationItemInstance(
        NavigationEntry entry, NavigationTransaction transaction)
    {
        var targetPageType = entry.TargetPageType;
        if (targetPageType is null)
        {
            throw new InvalidOperationException(
                $"The {nameof(entry)}.{nameof(entry.TargetPageType)} property cannot be null."
            );
        }

        if (entry.NavigationCacheMode != NavigationCacheMode.Disabled
            && _cache.TryGetValue(targetPageType, entry.NavigationCacheMode, out var cached))
        {
            var instance = (NavigationPageInstance)cached!;
            instance.Retain();
            return instance;
        }

        return CreatePageInstance(targetPageType, transaction);
    }

    /// <summary>
    /// Only committed pages acquire cache retention rights; request completion releases uncommitted
    /// candidates.
    /// </summary>
    private void RememberCommittedInstance(NavigationTransaction transaction)
    {
        if (transaction.Intent == NavigationIntent.Replace || !IsCurrentRequest(transaction))
        {
            return;
        }

        var entry    = transaction.Entry!;
        var pageType = entry.TargetPageType!;
        var mode     = entry.NavigationCacheMode;
        if (mode == NavigationCacheMode.Disabled)
        {
            _cache.Remove(pageType);
            return;
        }

        var instance = transaction.Instance!;
        if (_cache.TryGetValue(pageType, mode, out var cached) && ReferenceEquals(cached, instance))
        {
            return;
        }

        instance.Retain();
        _cache.Remember(pageType, mode, NavigationCacheSize, instance);
    }

    private static void ReleaseCachedInstance(object? value)
    {
        if (value is NavigationPageInstance instance)
        {
            instance.Release();
        }
    }

    internal void NotifyNavigationCacheModeChanged(
        NavigationEntry     entry,
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode)
    {
        _cache.UpdateCacheMode(entry.TargetPageType, oldCacheMode, newCacheMode, NavigationCacheSize);
    }

    /// <summary>
    /// Migrates cache state after mutable menu metadata changes.
    /// </summary>
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

        entry.LastKnownTargetPageType      = newPageType;
        entry.LastKnownNavigationCacheMode = newCacheMode;
    }

    /// <summary>
    /// Resolves a page instance for the given type via the attached page provider, or the
    /// container-free activator when no provider is attached. The navigation cache is consulted by
    /// <see cref="GetNavigationItemInstance"/> before this factory is invoked.
    /// </summary>
    private NavigationPageInstance CreatePageInstance(Type targetPageType, NavigationTransaction transaction)
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

        var provider = _pageService ?? throw new InvalidOperationException("The page provider is detached.");

        var lease = provider.GetPage(targetPageType)
            ?? throw new InvalidOperationException(
                $"The page provider {provider.GetType().Name} returned null for {targetPageType}."
            );

        var work = transaction.AcquireCleanupWork();

        return transaction.Session.RetainPage(lease, work);
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

    /// <summary>
    /// Moves scope context from the exited page root to the destination page root.
    /// </summary>
    private void SetNavigationContextOwner(
        DependencyObject? newOwner,
        object?           navigationParameter = null)
    {
        var session = _navigationSession;
        var service = _navigationService;

        var failures = new NavigationFailures();

        if (!ReferenceEquals(_navigationContextOwner, newOwner))
        {
            var previous = _navigationContextOwner;
            _navigationContextOwner = null;

            if (previous is not null)
            {
                failures.Try(() => Navigation.ClearContext(previous));
            }

            if (!ReferenceEquals(session, _navigationSession) || session is null || session.IsStopping)
            {
                failures.ThrowIfAny();
                return;
            }

            _navigationContextOwner = newOwner;
        }

        if (newOwner is not null && service is not null && session is { IsStopping: false })
        {
            failures.Try(() => Navigation.SetContext(newOwner, service, this, navigationParameter));
        }

        failures.ThrowIfAny();
    }

    /// <summary>
    /// Moves map notifications and resolves the active occurrence against the new graph.
    /// </summary>
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

    private void OnNavigationMapChanged(object? sender, EventArgs e)
    {
        RefreshCurrentLocationOnDispatcher();
    }

    /// <summary>
    /// Resolves the current occurrence using deterministic request, page, map, and fallback
    /// priority.
    /// </summary>
    /// <remarks>
    /// The resolver never infers hierarchy from the previous journal entry or from menu nesting.
    /// Anonymous pages therefore produce a single leaf unless a caller, page source, or map
    /// supplies
    /// more information. This prevents ordinary A → D navigation from being misrepresented as a
    /// semantic A → D relationship.
    /// </remarks>
    internal void RefreshCurrentLocation()
    {
        var session    = _navigationSession;
        var occurrence = _currentOccurrence;
        var page       = _currentPage;

        if (session is null || session.IsStopping || occurrence is null || page is null)
        {
            return;
        }

        using var work     = session.Work.Acquire();
        var       revision = ++_locationRevision;

        bool IsCurrent()
        {
            return ReferenceEquals(session, _navigationSession) && !session.IsStopping
                && ReferenceEquals(occurrence, _currentOccurrence) && ReferenceEquals(page, _currentPage)
                && revision == _locationRevision;
        }

        var request    = occurrence.Request;
        var context    = new NavigationLocationContext(request.Target, page);
        var descriptor = _currentLocationSource?.GetNavigationLocation(context);

        if (!IsCurrent())
        {
            return;
        }

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
                var combined   = new List<BreadcrumbSegment>(NavigationMap.ResolvePath(parent, context));
                var leaf       = CreateLeafSegment(request.Target, fallbackLeafContent);
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

        var immutablePath = SnapshotResolvedPath(resolved);

        if (!IsCurrent())
        {
            return;
        }

        _oneTimeBreadcrumbFallback = null;

        occurrence.BreadcrumbSnapshot = immutablePath;

        var location = new NavigationLocation(request.Target, page, immutablePath);
        var failures = new NavigationFailures();

        failures.Try(() => SetValue(CurrentLocationPropertyKey, location));

        if (IsCurrent())
        {
            failures.Try(() => _breadcrumbs.ReplaceWith(immutablePath, IsCurrent));
        }

        if (IsCurrent())
        {
            failures.Try(() => CurrentLocationChanged?.Invoke(this, EventArgs.Empty));
        }

        failures.ThrowIfAny();
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
        var session  = _navigationSession;
        var failures = new NavigationFailures();

        failures.Try(DetachCurrentLocationSource);

        if (session is null || session.IsStopping || !ReferenceEquals(session, _navigationSession))
        {
            failures.ThrowIfAny();
            return;
        }

        _currentLocationElement = page as FrameworkElement;

        if (_currentLocationElement is not null)
        {
            _currentLocationElement.DataContextChanged += OnCurrentPageDataContextChanged;
        }

        _currentLocationSource = page as INavigationLocationSource
            ?? _currentLocationElement?.DataContext as INavigationLocationSource;

        if (_currentLocationSource is not null)
        {
            var source = _currentLocationSource;

            EventHandler handler = (_, _) =>
            {
                if (ReferenceEquals(session, _navigationSession)
                    && ReferenceEquals(source, _currentLocationSource) && !session.IsStopping)
                {
                    RefreshCurrentLocationOnDispatcher();
                }
            };

            _currentLocationSourceHandler = handler;

            failures.Try(() => source.LocationChanged += handler);

            if (!ReferenceEquals(source, _currentLocationSource)
                || !ReferenceEquals(handler, _currentLocationSourceHandler))
            {
                // A subscription accessor may reenter stop before completing its addition; finish
                // the transfer after it returns.
                failures.Try(() => source.LocationChanged -= handler);
            }
        }

        failures.ThrowIfAny();
    }

    private void DetachCurrentLocationSource()
    {
        var element = _currentLocationElement;
        var source  = _currentLocationSource;
        var handler = _currentLocationSourceHandler;

        _currentLocationElement       = null;
        _currentLocationSource        = null;
        _currentLocationSourceHandler = null;

        var failures = new NavigationFailures();

        if (element is not null)
        {
            failures.Try(() => element.DataContextChanged -= OnCurrentPageDataContextChanged);
        }

        if (source is not null && handler is not null)
        {
            failures.Try(() => source.LocationChanged -= handler);
        }

        failures.ThrowIfAny();
    }

    private void OnCurrentPageDataContextChanged(
        object                             sender,
        DependencyPropertyChangedEventArgs e)
    {
        var session = _navigationSession;
        if (session is { IsStopping: false } && _currentPage is { } page
            && ReferenceEquals(sender, _currentLocationElement))
        {
            using var work = session.Work.Acquire();

            AttachCurrentLocationSource(page);

            if (ReferenceEquals(session, _navigationSession) && !session.IsStopping)
            {
                RefreshCurrentLocation();
            }
        }
    }

    /// <summary>
    /// Marshals model notifications because maps and view models are ordinary .NET objects and may
    /// raise their change events from a worker thread. Each host resolves on its own Dispatcher, so
    /// the same read-only map can safely notify navigation surfaces in different windows.
    /// </summary>
    private void RefreshCurrentLocationOnDispatcher()
    {
        var session = _navigationSession;
        if (session is null || session.IsStopping)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            RefreshCurrentLocation();
        }
        else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
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
                    if (ReferenceEquals(session, _navigationSession) && !session.IsStopping)
                    {
                        RefreshCurrentLocation();
                    }
                })
            );
        }
    }
}