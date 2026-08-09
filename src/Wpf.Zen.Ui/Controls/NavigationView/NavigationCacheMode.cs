// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies caching characteristics for a page involved in a navigation.
/// </summary>
public enum NavigationCacheMode
{
    /// <summary>
    /// The page is never cached and a new instance is created on each visit.
    /// </summary>
    Disabled,

    /// <summary>
    /// The page is cached and reused across visits, and may be evicted under LRU policy
    /// when the number of cached pages exceeds <see cref="NavigationView.NavigationCacheSize"/>.
    /// </summary>
    Evictable,

    /// <summary>
    /// The page is cached and reused across visits. The instance is never evicted
    /// and does not count toward <see cref="NavigationView.NavigationCacheSize"/>.
    /// </summary>
    Permanent,
}
