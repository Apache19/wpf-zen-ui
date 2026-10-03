// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using TextBlock = System.Windows.Controls.TextBlock;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies direct element presentation, host lifetimes, and rejection of conflicting ownership.
/// </summary>
public class PanelItemsTests
{
    /// <summary>
    /// Verifies unloading, mutation while detached, reloading, and replacement of the visual host.
    /// </summary>
    [Fact]
    public void HostReloadAndReplacementReleaseElementsAndRetainStaticChildren()
    {
        WpfTestThread.Run(() =>
        {
            var first = new TextBlock { Text = "First" };
            var second = new TextBlock { Text = "Second" };
            var source = new ObservableCollection<UIElement> { first, second };
            var staticChild = new TextBlock { Text = "Static" };
            var panel = new Grid();
            panel.Children.Add(staticChild);
            PanelItems.SetSource(panel, source);
            Assert.Single(panel.Children);
            var window = CreateWindow(panel);
            try
            {
                window.Show();
                DrainDispatcher();
                Assert.Equal(new UIElement[] { staticChild, first, second }, panel.Children.Cast<UIElement>());
                window.Content = null;
                DrainDispatcher();
                Assert.Single(panel.Children);
                Assert.Null(VisualTreeHelper.GetParent(first));
                var third = new TextBlock { Text = "Third" };
                source.Remove(first);
                source.Add(third);
                Assert.Null(VisualTreeHelper.GetParent(third));
                window.Content = panel;
                DrainDispatcher();
                Assert.Equal(new UIElement[] { staticChild, second, third }, panel.Children.Cast<UIElement>());

                var replacement = new Grid();
                var replacementStatic = new TextBlock { Text = "Replacement static" };
                replacement.Children.Add(replacementStatic);
                PanelItems.SetSource(replacement, source);
                window.Content = replacement;
                DrainDispatcher();
                Assert.Single(panel.Children);
                Assert.Equal(new UIElement[] { replacementStatic, second, third }, replacement.Children.Cast<UIElement>());
                PanelItems.SetSource(replacement, null);
                Assert.Single(replacement.Children);
                Assert.Null(LogicalTreeHelper.GetParent(second));
                source.Clear();
                Assert.Single(replacement.Children);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    /// <summary>
    /// Verifies that complete validation precedes mutation and another owner's element is retained.
    /// </summary>
    [Fact]
    public void InvalidSourcesDoNotPartiallyReplaceChildrenOrStealElements()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new Grid();
            var staticChild = new TextBlock();
            var original = new TextBlock();
            panel.Children.Add(staticChild);
            PanelItems.SetSource(panel, new[] { original });
            var otherOwner = new Grid();
            var occupied = new TextBlock();
            otherOwner.Children.Add(occupied);
            var window = CreateWindow(panel);
            try
            {
                window.Show();
                DrainDispatcher();
                var available = new TextBlock();
                Assert.Throws<InvalidOperationException>(() => PanelItems.SetSource(panel, new[] { available, occupied }));
                Assert.Null(VisualTreeHelper.GetParent(available));
                Assert.Same(otherOwner, VisualTreeHelper.GetParent(occupied));
                Assert.Equal(new UIElement[] { staticChild, original }, panel.Children.Cast<UIElement>());
                Assert.Throws<InvalidOperationException>(() => PanelItems.SetSource(panel, new[] { available, available }));
                Assert.Throws<InvalidOperationException>(() => PanelItems.SetSource(panel, new[] { staticChild }));
                Assert.Throws<InvalidOperationException>(() => PanelItems.SetSource(panel, new UIElement[] { panel }));
                PanelItems.SetSource(panel, new[] { available });
                Assert.Same(panel, VisualTreeHelper.GetParent(available));
                Assert.Null(VisualTreeHelper.GetParent(original));
                Assert.Same(staticChild, panel.Children[0]);
            }
            finally
            {
                window.Close();
                DrainDispatcher();
            }
        });
    }

    private static Window CreateWindow(object content)
    {
        return new Window
        {
            Content = content,
            Width = 400,
            Height = 200,
            Left = -10_000,
            Top = -10_000,
            ShowActivated = false,
            ShowInTaskbar = false,
        };
    }

    private static void DrainDispatcher()
    {
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
    }
}
