// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using ContextMenu = System.Windows.Controls.ContextMenu;
using UiButton = Wpf.Zen.Ui.Controls.Button;

namespace Wpf.Zen.Ui.UnitTests.Controls.ButtonControl;

public class ButtonAuditTests
{
    [Theory]
    [InlineData(ButtonIconPlacement.Left, Dock.Left, 0, 0, 8, 0)]
    [InlineData(ButtonIconPlacement.Top, Dock.Top, 0, 0, 0, 4)]
    [InlineData(ButtonIconPlacement.Right, Dock.Right, 8, 0, 0, 0)]
    [InlineData(ButtonIconPlacement.Bottom, Dock.Bottom, 0, 4, 0, 0)]
    public void DefaultTemplate_ArrangesIconAndDefaultGapForPlacement(
        ButtonIconPlacement placement,
        Dock expectedDock,
        double left,
        double top,
        double right,
        double bottom)
    {
        WpfTestThread.Run(() =>
        {
            var button = new UiButton
            {
                Content = "Content",
                Icon = new Border { Width = 12, Height = 12 },
                IconPlacement = placement,
            };

            ApplyTheme(button);

            var presenter = Assert.IsType<IconPresenter>(
                button.Template.FindName("iconPresenter", button));
            Assert.Equal(expectedDock, DockPanel.GetDock(presenter));
            Assert.Equal(new Thickness(left, top, right, bottom), presenter.Margin);
            Assert.Equal(button.IconMargin, presenter.Margin);
        });
    }

    [Theory]
    [InlineData(ControlAppearance.Normal)]
    [InlineData(ControlAppearance.Accent)]
    [InlineData(ControlAppearance.Transparent)]
    public void DisabledNullBrushes_RetainStyleProvidedNormalPalette(ControlAppearance appearance)
    {
        WpfTestThread.Run(() =>
        {
            var button = new UiButton
            {
                Appearance = appearance,
                DisabledBackground = null,
                DisabledBorderBrush = null,
                DisabledForeground = null,
            };
            ApplyTheme(button);
            var background = button.Background;
            var border = button.BorderBrush;
            var foreground = button.Foreground;

            button.IsEnabled = false;

            Assert.Same(background, button.Background);
            Assert.Same(border, button.BorderBrush);
            Assert.Same(foreground, button.Foreground);
            Assert.Same(background, button.VisualBackground);
            Assert.Same(border, button.VisualBorderBrush);
            Assert.Same(foreground, button.VisualForeground);
            button.IsEnabled = true;
            Assert.Same(background, button.VisualBackground);
        });
    }

    [Fact]
    public void HoverPalette_TracksBindingsAndStatePriority()
    {
        WpfTestThread.Run(() =>
        {
            var source = new UiButton { Foreground = Brushes.Orange };
            var button = new ButtonProbe
            {
                Foreground = Brushes.Blue,
                Background = Brushes.Black,
                BorderBrush = Brushes.Gray,
                MouseOverBackground = Brushes.Red,
                MouseOverBorderBrush = Brushes.Green,
                PressedForeground = Brushes.Purple,
                DisabledForeground = Brushes.White,
            };
            button.SetBinding(
                UiButton.MouseOverForegroundProperty,
                new Binding(nameof(UiButton.Foreground)) { Source = source });
            SetHovered(button, true);
            Assert.Same(Brushes.Orange, button.VisualForeground);
            Assert.Same(Brushes.Red, button.VisualBackground);
            Assert.Same(Brushes.Green, button.VisualBorderBrush);
            source.Foreground = Brushes.Yellow;
            Assert.Same(Brushes.Yellow, button.VisualForeground);
            button.SetPressed(true);
            Assert.Same(Brushes.Purple, button.VisualForeground);
            Assert.Same(Brushes.Black, button.VisualBackground);
            button.IsEnabled = false;
            Assert.Same(Brushes.White, button.VisualForeground);
            button.SetPressed(false);
            button.IsEnabled = true;
            Assert.Same(Brushes.Yellow, button.VisualForeground);
            SetHovered(button, false);
            Assert.Same(Brushes.Blue, button.VisualForeground);
            Assert.True(BindingOperations.IsDataBound(button, UiButton.MouseOverForegroundProperty));
        });
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void InvalidCornerRadius_IsRejectedBeforeTemplateApplication(double value)
    {
        WpfTestThread.Run(() =>
        {
            Assert.Throws<ArgumentException>(() => new UiButton().CornerRadius = new CornerRadius(value));
            Assert.Throws<ArgumentException>(() => new SplitButton().CornerRadius = new CornerRadius(value));
        });
    }

    [Fact]
    public void DropDownMenuReplacement_DetachesEventsAndClearsRemovedMenu()
    {
        WpfTestThread.Run(() =>
        {
            var oldMenu = new ContextMenu();
            var currentMenu = new ContextMenu();
            var button = new DropDownButton { Flyout = oldMenu };
            button.Flyout = currentMenu;
            oldMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, oldMenu));
            Assert.False(button.IsDropDownOpen);
            currentMenu.IsOpen = true;
            Assert.True(button.IsDropDownOpen);
            oldMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, oldMenu));
            Assert.True(button.IsDropDownOpen);
            button.Flyout = null;
            Assert.False(button.IsDropDownOpen);
            button.IsDropDownOpen = true;
            Assert.False(button.IsDropDownOpen);
            currentMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent, currentMenu));
            Assert.False(button.IsDropDownOpen);
        });
    }

    [Fact]
    public void DropDownOpenBinding_OperatesPopupAndSurvivesDismissalAndReplacement()
    {
        WpfTestThread.Run(() =>
        {
            var menu = new ContextMenu { Items = { new System.Windows.Controls.MenuItem { Header = "Item" } } };
            var button = new DropDownButton { Flyout = menu, Width = 180 };
            var state = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = false };
            button.SetBinding(
                DropDownButton.IsDropDownOpenProperty,
                new Binding(nameof(state.IsChecked)) { Source = state });
            var window = new Window { Content = button, Width = 240, Height = 120 };
            try
            {
                window.Show();
                state.IsChecked = true;
                DrainDispatcher();
                Assert.True(menu.IsOpen);
                Assert.Same(button, menu.PlacementTarget);
                Assert.Equal(PlacementMode.Bottom, menu.Placement);
                Assert.Equal(button.ActualWidth, menu.MinWidth);
                menu.IsOpen = false;
                WaitForMenuDismissal(button);
                Assert.False(button.IsDropDownOpen);
                Assert.False(state.IsChecked);
                state.IsChecked = true;
                DrainDispatcher();
                Assert.True(menu.IsOpen);
                button.Flyout = new ContextMenu();
                DrainDispatcher();
                Assert.False(menu.IsOpen);
                Assert.False(state.IsChecked);
                Assert.True(BindingOperations.IsDataBound(button, DropDownButton.IsDropDownOpenProperty));
            }
            finally
            {
                menu.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public void DropDownIconOnly_HasOneChevronGapAndUsesResolvedForeground()
    {
        WpfTestThread.Run(() =>
        {
            var button = new DropDownButton
            {
                Icon = new FluentIconSource(FluentSymbolRegular.Add24),
                MouseOverForeground = Brushes.Red,
            };
            ApplyTheme(button);
            var presenter = Assert.IsType<IconPresenter>(button.Template.FindName("iconPresenter", button));
            var chevron = Assert.IsType<FontIcon>(button.Template.FindName("DropDownChevron", button));
            Assert.Equal(default(Thickness), presenter.Margin);
            SetHovered(button, true);
            Assert.Same(Brushes.Red, TextElement.GetForeground(presenter));
            Assert.Same(Brushes.Red, chevron.Foreground);
            button.Content = "Add";
            Assert.Equal(button.IconMargin, presenter.Margin);
        });
    }

    [Fact]
    public void DropDownPopup_SynchronizesBindingAndClosesWhenOwnerUnloads()
    {
        WpfTestThread.Run(() =>
        {
            var popup = new Popup { Child = new System.Windows.Controls.TextBox { Text = "Editable content" } };
            var button = new DropDownButton { Flyout = popup };
            var state = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = false };
            button.SetBinding(
                DropDownButton.IsDropDownOpenProperty,
                new Binding(nameof(state.IsChecked)) { Source = state });
            var window = new Window { Content = button, Width = 240, Height = 120 };
            try
            {
                window.Show();
                state.IsChecked = true;
                DrainDispatcher();
                Assert.True(popup.IsOpen);
                Assert.Same(button, popup.PlacementTarget);
                Assert.Equal(PlacementMode.Bottom, popup.Placement);
                Assert.False(popup.StaysOpen);

                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                WaitForMenuDismissal(button);
                Assert.False(state.IsChecked);
                popup.SetCurrentValue(Popup.IsOpenProperty, true);
                DrainDispatcher();
                Assert.True(button.IsDropDownOpen);
                Assert.True(state.IsChecked);

                window.Content = null;
                DrainDispatcher();
                Assert.False(popup.IsOpen);
                Assert.False(state.IsChecked);
                Assert.True(BindingOperations.IsDataBound(button, DropDownButton.IsDropDownOpenProperty));
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DropDownPopup_ReplacementAcrossFlyoutTypesClosesAndDetachesOldFlyout(bool popupFirst)
    {
        WpfTestThread.Run(() =>
        {
            var popup = new PopupProbe { Child = new Border { Width = 100, Height = 40 } };
            var menu = new ContextMenu { Items = { new System.Windows.Controls.MenuItem { Header = "Item" } } };
            var button = new DropDownButton { Flyout = popupFirst ? popup : menu };
            var window = new Window { Content = button, Width = 240, Height = 120 };
            try
            {
                window.Show();
                button.IsDropDownOpen = true;
                DrainDispatcher();
                Assert.True(popupFirst ? popup.IsOpen : menu.IsOpen);
                button.Flyout = popupFirst ? menu : popup;
                DrainDispatcher();
                Assert.False(popup.IsOpen);
                Assert.False(menu.IsOpen);
                Assert.False(button.IsDropDownOpen);
                button.IsDropDownOpen = true;
                DrainDispatcher();
                Assert.True(popupFirst ? menu.IsOpen : popup.IsOpen);

                // A detached flyout can finish its close animation after the replacement opens.
                if (popupFirst)
                {
                    popup.RaiseClosed();
                }
                else
                {
                    menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent, menu));
                }

                Assert.True(button.IsDropDownOpen);
                button.Flyout = null;
                DrainDispatcher();
                Assert.False(popup.IsOpen);
                Assert.False(menu.IsOpen);
                popup.RaiseOpened();
                Assert.False(button.IsDropDownOpen);
            }
            finally
            {
                popup.IsOpen = false;
                menu.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public void DropDownPopup_PreservesExplicitPlacementStyleAndOpenBinding()
    {
        WpfTestThread.Run(() =>
        {
            var state = new System.Windows.Controls.Primitives.ToggleButton { IsChecked = false };
            var popup = new Popup
            {
                Child = new Border { Width = 120, Height = 40 },
                PlacementTarget = state,
                Style = new Style(typeof(Popup))
                {
                    Setters = { new Setter(Popup.StaysOpenProperty, true) },
                },
            };
            popup.SetBinding(Popup.IsOpenProperty, new Binding(nameof(state.IsChecked)) { Source = state });
            var placementSource = new Popup { Placement = PlacementMode.Right };
            popup.SetBinding(Popup.PlacementProperty, new Binding(nameof(Popup.Placement)) { Source = placementSource });
            var button = new DropDownButton { Flyout = popup };
            var window = new Window
            {
                Content = new StackPanel { Children = { button, state } },
                Width = 240,
                Height = 120,
            };
            try
            {
                window.Show();
                button.IsDropDownOpen = true;
                DrainDispatcher();
                Assert.True(popup.IsOpen);
                Assert.True(state.IsChecked);
                Assert.Same(state, popup.PlacementTarget);
                Assert.Equal(PlacementMode.Right, popup.Placement);
                Assert.True(popup.StaysOpen);
                button.IsDropDownOpen = false;
                WaitForMenuDismissal(button);
                Assert.False(popup.IsOpen);
                Assert.False(state.IsChecked);
                Assert.True(BindingOperations.IsDataBound(popup, Popup.IsOpenProperty));
                Assert.True(BindingOperations.IsDataBound(popup, Popup.PlacementProperty));
            }
            finally
            {
                popup.IsOpen = false;
                window.Close();
            }
        });
    }

    [Fact]
    public void DropDownFlyout_AcceptsUnwrappedPanelAndDataContent()
    {
        WpfTestThread.Run(() =>
        {
            var button = new DropDownButton();
            var panel = new StackPanel();
            button.Flyout = panel;
            Assert.Same(panel, button.Flyout);
            button.SetValue(DropDownButton.FlyoutProperty, "Content");
            Assert.Equal("Content", button.Flyout);
            Assert.Null(LogicalTreeHelper.GetParent(panel));
        });
    }

    [Fact]
    public void NavigationMapItem_UsesInheritedInteractionPaletteAndCornerRadius()
    {
        WpfTestThread.Run(() =>
        {
            var button = new NavigationMapItem
            {
                Content = "Node",
                MouseOverBackground = Brushes.Red,
                MouseOverForeground = Brushes.Green,
                DisabledBackground = Brushes.Gray,
                DisabledForeground = Brushes.White,
                CornerRadius = new CornerRadius(12),
            };
            ApplyTheme(button);
            var border = Assert.IsType<Border>(button.Template.FindName("RootBorder", button));
            var presenter = Assert.IsType<ContentPresenter>(border.Child);
            SetHovered(button, true);
            Assert.Same(Brushes.Red, border.Background);
            Assert.Same(Brushes.Green, TextElement.GetForeground(presenter));
            Assert.Equal(button.CornerRadius, border.CornerRadius);
            button.IsEnabled = false;
            Assert.Same(Brushes.Gray, border.Background);
            Assert.Same(Brushes.White, TextElement.GetForeground(presenter));
        });
    }

    private static void ApplyTheme(Control control)
    {
        control.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Resources/Theme/Light.xaml", UriKind.Absolute),
        });
        control.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute),
        });
        Assert.True(control.ApplyTemplate());
    }

    private static void WaitForMenuDismissal(DropDownButton button)
    {
        // Popup.Closed follows the native fade-out animation, not merely the IsOpen assignment.
        // Wait for that event's observable result while continuing to process dispatcher work.
        var frame = new DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => frame.Continue = button.IsDropDownOpen && DateTime.UtcNow < deadline;
        timer.Start();
        try
        {
            Dispatcher.PushFrame(frame);
        }
        finally
        {
            timer.Stop();
        }
    }

    private static void DrainDispatcher()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }

    private static void SetHovered(UiButton button, bool value)
    {
        // WPF stores reverse-inherited mouse-over state in both a cache flag and a read-only DP.
        // No public API sets it without moving the user's pointer. Drive both native stores and
        // the corresponding event; fail explicitly if this test-only framework contract changes.
        var writeFlag = Assert.IsAssignableFrom<MethodInfo>(typeof(UIElement).GetMethod(
            "WriteFlag", BindingFlags.Instance | BindingFlags.NonPublic));
        var flag = Enum.Parse(writeFlag.GetParameters()[0].ParameterType, "IsMouseOverCache");
        writeFlag.Invoke(button, new[] { flag, (object)value });
        var key = Assert.IsType<DependencyPropertyKey>(typeof(UIElement).GetField(
            "IsMouseOverPropertyKey", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null));
        button.SetValue(key, value);
        button.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
        {
            RoutedEvent = value ? Mouse.MouseEnterEvent : Mouse.MouseLeaveEvent,
        });
    }

    private sealed class ButtonProbe : UiButton
    {
        public void SetPressed(bool value) => IsPressed = value;
    }

    private sealed class PopupProbe : Popup
    {
        public void RaiseOpened() => OnOpened(EventArgs.Empty);

        public void RaiseClosed() => OnClosed(EventArgs.Empty);
    }
}
