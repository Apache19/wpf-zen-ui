// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Holds cache retention rights, completing internal transfers before releasing an independent
/// snapshot that permits reentrancy.
/// </summary>
internal sealed class NavigationCache(Action<object?>? release = null)
{
    private readonly Dictionary<Type, object?> _permanentEntries    = [];
    private readonly Dictionary<Type, object?> _expirableEntries    = [];
    private readonly LinkedList<Type>          _expirableEntryUsage = [];

    /// <summary>
    /// Gets an existing instance or creates a cache value; the caller retains the created value
    /// when caching is disabled.
    /// </summary>
    public object? Remember(Type? entryType, NavigationCacheMode cacheMode, int cacheSize, Func<object?> generate)
    {
        if (entryType is null)
        {
            return null;
        }

        if (cacheMode == NavigationCacheMode.Disabled)
        {
            Remove(entryType);
            return generate();
        }

        if (TryGetValue(entryType, cacheMode, out var existing))
        {
            return existing;
        }

        var value = generate();
        Remember(entryType, cacheMode, cacheSize, value);
        return value;
    }

    /// <summary>
    /// Revokes all retention rights for the type, updating dictionaries and usage order before
    /// release callbacks.
    /// </summary>
    public void Remove(Type? entryType)
    {
        if (entryType is null)
        {
            return;
        }

        var retired = new List<object?>();

        RemoveEntry(_permanentEntries, entryType, retired);
        RemoveEntry(_expirableEntries, entryType, retired);

        RemoveFromUsage(entryType);

        ReleaseAll(retired);
    }

    /// <summary>
    /// Atomically transfers current cache retention rights; reentrant additions survive previous
    /// cleanup.
    /// </summary>
    public void Clear()
    {
        var retired = new List<object?>(_permanentEntries.Values);
        retired.AddRange(_expirableEntries.Values);

        _permanentEntries.Clear();
        _expirableEntries.Clear();
        _expirableEntryUsage.Clear();

        ReleaseAll(retired);
    }

    /// <summary>
    /// Transfers the same retention right between permanent and LRU caches; disabling or capacity
    /// eviction relinquishes it.
    /// </summary>
    public void UpdateCacheMode(
        Type? entryType, NavigationCacheMode oldCacheMode, NavigationCacheMode newCacheMode, int cacheSize)
    {
        if (entryType is null || oldCacheMode == newCacheMode)
        {
            return;
        }

        if (newCacheMode == NavigationCacheMode.Disabled)
        {
            Remove(entryType);
            return;
        }

        var source = oldCacheMode == NavigationCacheMode.Permanent ? _permanentEntries : _expirableEntries;
        if (source.TryGetValue(entryType, out var value))
        {
            source.Remove(entryType);

            RemoveFromUsage(entryType);
            Remember(entryType, newCacheMode, cacheSize, value);

            return;
        }

        Trim(cacheSize);
    }

    /// <summary>
    /// Removes entries exceeding capacity in least recently used order, then delivers each release.
    /// </summary>
    public void Trim(int cacheSize)
    {
        var retired = new List<object?>();
        TrimCore(cacheSize, retired);
        ReleaseAll(retired);
    }

    /// <summary>
    /// Gets the requested entry and updates LRU order; permanent entries are exempt from capacity
    /// limits.
    /// </summary>
    internal bool TryGetValue(Type entryType, NavigationCacheMode cacheMode, out object? value)
    {
        if (cacheMode == NavigationCacheMode.Permanent)
        {
            return _permanentEntries.TryGetValue(entryType, out value);
        }

        if (_expirableEntries.TryGetValue(entryType, out value))
        {
            MoveToMostRecentlyUsed(entryType);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Accepts a retention right acquired by the caller, completing replacement and eviction before
    /// callbacks.
    /// </summary>
    internal void Remember(Type entryType, NavigationCacheMode cacheMode, int cacheSize, object? value)
    {
        var retired = new List<object?>();

        var destination = cacheMode == NavigationCacheMode.Permanent ? _permanentEntries : _expirableEntries;

        if (destination.TryGetValue(entryType, out var previous) && !ReferenceEquals(previous, value))
        {
            retired.Add(previous);
        }

        destination[entryType] = value;

        if (cacheMode != NavigationCacheMode.Permanent)
        {
            MoveToMostRecentlyUsed(entryType);
            TrimCore(cacheSize, retired);
        }

        ReleaseAll(retired);
    }

    /// <summary>
    /// Updates data structures without invoking external release logic that may stop or reattach
    /// the host.
    /// </summary>
    private void TrimCore(int cacheSize, List<object?> retired)
    {
        while (_expirableEntries.Count > cacheSize && _expirableEntryUsage.First is { } oldest)
        {
            RemoveEntry(_expirableEntries, oldest.Value, retired);
            _expirableEntryUsage.RemoveFirst();
        }
    }

    private static void RemoveEntry(Dictionary<Type, object?> entries, Type key, List<object?> retired)
    {
        if (entries.TryGetValue(key, out var value))
        {
            entries.Remove(key);
            retired.Add(value);
        }
    }

    /// <summary>
    /// Attempts every release in the transferred snapshot despite failures or reentrant callbacks.
    /// </summary>
    private void ReleaseAll(IEnumerable<object?> retired)
    {
        var failures = new NavigationFailures();

        foreach (var value in retired)
        {
            failures.Try(() => release?.Invoke(value));
        }

        failures.ThrowIfAny();
    }

    private void MoveToMostRecentlyUsed(Type entryType)
    {
        RemoveFromUsage(entryType);
        _expirableEntryUsage.AddLast(entryType);
    }

    private void RemoveFromUsage(Type entryType)
    {
        var node = _expirableEntryUsage.Find(entryType);
        if (node is not null)
        {
            _expirableEntryUsage.Remove(node);
        }
    }
}