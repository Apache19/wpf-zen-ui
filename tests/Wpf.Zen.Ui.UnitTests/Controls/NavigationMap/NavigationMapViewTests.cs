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
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
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
                LayoutMode = NavigationMapLayoutMode.Manual,
                ZoomFactor = 0.75D,
            };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);

            MeasureAndArrange(host, 800D, 600D);

            Assert.True(view.Focusable);
            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(map[0])
            );
            Assert.Equal(view.NodeWidth, container.ActualWidth);
            var panel = Assert.IsType<NavigationMapPanel>(
                FindVisualDescendant<NavigationMapPanel>(view)
            );
            Assert.Equal(NavigationMapDirection.LeftToRight, panel.Direction);
            Assert.Equal(NavigationMapLayoutMode.Manual, panel.LayoutMode);
            Assert.Same(view.LayoutDefinition, panel.LayoutDefinition);
            Assert.Equal(view.GridSize, panel.GridSize);
            Assert.Equal(view.RoutingClearance, panel.RoutingClearance);
            ScaleTransform zoomTransform = Assert.Single(
                EnumerateVisualDescendants<FrameworkElement>(view)
                    .Select(static element => element.LayoutTransform)
                    .OfType<ScaleTransform>()
            );
            Assert.Equal(0.75D, zoomTransform.ScaleX);
            Assert.Equal(0.75D, zoomTransform.ScaleY);
            Assert.Equal("Explains the localized root feature.", container.ToolTip);
            Assert.Equal(
                "Explains the localized root feature.",
                AutomationProperties.GetHelpText(container)
            );
            map[0].Description = "Updated localized help.";
            Assert.Equal("Updated localized help.", container.Description);
            Assert.Equal("Updated localized help.", container.ToolTip);
            Assert.Equal("Updated localized help.", AutomationProperties.GetHelpText(container));
            map[0].Description = null;
            Assert.Null(container.Description);
            Assert.Null(container.ToolTip);
            Assert.Equal(string.Empty, AutomationProperties.GetHelpText(container));
            Assert.Equal(
                string.Empty,
                BindingOperations.GetBinding(
                    container,
                    AutomationProperties.HelpTextProperty
                )?.TargetNullValue
            );
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
                    Direction="LeftToRight"
                    LayoutMode="Manual">
                    <ui:NavigationMapView.LayoutDefinition>
                        <ui:NavigationMapLayout>
                            <ui:NavigationMapPlacement NodeKey="root" Position="240,120" />
                        </ui:NavigationMapLayout>
                    </ui:NavigationMapView.LayoutDefinition>
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
            Assert.Equal(NavigationMapLayoutMode.Manual, view.LayoutMode);
            Assert.NotNull(view.LayoutDefinition);
            Assert.True(view.LayoutDefinition.TryGetPlacement("root", out NavigationMapPlacement? placement));
            Assert.NotNull(placement);
            Assert.Equal(new Point(240D, 120D), placement.Position);
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

            // Normalization may reduce a perfectly aligned fan-out to a two-segment T junction or
            // retain separate endpoint stems when margins differ. In both cases shared pixels must
            // occur only once in the rendered geometry.
            Assert.True(panel.ConnectorSegments.Count >= 2);
            AssertNoCollinearOverlap(panel.ConnectorSegments);

            Point[] intersections = (
                from horizontal in panel.ConnectorSegments
                where horizontal.IsHorizontal
                from vertical in panel.ConnectorSegments
                where !vertical.IsHorizontal
                    && horizontal.Start <= vertical.Offset
                    && horizontal.End >= vertical.Offset
                    && vertical.Start <= horizontal.Offset
                    && vertical.End >= horizontal.Offset
                select new Point(vertical.Offset, horizontal.Offset)
            ).ToArray();
            Assert.NotEmpty(intersections);
            Point exclusiveSample = panel.ConnectorSegments[0].StartPoint
                + ((panel.ConnectorSegments[0].EndPoint - panel.ConnectorSegments[0].StartPoint) / 2D);
            Point branchJointSample = intersections[0];

            var bitmap = new RenderTargetBitmap(600, 400, 96D, 96D, PixelFormats.Pbgra32);
            bitmap.Render(panel);
            byte jointAlpha = GetPixelAlpha(bitmap, branchJointSample);
            byte exclusiveAlpha = GetPixelAlpha(bitmap, exclusiveSample);

            Assert.InRange(exclusiveAlpha, (byte)126, (byte)130);
            Assert.InRange(jointAlpha, (byte)126, (byte)130);
            Assert.InRange(Math.Abs(jointAlpha - exclusiveAlpha), 0, 1);
        });
    }

    [Fact]
    public void ManualPanel_AppliesPinnedCentersAndTracksRuntimeLayoutChanges()
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var child = CreateItem("child", "root");
            var layout = new NavigationMapLayout();
            layout.SetPosition("root", new Point(300D, 120D));
            layout.SetPosition("child", new Point(120D, 260D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(root);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 800D, 600D);

            Assert.Equal(
                new Vector(
                    300D - (root.DesiredSize.Width / 2D),
                    120D - (root.DesiredSize.Height / 2D)
                ),
                VisualTreeHelper.GetOffset(root)
            );
            Assert.Equal(
                new Vector(
                    120D - (child.DesiredSize.Width / 2D),
                    260D - (child.DesiredSize.Height / 2D)
                ),
                VisualTreeHelper.GetOffset(child)
            );

            layout.SetPosition("child", new Point(420D, 310D));
            MeasureAndArrange(panel, 800D, 600D);

            Assert.Equal(
                new Vector(
                    420D - (child.DesiredSize.Width / 2D),
                    310D - (child.DesiredSize.Height / 2D)
                ),
                VisualTreeHelper.GetOffset(child)
            );
            Assert.NotEmpty(panel.ConnectorSegments);
        });
    }

    [Fact]
    public void EditingMode_SuppressesNavigationInvocation()
    {
        WpfTestThread.Run(() =>
        {
            var node = new NavigationMapNode("root", "Root");
            var command = new RecordingCommand();
            var view = new NavigationMapView
            {
                ItemsSource = new[] { node },
                Command = command,
                IsEditing = true,
                LayoutMode = NavigationMapLayoutMode.Manual,
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };
            int invocationCount = 0;
            view.NodeInvoked += (_, _) => invocationCount++;

            MeasureAndArrange(view, 800D, 600D);
            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(node)
            );
            container.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, container));

            Assert.Equal(0, invocationCount);
            Assert.Equal(0, command.ExecutionCount);
        });
    }

    [Fact]
    public void ManualPanel_RoutesConnectorsAroundUnrelatedNodes()
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            var obstacle = CreateItem("obstacle", null);
            var child = CreateItem("child", "parent");
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(200D, 80D));
            layout.SetPosition("obstacle", new Point(200D, 220D));
            layout.SetPosition("child", new Point(200D, 380D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(parent);
            panel.Children.Add(obstacle);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 500D, 500D);

            AssertConnectorsAvoidNodeInteriors(panel, [parent, obstacle, child]);
            Assert.True(
                panel.ConnectorSegments.Any(segment => segment.IsHorizontal),
                "the blocked vertical route should leave the center column through a horizontal lane"
            );
        });
    }

    [Fact]
    public void ManualPanel_DoesNotMoveAutomaticNodesOutOfPinnedPositions()
    {
        WpfTestThread.Run(() =>
        {
            var root = CreateItem("root", null);
            var child = CreateItem("child", "root");
            var layout = new NavigationMapLayout();
            // The automatic child center is (60, 116). Pinning the root there deliberately creates
            // an overlap and verifies that the editor no longer moves unrelated cards behind the
            // user's back.
            layout.SetPosition("root", new Point(60D, 116D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(root);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 500D, 500D);

            Rect rootBounds = GetRenderedBounds(root);
            Rect childBounds = GetRenderedBounds(child);
            Assert.True(rootBounds.IntersectsWith(childBounds));
        });
    }

    [Fact]
    public void ManualPanel_UsesHierarchicalGeometry_WhenLayoutIsEmpty()
    {
        WpfTestThread.Run(() =>
        {
            NavigationMapItem hierarchicalRoot = CreateItem("root", null);
            NavigationMapItem hierarchicalFirst = CreateItem("first", "root");
            NavigationMapItem hierarchicalSecond = CreateItem("second", "root");
            var hierarchical = new NavigationMapPanel();
            hierarchical.Children.Add(hierarchicalRoot);
            hierarchical.Children.Add(hierarchicalFirst);
            hierarchical.Children.Add(hierarchicalSecond);

            NavigationMapItem manualRoot = CreateItem("root", null);
            NavigationMapItem manualFirst = CreateItem("first", "root");
            NavigationMapItem manualSecond = CreateItem("second", "root");
            var manual = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = new NavigationMapLayout(),
            };
            manual.Children.Add(manualRoot);
            manual.Children.Add(manualFirst);
            manual.Children.Add(manualSecond);

            MeasureAndArrange(hierarchical, 700D, 500D);
            MeasureAndArrange(manual, 700D, 500D);

            Vector hierarchicalRootOffset = VisualTreeHelper.GetOffset(hierarchicalRoot);
            Vector manualRootOffset = VisualTreeHelper.GetOffset(manualRoot);
            Assert.Equal(
                VisualTreeHelper.GetOffset(hierarchicalFirst) - hierarchicalRootOffset,
                VisualTreeHelper.GetOffset(manualFirst) - manualRootOffset
            );
            Assert.Equal(
                VisualTreeHelper.GetOffset(hierarchicalSecond) - hierarchicalRootOffset,
                VisualTreeHelper.GetOffset(manualSecond) - manualRootOffset
            );
        });
    }

    [Fact]
    public void ManualPanel_CreatesSharedBus_WhenParentAndChildrenShareOneRow()
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            NavigationMapItem[] children =
            [
                CreateItem("first", "parent"),
                CreateItem("second", "parent"),
                CreateItem("third", "parent"),
            ];
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(80D, 200D));
            layout.SetPosition("first", new Point(260D, 200D));
            layout.SetPosition("second", new Point(440D, 200D));
            layout.SetPosition("third", new Point(620D, 200D));
            var panel = new NavigationMapPanel
            {
                // Manual bus detection follows the edited coordinates, not this hierarchy hint.
                Direction = NavigationMapDirection.LeftToRight,
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
                RoutingClearance = 10D,
            };
            panel.Children.Add(parent);
            foreach (NavigationMapItem child in children)
            {
                panel.Children.Add(child);
            }

            MeasureAndArrange(panel, 700D, 400D);

            Assert.Equal(3, panel.ConnectorRoutes.Count);
            NavigationMapPanel.ConnectorSegment bus = Assert.Single(
                panel.ConnectorSegments,
                segment =>
                    segment.IsHorizontal
                    && segment.Start == 80D
                    && segment.End == 620D
            );
            Assert.Equal(GetRenderedBounds(parent).Top - panel.RoutingClearance, bus.Offset);
            AssertNoCollinearOverlap(panel.ConnectorSegments);
        });
    }

    [Fact]
    public void ManualPanel_PrefersOneBendRoutes_WhenChildrenFormARowBelowParent()
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            NavigationMapItem[] children = Enumerable
                .Range(0, 7)
                .Select(index => CreateItem($"child-{index}", "parent"))
                .ToArray();
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(520D, 80D));
            for (int index = 0; index < children.Length; index++)
            {
                layout.SetPosition(children[index].NodeKey!, new Point(100D + (index * 140D), 180D));
            }

            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
                RoutingClearance = 10D,
            };
            panel.Children.Add(parent);
            foreach (NavigationMapItem child in children)
            {
                panel.Children.Add(child);
            }

            MeasureAndArrange(panel, 1100D, 320D);

            Rect parentBounds = GetRenderedBounds(parent);
            foreach (NavigationMapItem child in children.Skip(4))
            {
                NavigationMapPanel.ConnectorRoute route = Assert.Single(
                    panel.ConnectorRoutes,
                    candidate => candidate.ChildKey == child.NodeKey
                );
                Assert.True(
                    route.Segments.Count <= 2,
                    $"Expected a direct right-then-down route, but {child.NodeKey} used "
                        + $"{route.Segments.Count} segments: {string.Join(", ", route.Segments)}."
                );
                Assert.Contains(
                    route.Segments,
                    segment => segment.StartPoint
                            == new Point(parentBounds.Right, parentBounds.Top + (parentBounds.Height / 2D))
                        || segment.EndPoint
                            == new Point(parentBounds.Right, parentBounds.Top + (parentBounds.Height / 2D))
                );
            }

            AssertConnectorsAvoidNodeInteriors(panel, [parent, .. children]);
        });
    }

    [Fact]
    public void RoutedConnectors_AttachToBothEndpointCardEdges()
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            var obstacle = CreateItem("obstacle", null);
            var child = CreateItem("child", "parent");
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(80D, 220D));
            layout.SetPosition("obstacle", new Point(260D, 220D));
            layout.SetPosition("child", new Point(440D, 220D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(parent);
            panel.Children.Add(obstacle);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 540D, 440D);

            NavigationMapPanel.ConnectorRoute route = Assert.Single(panel.ConnectorRoutes);
            Rect parentBounds = GetRenderedBounds(parent);
            Rect childBounds = GetRenderedBounds(child);
            Assert.Contains(route.Segments, segment => HasEndpointOnBorder(segment, parentBounds));
            Assert.Contains(route.Segments, segment => HasEndpointOnBorder(segment, childBounds));
            AssertConnectorsAvoidNodeInteriors(panel, [parent, obstacle, child]);
            Assert.DoesNotContain(
                route.Segments,
                segment => segment.Offset == 0D
            );
        });
    }

    [Theory]
    [InlineData(NavigationMapDirection.LeftToRight, 120D, 200D, false)]
    [InlineData(NavigationMapDirection.TopToBottom, 300D, 120D, true)]
    public void ManualPanel_ChoosesCenteredFacingPortsFromActualPositions(
        NavigationMapDirection direction,
        double childX,
        double childY,
        bool horizontal)
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            var child = CreateItem("child", "parent");
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(120D, 120D));
            layout.SetPosition("child", new Point(childX, childY));
            var panel = new NavigationMapPanel
            {
                Direction = direction,
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
                RoutingClearance = 20D,
            };
            panel.Children.Add(parent);
            panel.Children.Add(child);

            MeasureAndArrange(panel, 400D, 320D);

            NavigationMapPanel.ConnectorRoute route = Assert.Single(panel.ConnectorRoutes);
            Rect parentBounds = GetRenderedBounds(parent);
            Rect childBounds = GetRenderedBounds(child);
            Assert.True(
                route.Segments.Count == 1,
                $"Expected one segment between {parentBounds} and {childBounds}; found {route.Segments.Count}."
            );
            NavigationMapPanel.ConnectorSegment segment = route.Segments[0];
            Assert.Equal(horizontal, segment.IsHorizontal);
            if (horizontal)
            {
                Assert.Equal(parentBounds.Right, segment.Start);
                Assert.Equal(childBounds.Left, segment.End);
                Assert.Equal(parentBounds.Top + (parentBounds.Height / 2D), segment.Offset);
            }
            else
            {
                Assert.Equal(parentBounds.Bottom, segment.Start);
                Assert.Equal(childBounds.Top, segment.End);
                Assert.Equal(parentBounds.Left + (parentBounds.Width / 2D), segment.Offset);
            }
        });
    }

    [Fact]
    public void ManualPanel_RoutesDoNotChangeWithHierarchicalDirection()
    {
        WpfTestThread.Run(() =>
        {
            var parent = CreateItem("parent", null);
            var first = CreateItem("first", "parent");
            var second = CreateItem("second", "parent");
            var layout = new NavigationMapLayout();
            layout.SetPosition("parent", new Point(420D, 280D));
            layout.SetPosition("first", new Point(100D, 80D));
            layout.SetPosition("second", new Point(760D, 500D));

            var panel = new NavigationMapPanel
            {
                Direction = NavigationMapDirection.TopToBottom,
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(parent);
            panel.Children.Add(first);
            panel.Children.Add(second);

            MeasureAndArrange(panel, 1000D, 680D);
            var topToBottomRoutes = panel.ConnectorRoutes
                .SelectMany(route => route.Segments.Select(segment =>
                    (route.ParentKey, route.ChildKey, segment)))
                .ToArray();

            panel.Direction = NavigationMapDirection.LeftToRight;
            MeasureAndArrange(panel, 1000D, 680D);

            Assert.Equal(
                topToBottomRoutes,
                panel.ConnectorRoutes
                    .SelectMany(route => route.Segments.Select(segment =>
                        (route.ParentKey, route.ChildKey, segment)))
                    .ToArray()
            );
        });
    }

    [Fact]
    public void DefaultSelectionChrome_DoesNotRemeasureOrRerouteWrappedNodes()
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
            var root = new NavigationMapNode(
                "root",
                "A deliberately long wrapped navigation map heading"
            );
            var child = new NavigationMapNode("child", "Child", parentKey: "root");
            var view = new NavigationMapView { ItemsSource = new[] { root, child } };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);
            MeasureAndArrange(host, 800D, 600D);

            var container = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(root)
            );
            var panel = Assert.IsType<NavigationMapPanel>(
                FindVisualDescendant<NavigationMapPanel>(view)
            );
            Size desiredBefore = container.DesiredSize;
            Size renderedBefore = container.RenderSize;
            var routesBefore = panel.ConnectorRoutes
                .SelectMany(route => route.Segments.Select(segment =>
                    (route.ParentKey, route.ChildKey, segment)))
                .ToArray();

            Assert.True(view.SelectNode("root"));
            MeasureAndArrange(host, 800D, 600D);

            Assert.Equal(desiredBefore, container.DesiredSize);
            Assert.Equal(renderedBefore, container.RenderSize);
            Assert.Equal(
                routesBefore,
                panel.ConnectorRoutes
                    .SelectMany(route => route.Segments.Select(segment =>
                        (route.ParentKey, route.ChildKey, segment)))
                    .ToArray()
            );
        });
    }

    [Fact]
    public void ManualPanel_DoesNotReuseTrunksAcrossDifferentParentNetworks()
    {
        WpfTestThread.Run(() =>
        {
            NavigationMapItem firstParent = CreateItem("first-parent", null);
            NavigationMapItem firstChild = CreateItem("first-child", "first-parent");
            NavigationMapItem secondParent = CreateItem("second-parent", null);
            NavigationMapItem secondChild = CreateItem("second-child", "second-parent");
            var layout = new NavigationMapLayout();
            layout.SetPosition("first-parent", new Point(100D, 100D));
            layout.SetPosition("first-child", new Point(500D, 360D));
            layout.SetPosition("second-parent", new Point(100D, 360D));
            layout.SetPosition("second-child", new Point(500D, 100D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(firstParent);
            panel.Children.Add(firstChild);
            panel.Children.Add(secondParent);
            panel.Children.Add(secondChild);

            MeasureAndArrange(panel, 650D, 500D);

            Assert.Equal(2, panel.ConnectorRoutes.Count);
            AssertNoCollinearOverlapBetweenRoutes(
                panel.ConnectorRoutes[0].Segments,
                panel.ConnectorRoutes[1].Segments
            );
        });
    }

    [Fact]
    public void ManualPanel_HoldsExpandedExtentUntilDragCompletes()
    {
        WpfTestThread.Run(() =>
        {
            var node = CreateItem("node", null);
            var layout = new NavigationMapLayout();
            layout.SetPosition("node", new Point(100D, 100D));
            var panel = new NavigationMapPanel
            {
                LayoutMode = NavigationMapLayoutMode.Manual,
                LayoutDefinition = layout,
            };
            panel.Children.Add(node);
            MeasureAndArrangeToDesiredSize(panel);
            panel.BeginExtentHold();

            layout.SetPosition("node", new Point(700D, 100D));
            MeasureAndArrangeToDesiredSize(panel);
            double expandedWidth = panel.DesiredSize.Width;
            layout.SetPosition("node", new Point(100D, 100D));
            MeasureAndArrangeToDesiredSize(panel);

            Assert.Equal(expandedWidth, panel.DesiredSize.Width);
            panel.EndExtentHold();
            MeasureAndArrangeToDesiredSize(panel);
            Assert.True(panel.DesiredSize.Width < expandedWidth);
        });
    }

    [Fact]
    public void Panel_ProvidesBlankCanvasHitSurface_WhenGridIsHidden()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new NavigationMapPanel
            {
                ConnectorBrush = null,
                IsEditing = true,
                ShowGrid = false,
            };
            panel.Children.Add(CreateItem("node", null));
            MeasureAndArrange(panel, 500D, 300D);

            HitTestResult? result = VisualTreeHelper.HitTest(panel, new Point(490D, 290D));

            Assert.NotNull(result);
            Assert.Same(panel, result.VisualHit);
        });
    }

    [Fact]
    public void Search_SelectsAndCyclesMatchingNodes()
    {
        WpfTestThread.Run(() =>
        {
            var root = new NavigationMapNode("root", "Dashboard");
            var first = new NavigationMapNode("users", "User management", parentKey: "root");
            var second = new NavigationMapNode("content", "Content management", parentKey: "root");
            var view = new NavigationMapView
            {
                ItemsSource = new[] { root, first, second },
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };
            MeasureAndArrange(view, 800D, 600D);

            view.SearchText = "management";

            Assert.Equal(2, view.SearchResultCount);
            Assert.True(view.FindNext());
            Assert.Equal(0, view.CurrentSearchResultIndex);
            Assert.Single(view.SelectedNodeKeys);
            Assert.True(view.FindNext());
            Assert.Equal(1, view.CurrentSearchResultIndex);
            Assert.Single(view.SelectedNodeKeys);
        });
    }

    [Fact]
    public void MultiSelection_AlignsToPrimaryNodeAndKeepsBothSelected()
    {
        WpfTestThread.Run(() =>
        {
            var root = new NavigationMapNode("root", "Root");
            var child = new NavigationMapNode("child", "Child", parentKey: "root");
            var layout = new NavigationMapLayout();
            layout.SetPosition("root", new Point(180D, 100D));
            layout.SetPosition("child", new Point(480D, 280D));
            var view = new NavigationMapView
            {
                IsEditing = true,
                ItemsSource = new[] { root, child },
                LayoutDefinition = layout,
                LayoutMode = NavigationMapLayoutMode.Manual,
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };
            MeasureAndArrange(view, 800D, 600D);

            Assert.True(view.SelectNode("child"));
            Assert.True(view.SelectNode("root", true));
            var rootContainer = Assert.IsType<NavigationMapItem>(
                view.ItemContainerGenerator.ContainerFromItem(root)
            );
            double expectedCenter = GetRenderedBounds(rootContainer).Left
                + (rootContainer.RenderSize.Width / 2D);
            Assert.True(view.AlignSelection(NavigationMapAlignment.Left));

            Assert.Equal(2, view.SelectedNodeKeys.Count);
            Assert.Equal("root", view.PrimarySelectedNodeKey);
            Assert.True(layout.TryGetPlacement("child", out NavigationMapPlacement? placement));
            Assert.NotNull(placement);
            Assert.Equal(expectedCenter, placement.Position.X);
            Assert.True(view.CanUndo);
            Assert.True(view.Undo());
            Assert.Equal(480D, placement.Position.X);
            Assert.True(view.CanRedo);
            Assert.True(view.Redo());
            Assert.Equal(expectedCenter, placement.Position.X);
        });
    }

    [Fact]
    public void AlignToGrid_UsesSelectionOrAllNodes_WhenSelectionIsEmpty()
    {
        WpfTestThread.Run(() =>
        {
            var root = new NavigationMapNode("root", "Root");
            var child = new NavigationMapNode("child", "Child", parentKey: "root");
            var layout = new NavigationMapLayout();
            layout.SetPosition("root", new Point(183D, 103D));
            layout.SetPosition("child", new Point(477D, 279D));
            var resources = new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            };
            var view = new NavigationMapView
            {
                GridSize = 16D,
                ItemsSource = new[] { root, child },
                LayoutDefinition = layout,
                LayoutMode = NavigationMapLayoutMode.Manual,
                IsEditing = true,
                SnapToGrid = false,
            };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);
            MeasureAndArrange(host, 800D, 600D);

            Assert.True(view.SelectNode("child"));
            Assert.True(view.AlignToGrid());
            Assert.True(layout.TryGetPlacement("root", out NavigationMapPlacement? rootPlacementResult));
            Assert.True(layout.TryGetPlacement("child", out NavigationMapPlacement? childPlacementResult));
            NavigationMapPlacement rootPlacement = Assert.IsType<NavigationMapPlacement>(rootPlacementResult);
            NavigationMapPlacement childPlacement = Assert.IsType<NavigationMapPlacement>(childPlacementResult);
            Assert.Equal(new Point(183D, 103D), rootPlacement.Position);
            Assert.Equal(new Point(480D, 272D), childPlacement.Position);

            view.ClearSelection();
            Assert.True(view.AlignToGrid());
            Assert.Equal(new Point(176D, 96D), rootPlacement.Position);
        });
    }

    [Fact]
    public void EditingInitialization_MaterializesEveryNodeOnGrid()
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
            var view = new NavigationMapView
            {
                GridSize = 16D,
                ItemsSource = new[]
                {
                    new NavigationMapNode("root", "Root"),
                    new NavigationMapNode("child", "Child", parentKey: "root"),
                },
                LayoutMode = NavigationMapLayoutMode.Manual,
                IsEditing = true,
            };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);
            MeasureAndArrange(host, 800D, 600D);
            view.Dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);

            NavigationMapLayout layout = Assert.IsType<NavigationMapLayout>(view.LayoutDefinition);
            Assert.Equal(2, layout.Placements.Count);
            Assert.All(
                layout.Placements,
                placement =>
                {
                    Assert.Equal(0D, placement.Position.X % view.GridSize);
                    Assert.Equal(0D, placement.Position.Y % view.GridSize);
                }
            );
        });
    }

    [Fact]
    public void Editing_RealignsEveryNodeAfterDirectionChangeAndReset()
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
            var view = new NavigationMapView
            {
                GridSize = 16D,
                ItemsSource = new[]
                {
                    new NavigationMapNode("root", "Root"),
                    new NavigationMapNode("child", "Child", parentKey: "root"),
                },
                LayoutMode = NavigationMapLayoutMode.Manual,
                IsEditing = true,
            };
            var host = new Grid();
            host.Resources.MergedDictionaries.Add(resources);
            host.Children.Add(view);
            MeasureAndArrange(host, 800D, 600D);
            view.Dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);

            NavigationMapLayout layout = Assert.IsType<NavigationMapLayout>(view.LayoutDefinition);
            layout.SetPosition("root", new Point(183D, 103D));
            layout.SetPosition("child", new Point(477D, 279D));
            view.Direction = NavigationMapDirection.LeftToRight;
            view.Dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);
            Assert.All(
                layout.Placements,
                placement => AssertPointIsOnGrid(placement.Position, view.GridSize)
            );

            layout.SetPosition("root", new Point(185D, 105D));
            view.ResetLayout();
            view.Dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);
            Assert.Equal(2, layout.Placements.Count);
            Assert.All(
                layout.Placements,
                placement => AssertPointIsOnGrid(placement.Position, view.GridSize)
            );
            Assert.False(view.CanUndo);
            Assert.False(view.CanRedo);
        });
    }

    [Fact]
    public void SelectAll_DoesNothingOutsideManualEditing()
    {
        WpfTestThread.Run(() =>
        {
            var view = new NavigationMapView
            {
                ItemsSource = new[]
                {
                    new NavigationMapNode("root", "Root"),
                    new NavigationMapNode("child", "Child", parentKey: "root"),
                },
                Template = CreateTestTemplate(),
                ItemsPanel = CreateTestItemsPanel(),
            };
            MeasureAndArrange(view, 800D, 600D);

            view.SelectAll();

            Assert.Empty(view.SelectedNodeKeys);
        });
    }

    [Fact]
    public void ZoomFactor_IsCoercedToConfigurableLimits()
    {
        WpfTestThread.Run(() =>
        {
            var view = new NavigationMapView
            {
                MinimumZoomFactor = 0.4D,
                MaximumZoomFactor = 2D,
                ZoomFactor = 10D,
            };

            Assert.Equal(2D, view.ZoomFactor);
            view.ZoomFactor = 0.1D;
            Assert.Equal(0.4D, view.ZoomFactor);
            view.MinimumZoomFactor = 3D;
            Assert.Equal(3D, view.MaximumZoomFactor);
            Assert.Equal(3D, view.ZoomFactor);
        });
    }

    [Fact]
    public void LayoutModeAndPlacement_RejectInvalidValues()
    {
        WpfTestThread.Run(() =>
        {
            var view = new NavigationMapView();
            var panel = new NavigationMapPanel();
            var placement = new NavigationMapPlacement("root", default);

            view.IsEditing = true;
            Assert.False(view.IsEditing);
            view.LayoutMode = NavigationMapLayoutMode.Manual;
            Assert.True(view.IsEditing);
            view.LayoutMode = NavigationMapLayoutMode.Hierarchical;
            Assert.False(view.IsEditing);

            Assert.Throws<ArgumentException>(() =>
                view.LayoutMode = (NavigationMapLayoutMode)int.MaxValue
            );
            Assert.Throws<ArgumentException>(() =>
                panel.LayoutMode = (NavigationMapLayoutMode)int.MaxValue
            );
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                placement.Position = new Point(double.NaN, 0D)
            );
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

    private static void AssertNoCollinearOverlapBetweenRoutes(
        IReadOnlyList<NavigationMapPanel.ConnectorSegment> firstRoute,
        IReadOnlyList<NavigationMapPanel.ConnectorSegment> secondRoute)
    {
        foreach (NavigationMapPanel.ConnectorSegment first in firstRoute)
        {
            foreach (NavigationMapPanel.ConnectorSegment second in secondRoute)
            {
                if (first.IsHorizontal != second.IsHorizontal || first.Offset != second.Offset)
                {
                    continue;
                }

                Assert.True(
                    Math.Min(first.End, second.End) <= Math.Max(first.Start, second.Start),
                    $"Independent connector routes overlap on {first} and {second}. "
                        + $"First route: {string.Join(", ", firstRoute)}. "
                        + $"Second route: {string.Join(", ", secondRoute)}."
                );
            }
        }
    }

    private static bool HasEndpointOnBorder(
        NavigationMapPanel.ConnectorSegment segment,
        Rect bounds)
    {
        return IsPointOnBorder(segment.StartPoint, bounds) || IsPointOnBorder(segment.EndPoint, bounds);
    }

    private static void AssertPointIsOnGrid(Point point, double gridSize)
    {
        Assert.Equal(0D, point.X % gridSize);
        Assert.Equal(0D, point.Y % gridSize);
    }

    private static bool IsPointOnBorder(Point point, Rect bounds) =>
        ((point.X == bounds.Left || point.X == bounds.Right) && point.Y >= bounds.Top && point.Y <= bounds.Bottom)
        || ((point.Y == bounds.Top || point.Y == bounds.Bottom) && point.X >= bounds.Left && point.X <= bounds.Right);

    private static void AssertConnectorsAvoidNodeInteriors(
        NavigationMapPanel panel,
        IReadOnlyList<NavigationMapItem> nodes)
    {
        foreach (NavigationMapPanel.ConnectorSegment segment in panel.ConnectorSegments)
        {
            foreach (NavigationMapItem node in nodes)
            {
                Rect bounds = GetRenderedBounds(node);
                bool intersectsInterior = segment.IsHorizontal
                    ? segment.Offset > bounds.Top
                        && segment.Offset < bounds.Bottom
                        && segment.End > bounds.Left
                        && segment.Start < bounds.Right
                    : segment.Offset > bounds.Left
                        && segment.Offset < bounds.Right
                        && segment.End > bounds.Top
                        && segment.Start < bounds.Bottom;
                Assert.False(
                    intersectsInterior,
                    $"Connector {segment} crosses the rendered interior of node {node.NodeKey}."
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

    private static void MeasureAndArrangeToDesiredSize(UIElement element)
    {
        element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        element.Arrange(new Rect(element.DesiredSize));
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
