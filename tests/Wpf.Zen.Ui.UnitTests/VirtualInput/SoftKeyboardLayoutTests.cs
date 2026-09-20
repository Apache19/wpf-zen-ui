// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Wpf.Zen.Ui.Controls;
using Wpf.Zen.Ui.VirtualInput.Controls;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;
using Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class SoftKeyboardLayoutTests
{
    [Fact]
    public void ApplyingTemplate_EndsHeldKeyAndAlternatePresentation()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard { IsOpen = true };
            var window = new Window { Content = keyboard, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var cap = FindVisualDescendant<SoftKeyboardKeyControl>(keyboard)!;
                cap.Key = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "a", "a", text: "a", alternates: ["á"]);
                cap.BeginPress();
                cap.AdvanceHold();
                Assert.True(cap.IsAlternatePopupOpen);

                keyboard.Template = new ControlTemplate(typeof(SoftKeyboard));
                keyboard.ApplyTemplate();

                Assert.False(cap.IsPressed);
                Assert.False(cap.IsAlternatePopupOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void UnconstrainedPanel_ReservesLargestUniformTrackForTheCompleteGrid()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 3, ColumnSpacing = 4, RowSpacing = 6 };
            var first = new Border { Width = 100, Height = 40 };
            var second = new Border { Width = 20, Height = 40 };
            var third = new Border { Width = 80, Height = 30 };
            SoftKeyboardPanel.SetColumn(second, 1);
            SoftKeyboardPanel.SetColumnSpan(second, 2);
            SoftKeyboardPanel.SetRow(third, 4);
            SoftKeyboardPanel.SetColumnSpan(third, 3);
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Children.Add(third);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(308, 86), panel.DesiredSize);
            panel.Arrange(new Rect(panel.DesiredSize));
            Assert.Equal(100, LayoutInformation.GetLayoutSlot(first).Width);
            Assert.Equal(204, LayoutInformation.GetLayoutSlot(second).Width);
            Assert.Equal(new Rect(0, 46, 308, 40), LayoutInformation.GetLayoutSlot(third));

            second.Visibility = Visibility.Collapsed;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(308, 86), panel.DesiredSize);
        });
    }

    [Fact]
    public void FinitePanel_ArrangesRowsColumnsSpansAndSpacingFromOneAuthority()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 3, ColumnSpacing = 4, RowSpacing = 8 };
            var first = new Border();
            var second = new Border();
            var third = new Border();
            SoftKeyboardPanel.SetColumn(second, 1);
            SoftKeyboardPanel.SetColumnSpan(second, 2);
            SoftKeyboardPanel.SetRow(third, 1);
            SoftKeyboardPanel.SetColumnSpan(third, 3);
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Children.Add(third);

            panel.Measure(new Size(308, 88));
            panel.Arrange(new Rect(0, 0, 308, 88));

            Assert.Equal(new Rect(0, 0, 100, 40), LayoutInformation.GetLayoutSlot(first));
            Assert.Equal(new Rect(104, 0, 204, 40), LayoutInformation.GetLayoutSlot(second));
            Assert.Equal(new Rect(0, 48, 308, 40), LayoutInformation.GetLayoutSlot(third));
        });
    }

    [Fact]
    public void SharedColumns_KeepAStaggeredShortRowCenteredInsteadOfStretchingIt()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 20, ColumnSpacing = 0, RowSpacing = 0 };
            var fullRow = new Border();
            var firstShortRowKey = new Border();
            var lastShortRowKey = new Border();
            SoftKeyboardPanel.SetColumnSpan(fullRow, 20);
            SoftKeyboardPanel.SetRow(firstShortRowKey, 1);
            SoftKeyboardPanel.SetColumn(firstShortRowKey, 1);
            SoftKeyboardPanel.SetColumnSpan(firstShortRowKey, 2);
            SoftKeyboardPanel.SetRow(lastShortRowKey, 1);
            SoftKeyboardPanel.SetColumn(lastShortRowKey, 17);
            SoftKeyboardPanel.SetColumnSpan(lastShortRowKey, 2);
            panel.Children.Add(fullRow);
            panel.Children.Add(firstShortRowKey);
            panel.Children.Add(lastShortRowKey);

            panel.Measure(new Size(200, 80));
            panel.Arrange(new Rect(0, 0, 200, 80));

            Assert.Equal(new Rect(0, 0, 200, 40), LayoutInformation.GetLayoutSlot(fullRow));
            Assert.Equal(new Rect(10, 40, 20, 40), LayoutInformation.GetLayoutSlot(firstShortRowKey));
            Assert.Equal(new Rect(170, 40, 20, 40), LayoutInformation.GetLayoutSlot(lastShortRowKey));
        });
    }

    [Fact]
    public void UndersizedPanel_ContractsSpacingAndKeepsEverySlotInsideFinalBounds()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 3, ColumnSpacing = 10, RowSpacing = 10 };
            for (var index = 0; index < 3; index++)
            {
                var child = new Border();
                SoftKeyboardPanel.SetColumn(child, index);
                panel.Children.Add(child);
            }

            panel.Measure(new Size(3, 1));
            panel.Arrange(new Rect(0, 0, 3, 1));

            foreach (FrameworkElement child in panel.Children)
            {
                Rect slot = LayoutInformation.GetLayoutSlot(child);
                Assert.InRange(slot.Left, 0, 3);
                Assert.InRange(slot.Right, 0, 3);
                Assert.InRange(slot.Top, 0, 1);
                Assert.InRange(slot.Bottom, 0, 1);
            }

            Assert.True(panel.ClipToBounds);
        });
    }

    [Fact]
    public void PlacementValues_RejectInvalidRowsColumnsSpansCountsAndSpacing()
    {
        WpfTestThread.Run(() =>
        {
            var child = new Border();
            var panel = new SoftKeyboardPanel();

            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetRow(child, -1));
            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetColumn(child, -1));
            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetColumnSpan(child, 0));
            Assert.Throws<ArgumentException>(() => panel.ColumnCount = 0);
            Assert.Throws<ArgumentException>(() => panel.RowSpacing = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => panel.ColumnSpacing = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", row: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", column: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", columnSpan: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SoftKeyboardLayout([], 0));
            Assert.Throws<ArgumentException>(() => new SoftKeyboardLayout(
                [new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", column: 2, columnSpan: 2)],
                3));
        });
    }

    [Fact]
    public void DefaultLetterLayout_UsesHalfKeyTracksForNaturalStaggerAndWideCommandCaps()
    {
        SoftKeyboardLayout layout = Assert.IsType<SoftKeyboardLayout>(DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
            new SoftKeyboardLayoutContext(
                System.Globalization.CultureInfo.GetCultureInfo("en-US"),
                System.Windows.Input.InputScopeNameValue.Default,
                SoftKeyboardPages.Letters,
                SoftKeyboardShiftState.Off
            )
        ));

        SoftKeyboardKey q = Assert.Single(layout.Keys, key => key.Key == System.Windows.Input.Key.Q);
        SoftKeyboardKey a = Assert.Single(layout.Keys, key => key.Key == System.Windows.Input.Key.A);
        SoftKeyboardKey z = Assert.Single(layout.Keys, key => key.Key == System.Windows.Input.Key.Z);
        SoftKeyboardKey shift = Assert.Single(layout.Keys, key => key.Kind == SoftKeyboardKeyKind.Shift);
        SoftKeyboardKey backspace = Assert.Single(layout.Keys, key => key.Key == System.Windows.Input.Key.Back);

        Assert.Equal(20, layout.ColumnCount);
        Assert.Equal((0, 0, 2), (q.Row, q.Column, q.ColumnSpan));
        Assert.Equal((1, 1, 2), (a.Row, a.Column, a.ColumnSpan));
        Assert.Equal((2, 0, 2), (shift.Row, shift.Column, shift.ColumnSpan));
        Assert.Equal((2, 2, 2), (z.Row, z.Column, z.ColumnSpan));
        Assert.Equal((2, 16, 4), (backspace.Row, backspace.Column, backspace.ColumnSpan));
    }

    [Fact]
    public void DefaultTemplate_ForwardsSurfaceProperties()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var keyboard = new SoftKeyboard
            {
                IsOpen = true,
                Background = Brushes.Red,
                BorderBrush = Brushes.Blue,
                BorderThickness = new Thickness(3),
                Padding = new Thickness(12),
                CornerRadius = new CornerRadius(16),
                RowSpacing = 7,
                ColumnSpacing = 9,
            };
            var window = new Window { Content = keyboard, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var root = Assert.IsType<Border>(keyboard.Template.FindName("KeyboardRoot", keyboard));
                Assert.Same(keyboard.Background, root.Background);
                Assert.Same(keyboard.BorderBrush, root.BorderBrush);
                Assert.Equal(keyboard.BorderThickness, root.BorderThickness);
                Assert.Equal(keyboard.Padding, root.Padding);
                Assert.Equal(keyboard.CornerRadius, root.CornerRadius);
                var keys = Assert.IsType<SoftKeyboardKeyList>(keyboard.Template.FindName("PART_Keys", keyboard));
                var panel = Assert.IsType<SoftKeyboardPanel>(FindVisualDescendant<SoftKeyboardPanel>(keys));
                Assert.Equal(keyboard.CurrentLayout.ColumnCount, panel.ColumnCount);
                Assert.Equal(7, panel.RowSpacing);
                Assert.Equal(9, panel.ColumnSpacing);
                int wideKeyIndex = Enumerable.Range(0, keyboard.CurrentLayout.Keys.Count)
                    .First(index => keyboard.CurrentLayout.Keys[index].ColumnSpan > 2);
                SoftKeyboardKey wideKey = keyboard.CurrentLayout.Keys[wideKeyIndex];
                var container = Assert.IsType<SoftKeyboardKeyControl>(
                    keys.ItemContainerGenerator.ContainerFromIndex(wideKeyIndex)
                );
                Assert.Same(wideKey, container.Key);
                Assert.Same(wideKey.Content, container.Content);
                Assert.Same(panel, VisualTreeHelper.GetParent(container));
                Assert.Equal(wideKey.AutomationName, System.Windows.Automation.AutomationProperties.GetName(container));
                Assert.Equal(wideKey.Row, SoftKeyboardPanel.GetRow(container));
                Assert.Equal(wideKey.Column, SoftKeyboardPanel.GetColumn(container));
                Assert.Equal(wideKey.ColumnSpan, SoftKeyboardPanel.GetColumnSpan(container));
                keyboard.Padding = new Thickness(20);
                Assert.Equal(keyboard.Padding, root.Padding);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Verifies snapshot replacement also ends interactions owned by the outgoing container.
    /// </summary>
    [Fact]
    public void KeyContainers_ReplaceTheirSnapshotsAndCancelOutgoingInteractions()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var first = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "a", "letter a", text: "a",
                alternates: ["á"]);
            var replacement = new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "b", "letter b", text: "b",
                row: 3, column: 2, columnSpan: 2);
            var list = new SoftKeyboardKeyList { ItemsSource = new[] { first } };
            var window = new Window { Content = list, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var outgoing = Assert.IsType<SoftKeyboardKeyControl>(list.ItemContainerGenerator.ContainerFromIndex(0));
                outgoing.BeginPress();
                outgoing.AdvanceHold();
                Assert.True(outgoing.IsAlternatePopupOpen);

                list.ItemsSource = new[] { replacement };
                window.UpdateLayout();

                Assert.False(outgoing.IsPressed);
                Assert.False(outgoing.IsAlternatePopupOpen);
                Assert.Null(outgoing.Key);
                var current = Assert.IsType<SoftKeyboardKeyControl>(list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Same(replacement, current.Key);
                Assert.Equal("b", current.Content);
                Assert.Equal("letter b", System.Windows.Automation.AutomationProperties.GetName(current));
                Assert.Equal(3, SoftKeyboardPanel.GetRow(current));
                Assert.Equal(2, SoftKeyboardPanel.GetColumn(current));
                Assert.Equal(2, SoftKeyboardPanel.GetColumnSpan(current));
                Assert.False(current.Focusable);
                Assert.False(current.IsTabStop);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Preserves the standard ItemsControl styling and content presentation extension points.
    /// </summary>
    [Fact]
    public void KeyContainers_ForwardContentTemplatesAndContainerStyles()
    {
        WpfApplicationTestHost.Run(() =>
        {
            var template = (DataTemplate)System.Windows.Markup.XamlReader.Parse(
                "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"
                + "<TextBlock Text='{Binding}' /></DataTemplate>"
            );
            var style = new Style(typeof(SoftKeyboardKeyControl));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13)));
            var list = new SoftKeyboardKeyList
            {
                ItemsSource = new[] { new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "custom", "custom", text: "a") },
                ItemTemplate = template,
                ItemContainerStyle = style,
            };
            var window = new Window { Content = list, ShowInTaskbar = false };
            try
            {
                window.Show();
                window.UpdateLayout();
                var container = Assert.IsType<SoftKeyboardKeyControl>(list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.Same(template, container.ContentTemplate);
                Assert.Equal(new Thickness(13), container.Padding);
                var presenter = FindVisualDescendant<ContentPresenter>(container)!;
                Assert.Equal("custom", presenter.Content);
                Assert.Equal("custom", FindVisualDescendant<System.Windows.Controls.TextBlock>(presenter)!.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    /// <summary>
    /// Exercises row-cache invalidation through changes that can leave the child count unchanged.
    /// </summary>
    [Fact]
    public void CachedRows_TrackPlacementVisibilityAndReplacementAtTheSameChildCount()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 4, ColumnSpacing = 0, RowSpacing = 0 };
            var first = new Border();
            var second = new Border();
            panel.Children.Add(first);
            panel.Children.Add(second);
            SoftKeyboardPanel.SetRow(first, 10);
            SoftKeyboardPanel.SetRow(second, 20);

            void Layout()
            {
                panel.Measure(new Size(400, 80));
                panel.Arrange(new Rect(0, 0, 400, 80));
            }

            Layout();
            Assert.Equal(new Rect(0, 40, 100, 40), LayoutInformation.GetLayoutSlot(second));

            SoftKeyboardPanel.SetRow(second, 5);
            SoftKeyboardPanel.SetColumn(second, 1);
            SoftKeyboardPanel.SetColumnSpan(second, 2);
            Layout();
            Assert.Equal(new Rect(100, 0, 200, 40), LayoutInformation.GetLayoutSlot(second));
            Assert.Equal(new Rect(0, 40, 100, 40), LayoutInformation.GetLayoutSlot(first));

            second.Visibility = Visibility.Hidden;
            Layout();
            Assert.Equal(40, LayoutInformation.GetLayoutSlot(first).Height);
            second.Visibility = Visibility.Collapsed;
            Layout();
            Assert.Equal(new Rect(0, 0, 100, 80), LayoutInformation.GetLayoutSlot(first));

            second.Visibility = Visibility.Visible;
            Layout();
            Assert.Equal(new Rect(100, 0, 200, 40), LayoutInformation.GetLayoutSlot(second));

            var replacement = new Border();
            SoftKeyboardPanel.SetRow(replacement, 30);
            panel.Children.Remove(second);
            panel.Children.Add(replacement);
            Layout();
            Assert.Equal(new Rect(0, 0, 100, 40), LayoutInformation.GetLayoutSlot(first));
            Assert.Equal(new Rect(0, 40, 100, 40), LayoutInformation.GetLayoutSlot(replacement));

            first.Visibility = Visibility.Collapsed;
            replacement.Visibility = Visibility.Collapsed;
            Layout();
            Assert.Equal(default, panel.DesiredSize);
            panel.Children.Clear();
            Layout();
            first.Visibility = Visibility.Visible;
            panel.Children.Add(first);
            Layout();
            Assert.Equal(new Rect(0, 0, 100, 80), LayoutInformation.GetLayoutSlot(first));
        });
    }

    /// <summary>
    /// Keeps cached row membership independent of current column and span clamping.
    /// </summary>
    [Fact]
    public void CachedRows_ApplyColumnAndSpanChangesWithoutChangingRows()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnCount = 4, ColumnSpacing = 0, RowSpacing = 0 };
            var child = new Border();
            panel.Children.Add(child);
            panel.Measure(new Size(400, 80));
            panel.Arrange(new Rect(0, 0, 400, 80));

            SoftKeyboardPanel.SetColumn(child, 2);
            SoftKeyboardPanel.SetColumnSpan(child, 8);
            panel.Measure(new Size(400, 80));
            panel.Arrange(new Rect(0, 0, 400, 80));
            Assert.Equal(new Rect(200, 0, 200, 80), LayoutInformation.GetLayoutSlot(child));

            panel.ColumnCount = 2;
            panel.Measure(new Size(400, 80));
            panel.Arrange(new Rect(0, 0, 400, 80));
            Assert.Equal(new Rect(200, 0, 200, 80), LayoutInformation.GetLayoutSlot(child));
        });
    }

    [Fact]
    public void ApplicationButtonClick_CanBubbleThroughKeyboard()
    {
        WpfTestThread.Run(() =>
        {
            var keyboard = new SoftKeyboard();
            var button = new System.Windows.Controls.Button();
            var click = new RoutedEventArgs(ButtonBase.ClickEvent, button);
            keyboard.RaiseEvent(click);
            Assert.False(click.Handled);
        });
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            T? descendant = FindVisualDescendant<T>(child);
            if (descendant is not null)
            {
                return descendant;
            }
        }

        return null;
    }
}
