// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls;

public class NavigationKeyTests
{
    [Fact]
    public void EqualPageTypeAndVariant_ProduceEqualKeys()
    {
        var first = new NavigationKey(typeof(string), "primary");
        var second = new NavigationKey(typeof(string), "primary");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void DifferentVariants_ProduceDifferentKeys()
    {
        var first = new NavigationKey(typeof(string), "primary");
        var second = new NavigationKey(typeof(string), "secondary");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void EmptyVariant_IsNormalizedToDefaultDestination()
    {
        var defaultKey = new NavigationKey(typeof(string));
        var emptyVariantKey = new NavigationKey(typeof(string), " ");

        Assert.Equal(defaultKey, emptyVariantKey);
        Assert.Null(emptyVariantKey.Variant);
    }
}
