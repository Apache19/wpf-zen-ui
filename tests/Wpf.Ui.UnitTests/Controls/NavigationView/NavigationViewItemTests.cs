// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using Wpf.Ui.Controls;

namespace Wpf.Ui.UnitTests.Controls.NavigationView;

public class NavigationViewItemTests
{
    private static readonly MethodInfo RemoveAccessKeyMarkersMethod = typeof(NavigationViewItem)
        .GetMethod("RemoveAccessKeyMarkers", BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("Date & Time")]
    [InlineData("Dialogs & flyouts")]
    [InlineData("File _ name")]
    [InlineData("Trailing &")]
    [InlineData("Punctuation &!")]
    public void RemoveAccessKeyMarkers_PreservesCharactersThatDoNotPrefixAccessKeys(string value)
    {
        Assert.Equal(value, RemoveAccessKeyMarkers(value));
    }

    [Theory]
    [InlineData("_File", "File")]
    [InlineData("E_xit", "Exit")]
    [InlineData("&Settings", "Settings")]
    [InlineData("文件(_F)", "文件F")]
    [InlineData("Save && Close", "Save & Close")]
    [InlineData("Save __ Close", "Save _ Close")]
    public void RemoveAccessKeyMarkers_RemovesValidMarkersAndUnescapesLiteralMarkers(
        string value,
        string expected
    )
    {
        Assert.Equal(expected, RemoveAccessKeyMarkers(value));
    }

    private static string RemoveAccessKeyMarkers(string value) =>
        (string)RemoveAccessKeyMarkersMethod.Invoke(null, [value])!;
}