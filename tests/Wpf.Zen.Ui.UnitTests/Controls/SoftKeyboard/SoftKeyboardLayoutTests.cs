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

namespace Wpf.Zen.Ui.UnitTests.Controls.SoftKeyboardControl;

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
                var heldKey = typeof(SoftKeyboard).GetField("_heldKey", BindingFlags.Instance | BindingFlags.NonPublic)!;
                heldKey.SetValue(keyboard, new SoftKeyboardKey(
                    SoftKeyboardKeyKind.Text, "a", "a", text: "a", alternates: ["á"]));
                typeof(SoftKeyboard).GetMethod("OnHoldTimerTick", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(keyboard, [null, System.EventArgs.Empty]);
                Assert.True(keyboard.IsAlternatePopupOpen);

                keyboard.Template = new ControlTemplate(typeof(SoftKeyboard));
                keyboard.ApplyTemplate();

                Assert.Null(heldKey.GetValue(keyboard));
                Assert.False(keyboard.IsAlternatePopupOpen);
                Assert.Empty(keyboard.AlternateValues);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void UnconstrainedPanel_ReservesLargestWeightedSlotForEachRow()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnSpacing = 4, RowSpacing = 6 };
            var first = new Border { Width = 100, Height = 40 };
            var second = new Border { Width = 20, Height = 40 };
            var third = new Border { Width = 80, Height = 30 };
            SoftKeyboardPanel.SetWidthWeight(second, 2);
            SoftKeyboardPanel.SetRow(third, 4);
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Children.Add(third);
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(304, 86), panel.DesiredSize);
            panel.Arrange(new Rect(panel.DesiredSize));
            Assert.Equal(100, LayoutInformation.GetLayoutSlot(first).Width);
            Assert.Equal(200, LayoutInformation.GetLayoutSlot(second).Width);
            Assert.Equal(new Rect(0, 46, 304, 40), LayoutInformation.GetLayoutSlot(third));

            second.Visibility = Visibility.Collapsed;
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Assert.Equal(new Size(100, 86), panel.DesiredSize);
        });
    }

    [Fact]
    public void FinitePanel_ArrangesRowsWeightsAndSpacingFromOneAuthority()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnSpacing = 4, RowSpacing = 8 };
            var first = new Border();
            var second = new Border();
            var third = new Border();
            SoftKeyboardPanel.SetWidthWeight(second, 2);
            SoftKeyboardPanel.SetRow(third, 1);
            panel.Children.Add(first);
            panel.Children.Add(second);
            panel.Children.Add(third);

            panel.Measure(new Size(304, 88));
            panel.Arrange(new Rect(0, 0, 304, 88));

            Assert.Equal(new Rect(0, 0, 100, 40), LayoutInformation.GetLayoutSlot(first));
            Assert.Equal(new Rect(104, 0, 200, 40), LayoutInformation.GetLayoutSlot(second));
            Assert.Equal(new Rect(0, 48, 304, 40), LayoutInformation.GetLayoutSlot(third));
        });
    }

    [Fact]
    public void UndersizedPanel_ContractsSpacingAndKeepsEverySlotInsideFinalBounds()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new SoftKeyboardPanel { ColumnSpacing = 10, RowSpacing = 10 };
            for (var index = 0; index < 3; index++)
            {
                panel.Children.Add(new Border());
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
    public void PlacementValues_RejectInvalidRowsWeightsAndSpacing()
    {
        WpfTestThread.Run(() =>
        {
            var child = new Border();
            var panel = new SoftKeyboardPanel();

            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetRow(child, -1));
            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetWidthWeight(child, 0));
            Assert.Throws<ArgumentException>(() => SoftKeyboardPanel.SetWidthWeight(child, double.NaN));
            Assert.Throws<ArgumentException>(() => panel.RowSpacing = double.PositiveInfinity);
            Assert.Throws<ArgumentException>(() => panel.ColumnSpacing = -1);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", row: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "x", "x", text: "x", widthWeight: 0));
        });
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
                var keys = Assert.IsType<ItemsControl>(keyboard.Template.FindName("PART_Keys", keyboard));
                var panel = Assert.IsType<SoftKeyboardPanel>(FindVisualDescendant<SoftKeyboardPanel>(keys));
                Assert.Equal(7, panel.RowSpacing);
                Assert.Equal(9, panel.ColumnSpacing);
                int weightedIndex = Enumerable.Range(0, keyboard.CurrentLayout.Keys.Count)
                    .First(index => keyboard.CurrentLayout.Keys[index].WidthWeight > 1);
                SoftKeyboardKey weightedKey = keyboard.CurrentLayout.Keys[weightedIndex];
                var container = Assert.IsType<ContentPresenter>(
                    keys.ItemContainerGenerator.ContainerFromIndex(weightedIndex)
                );
                Assert.Equal(weightedKey.Row, SoftKeyboardPanel.GetRow(container));
                Assert.Equal(weightedKey.WidthWeight, SoftKeyboardPanel.GetWidthWeight(container));
                keyboard.Padding = new Thickness(20);
                Assert.Equal(keyboard.Padding, root.Padding);
            }
            finally
            {
                window.Close();
            }
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
