// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationCacheTests
{
    [Fact]
    public void Remember_DoesNotEvictRequiredEntries_WhenEnabledCacheSizeIsExceeded()
    {
        var cache = new NavigationCacheAccessor();
        var requiredPage = new object();

        var firstRequiredResult = cache.Remember(
            typeof(RequiredPage),
            NavigationCacheMode.Required,
            cacheSize: 1,
            () => requiredPage
        );

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => new object()
        );
        _ = cache.Remember(
            typeof(SecondEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => new object()
        );

        var secondRequiredResult = cache.Remember(
            typeof(RequiredPage),
            NavigationCacheMode.Required,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(requiredPage, firstRequiredResult);
        Assert.Same(requiredPage, secondRequiredResult);
    }

    [Fact]
    public void Remember_EvictsLeastRecentlyUsedEnabledEntry_WhenEnabledCacheSizeIsExceeded()
    {
        var cache = new NavigationCacheAccessor();
        var firstPage = new object();
        var secondPage = new object();
        var recreatedFirstPage = new object();

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => firstPage
        );
        _ = cache.Remember(
            typeof(SecondEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => secondPage
        );

        var result = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => recreatedFirstPage
        );

        Assert.Same(recreatedFirstPage, result);
        Assert.NotSame(firstPage, result);
    }

    [Theory]
    [InlineData(NavigationCacheMode.Enabled)]
    [InlineData(NavigationCacheMode.Required)]
    public void UpdateCacheMode_RemovesEntry_WhenCacheModeChangesToDisabled(
        NavigationCacheMode oldCacheMode
    )
    {
        var cache = new NavigationCacheAccessor();
        var cachedPage = new object();
        var recreatedPage = new object();

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            oldCacheMode,
            cacheSize: 1,
            () => cachedPage
        );

        cache.UpdateCacheMode(
            typeof(FirstEnabledPage),
            oldCacheMode,
            NavigationCacheMode.Disabled,
            cacheSize: 1
        );

        var result = cache.Remember(
            typeof(FirstEnabledPage),
            oldCacheMode,
            cacheSize: 1,
            () => recreatedPage
        );

        Assert.Same(recreatedPage, result);
        Assert.NotSame(cachedPage, result);
    }

    [Theory]
    [InlineData(NavigationCacheMode.Enabled)]
    [InlineData(NavigationCacheMode.Required)]
    public void UpdateCacheMode_DoesNotCacheTemporaryEntry_WhenCacheModeChangesFromDisabled(
        NavigationCacheMode newCacheMode
    )
    {
        var cache = new NavigationCacheAccessor();
        var disabledPage = new object();
        var cachedPage = new object();

        var disabledResult = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Disabled,
            cacheSize: 1,
            () => disabledPage
        );

        cache.UpdateCacheMode(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Disabled,
            newCacheMode,
            cacheSize: 1
        );

        var firstCachedResult = cache.Remember(
            typeof(FirstEnabledPage),
            newCacheMode,
            cacheSize: 1,
            () => cachedPage
        );
        var secondCachedResult = cache.Remember(
            typeof(FirstEnabledPage),
            newCacheMode,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(disabledPage, disabledResult);
        Assert.Same(cachedPage, firstCachedResult);
        Assert.Same(cachedPage, secondCachedResult);
        Assert.NotSame(disabledPage, firstCachedResult);
    }

    [Theory]
    [InlineData(NavigationCacheMode.Enabled, NavigationCacheMode.Required)]
    [InlineData(NavigationCacheMode.Required, NavigationCacheMode.Enabled)]
    public void UpdateCacheMode_PreservesEntry_WhenCacheModeChangesBetweenEnabledAndRequired(
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode
    )
    {
        var cache = new NavigationCacheAccessor();
        var cachedPage = new object();

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            oldCacheMode,
            cacheSize: 1,
            () => cachedPage
        );

        cache.UpdateCacheMode(
            typeof(FirstEnabledPage),
            oldCacheMode,
            newCacheMode,
            cacheSize: 1
        );

        var result = cache.Remember(
            typeof(FirstEnabledPage),
            newCacheMode,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(cachedPage, result);
    }

    [Fact]
    public void UpdateCacheMode_MakesEntryEvictable_WhenCacheModeChangesFromRequiredToEnabled()
    {
        var cache = new NavigationCacheAccessor();
        var requiredPage = new object();
        var recreatedPage = new object();

        _ = cache.Remember(
            typeof(RequiredPage),
            NavigationCacheMode.Required,
            cacheSize: 1,
            () => requiredPage
        );

        cache.UpdateCacheMode(
            typeof(RequiredPage),
            NavigationCacheMode.Required,
            NavigationCacheMode.Enabled,
            cacheSize: 1
        );

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => new object()
        );

        var result = cache.Remember(
            typeof(RequiredPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => recreatedPage
        );

        Assert.Same(recreatedPage, result);
        Assert.NotSame(requiredPage, result);
    }

    [Fact]
    public void UpdateCacheMode_PreventsEntryEviction_WhenCacheModeChangesFromEnabledToRequired()
    {
        var cache = new NavigationCacheAccessor();
        var enabledPage = new object();

        _ = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => enabledPage
        );

        cache.UpdateCacheMode(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Enabled,
            NavigationCacheMode.Required,
            cacheSize: 1
        );

        _ = cache.Remember(
            typeof(SecondEnabledPage),
            NavigationCacheMode.Enabled,
            cacheSize: 1,
            () => new object()
        );

        var result = cache.Remember(
            typeof(FirstEnabledPage),
            NavigationCacheMode.Required,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(enabledPage, result);
    }

    private sealed class NavigationCacheAccessor
    {
        private static readonly Type CacheType = typeof(NavigationViewItem).Assembly.GetType(
            "Wpf.Zen.Ui.Controls.NavigationCache",
            throwOnError: true
        )!;

        private static readonly MethodInfo RememberMethod = CacheType.GetMethod(nameof(Remember))!;

        private static readonly MethodInfo UpdateCacheModeMethod = CacheType.GetMethod(
            nameof(UpdateCacheMode)
        )!;

        private readonly object _cache = Activator.CreateInstance(CacheType)!;

        public object? Remember(
            Type entryType,
            NavigationCacheMode cacheMode,
            int cacheSize,
            Func<object?> generate
        ) => RememberMethod.Invoke(_cache, [entryType, cacheMode, cacheSize, generate]);

        public void UpdateCacheMode(
            Type entryType,
            NavigationCacheMode oldCacheMode,
            NavigationCacheMode newCacheMode,
            int cacheSize
        ) => UpdateCacheModeMethod.Invoke(
            _cache,
            [entryType, oldCacheMode, newCacheMode, cacheSize]
        );
    }

    private sealed class RequiredPage;

    private sealed class FirstEnabledPage;

    private sealed class SecondEnabledPage;
}
