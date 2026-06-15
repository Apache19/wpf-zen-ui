// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal class NavigationCache
{
    private sealed class CacheEntry(object value, NavigationCacheMode cacheMode)
    {
        public object Value { get; } = value;

        public NavigationCacheMode CacheMode { get; set; } = cacheMode;

        public LinkedListNode<NavigationKey>? EnabledNode { get; set; }
    }

    private readonly Dictionary<NavigationKey, CacheEntry> _entries = [];

    private readonly LinkedList<NavigationKey> _enabledEntries = [];

    private int _capacity = 10;

    public int Capacity
    {
        get => _capacity;
        set
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            _capacity = value;
            TrimEnabledEntries();
        }
    }

    public object? Remember(NavigationKey key, NavigationCacheMode cacheMode, Func<object?> generate)
    {
        if (cacheMode == NavigationCacheMode.Disabled)
        {
            Remove(key);

            System.Diagnostics.Debug.WriteLine(
                $"Cache for {key} is disabled. Generating instance using action..."
            );

            return generate.Invoke();
        }

        if (_entries.TryGetValue(key, out var entry))
        {
            System.Diagnostics.Debug.WriteLine($"{key} found in cache.");

            UpdateCacheMode(key, entry, cacheMode);
            return entry.Value;
        }

        System.Diagnostics.Debug.WriteLine(
            $"{key} not found in cache, generating instance using action..."
        );

        var value = generate.Invoke();
        if (value is null)
        {
            return null;
        }

        entry = new CacheEntry(value, cacheMode);
        _entries.Add(key, entry);

        if (cacheMode == NavigationCacheMode.Enabled)
        {
            entry.EnabledNode = _enabledEntries.AddLast(key);
            TrimEnabledEntries();
        }

        return value;
    }

    public void SetMode(NavigationKey key, NavigationCacheMode cacheMode)
    {
        if (cacheMode == NavigationCacheMode.Disabled)
        {
            Remove(key);
            return;
        }

        if (_entries.TryGetValue(key, out var entry))
        {
            UpdateCacheMode(key, entry, cacheMode);
        }
    }

    public bool Remove(NavigationKey key)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        _entries.Remove(key);

        if (entry.EnabledNode is not null)
        {
            _enabledEntries.Remove(entry.EnabledNode);
        }

        return true;
    }

    public void Clear()
    {
        _entries.Clear();
        _enabledEntries.Clear();
    }

    private void UpdateCacheMode(
        NavigationKey key,
        CacheEntry entry,
        NavigationCacheMode cacheMode
    )
    {
        if (entry.CacheMode == cacheMode)
        {
            if (entry.EnabledNode is not null)
            {
                _enabledEntries.Remove(entry.EnabledNode);
                entry.EnabledNode = _enabledEntries.AddLast(key);
            }

            return;
        }

        if (entry.EnabledNode is not null)
        {
            _enabledEntries.Remove(entry.EnabledNode);
            entry.EnabledNode = null;
        }

        entry.CacheMode = cacheMode;

        if (cacheMode == NavigationCacheMode.Enabled)
        {
            entry.EnabledNode = _enabledEntries.AddLast(key);
            TrimEnabledEntries();
        }
    }

    private void TrimEnabledEntries()
    {
        while (_enabledEntries.Count > Capacity)
        {
            var key = _enabledEntries.First!.Value;
            _enabledEntries.RemoveFirst();
            _entries.Remove(key);
        }
    }
}
