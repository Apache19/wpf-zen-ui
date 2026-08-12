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
    public void BeginInit_PublicAndInterfacePathsDeferConfiguration()
    {
        var publicOptions = new ThemeManagerOptions();
        publicOptions.BeginInit();
        publicOptions.ThemeMode = UiThemeMode.Dark;

        Assert.True(GetPrivateFlag(publicOptions, "_isInitializing"));
        Assert.True(GetPrivateFlag(publicOptions, "_pendingApply"));

        var xamlOptions = new ThemeManagerOptions();
        ((ISupportInitialize)xamlOptions).BeginInit();
        xamlOptions.ThemeMode = UiThemeMode.Dark;

        Assert.True(GetPrivateFlag(xamlOptions, "_isInitializing"));
        Assert.True(GetPrivateFlag(xamlOptions, "_pendingApply"));
    }

    [Fact]
    public void NativeControlStyling_IsDisabledByDefault()
    {
        var options = new ThemeManagerOptions();

        Assert.False(options.EnableNativeControlStyling);
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
