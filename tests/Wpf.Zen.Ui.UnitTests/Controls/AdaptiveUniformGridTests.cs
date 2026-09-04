// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public class AdaptiveUniformGridTests
{
    [Fact]
    public void PartialRows_KeepMembershipAndDivideWidthWithFixedSpacing()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(8);
            panel.Children[1].Visibility = Visibility.Collapsed;
            panel.Children[3].Visibility = Visibility.Collapsed;
            panel.Children[5].Visibility = Visibility.Collapsed;

            Layout(panel, new Size(320, 170));

            AssertSlot(panel, 0, new Rect(0, 0, 155, 50));
            AssertSlot(panel, 2, new Rect(165, 0, 155, 50));
            AssertSlot(panel, 4, new Rect(0, 60, 320, 50));
            AssertSlot(panel, 6, new Rect(0, 120, 155, 50));
            AssertSlot(panel, 7, new Rect(165, 120, 155, 50));
        });
    }

    [Fact]
    public void CollapsedWholeRow_DisappearsAndReturnsWithoutRegroupingChildren()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(8);
            Layout(panel, new Size(320, 170));
            for (int index = 3; index < 6; index++)
            {
                panel.Children[index].Visibility = Visibility.Collapsed;
            }

            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 0, new Rect(0, 0, 100, 80));
            AssertSlot(panel, 6, new Rect(0, 90, 155, 80));

            panel.Children[4].Visibility = Visibility.Visible;
            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 4, new Rect(0, 60, 320, 50));
            AssertSlot(panel, 6, new Rect(0, 120, 155, 50));
        });
    }

    [Fact]
    public void HiddenChild_StillReservesSpace()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(3);
            panel.Children[1].Visibility = Visibility.Hidden;
            Layout(panel, new Size(320, 50));
            AssertSlot(panel, 0, new Rect(0, 0, 100, 50));
            AssertSlot(panel, 2, new Rect(220, 0, 100, 50));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ChildDimensionsMarginsAndAlignment_AreHonored(bool stretch)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(2);
            panel.IsRowStretchEnabled = stretch;
            var child = (Border)panel.Children[0];
            child.Width = 40;
            child.Height = 20;
            child.Margin = new Thickness(5, 7, 11, 13);
            child.HorizontalAlignment = HorizontalAlignment.Right;
            child.VerticalAlignment = VerticalAlignment.Bottom;
            var constrained = (Border)panel.Children[1];
            constrained.MaxWidth = 60;
            constrained.MaxHeight = 30;

            Layout(panel, new Size(320, 100));

            double cellWidth = stretch ? 155 : 100;
            Assert.Equal(new Size(40, 20), child.RenderSize);
            Assert.Equal(new Point(cellWidth - 11 - 40, 100 - 13 - 20), child.TranslatePoint(default, panel));
            Assert.Equal(new Size(60, 30), constrained.RenderSize);
            Assert.Equal(40, child.Width);
            Assert.Equal(20, child.Height);
            Assert.True(double.IsNaN(constrained.Width));
            Assert.True(double.IsNaN(constrained.Height));
        });
    }

    [Fact]
    public void OversizedChildren_UseTheSameClippingAndMinimumSizeRulesAsUniformGrid()
    {
        WpfTestThread.Run(() =>
        {
            var native = new UniformGrid { Columns = 2 };
            var panel = new AdaptiveUniformGrid { Columns = 2 };
            foreach (UniformGrid grid in new UniformGrid[] { native, panel })
            {
                grid.Children.Add(new Border { Width = 240, Height = 140, Margin = new Thickness(5) });
                grid.Children.Add(new Border { MinWidth = 180, MinHeight = 120 });
                Layout(grid, new Size(200, 80));
            }

            AssertSameLayout(native, panel);
        });
    }

    [Theory]
    [InlineData(0, 0, 0, 8)]
    [InlineData(0, 3, 1, 8)]
    [InlineData(3, 0, 2, 8)]
    [InlineData(2, 3, 0, 8)]
    [InlineData(4, 3, 0, 2)]
    [InlineData(0, 3, 6, 5)]
    [InlineData(0, 0, 0, 0)]
    public void DisabledWithoutSpacing_MatchesNativeUniformGrid(int rows, int columns, int firstColumn, int count)
    {
        WpfTestThread.Run(() =>
        {
            var native = new UniformGrid { Rows = rows, Columns = columns, FirstColumn = firstColumn };
            var panel = new AdaptiveUniformGrid
            {
                Rows = rows,
                Columns = columns,
                FirstColumn = firstColumn,
                IsRowStretchEnabled = false,
            };
            foreach (UniformGrid grid in new UniformGrid[] { native, panel })
            {
                for (int index = 0; index < count; index++)
                {
                    grid.Children.Add(new Border
                    {
                        Width = index == 0 ? 40 : double.NaN,
                        Height = index == 0 ? 20 : double.NaN,
                        Margin = new Thickness(index),
                        Visibility = index % 3 == 1 ? Visibility.Collapsed : Visibility.Visible,
                    });
                }

                Layout(grid, new Size(320, 170));
            }

            AssertSameLayout(native, panel);
            // Layout follows the native offset rules, but normalization must not overwrite the request.
            Assert.Equal(firstColumn, panel.FirstColumn);
        });
    }

    [Fact]
    public void DisabledWithSpacing_PacksCollapsedChildrenAndLeavesTrailingCellsEmpty()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(6);
            panel.Children[1].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 110));
            panel.IsRowStretchEnabled = false;
            Layout(panel, new Size(320, 110));

            AssertSlot(panel, 2, new Rect(110, 0, 100, 50));
            AssertSlot(panel, 3, new Rect(220, 0, 100, 50));
            AssertSlot(panel, 4, new Rect(0, 60, 100, 50));
            AssertSlot(panel, 5, new Rect(110, 60, 100, 50));

            panel.IsRowStretchEnabled = true;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 2, new Rect(165, 0, 155, 50));
            AssertSlot(panel, 3, new Rect(0, 60, 100, 50));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void StretchMode_IgnoresFirstColumnForGroupingAndHeightDivision(int rows)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(6);
            panel.Rows = rows;
            panel.FirstColumn = 1;
            panel.Children[0].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 170));

            double cellHeight = rows == 0 ? 80 : 50;
            AssertSlot(panel, 1, new Rect(0, 0, 155, cellHeight));
            AssertSlot(panel, 2, new Rect(165, 0, 155, cellHeight));
            AssertSlot(panel, 3, new Rect(0, cellHeight + 10, 100, cellHeight));

            panel.Children[1].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 2, new Rect(0, 0, 320, cellHeight));
            AssertSlot(panel, 3, new Rect(0, cellHeight + 10, 100, cellHeight));

            // The second logical row moves only when every member of the first is collapsed.
            panel.Children[2].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 3, new Rect(0, 0, 100, rows == 0 ? 170 : 80));
            Assert.Equal(1, panel.FirstColumn);
        });
    }

    [Theory]
    [InlineData(0D)]
    [InlineData(10D)]
    public void FirstColumnBinding_SurvivesModeSpacingAndColumnChanges(double spacing)
    {
        WpfTestThread.Run(() =>
        {
            var source = new Border { Tag = 1 };
            var panel = CreatePanel(3);
            panel.RowSpacing = spacing;
            panel.ColumnSpacing = spacing;
            var expression = BindingOperations.SetBinding(
                panel,
                UniformGrid.FirstColumnProperty,
                new Binding(nameof(Border.Tag)) { Source = source, Mode = BindingMode.TwoWay }
            );

            Layout(panel, new Size(320, 110));
            double cellWidth = (320 - (2 * spacing)) / 3;
            AssertSlot(panel, 0, new Rect(0, 0, cellWidth, 110));

            panel.IsRowStretchEnabled = false;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 0, new Rect(cellWidth + spacing, 0, cellWidth, (110 - spacing) / 2));
            AssertSlot(panel, 2, new Rect(0, (110 + spacing) / 2, cellWidth, (110 - spacing) / 2));

            // An invalid offset is ignored even for automatic columns; it must remain bound
            // across zero/nonzero spacing so increasing Columns can make it effective again.
            foreach (int columns in new[] { 1, 0 })
            {
                panel.Columns = columns;
                foreach (double currentSpacing in new[] { 0D, 10D })
                {
                    panel.RowSpacing = currentSpacing;
                    panel.ColumnSpacing = currentSpacing;
                    Layout(panel, new Size(320, 110));
                    Assert.Equal(0, LayoutInformation.GetLayoutSlot((FrameworkElement)panel.Children[0]).X);
                    Assert.Equal(1, panel.FirstColumn);
                    Assert.Equal(1, source.Tag);
                    Assert.Same(expression, BindingOperations.GetBindingExpression(panel, UniformGrid.FirstColumnProperty));
                }
            }

            panel.Columns = 3;
            panel.RowSpacing = spacing;
            panel.ColumnSpacing = spacing;
            source.Tag = 2;
            Layout(panel, new Size(320, 110));
            Assert.Equal(2, panel.FirstColumn);
            AssertSlot(panel, 0, new Rect(2 * (cellWidth + spacing), 0, cellWidth, (110 - spacing) / 2));

            panel.IsRowStretchEnabled = true;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 0, new Rect(0, 0, cellWidth, 110));
            panel.IsRowStretchEnabled = false;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 0, new Rect(2 * (cellWidth + spacing), 0, cellWidth, (110 - spacing) / 2));
            Assert.Same(expression, BindingOperations.GetBindingExpression(panel, UniformGrid.FirstColumnProperty));
        });
    }

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    public void CollapsedOverflowRows_DoNotChangeInGridRowHeights(int count)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(count);
            panel.Rows = 2;
            panel.Columns = 2;
            foreach (Border child in panel.Children)
            {
                child.Height = 20;
            }

            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 2, new Rect(0, 60, 155, 50));
            AssertSlot(panel, 4, new Rect(0, 120, 155, 50));

            for (int index = 4; index < count; index++)
            {
                panel.Children[index].Visibility = Visibility.Collapsed;
            }

            Layout(panel, new Size(320, 110));
            Assert.Equal(new Size(10, 50), panel.DesiredSize);
            AssertSlot(panel, 0, new Rect(0, 0, 155, 50));
            AssertSlot(panel, 2, new Rect(0, 60, 155, 50));

            // Only the first (in-grid) collapsed row changes the height division.
            panel.Children[0].Visibility = Visibility.Collapsed;
            panel.Children[1].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 110));
            Assert.Equal(new Size(10, 20), panel.DesiredSize);
            AssertSlot(panel, 2, new Rect(0, 0, 155, 110));

            panel.Children[4].Visibility = Visibility.Visible;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 2, new Rect(0, 0, 155, 110));
            AssertSlot(panel, 4, new Rect(0, 120, 320, 110));
        });
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void AutomaticColumns_IncludeCollapsedChildrenWhenPreservingRows(int rows)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(8);
            panel.Columns = 0;
            panel.Rows = rows;
            panel.Children[0].Visibility = Visibility.Collapsed;
            panel.Children[1].Visibility = Visibility.Collapsed;
            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 2, new Rect(0, 0, 320, 50));
            AssertSlot(panel, 3, new Rect(0, 60, 100, 50));
        });
    }

    [Fact]
    public void WrappingContent_IsMeasuredAtTheExpandedRowWidth()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(0);
            panel.Children.Add(new Border { Visibility = Visibility.Collapsed });
            var child = new WrappingElement();
            panel.Children.Add(child);
            panel.Measure(new Size(320, double.PositiveInfinity));

            Assert.Equal(320, child.Constraint.Width);
            Assert.Equal(20, panel.DesiredSize.Height);
            Assert.Equal(320, panel.DesiredSize.Width);
        });
    }

    [Fact]
    public void UnboundedMeasure_IncludesOnlyRequiredRowSpacingAndEqualCellWidths()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(4);
            for (int index = 0; index < 4; index++)
            {
                ((Border)panel.Children[index]).Width = index == 3 ? 200 : 40;
                ((Border)panel.Children[index]).Height = 20;
            }

            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(200, 50), panel.DesiredSize);
        });
    }

    [Fact]
    public void EmptyOrEntirelyCollapsedPanel_HasNoPhantomSpacingAndCanRecover()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(0);
            Layout(panel, new Size(320, 170));
            Assert.Equal(default, panel.DesiredSize);
            panel.Children.Add(new Border { Visibility = Visibility.Collapsed });
            Layout(panel, new Size(320, 170));
            Assert.Equal(default, panel.DesiredSize);
            panel.Children[0].Visibility = Visibility.Visible;
            Layout(panel, new Size(320, 170));
            AssertSlot(panel, 0, new Rect(0, 0, 320, 170));
        });
    }

    [Fact]
    public void CollectionColumnsAndSpacingChanges_RecomputeGrouping()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(4);
            Layout(panel, new Size(320, 110));
            panel.Children.RemoveAt(0);
            panel.Columns = 2;
            panel.ColumnSpacing = 20;
            panel.RowSpacing = 30;
            Layout(panel, new Size(320, 110));
            AssertSlot(panel, 0, new Rect(0, 0, 150, 40));
            AssertSlot(panel, 1, new Rect(170, 0, 150, 40));
            AssertSlot(panel, 2, new Rect(0, 70, 320, 40));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TemplateCollapsesWholeRow_FirstLayoutUsesCurrentVisibility(bool stretch)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(0);
            panel.IsRowStretchEnabled = stretch;
            panel.Columns = 1;
            var control = new TemplateCallbackControl();
            control.TemplateApplied = () => control.Visibility = Visibility.Collapsed;
            var child = new WrappingElement();
            panel.Children.Add(control);
            panel.Children.Add(child);

            Layout(panel, new Size(320, 110));

            Assert.Equal(new Size(320, 20), panel.DesiredSize);
            AssertSlot(panel, 1, new Rect(0, 0, 320, 110));
            Assert.Equal(110, child.Constraint.Height);
        });
    }

    [Fact]
    public void TemplateRevealsEarlierRow_FirstLayoutMeasuresAndArrangesIt()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(0);
            panel.Columns = 1;
            var child = new WrappingElement { Visibility = Visibility.Collapsed };
            panel.Children.Add(child);
            panel.Children.Add(new TemplateCallbackControl
            {
                TemplateApplied = () => child.Visibility = Visibility.Visible,
            });

            Layout(panel, new Size(320, 110));

            Assert.Equal(new Size(320, 50), panel.DesiredSize);
            AssertSlot(panel, 0, new Rect(0, 0, 320, 50));
            AssertSlot(panel, 1, new Rect(0, 60, 320, 50));
            Assert.Equal(new Size(320, 50), child.Constraint);
        });
    }

    [Theory]
    [InlineData(nameof(UniformGrid.Rows))]
    [InlineData(nameof(UniformGrid.Columns))]
    [InlineData(nameof(UniformGrid.FirstColumn))]
    [InlineData(nameof(AdaptiveUniformGrid.RowSpacing))]
    [InlineData(nameof(AdaptiveUniformGrid.ColumnSpacing))]
    [InlineData(nameof(AdaptiveUniformGrid.IsRowStretchEnabled))]
    public void TemplateChangesLayoutSetting_MatchesSettingItBeforeMeasure(string property)
    {
        WpfTestThread.Run(() =>
        {
            var expected = CreatePanel(0);
            var actual = CreatePanel(0);
            foreach (AdaptiveUniformGrid panel in new[] { expected, actual })
            {
                panel.IsRowStretchEnabled = property != nameof(UniformGrid.FirstColumn);
                panel.Children.Add(new TemplateCallbackControl
                {
                    TemplateApplied = panel == actual ? () => ChangeSetting(panel) : null,
                });
                for (int index = 0; index < 3; index++)
                {
                    panel.Children.Add(new Border { Width = 40, Height = 20 });
                }
            }

            ChangeSetting(expected);
            Layout(expected, new Size(320, 170));
            Layout(actual, new Size(320, 170));
            AssertSameLayout(expected, actual);

            void ChangeSetting(AdaptiveUniformGrid panel)
            {
                switch (property)
                {
                    case nameof(UniformGrid.Rows): panel.Rows = 4; break;
                    case nameof(UniformGrid.Columns): panel.Columns = 2; break;
                    case nameof(UniformGrid.FirstColumn): panel.FirstColumn = 2; break;
                    case nameof(AdaptiveUniformGrid.RowSpacing): panel.RowSpacing = 30; break;
                    case nameof(AdaptiveUniformGrid.ColumnSpacing): panel.ColumnSpacing = 30; break;
                    case nameof(AdaptiveUniformGrid.IsRowStretchEnabled): panel.IsRowStretchEnabled = false; break;
                }
            }
        });
    }

    [Fact]
    public void SuccessiveTemplateVisibilityChanges_ConvergeThroughNormalLayoutQueue()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(0);
            panel.Columns = 1;
            var first = new WrappingElement { Visibility = Visibility.Collapsed };
            var second = new TemplateCallbackControl
            {
                Visibility = Visibility.Collapsed,
                TemplateApplied = () => first.Visibility = Visibility.Visible,
            };
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Children.Add(new TemplateCallbackControl
            {
                TemplateApplied = () => second.Visibility = Visibility.Visible,
            });

            Layout(panel, new Size(320, 170));
            panel.UpdateLayout();

            Assert.True(panel.IsMeasureValid);
            Assert.True(panel.IsArrangeValid);
            Assert.Equal(new Size(320, 80), panel.DesiredSize);
            AssertSlot(panel, 0, new Rect(0, 0, 320, 50));
            AssertSlot(panel, 1, new Rect(0, 60, 320, 50));
            AssertSlot(panel, 2, new Rect(0, 120, 320, 50));
        });
    }

    [Fact]
    public void DisabledWithSubDipCells_WrapsAtTheConfiguredColumnCount()
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(6);
            panel.IsRowStretchEnabled = false;
            panel.RowSpacing = 0;
            panel.ColumnSpacing = 0;

            Layout(panel, new Size(0.6, 1));

            AssertSlot(panel, 2, new Rect(0.4, 0, 0.2, 0.5));
            AssertSlot(panel, 3, new Rect(0, 0.5, 0.2, 0.5));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MaximumFiniteSpacing_UnboundedMeasureAndArrangeRemainFinite(bool stretch)
    {
        WpfTestThread.Run(() =>
        {
            var panel = CreatePanel(9);
            panel.IsRowStretchEnabled = stretch;
            panel.RowSpacing = double.MaxValue;
            panel.ColumnSpacing = double.MaxValue;

            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(double.MaxValue, double.MaxValue), panel.DesiredSize);
            panel.Arrange(new Rect(0, 0, 320, 170));

            AssertSlot(panel, 8, new Rect(double.MaxValue, double.MaxValue, 0, 0));
        });
    }

    [Theory]
    [InlineData(-1D)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidSpacing_IsRejected(double value)
    {
        WpfTestThread.Run(() =>
        {
            var panel = new AdaptiveUniformGrid();
            Assert.Throws<ArgumentException>(() => panel.RowSpacing = value);
            Assert.Throws<ArgumentException>(() => panel.ColumnSpacing = value);
        });
    }

    [Fact]
    public void SpacingLargerThanAvailableSize_DoesNotProduceNegativeSlots()
    {
        WpfTestThread.Run(() =>
        {
            // Call the layout override through a probe to inspect a genuinely undersized allocation;
            // FrameworkElement otherwise expands the panel to its unclipped desired spacing size.
            var probe = new ArrangeProbe { Columns = 3, RowSpacing = 10, ColumnSpacing = 10 };
            for (int index = 0; index < 6; index++)
            {
                probe.Children.Add(new Border());
            }

            probe.Measure(new Size(5, 5));
            probe.ArrangeContent(new Size(5, 5));
            AssertSlot(probe, 0, new Rect(0, 0, 0, 0));
            AssertSlot(probe, 5, new Rect(20, 10, 0, 0));
        });
    }

    [Fact]
    public void PublicXamlNamespace_ExposesThePanelAndItsProperties()
    {
        WpfTestThread.Run(() =>
        {
            const string xaml = "<ui:AdaptiveUniformGrid xmlns:ui=\"http://schemas.thezendev.com/wpf/2027\" Columns=\"3\" RowSpacing=\"8\" ColumnSpacing=\"12\" />";
            var panel = Assert.IsType<AdaptiveUniformGrid>(XamlReader.Parse(xaml));
            Assert.True(panel.IsRowStretchEnabled);
            Assert.Equal(3, panel.Columns);
            Assert.Equal(8, panel.RowSpacing);
            Assert.Equal(12, panel.ColumnSpacing);
        });
    }

    private static AdaptiveUniformGrid CreatePanel(int count)
    {
        var panel = new AdaptiveUniformGrid { Columns = 3, RowSpacing = 10, ColumnSpacing = 10 };
        for (int index = 0; index < count; index++)
        {
            panel.Children.Add(new Border());
        }

        return panel;
    }

    private static void Layout(FrameworkElement panel, Size size)
    {
        panel.Measure(size);
        panel.Arrange(new Rect(default, size));
    }

    private static void AssertSlot(Panel panel, int index, Rect expected)
    {
        Rect actual = LayoutInformation.GetLayoutSlot((FrameworkElement)panel.Children[index]);
        Assert.Equal(expected.X, actual.X, 6);
        Assert.Equal(expected.Y, actual.Y, 6);
        Assert.Equal(expected.Width, actual.Width, 6);
        Assert.Equal(expected.Height, actual.Height, 6);
    }

    private static void AssertSameLayout(UniformGrid expected, UniformGrid actual)
    {
        Assert.Equal(expected.DesiredSize, actual.DesiredSize);
        for (int index = 0; index < expected.Children.Count; index++)
        {
            var expectedChild = (FrameworkElement)expected.Children[index];
            var actualChild = (FrameworkElement)actual.Children[index];
            Assert.Equal(LayoutInformation.GetLayoutSlot(expectedChild), LayoutInformation.GetLayoutSlot(actualChild));
            Assert.Equal(expectedChild.RenderSize, actualChild.RenderSize);
            Assert.Equal(expectedChild.TranslatePoint(default, expected), actualChild.TranslatePoint(default, actual));
            Assert.Equal(VisualTreeHelper.GetClip(expectedChild)?.Bounds, VisualTreeHelper.GetClip(actualChild)?.Bounds);
        }
    }

    private sealed class WrappingElement : FrameworkElement
    {
        public Size Constraint { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            Constraint = availableSize;
            return new Size(availableSize.Width, availableSize.Width < 200 ? 80 : 20);
        }
    }

    private sealed class ArrangeProbe : AdaptiveUniformGrid
    {
        public void ArrangeContent(Size size) => ArrangeOverride(size);
    }

    private sealed class TemplateCallbackControl : Control
    {
        public TemplateCallbackControl()
        {
            Template = (ControlTemplate)XamlReader.Parse(
                "<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"><Border /></ControlTemplate>"
            );
        }

        public Action? TemplateApplied { get; set; }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            TemplateApplied?.Invoke();
        }
    }
}
