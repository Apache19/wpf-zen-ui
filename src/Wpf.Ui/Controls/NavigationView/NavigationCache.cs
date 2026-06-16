// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

using System.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal class NavigationCache
{
    private readonly Dictionary<Type, object?> _requiredEntries = [];
    private readonly Dictionary<Type, object?> _enabledEntries = [];
    private readonly LinkedList<Type> _enabledEntryUsage = [];

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

            Debug.WriteLine(
                $"Cache for {entryType} is disabled. Generating instance using action..."
            );

            return generate.Invoke();
        }

        if (TryGetValue(entryType, cacheMode, out var value))
        {
            Debug.WriteLine($"{entryType} found in cache.");

            return value;
        }

        Debug.WriteLine(
            $"{entryType} not found in cache, generating instance using action..."
        );

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

        _requiredEntries.Remove(entryType);
        _enabledEntries.Remove(entryType);

        RemoveFromUsage(entryType);
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

        if (oldCacheMode == NavigationCacheMode.Required
            && _requiredEntries.TryGetValue(entryType, out var requiredEntry))
        {
            _requiredEntries.Remove(entryType);
            Remember(entryType, newCacheMode, cacheSize, requiredEntry);
            return;
        }

        if (oldCacheMode == NavigationCacheMode.Enabled
            && _enabledEntries.TryGetValue(entryType, out var enabledEntry))
        {
            _enabledEntries.Remove(entryType);
            RemoveFromUsage(entryType);
            Remember(entryType, newCacheMode, cacheSize, enabledEntry);
            return;
        }

        Trim(cacheSize);
    }

    public void Trim(int cacheSize)
    {
        while (_enabledEntries.Count > cacheSize)
        {
            LinkedListNode<Type>? oldestEntryNode = _enabledEntryUsage.First;
            if (oldestEntryNode is null)
            {
                return;
            }

            _enabledEntries.Remove(oldestEntryNode.Value);
            _enabledEntryUsage.RemoveFirst();
        }
    }

    private bool TryGetValue(Type entryType, NavigationCacheMode cacheMode, out object? value)
    {
        if (cacheMode == NavigationCacheMode.Required)
        {
            return _requiredEntries.TryGetValue(entryType, out value);
        }

        if (_enabledEntries.TryGetValue(entryType, out value))
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
        if (cacheMode == NavigationCacheMode.Required)
        {
            _requiredEntries[entryType] = value;
            return;
        }

        if (cacheSize <= 0)
        {
            return;
        }

        _enabledEntries[entryType] = value;
        MoveToMostRecentlyUsed(entryType);
        Trim(cacheSize);
    }

    private void MoveToMostRecentlyUsed(Type entryType)
    {
        RemoveFromUsage(entryType);
        _enabledEntryUsage.AddLast(entryType);
    }

    private void RemoveFromUsage(Type entryType)
    {
        LinkedListNode<Type>? node = _enabledEntryUsage.Find(entryType);
        if (node is not null)
        {
            _enabledEntryUsage.Remove(node);
        }
    }
}
