// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationMaps;

public class NavigationMapViewTests
{
    [Fact]
    public void DefaultStyle_ProjectsLocalizedContentAndCreatesMapPanel()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            };
            var map = new Wpf.Zen.Ui.NavigationMap
            {
                new NavigationMapNode(
                    "root",
                    "Localized root",
                    description: "Explains the localized root feature."),
            };
            var view = new NavigationMapView
            {
                Direction = NavigationMapDirection.LeftToRight,
                ItemsSource = map,
            };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);

            MeasureAndArrange(host, 800D, 600D);

            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(map[0])
            );
            Assert.Equal(view.NodeWidth, container.ActualWidth);
            var panel = Assert.IsType<NavigationMapPanel>(
                FindVisualDescendant<NavigationMapPanel>(view)
            );
            Assert.Equal(NavigationMapDirection.LeftToRight, panel.Direction);
            Assert.Equal("Explains the localized root feature.", container.ToolTip);
            Assert.Equal(
                "Explains the localized root feature.",
                AutomationProperties.GetHelpText(container)
            );
            map[0].Description = "Updated localized help.";
            Assert.Equal("Updated localized help.", container.Description);
            Assert.Equal("Updated localized help.", container.ToolTip);
            Assert.Equal("Updated localized help.", AutomationProperties.GetHelpText(container));
            Assert.Contains(
                EnumerateVisualDescendants<ContentPresenter>(container),
                presenter => Equals(presenter.Content, "Localized root")
            );
            AccessText label = Assert.Single(
                EnumerateVisualDescendants<AccessText>(container),
                accessText => accessText.Text == "Localized root"
            );
            Assert.Equal(TextWrapping.Wrap, label.TextWrapping);
        });
    }

    [Fact]
    public void NavigationMapView_ParsesDirectNodeItemsFromXaml()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = """
                <ui:NavigationMapView
                    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:ui="http://schemas.thezendev.com/wpf/2027"
                    Direction="LeftToRight">
                    <ui:NavigationMapNode Key="root" Content="Localized root" />
                    <ui:NavigationMapNode
                        Key="child"
                        ParentKey="root"
                        Content="Localized child"
                        Description="Explains what the child page does." />
                </ui:NavigationMapView>
                """;

            var view = Assert.IsType<NavigationMapView>(XamlReader.Parse(xaml));

            Assert.Equal(2, view.Items.Count);
            Assert.Equal(NavigationMapDirection.LeftToRight, view.Direction);
            var child = Assert.IsType<NavigationMapNode>(view.Items[1]);
            Assert.Equal("Localized child", child.Content);
            Assert.Equal("Explains what the child page does.", child.Description);
            Assert.Equal("root", child.ParentKey);
        });
    }

    [Fact]
    public void DefaultStyle_PreservesObjectContentTemplateFallback()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            };
            var customContent = new Border { Tag = "custom-content" };
            var node = new NavigationMapNode("root", customContent);
            var view = new NavigationMapView { ItemsSource = new[] { node } };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);

            MeasureAndArrange(host, 800D, 600D);

            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(node)
            );
            Assert.Contains(
                EnumerateVisualDescendants<Border>(container),
                border => ReferenceEquals(border, customContent)
            );
        });
    }

    [Fact]
    public void Invocation_RaisesNodeEventAndHonorsHandledBeforeExecutingCommand()
    {
        WpfTestThread.Run(() =>
        {
            var node = new NavigationMapNode("root", "Root");
            var command = new RecordingCommand();
            var view = new NavigationMapView
            {
                ItemsSource = new[] { node },
                Command = command,
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };
            bool handleInvocation = false;
            int invocationCount = 0;
            view.NodeInvoked += (sender, args) =>
            {
                Assert.Same(view, sender);
                Assert.Same(node, args.Item);
                Assert.Same(node, args.Node);
                invocationCount++;
                args.Handled = handleInvocation;
            };

            MeasureAndArrange(view, 800D, 600D);
            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(node)
            );

            container.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, container));
            handleInvocation = true;
            container.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, container));

            Assert.Equal(2, invocationCount);
            Assert.Equal(1, command.ExecutionCount);
            Assert.Same(node, command.Parameter);
        });
    }

    [Fact]
    public void NavigationMap_IsDirectlyObservableByItemsControl()
    {
        WpfTestThread.Run(() =>
        {
            var map = new Wpf.Zen.Ui.NavigationMap();
            var actions = new List<NotifyCollectionChangedAction>();
            map.CollectionChanged += (_, args) => actions.Add(args.Action);

            var view = new NavigationMapView { ItemsSource = map };
            var root = new NavigationMapNode("root", "Localized root");
            var child = new NavigationMapNode("child", "Localized child", parentKey: "root");

            map.Add(root);
            map.Add(child);
            map.Remove(root);

            Assert.Single(view.Items);
            Assert.Same(child, view.Items[0]);
            Assert.Equal(
                new[]
                {
                    NotifyCollectionChangedAction.Add,
                    NotifyCollectionChangedAction.Add,
                    NotifyCollectionChangedAction.Remove,
                },
                actions
            );
        });
    }

    [Fact]
    public void NavigationMap_MovePreservesGeneratedContainerAndEmitsOneMove()
    {
        WpfTestThread.Run(() =>
        {
            var map = new Wpf.Zen.Ui.NavigationMap
            {
                new NavigationMapNode("first", "First"),
                new NavigationMapNode("second", "Second"),
                new NavigationMapNode("third", "Third"),
            };
            var actions = new List<NotifyCollectionChangedAction>();
            map.CollectionChanged += (_, args) => actions.Add(args.Action);
            var view = new NavigationMapView
            {
                ItemsSource = map,
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };

            MeasureAndArrange(view, 800D, 600D);
            DependencyObject originalContainer = Assert.IsAssignableFrom<DependencyObject>(
                view.ItemContainerGenerator.ContainerFromItem(map[0])
            );

            map.Move(0, 2);
            MeasureAndArrange(view, 800D, 600D);

            Assert.Equal(new[] { "second", "third", "first" }, map.Select(node => node.Key));
            Assert.Equal(new[] { NotifyCollectionChangedAction.Move }, actions);
            Assert.Same(
                originalContainer,
                view.ItemContainerGenerator.ContainerFromItem(map[2])
            );
        });
    }

    [Fact]
    public void GeneratedContainers_TrackNodeIdentityAndRuntimeReparenting()
    {
        WpfTestThread.Run(() =>
        {
            var map = new Wpf.Zen.Ui.NavigationMap
            {
                new NavigationMapNode("root", "Root"),
                new NavigationMapNode("child", "Child", parentKey: "root"),
            };
            var view = new NavigationMapView
            {
                ItemsSource = map,
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };

            MeasureAndArrange(view, 800D, 600D);

            var childContainer = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(map[1])
            );
            Assert.Equal("child", childContainer.NodeKey);
            Assert.Equal("root", childContainer.ParentKey);
            Assert.Equal(1, childContainer.Depth);
            Assert.False(childContainer.IsRoot);
            Assert.Equal(1, childContainer.PositionInSet);
            Assert.Equal(1, childContainer.SizeOfSet);

            map[1].ParentKey = "missing";
            MeasureAndArrange(view, 800D, 600D);

            Assert.Equal("missing", childContainer.ParentKey);
            Assert.Equal(0, childContainer.Depth);
            Assert.True(childContainer.IsRoot);
            Assert.Equal(2, childContainer.PositionInSet);
            Assert.Equal(2, childContainer.SizeOfSet);
        });
    }

    [Fact]
    public void Panel_PromotesEveryCycleParticipantToARoot()
    {
        WpfTestThread.Run(() =>
        {
            var first = new NavigationMapItem
            {
                NodeKey = "a",
                ParentKey = "b",
                Width = 120D,
                Height = 48D,
            };
            var second = new NavigationMapItem
            {
                NodeKey = "b",
                ParentKey = "a",
                Width = 120D,
                Height = 48D,
            };
            var panel = new NavigationMapPanel
            {
                HorizontalSpacing = 20D,
                VerticalSpacing = 30D,
            };
            panel.Children.Add(first);
            panel.Children.Add(second);

            MeasureAndArrange(panel, 800D, 600D);

            Assert.True(first.IsRoot);
            Assert.True(second.IsRoot);
            Assert.Equal(0, first.Depth);
            Assert.Equal(0, second.Depth);
            Assert.Equal(260D, panel.DesiredSize.Width);
        });
    }

    [Fact]
    public void Panel_CentersParentAboveSiblingSubtrees()
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var left = CreateItem("left", "root");
            var right = CreateItem("right", "root");
            var panel = new NavigationMapPanel
            {
                HorizontalSpacing = 24D,
                VerticalSpacing = 32D,
            };
            panel.Children.Add(root);
            panel.Children.Add(left);
            panel.Children.Add(right);

            MeasureAndArrange(panel, 800D, 600D);

            Vector rootOffset = VisualTreeHelper.GetOffset(root);
            Vector leftOffset = VisualTreeHelper.GetOffset(left);
            Vector rightOffset = VisualTreeHelper.GetOffset(right);
            double rootCenter = rootOffset.X + (root.ActualWidth / 2D);
            double childGroupCenter = (
                leftOffset.X
                + (left.ActualWidth / 2D)
                + rightOffset.X
                + (right.ActualWidth / 2D)
            ) / 2D;

            Assert.Equal(childGroupCenter, rootCenter, 5);
            Assert.Equal(root.ActualHeight + panel.VerticalSpacing, leftOffset.Y);
            Assert.Equal(leftOffset.Y, rightOffset.Y);
        });
    }

    [Fact]
    public void Panel_CentersParentBeforeSiblingSubtrees_WhenDirectionIsLeftToRight()
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var top = CreateItem("top", "root");
            var bottom = CreateItem("bottom", "root");
            var panel = new NavigationMapPanel
            {
                Direction = NavigationMapDirection.LeftToRight,
                HorizontalSpacing = 36D,
                VerticalSpacing = 24D,
            };
            panel.Children.Add(root);
            panel.Children.Add(top);
            panel.Children.Add(bottom);

            MeasureAndArrange(panel, 800D, 600D);

            Vector rootOffset = VisualTreeHelper.GetOffset(root);
            Vector topOffset = VisualTreeHelper.GetOffset(top);
            Vector bottomOffset = VisualTreeHelper.GetOffset(bottom);
            double rootCenter = rootOffset.Y + (root.ActualHeight / 2D);
            double childGroupCenter = (
                topOffset.Y
                + (top.ActualHeight / 2D)
                + bottomOffset.Y
                + (bottom.ActualHeight / 2D)
            ) / 2D;

            Assert.Equal(childGroupCenter, rootCenter, 5);
            Assert.Equal(root.ActualWidth + panel.HorizontalSpacing, topOffset.X);
            Assert.Equal(topOffset.X, bottomOffset.X);
            Assert.Equal(276D, panel.DesiredSize.Width);
            Assert.Equal(
                top.DesiredSize.Height + panel.VerticalSpacing + bottom.DesiredSize.Height,
                panel.DesiredSize.Height
            );
        });
    }

    [Fact]
    public void Direction_RejectsUndefinedValues()
    {
        WpfTestThread.Run(() =>
        {
            var view = new NavigationMapView();
            var panel = new NavigationMapPanel();

            Assert.Throws<ArgumentException>(() =>
                view.Direction = (NavigationMapDirection)int.MaxValue
            );
            Assert.Throws<ArgumentException>(() =>
                panel.Direction = (NavigationMapDirection)int.MaxValue
            );
        });
    }

    [Fact]
    public void Panel_ReflowsExistingChildren_WhenDirectionChanges()
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var child = CreateItem("child", "root");
            var panel = new NavigationMapPanel
            {
                HorizontalSpacing = 36D,
                VerticalSpacing = 24D,
            };
            panel.Children.Add(root);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 800D, 600D);
            Vector topDownRootOffset = VisualTreeHelper.GetOffset(root);
            Vector topDownChildOffset = VisualTreeHelper.GetOffset(child);
            Assert.Equal(topDownRootOffset.X, topDownChildOffset.X);
            Assert.Equal(
                topDownRootOffset.Y + root.ActualHeight + panel.VerticalSpacing,
                topDownChildOffset.Y
            );

            panel.Direction = NavigationMapDirection.LeftToRight;
            MeasureAndArrange(panel, 800D, 600D);
            Vector leftToRightRootOffset = VisualTreeHelper.GetOffset(root);
            Vector leftToRightChildOffset = VisualTreeHelper.GetOffset(child);
            Assert.Equal(leftToRightRootOffset.Y, leftToRightChildOffset.Y);
            Assert.Equal(
                leftToRightRootOffset.X + root.ActualWidth + panel.HorizontalSpacing,
                leftToRightChildOffset.X
            );
        });
    }

    [Theory]
    [InlineData(NavigationMapDirection.TopToBottom)]
    [InlineData(NavigationMapDirection.LeftToRight)]
    public void Panel_AttachesConnectorsToRenderedBounds_WhenContainersHaveMargins(
        NavigationMapDirection direction)
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            root.Margin = new Thickness(9D, 13D, 21D, 27D);
            var child = CreateItem("child", "root");
            child.Margin = new Thickness(17D, 23D, 11D, 19D);
            var panel = new NavigationMapPanel
            {
                Direction = direction,
                HorizontalSpacing = 36D,
                VerticalSpacing = 32D,
            };
            panel.Children.Add(root);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 800D, 600D);

            Rect rootBounds = GetRenderedBounds(root);
            Rect childBounds = GetRenderedBounds(child);
            Point rootAttachment = direction == NavigationMapDirection.TopToBottom
                ? new Point(rootBounds.Left + (rootBounds.Width / 2D), rootBounds.Bottom)
                : new Point(rootBounds.Right, rootBounds.Top + (rootBounds.Height / 2D));
            Point childAttachment = direction == NavigationMapDirection.TopToBottom
                ? new Point(childBounds.Left + (childBounds.Width / 2D), childBounds.Top)
                : new Point(childBounds.Left, childBounds.Top + (childBounds.Height / 2D));

            Assert.Contains(
                panel.ConnectorSegments,
                segment => segment.StartPoint == rootAttachment || segment.EndPoint == rootAttachment
            );
            Assert.Contains(
                panel.ConnectorSegments,
                segment => segment.StartPoint == childAttachment || segment.EndPoint == childAttachment
            );
        });
    }

    [Theory]
    [InlineData(NavigationMapDirection.TopToBottom)]
    [InlineData(NavigationMapDirection.LeftToRight)]
    public void Panel_MergesSharedConnectorSegments_AndCompositesAlphaOnlyOnce(
        NavigationMapDirection direction)
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var first = CreateItem("first", "root");
            var second = CreateItem("second", "root");
            root.Opacity = 0D;
            first.Opacity = 0D;
            second.Opacity = 0D;

            var panel = new NavigationMapPanel
            {
                Direction = direction,
                HorizontalSpacing = 32D,
                VerticalSpacing = 32D,
                ConnectorBrush = new SolidColorBrush(Color.FromArgb(128, 0, 0, 0)),
                ConnectorThickness = 8D,
            };
            panel.Children.Add(root);
            panel.Children.Add(first);
            panel.Children.Add(second);

            MeasureAndArrange(panel, 600D, 400D);

            // Two sibling paths contain six raw orthogonal intervals. Their common trunk and
            // touching branch-bar portions normalize to four disjoint intervals in either axis.
            Assert.Equal(4, panel.ConnectorSegments.Count);
            AssertNoCollinearOverlap(panel.ConnectorSegments);

            Vector rootOffset = VisualTreeHelper.GetOffset(root);
            Vector firstOffset = VisualTreeHelper.GetOffset(first);
            Point sharedTrunkSample;
            Point branchJointSample;
            Point exclusiveBranchSample;
            if (direction == NavigationMapDirection.TopToBottom)
            {
                double parentEnd = rootOffset.Y + root.ActualHeight;
                double middle = parentEnd + ((firstOffset.Y - parentEnd) / 2D);
                sharedTrunkSample = new Point(
                    rootOffset.X + (root.ActualWidth / 2D),
                    (parentEnd + middle) / 2D
                );
                branchJointSample = new Point(
                    rootOffset.X + (root.ActualWidth / 2D),
                    middle
                );
                exclusiveBranchSample = new Point(
                    firstOffset.X + (first.ActualWidth / 2D),
                    (middle + firstOffset.Y) / 2D
                );
            }
            else
            {
                double parentEnd = rootOffset.X + root.ActualWidth;
                double middle = parentEnd + ((firstOffset.X - parentEnd) / 2D);
                sharedTrunkSample = new Point(
                    (parentEnd + middle) / 2D,
                    rootOffset.Y + (root.ActualHeight / 2D)
                );
                branchJointSample = new Point(
                    middle,
                    rootOffset.Y + (root.ActualHeight / 2D)
                );
                exclusiveBranchSample = new Point(
                    (middle + firstOffset.X) / 2D,
                    firstOffset.Y + (first.ActualHeight / 2D)
                );
            }

            var bitmap = new RenderTargetBitmap(600, 400, 96D, 96D, PixelFormats.Pbgra32);
            bitmap.Render(panel);
            byte sharedAlpha = GetPixelAlpha(bitmap, sharedTrunkSample);
            byte jointAlpha = GetPixelAlpha(bitmap, branchJointSample);
            byte exclusiveAlpha = GetPixelAlpha(bitmap, exclusiveBranchSample);

            Assert.InRange(exclusiveAlpha, (byte)126, (byte)130);
            Assert.InRange(sharedAlpha, (byte)126, (byte)130);
            Assert.InRange(jointAlpha, (byte)126, (byte)130);
            Assert.InRange(Math.Abs(sharedAlpha - exclusiveAlpha), 0, 1);
            Assert.InRange(Math.Abs(jointAlpha - exclusiveAlpha), 0, 1);
        });
    }

    private static NavigationMapItem CreateItem(string key, string? parentKey) =>
        new()
        {
            NodeKey = key,
            ParentKey = parentKey,
            Width = 120D,
            Height = 48D,
        };

    private static void AssertNoCollinearOverlap(
        IReadOnlyList<NavigationMapPanel.ConnectorSegment> segments)
    {
        for (int firstIndex = 0; firstIndex < segments.Count; firstIndex++)
        {
            NavigationMapPanel.ConnectorSegment first = segments[firstIndex];
            for (int secondIndex = firstIndex + 1; secondIndex < segments.Count; secondIndex++)
            {
                NavigationMapPanel.ConnectorSegment second = segments[secondIndex];
                if (
                    first.IsHorizontal != second.IsHorizontal
                    || first.Offset != second.Offset
                )
                {
                    continue;
                }

                Assert.True(
                    first.End < second.Start || second.End < first.Start,
                    $"Connector intervals {first} and {second} overlap or touch."
                );
            }
        }
    }

    private static byte GetPixelAlpha(BitmapSource bitmap, Point point)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(
            new Int32Rect((int)Math.Floor(point.X), (int)Math.Floor(point.Y), 1, 1),
            pixel,
            4,
            0
        );
        return pixel[3];
    }

    private static Rect GetRenderedBounds(UIElement element)
    {
        Vector offset = VisualTreeHelper.GetOffset(element);
        return new Rect(offset.X, offset.Y, element.RenderSize.Width, element.RenderSize.Height);
    }

    private static ControlTemplate CreateTestTemplate()
    {
        var factory = new FrameworkElementFactory(typeof(ItemsPresenter));
        return new ControlTemplate(typeof(NavigationMapView)) { VisualTree = factory };
    }

    private static ItemsPanelTemplate CreateTestItemsPanel()
    {
        var factory = new FrameworkElementFactory(typeof(NavigationMapPanel));
        return new ItemsPanelTemplate(factory);
    }

    private static void MeasureAndArrange(UIElement element, double width, double height)
    {
        element.Measure(new Size(width, height));
        element.Arrange(new Rect(0D, 0D, width, height));
        element.UpdateLayout();
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject => EnumerateVisualDescendants<T>(root).FirstOrDefault();

    private static IEnumerable<T> EnumerateVisualDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int childCount = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in EnumerateVisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class RecordingCommand : ICommand
    {
        internal int ExecutionCount { get; private set; }

        internal object? Parameter { get; private set; }

        event EventHandler? ICommand.CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            ExecutionCount++;
            Parameter = parameter;
        }
    }
}
