// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

public class NavigationCacheTests
{
    [Fact]
    public void Remember_DoesNotEvictPermanentEntries_WhenExpirableCacheSizeIsExceeded()
    {
        var cache = new NavigationCache();
        var permanentPage = new object();

        var firstPermanentResult = cache.Remember(
            typeof(PermanentPage),
            NavigationCacheMode.Permanent,
            cacheSize: 1,
            () => permanentPage
        );

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => new object()
        );
        _ = cache.Remember(
            typeof(SecondExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => new object()
        );

        var secondPermanentResult = cache.Remember(
            typeof(PermanentPage),
            NavigationCacheMode.Permanent,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(permanentPage, firstPermanentResult);
        Assert.Same(permanentPage, secondPermanentResult);
    }

    [Fact]
    public void Remember_EvictsLeastRecentlyUsedExpirableEntry_WhenExpirableCacheSizeIsExceeded()
    {
        var cache = new NavigationCache();
        var firstPage = new object();
        var secondPage = new object();
        var recreatedFirstPage = new object();

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => firstPage
        );
        _ = cache.Remember(
            typeof(SecondExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => secondPage
        );

        var result = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => recreatedFirstPage
        );

        Assert.Same(recreatedFirstPage, result);
        Assert.NotSame(firstPage, result);
    }

    [Theory]
    [InlineData(NavigationCacheMode.Evictable)]
    [InlineData(NavigationCacheMode.Permanent)]
    public void UpdateCacheMode_RemovesEntry_WhenCacheModeChangesToDisabled(
        NavigationCacheMode oldCacheMode
    )
    {
        var cache = new NavigationCache();
        var cachedPage = new object();
        var recreatedPage = new object();

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            oldCacheMode,
            cacheSize: 1,
            () => cachedPage
        );

        cache.UpdateCacheMode(
            typeof(FirstExpirablePage),
            oldCacheMode,
            NavigationCacheMode.Disabled,
            cacheSize: 1
        );

        var result = cache.Remember(
            typeof(FirstExpirablePage),
            oldCacheMode,
            cacheSize: 1,
            () => recreatedPage
        );

        Assert.Same(recreatedPage, result);
        Assert.NotSame(cachedPage, result);
    }

    [Theory]
    [InlineData(NavigationCacheMode.Evictable)]
    [InlineData(NavigationCacheMode.Permanent)]
    public void UpdateCacheMode_DoesNotCacheTemporaryEntry_WhenCacheModeChangesFromDisabled(
        NavigationCacheMode newCacheMode
    )
    {
        var cache = new NavigationCache();
        var disabledPage = new object();
        var cachedPage = new object();

        var disabledResult = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Disabled,
            cacheSize: 1,
            () => disabledPage
        );

        cache.UpdateCacheMode(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Disabled,
            newCacheMode,
            cacheSize: 1
        );

        var firstCachedResult = cache.Remember(
            typeof(FirstExpirablePage),
            newCacheMode,
            cacheSize: 1,
            () => cachedPage
        );
        var secondCachedResult = cache.Remember(
            typeof(FirstExpirablePage),
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
    [InlineData(NavigationCacheMode.Evictable, NavigationCacheMode.Permanent)]
    [InlineData(NavigationCacheMode.Permanent, NavigationCacheMode.Evictable)]
    public void UpdateCacheMode_PreservesEntry_WhenCacheModeChangesBetweenExpirableAndPermanent(
        NavigationCacheMode oldCacheMode,
        NavigationCacheMode newCacheMode
    )
    {
        var cache = new NavigationCache();
        var cachedPage = new object();

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            oldCacheMode,
            cacheSize: 1,
            () => cachedPage
        );

        cache.UpdateCacheMode(
            typeof(FirstExpirablePage),
            oldCacheMode,
            newCacheMode,
            cacheSize: 1
        );

        var result = cache.Remember(
            typeof(FirstExpirablePage),
            newCacheMode,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(cachedPage, result);
    }

    [Fact]
    public void UpdateCacheMode_MakesEntryEvictable_WhenCacheModeChangesFromPermanentToExpirable()
    {
        var cache = new NavigationCache();
        var permanentPage = new object();
        var recreatedPage = new object();

        _ = cache.Remember(
            typeof(PermanentPage),
            NavigationCacheMode.Permanent,
            cacheSize: 1,
            () => permanentPage
        );

        cache.UpdateCacheMode(
            typeof(PermanentPage),
            NavigationCacheMode.Permanent,
            NavigationCacheMode.Evictable,
            cacheSize: 1
        );

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => new object()
        );

        var result = cache.Remember(
            typeof(PermanentPage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => recreatedPage
        );

        Assert.Same(recreatedPage, result);
        Assert.NotSame(permanentPage, result);
    }

    [Fact]
    public void UpdateCacheMode_PreventsEntryEviction_WhenCacheModeChangesFromExpirableToPermanent()
    {
        var cache = new NavigationCache();
        var expirablePage = new object();

        _ = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => expirablePage
        );

        cache.UpdateCacheMode(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Evictable,
            NavigationCacheMode.Permanent,
            cacheSize: 1
        );

        _ = cache.Remember(
            typeof(SecondExpirablePage),
            NavigationCacheMode.Evictable,
            cacheSize: 1,
            () => new object()
        );

        var result = cache.Remember(
            typeof(FirstExpirablePage),
            NavigationCacheMode.Permanent,
            cacheSize: 1,
            () => new object()
        );

        Assert.Same(expirablePage, result);
    }

    private sealed class PermanentPage;

    private sealed class FirstExpirablePage;

    private sealed class SecondExpirablePage;
}
