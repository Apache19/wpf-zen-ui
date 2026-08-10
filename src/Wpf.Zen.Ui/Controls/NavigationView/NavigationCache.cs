// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

internal class NavigationCache
{
    private readonly Dictionary<Type, object?> _permanentEntries = [];
    private readonly Dictionary<Type, object?> _expirableEntries = [];
    private readonly LinkedList<Type> _expirableEntryUsage = [];

    public object? Remember(
        Type? entryType,
        NavigationCacheMode cacheMode,
        int cacheSize,
        Func<object?> generate)
    {
        if (entryType == null)
        {
            return null;
        }

        if (cacheMode == NavigationCacheMode.Disabled)
        {
            Remove(entryType);

            return generate.Invoke();
        }

        if (TryGetValue(entryType, cacheMode, out var value))
        {
            return value;
        }

        value = generate.Invoke();

        Remember(entryType, cacheMode, cacheSize, value);

        return value;
    }

    public void Remove(Type? entryType)
    {
        if (entryType is null)
        {
            return;
        }

        _permanentEntries.Remove(entryType);
        _expirableEntries.Remove(entryType);

        RemoveFromUsage(entryType);
    }

    /// <summary>Releases every page retained by this navigation host.</summary>
    /// <remarks>
    /// Page disposal remains the responsibility of the navigation dependency-injection scope. The
    /// cache must nevertheless drop its references when that scope ends so a later scope cannot
    /// reuse a page whose scoped services have already been disposed.
    /// </remarks>
    public void Clear()
    {
        _permanentEntries.Clear();
        _expirableEntries.Clear();
        _expirableEntryUsage.Clear();
    }

    public void UpdateCacheMode(
        Type? entryType,
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode,
        int cacheSize)
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

        if (oldCacheMode == NavigationCacheMode.Permanent
            && _permanentEntries.TryGetValue(entryType, out var permanentEntry))
        {
            _permanentEntries.Remove(entryType);
            Remember(entryType, newCacheMode, cacheSize, permanentEntry);
            return;
        }

        if (oldCacheMode == NavigationCacheMode.Evictable
            && _expirableEntries.TryGetValue(entryType, out var expirableEntry))
        {
            _expirableEntries.Remove(entryType);
            RemoveFromUsage(entryType);
            Remember(entryType, newCacheMode, cacheSize, expirableEntry);
            return;
        }

        Trim(cacheSize);
    }

    public void Trim(int cacheSize)
    {
        while (_expirableEntries.Count > cacheSize)
        {
            LinkedListNode<Type>? oldestEntryNode = _expirableEntryUsage.First;
            if (oldestEntryNode is null)
            {
                return;
            }

            _expirableEntries.Remove(oldestEntryNode.Value);
            _expirableEntryUsage.RemoveFirst();
        }
    }

    private bool TryGetValue(Type entryType, NavigationCacheMode cacheMode, out object? value)
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

    private void Remember(
        Type entryType,
        NavigationCacheMode cacheMode,
        int cacheSize,
        object? value)
    {
        if (cacheMode == NavigationCacheMode.Permanent)
        {
            _permanentEntries[entryType] = value;
            return;
        }

        if (cacheSize <= 0)
        {
            return;
        }

        _expirableEntries[entryType] = value;
        MoveToMostRecentlyUsed(entryType);
        Trim(cacheSize);
    }

    private void MoveToMostRecentlyUsed(Type entryType)
    {
        RemoveFromUsage(entryType);
        _expirableEntryUsage.AddLast(entryType);
    }

    private void RemoveFromUsage(Type entryType)
    {
        LinkedListNode<Type>? node = _expirableEntryUsage.Find(entryType);
        if (node is not null)
        {
            _expirableEntryUsage.Remove(node);
        }
    }
}
