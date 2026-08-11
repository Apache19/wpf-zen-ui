// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Wpf.Zen.Ui;

/// <summary>
/// Stores the application's default semantic route graph independently from its navigation menus.
/// </summary>
/// <remarks>
/// <para>
/// The map is intentionally not an <see cref="System.Windows.Controls.ItemsControl"/> source. A
/// menu can be filtered, reordered, duplicated, or replaced with shortcuts without changing this
/// graph. Conversely, structural map nodes can exist without occupying space in any menu.
/// </para>
/// <para>
/// Parent links form the path used by breadcrumbs. The API accepts incomplete graphs and detects
/// cycles during resolution so transient runtime edits cannot hang the UI. Changes are observable;
/// an attached navigation host recomputes its current location without changing journal history.
/// </para>
/// <para>
/// The map implements <see cref="IList{T}"/> so nodes can be declared directly as XAML
/// children. <see cref="NavigationMapNode.PageType"/> provides a concise destination declaration
/// without requiring XAML to construct an immutable <see cref="NavigationTarget"/>.
/// </para>
/// </remarks>
public sealed class NavigationMap : IList<NavigationMapNode>, System.Collections.IList
{
    private readonly Dictionary<string, NavigationMapNode> _nodes = new(StringComparer.Ordinal);
    private readonly ObservableCollection<NavigationMapNode> _observableNodes = [];
    private int _updateDepth;
    private bool _hasDeferredChange;

    /// <summary>Initializes a new instance of the <see cref="NavigationMap"/> class.</summary>
    public NavigationMap()
    {
        Nodes = new ReadOnlyObservableCollection<NavigationMapNode>(_observableNodes);
    }

    /// <summary>Gets the live, read-only sequence of registered placements.</summary>
    public ReadOnlyObservableCollection<NavigationMapNode> Nodes { get; }

    /// <inheritdoc />
    public int Count => _nodes.Count;

    /// <inheritdoc />
    public bool IsReadOnly => false;

    /// <summary>Gets or replaces the placement at the specified declaration-order index.</summary>
    /// <param name="index">The zero-based placement index.</param>
    /// <returns>The placement at <paramref name="index"/>.</returns>
    /// <remarks>
    /// Replacing an item updates key lookup and change subscriptions as one map mutation. The
    /// replacement must not belong to another map or duplicate another registered key.
    /// </remarks>
    public NavigationMapNode this[int index]
    {
        get => _observableNodes[index];
        set
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            NavigationMapNode oldNode = _observableNodes[index];
            if (ReferenceEquals(oldNode, value))
            {
                return;
            }

            ValidateNodeForInsertion(value, oldNode);
            value.AttachTo(this);

            oldNode.PropertyChanged -= OnNodePropertyChanged;
            oldNode.DetachFrom(this);
            _nodes.Remove(oldNode.Key);

            _nodes.Add(value.Key, value);
            value.PropertyChanged += OnNodePropertyChanged;
            _observableNodes[index] = value;
            NotifyChanged();
        }
    }

    /// <summary>Occurs after nodes are added, removed, reordered semantically, or mutated.</summary>
    public event EventHandler<EventArgs>? Changed;

    /// <summary>Adds a semantic placement to the map.</summary>
    /// <param name="node">The node to add.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The node has no key, or another node already uses the same key.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The node already belongs to another map.
    /// </exception>
    public void Add(NavigationMapNode node)
    {
        Insert(_observableNodes.Count, node);
    }

    /// <summary>Inserts a semantic placement at a declaration-order index.</summary>
    /// <param name="index">The zero-based insertion index.</param>
    /// <param name="node">The node to insert.</param>
    /// <exception cref="ArgumentNullException"><paramref name="node"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The node has no key, or another node already uses the same key.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The node already belongs to another map.
    /// </exception>
    public void Insert(int index, NavigationMapNode node)
    {
        if (index < 0 || index > _observableNodes.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        ValidateNodeForInsertion(node);
        node.AttachTo(this);
        _nodes.Add(node.Key, node);
        node.PropertyChanged += OnNodePropertyChanged;
        _observableNodes.Insert(index, node);
        NotifyChanged();
    }

    /// <summary>Creates and adds a semantic placement to the map.</summary>
    /// <param name="key">The stable route key used by children and explicit targets.</param>
    /// <param name="content">The default breadcrumb content.</param>
    /// <param name="target">The optional destination; omit it for a structural node.</param>
    /// <param name="parentKey">The optional key of the semantic parent.</param>
    /// <param name="isBreadcrumbVisible">
    /// Whether this node contributes a segment to resolved breadcrumb paths. Hidden nodes remain
    /// in the semantic graph and are still traversed when resolving descendants.
    /// </param>
    /// <returns>The created node, which can subsequently be configured or moved.</returns>
    public NavigationMapNode Add(
        string key,
        object? content,
        NavigationTarget? target = null,
        string? parentKey = null,
        bool isBreadcrumbVisible = true)
    {
        var node = new NavigationMapNode(key, content, target, parentKey)
        {
            IsBreadcrumbVisible = isBreadcrumbVisible,
        };
        Add(node);
        return node;
    }

    /// <summary>Creates and adds a page placement to the map.</summary>
    /// <typeparam name="TPage">The page type activated by the placement.</typeparam>
    /// <param name="key">The stable route key used by children and explicit targets.</param>
    /// <param name="content">The default breadcrumb content.</param>
    /// <param name="parentKey">The optional key of the semantic parent.</param>
    /// <param name="isBreadcrumbVisible">
    /// Whether this node contributes a segment to resolved breadcrumb paths. Hidden nodes remain
    /// in the semantic graph and are still traversed when resolving descendants.
    /// </param>
    /// <returns>The created node, which can subsequently be configured or moved.</returns>
    public NavigationMapNode Add<TPage>(
        string key,
        object? content,
        string? parentKey = null,
        bool isBreadcrumbVisible = true) =>
        Add(
            key,
            content,
            new NavigationTarget(typeof(TPage), key),
            parentKey,
            isBreadcrumbVisible
        );

    /// <summary>Performs a group of map mutations and raises <see cref="Changed"/> at most once.</summary>
    /// <param name="update">The synchronous set of changes to perform.</param>
    /// <remarks>
    /// Use this when importing or moving several routes as one logical operation. Active hosts then
    /// resolve only the final graph instead of observing temporary intermediate hierarchies. Node
    /// collection notifications remain ordinary <see cref="ObservableCollection{T}"/> notifications;
    /// this method batches semantic-location invalidation, not presentation of <see cref="Nodes"/>.
    /// Nested updates are supported.
    /// </remarks>
    public void Update(Action<NavigationMap> update)
    {
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        _updateDepth++;
        try
        {
            update(this);
        }
        finally
        {
            _updateDepth--;
            if (_updateDepth == 0 && _hasDeferredChange)
            {
                _hasDeferredChange = false;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>Removes a placement without recursively deleting its children.</summary>
    /// <param name="key">The exact route key to remove.</param>
    /// <returns><see langword="true"/> when a node was removed.</returns>
    /// <remarks>
    /// Children deliberately retain their parent keys. Until they are moved, resolution presents
    /// the suffix beginning at each child, which makes partial maps and staged runtime edits safe.
    /// </remarks>
    public bool Remove(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A navigation-map key cannot be empty.", nameof(key));
        }

        if (!_nodes.TryGetValue(key, out NavigationMapNode? node))
        {
            return false;
        }

        _nodes.Remove(key);
        node.PropertyChanged -= OnNodePropertyChanged;
        _observableNodes.Remove(node);
        node.DetachFrom(this);
        NotifyChanged();
        return true;
    }

    /// <summary>Removes the specified placement when this map owns that exact instance.</summary>
    /// <param name="node">The placement to remove.</param>
    /// <returns><see langword="true"/> when the placement was removed.</returns>
    public bool Remove(NavigationMapNode node)
    {
        if (node is null)
        {
            return false;
        }

        return _nodes.TryGetValue(node.Key, out NavigationMapNode? registered)
            && ReferenceEquals(node, registered)
            && Remove(node.Key);
    }

    /// <summary>Removes the placement at a declaration-order index.</summary>
    /// <param name="index">The zero-based index of the placement to remove.</param>
    public void RemoveAt(int index) => Remove(_observableNodes[index].Key);

    /// <summary>Removes every placement from the map.</summary>
    public void Clear()
    {
        if (_nodes.Count == 0)
        {
            return;
        }

        foreach (NavigationMapNode node in _observableNodes)
        {
            node.PropertyChanged -= OnNodePropertyChanged;
            node.DetachFrom(this);
        }

        _nodes.Clear();
        _observableNodes.Clear();
        NotifyChanged();
    }

    /// <summary>Attempts to get a placement by its exact route key.</summary>
    /// <param name="key">The route key to find.</param>
    /// <param name="node">Receives the registered node when found.</param>
    /// <returns><see langword="true"/> when the key is registered.</returns>
    public bool TryGetNode(string key, out NavigationMapNode node)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            node = null!;
            return false;
        }

        return _nodes.TryGetValue(key, out node!);
    }

    /// <summary>Determines whether this map owns the specified placement instance.</summary>
    /// <param name="node">The placement to locate.</param>
    /// <returns><see langword="true"/> when the exact placement belongs to this map.</returns>
    public bool Contains(NavigationMapNode node) =>
        node is not null
        && _nodes.TryGetValue(node.Key, out NavigationMapNode? registered)
        && ReferenceEquals(node, registered);

    /// <summary>Returns the declaration-order index of an exact placement instance.</summary>
    /// <param name="node">The placement to locate.</param>
    /// <returns>The zero-based index, or <c>-1</c> when the placement does not belong to this map.</returns>
    public int IndexOf(NavigationMapNode node) => _observableNodes.IndexOf(node);

    /// <summary>Copies the registered placements to an existing array.</summary>
    /// <param name="array">The destination array.</param>
    /// <param name="arrayIndex">The zero-based destination offset.</param>
    public void CopyTo(NavigationMapNode[] array, int arrayIndex) =>
        _observableNodes.CopyTo(array, arrayIndex);

    /// <summary>Returns an enumerator over placements in declaration order.</summary>
    /// <returns>An enumerator over the map.</returns>
    public IEnumerator<NavigationMapNode> GetEnumerator() => _observableNodes.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    bool System.Collections.IList.IsFixedSize => false;

    bool System.Collections.IList.IsReadOnly => false;

    bool System.Collections.ICollection.IsSynchronized => false;

    object System.Collections.ICollection.SyncRoot =>
        ((System.Collections.ICollection)_observableNodes).SyncRoot;

    object? System.Collections.IList.this[int index]
    {
        get => this[index];
        set => this[index] = GetTypedNode(value);
    }

    int System.Collections.IList.Add(object? value)
    {
        Add(GetTypedNode(value));
        return Count - 1;
    }

    bool System.Collections.IList.Contains(object? value) =>
        value is NavigationMapNode node && Contains(node);

    int System.Collections.IList.IndexOf(object? value) =>
        value is NavigationMapNode node ? IndexOf(node) : -1;

    void System.Collections.IList.Insert(int index, object? value) =>
        Insert(index, GetTypedNode(value));

    void System.Collections.IList.Remove(object? value)
    {
        if (value is NavigationMapNode node)
        {
            Remove(node);
        }
    }

    void System.Collections.ICollection.CopyTo(Array array, int index) =>
        ((System.Collections.ICollection)_observableNodes).CopyTo(array, index);

    /// <summary>Finds the placement selected by a route key or page type.</summary>
    /// <param name="target">The semantic target to locate.</param>
    /// <returns>The matching node, or <see langword="null"/> when the map has no placement.</returns>
    /// <remarks>
    /// Route identity always wins. A type-only target selects the first registered placement for
    /// that type; applications with aliases should navigate by route key when placement matters.
    /// </remarks>
    public NavigationMapNode? FindNode(NavigationTarget target)
    {
        if (target is null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (target.RouteKey is { } routeKey && _nodes.TryGetValue(routeKey, out NavigationMapNode? keyedNode))
        {
            return keyedNode;
        }

        if (target.RouteKey is { } targetRouteKey)
        {
            NavigationMapNode? aliasedNode = _observableNodes.FirstOrDefault(
                node => node.Target?.RouteKey == targetRouteKey
            );
            return aliasedNode;
        }

        return target.PageType is { } pageType
            ? _observableNodes.FirstOrDefault(node => node.Target?.PageType == pageType)
            : null;
    }

    internal IReadOnlyList<BreadcrumbSegment> ResolvePath(
        NavigationTarget target,
        NavigationLocationContext context,
        object? leafContentOverride = null)
    {
        NavigationMapNode? leaf = FindNode(target);
        if (leaf is null)
        {
            return [];
        }

        var reversedNodes = new List<NavigationMapNode>();
        var visitedKeys = new HashSet<string>(StringComparer.Ordinal);
        NavigationMapNode? current = leaf;

        while (current is not null && visitedKeys.Add(current.Key))
        {
            reversedNodes.Add(current);
            current = current.ParentKey is { } parentKey && _nodes.TryGetValue(parentKey, out NavigationMapNode? parent)
                ? parent
                : null;
        }

        reversedNodes.Reverse();

        // Visibility filters the projection, not traversal. Build the complete semantic chain
        // first so descendants can resolve through any number of hidden organizational nodes.
        NavigationMapNode[] visibleNodes = reversedNodes
            .Where(static node => node.IsBreadcrumbVisible)
            .ToArray();
        var result = new BreadcrumbSegment[visibleNodes.Length];
        for (int index = 0; index < visibleNodes.Length; index++)
        {
            NavigationMapNode node = visibleNodes[index];
            object? content = ReferenceEquals(node, leaf) && leafContentOverride is not null
                ? leafContentOverride
                : node.ContentResolver?.Invoke(context) ?? node.Content;

            NavigationTarget? segmentTarget = node.Target;
            if (ReferenceEquals(node, leaf))
            {
                // Preserve occurrence-specific parameters on the active leaf. Parent nodes keep
                // their registered targets because their parameters describe separate locations.
                // A route-only lookup must not accidentally turn a structural map node into a
                // clickable breadcrumb; it has no page destination even though its key was used
                // to locate the path prefix.
                if (segmentTarget is not null || target.PageType is not null)
                {
                    segmentTarget = new NavigationTarget(
                        target.PageType ?? segmentTarget?.PageType,
                        node.Key,
                        target.Parameter
                    );
                }
            }
            else if (segmentTarget is not null && segmentTarget.RouteKey is null)
            {
                segmentTarget = new NavigationTarget(segmentTarget.PageType, node.Key, segmentTarget.Parameter);
            }

            result[index] = new BreadcrumbSegment(node.Key, content, segmentTarget);
        }

        return result;
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e) => NotifyChanged();

    private void ValidateNodeForInsertion(
        NavigationMapNode? node,
        NavigationMapNode? nodeBeingReplaced = null)
    {
        if (node is null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        if (string.IsNullOrWhiteSpace(node.Key))
        {
            throw new ArgumentException(
                "A navigation-map node must declare a non-empty key before it is added.",
                nameof(node)
            );
        }

        if (_nodes.TryGetValue(node.Key, out NavigationMapNode? existing)
            && !ReferenceEquals(existing, nodeBeingReplaced))
        {
            throw new ArgumentException(
                $"A navigation-map node with key '{node.Key}' already exists.",
                nameof(node)
            );
        }
    }

    private static NavigationMapNode GetTypedNode(object? value) =>
        value as NavigationMapNode
        ?? throw new ArgumentException(
            $"A navigation map accepts only {nameof(NavigationMapNode)} values.",
            nameof(value)
        );

    private void NotifyChanged()
    {
        if (_updateDepth > 0)
        {
            _hasDeferredChange = true;
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
