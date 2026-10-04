// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using TreeView = System.Windows.Controls.TreeView;
using TreeViewItem = System.Windows.Controls.TreeViewItem;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies complete tree layout without advertising or activating hierarchical virtualization.
/// </summary>
public class StackPanelBoundaryTests
{
    /// <summary>
    /// Root and nested tree hosts generate expanded children even with virtualization requested.
    /// Collapsing and expanding a branch continues to update its ordinary layout size.
    /// </summary>
    [Theory]
    [InlineData(ScrollUnit.Pixel)]
    [InlineData(ScrollUnit.Item)]
    public void TreeHosts_UseCompleteLayout(ScrollUnit unit)
    {
        WpfTestThread.Run(() =>
        {
            var factory = new FrameworkElementFactory(typeof(UiVirtualizingStackPanel));
            factory.SetValue(UiVirtualizingStackPanel.SpacingProperty, 6D);
            var template = new ItemsPanelTemplate(factory);
            var tree = new TreeView { ItemsPanel = template };
            for (int index = 0; index < 3; index++)
            {
                var branch = new TreeViewItem
                {
                    Header = $"Branch {index}",
                    ItemsPanel = template,
                    IsExpanded = true,
                };
                for (int leaf = 0; leaf < 30; leaf++)
                {
                    branch.Items.Add($"Leaf {index}/{leaf}");
                }

                tree.Items.Add(branch);
            }

            VirtualizingPanel.SetIsVirtualizing(tree, true);
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(tree, true);
            VirtualizingPanel.SetScrollUnit(tree, unit);
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(tree, true);
            using var source = new HwndSource(new HwndSourceParameters("Stack boundary test")
            {
                Width = 300,
                Height = 200,
                WindowStyle = unchecked((int)0x80000000),
            });
            var grid = new Grid();
            grid.Children.Add(tree);
            source.RootVisual = grid;
            grid.Measure(new Size(300, 200));
            grid.Arrange(new Rect(0, 0, 300, 200));
            grid.UpdateLayout();
            UiVirtualizingStackPanel[] panels = Panels(tree).ToArray();
            Assert.Equal(4, panels.Length);
            Assert.All(panels, panel =>
            {
                Assert.False(panel.IsVirtualizationActive);
                Assert.False(panel.CanHierarchicallyScrollAndVirtualize);
            });
            Assert.Equal(93, panels.Sum(panel => panel.Children.Count));
            UiVirtualizingStackPanel root = panels[0];
            double expandedSize = root.Children[0].DesiredSize.Height;
            var first = (TreeViewItem)tree.Items[0];
            first.IsExpanded = false;
            tree.UpdateLayout();
            Assert.True(first.DesiredSize.Height < expandedSize);
            first.IsExpanded = true;
            tree.UpdateLayout();
            Assert.Equal(expandedSize, first.DesiredSize.Height, 3);
            Assert.All(Panels(tree), panel => Assert.False(panel.IsVirtualizationActive));
        });
    }

    private static IEnumerable<UiVirtualizingStackPanel> Panels(DependencyObject root)
    {
        if (root is UiVirtualizingStackPanel panel)
        {
            yield return panel;
        }

        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            foreach (UiVirtualizingStackPanel child in Panels(VisualTreeHelper.GetChild(root, index)))
            {
                yield return child;
            }
        }
    }
}
