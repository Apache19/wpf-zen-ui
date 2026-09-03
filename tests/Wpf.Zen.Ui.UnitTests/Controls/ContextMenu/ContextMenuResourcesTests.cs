// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class ContextMenuResourcesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_StylesInternalEditorMenuWithoutDispatcherCallback(bool useLoadComponent)
    {
        WpfTestThread.Run(() =>
        {
            var uri = new Uri(
                "/Wpf.Zen.Ui;component/Controls/ContextMenu/ContextMenu.xaml",
                UriKind.Relative
            );
            var resources = useLoadComponent
                ? (ResourceDictionary)Application.LoadComponent(uri)
                : new ResourceDictionary { Source = uri };
            var editorType = typeof(ContextMenu).Assembly.GetType(
                "System.Windows.Documents.TextEditorContextMenu+EditorContextMenu",
                throwOnError: true
            )!;
            var style = Assert.IsType<Style>(resources[typeof(ContextMenu)]);

            Assert.Same(style, resources[editorType]);

            // Exercise actual implicit-style lookup on WPF's private subclass, without pumping
            // the dispatcher. Loading the dictionary must make the alias immediately available.
            var menu = (ContextMenu)Activator.CreateInstance(editorType, nonPublic: true)!;
            menu.Resources.MergedDictionaries.Add(resources);
            Assert.Same(style, menu.Style);
        });
    }
}
