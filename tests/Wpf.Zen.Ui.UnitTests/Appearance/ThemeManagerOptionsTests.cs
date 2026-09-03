// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Reflection;
using Wpf.Zen.Ui.Appearance;

namespace Wpf.Zen.Ui.UnitTests.Appearance;

public class ThemeManagerOptionsTests
{
    [Fact]
    public void Preference_IsLightByDefault()
    {
        var options = new ThemeManagerOptions();

        Assert.Equal(ThemePreference.Light, options.Preference);
    }

    [Fact]
    public void BeginInit_PublicAndInterfacePathsDeferConfiguration()
    {
        var publicOptions = new ThemeManagerOptions();
        publicOptions.BeginInit();
        publicOptions.Preference = ThemePreference.Dark;

        Assert.True(GetPrivateFlag(publicOptions, "_isInitializing"));
        Assert.True(GetPrivateFlag(publicOptions, "_pendingApply"));

        var xamlOptions = new ThemeManagerOptions();
        ((ISupportInitialize)xamlOptions).BeginInit();
        xamlOptions.Preference = ThemePreference.Dark;

        Assert.True(GetPrivateFlag(xamlOptions, "_isInitializing"));
        Assert.True(GetPrivateFlag(xamlOptions, "_pendingApply"));
    }

    private static bool GetPrivateFlag(ThemeManagerOptions options, string fieldName)
    {
        var field = typeof(ThemeManagerOptions).GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        return Assert.IsType<bool>(field?.GetValue(options));
    }
}
