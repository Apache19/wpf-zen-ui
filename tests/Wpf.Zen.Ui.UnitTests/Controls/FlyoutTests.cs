// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using Button = System.Windows.Controls.Button;
using TextBlock = System.Windows.Controls.TextBlock;
using TextBox = System.Windows.Controls.TextBox;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;

namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class FlyoutTests
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NativeHost_ClosingRestoresFocusAndDeactivationCloses(bool childSource)
    {
        WpfTestThread.Run(() =>
        {
            using var host = new HwndSource(new HwndSourceParameters("Flyout native host")
            {
                Width = 500, Height = 360, WindowStyle = 0x10CF0000,
            });
            using var child = childSource ? new HwndSource(new HwndSourceParameters("Embedded WPF")
            {
                ParentWindow = host.Handle, Width = 480, Height = 320, WindowStyle = 0x50000000,
            }) : null;
            var anchor = new Button { Content = "Anchor" };
            var root = new Grid { Children = { anchor } };
            root.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute),
            });
            (child ?? host).RootVisual = root;
            var editor = new TextBox { Text = "Edit" };
            var flyout = new Flyout { Content = editor, StaysOpen = true };
            var other = new Window { Width = 200, Height = 150, ShowInTaskbar = false };
            try
            {
                Pump();
                Assert.Null(Window.GetWindow(anchor));
                Assert.True(SetForegroundWindow(host.Handle));
                Pump();
                anchor.Focus();
                flyout.ShowAt(anchor);
                Pump();
                Assert.True(flyout.IsOpen);
                Assert.True(editor.IsKeyboardFocused);
                flyout.Hide();
                Pump();
                Assert.True(anchor.IsKeyboardFocused);
                flyout.Show();
                Pump();
                other.Show();
                other.Activate();
                Pump();
                Assert.False(flyout.IsOpen);
                Assert.True(other.IsActive);
                Assert.False(anchor.IsKeyboardFocused);
            }
            finally
            {
                flyout.Hide();
                other.Close();
                Pump();
            }
        });
    }

    [Fact]
    public void ShowAt_UsesExplicitTargetWithoutLayoutPlaceholderAndCanRetarget()
    {
        WpfTestThread.Run(() =>
        {
            var first = new Button { Content = "First" };
            var second = new Button { Content = "Second" };
            var root = new StackPanel { Children = { first, second } };
            var flyout = new Flyout { Content = "Details", Placement = PlacementMode.Top, VerticalOffset = -4 };
            WithWindow(
                root,
                flyout,
                () =>
                {
                    var before = root.DesiredSize;
                    flyout.ShowAt(first);
                    Pump();
                    var popup = GetPopup(flyout);
                    Assert.True(flyout.IsOpen);
                    Assert.Same(first, popup.PlacementTarget);
                    Assert.Same(flyout, popup.Child);
                    Assert.Equal(PlacementMode.Top, popup.Placement);
                    Assert.Equal(-4D, popup.VerticalOffset);
                    Assert.Equal(before, root.DesiredSize);
                    Assert.Equal(2, root.Children.Count);

                    flyout.ShowAt(second);
                    Pump();
                    Assert.Same(popup, GetPopup(flyout));
                    Assert.Same(second, popup.PlacementTarget);
                    first.IsEnabled = false;
                    Assert.True(flyout.IsOpen);
                    second.IsEnabled = false;
                    Assert.False(flyout.IsOpen);
                });
        });
    }

    [Fact]
    public void NativeDismissal_UpdatesTwoWayBindingWithoutReplacingIt()
    {
        WpfTestThread.Run(() =>
        {
            var source = new ToggleButton();
            var anchor = new Button { Content = "Anchor" };
            var flyout = new Flyout { Content = "Details", PlacementTarget = anchor };
            flyout.SetBinding(Flyout.IsOpenProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = source });
            WithWindow(
                anchor,
                flyout,
                () =>
                {
                    source.IsChecked = true;
                    Pump();
                    Assert.True(flyout.IsOpen);
                    GetPopup(flyout).SetCurrentValue(Popup.IsOpenProperty, false);
                    Pump();
                    Assert.False(flyout.IsOpen);
                    Assert.False(source.IsChecked);
                    Assert.True(BindingOperations.IsDataBound(flyout, Flyout.IsOpenProperty));
                    source.IsChecked = true;
                    Pump();
                    Assert.True(GetPopup(flyout).IsOpen);
                });
        });
    }

    [Theory]
    [InlineData("unload")]
    [InlineData("hide")]
    [InlineData("disable")]
    public void TargetBecomesUnavailable_ClosesEvenWhenStaysOpen(string action)
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var root = new Grid { Children = { anchor } };
            var flyout = new Flyout { Content = "Details", StaysOpen = true };
            WithWindow(
                root,
                flyout,
                () =>
                {
                    flyout.ShowAt(anchor);
                    Pump();
                    if (action == "unload") root.Children.Clear();
                    else if (action == "hide") anchor.Visibility = Visibility.Collapsed;
                    else anchor.IsEnabled = false;
                    Pump();
                    Assert.False(flyout.IsOpen);
                    Assert.False(GetPopup(flyout).IsOpen);
                });
        });
    }

    [Fact]
    public void MissingOrDetachedTarget_DoesNotCreatePopup()
    {
        WpfTestThread.Run(() =>
        {
            var flyout = new Flyout { Content = "Details" };
            flyout.Show();
            Assert.False(flyout.IsOpen);
            flyout.ShowAt(new Button());
            Assert.False(flyout.IsOpen);
            Assert.Null(LogicalTreeHelper.GetParent(flyout));
        });
    }

    [Fact]
    public void InvalidContentAndTargets_AreRejectedBeforeCommitting()
    {
        WpfTestThread.Run(() =>
        {
            var content = new TextBox();
            var flyout = new Flyout { Content = content };
            Assert.Throws<ArgumentException>(() => flyout.Content = new ContextMenu());
            Assert.Throws<ArgumentException>(() => flyout.Content = new Popup());
            Assert.Throws<ArgumentException>(() => flyout.Content = new Flyout());
            Assert.Same(content, flyout.Content);
            Assert.Throws<ArgumentException>(() => flyout.PlacementTarget = flyout);
            Assert.Throws<ArgumentException>(() => flyout.PlacementTarget = content);
            Assert.Null(flyout.PlacementTarget);
        });
    }

    [Fact]
    public void OpenPanel_InheritsTargetContextAndSupportsContentTemplateReplacement()
    {
        WpfTestThread.Run(() =>
        {
            var firstData = new object();
            var anchor = new Button { Content = "Anchor", DataContext = firstData };
            var flyout = new Flyout { Content = "Details" };
            WithWindow(
                anchor,
                flyout,
                () =>
                {
                    flyout.ShowAt(anchor);
                    Pump();
                    var popup = GetPopup(flyout);
                    Assert.Same(firstData, flyout.DataContext);
                    var secondData = new object();
                    anchor.DataContext = secondData;
                    Assert.Same(secondData, flyout.DataContext);
                    flyout.ContentTemplate = (DataTemplate)XamlReader.Parse(
                        "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding}'/></DataTemplate>");
                    flyout.Content = "Changed";
                    Pump();
                    Assert.Same(popup, GetPopup(flyout));
                    Assert.True(popup.IsOpen);
                    Assert.Equal("Changed", FindVisual<TextBlock>(flyout)!.Text);
                    flyout.Template = (ControlTemplate)XamlReader.Parse(
                        "<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ContentControl'><Border Padding='20'><ContentPresenter ContentSource='Content'/></Border></ControlTemplate>");
                    Pump();
                    Assert.Same(popup, GetPopup(flyout));
                    Assert.Equal("Changed", FindVisual<TextBlock>(flyout)!.Text);
                });
        });
    }

    [Fact]
    public void Escape_ClosesAndReturnsKeyboardFocusToTarget()
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var editor = new TextBox { Text = "Edit" };
            var flyout = new Flyout { Content = editor };
            WithWindow(
                anchor,
                flyout,
                () =>
                {
                    Window.GetWindow(anchor)!.Activate();
                    anchor.Focus();
                    flyout.ShowAt(anchor);
                    Pump();
                    Assert.True(editor.IsKeyboardFocused, $"Focused: {Keyboard.FocusedElement}; open: {flyout.IsOpen}; visible: {editor.IsVisible}");
                    var key = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(editor)!, 0, Key.Escape)
                    {
                        RoutedEvent = Keyboard.KeyDownEvent,
                    };
                    editor.RaiseEvent(key);
                    Pump();
                    Assert.True(key.Handled);
                    Assert.False(flyout.IsOpen);
                    Assert.True(anchor.IsKeyboardFocused);
                });
        });
    }

    [Fact]
    public void MouseInputBoundary_PreservesRightButtonForWpfContextMenuService()
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var editor = new TextBox { Text = "Right-click me" };
            var flyout = new Flyout { Content = editor };
            WithWindow(anchor, flyout, () =>
            {
                flyout.ShowAt(anchor);
                Pump();

                var rightButton = new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Right
                )
                {
                    RoutedEvent = Mouse.MouseUpEvent,
                    Source = editor,
                };
                editor.RaiseEvent(rightButton);

                // PopupControlService observes MouseUp later in InputManager.PostProcessInput. The
                // flyout boundary must leave this event available so TextBox can create its standard menu.
                Assert.False(rightButton.Handled);
            });
        });
    }

    [Fact]
    public void FocusOnOpenFalse_PreservesEditorFocusAndHandlesDismissalKeysAtTarget()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Text = "Keep editing" };
            var flyout = new Flyout
            {
                Content = new Button { Content = "Suggestion" },
                FocusOnOpen = false,
            };
            WithWindow(editor, flyout, () =>
            {
                Window.GetWindow(editor)!.Activate();
                editor.Focus();
                flyout.ShowAt(editor);
                Pump();

                Assert.True(editor.IsKeyboardFocused);
                Assert.False(flyout.IsKeyboardFocusWithin);

                var left = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(editor)!,
                    0,
                    Key.Left
                )
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                editor.RaiseEvent(left);
                Assert.False(left.Handled);

                var escape = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(editor)!,
                    0,
                    Key.Escape
                )
                {
                    RoutedEvent = Keyboard.PreviewKeyDownEvent,
                };
                editor.RaiseEvent(escape);
                Pump();

                Assert.True(escape.Handled);
                Assert.False(flyout.IsOpen);
                Assert.True(editor.IsKeyboardFocused);
            });
        });
    }

    [Fact]
    public void FlyoutService_ManagesFocusScopeWithoutApplicationEventCoordination()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Text = "Editor" };
            var other = new Button { Content = "Other" };
            var root = new StackPanel { Children = { editor, other } };
            var flyout = new Flyout
            {
                Content = new Button { Content = "Suggestion" },
                FocusOnOpen = false,
            };
            FlyoutService.SetAttachedFlyout(editor, flyout);
            FlyoutService.SetShowOnFocusOrClick(editor, true);

            WithWindow(root, flyout, () =>
            {
                Window.GetWindow(editor)!.Activate();
                editor.Focus();
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.Same(editor, flyout.PlacementTarget);
                Assert.True(editor.IsKeyboardFocused);

                other.Focus();
                Pump();

                Assert.True(other.IsKeyboardFocused);
                Assert.False(flyout.IsOpen);
            });
        });
    }

    [Fact]
    public void FlyoutService_PrimaryClickReopensFocusedOwnerWithoutTogglingOpenFlyout()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox
            {
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = "Editor",
            };
            var outside = new Border
            {
                Width = 80,
                Height = 80,
                HorizontalAlignment = HorizontalAlignment.Right,
                Background = Brushes.Transparent,
            };
            var root = new Grid { Children = { editor, outside } };
            var flyout = new Flyout
            {
                Content = new Button { Content = "Suggestion" },
                FocusOnOpen = false,
            };
            var openedCount = 0;
            flyout.Opened += (_, _) => openedCount++;
            FlyoutService.SetAttachedFlyout(editor, flyout);
            FlyoutService.SetShowOnFocusOrClick(editor, true);

            WithWindow(root, flyout, () =>
            {
                Window.GetWindow(editor)!.Activate();
                editor.Focus();
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.Equal(1, openedCount);
                flyout.Hide();
                Pump();
                Assert.True(editor.IsKeyboardFocused);

                RaiseMouseButton(editor, Mouse.PreviewMouseDownEvent, MouseButton.Left);
                Pump();

                // Opening during mouse-down would leave the TextBox's selection capture in place.
                // The service waits for the completed click before creating the Popup.
                Assert.False(flyout.IsOpen);

                RaiseMouseButton(editor, Mouse.MouseUpEvent, MouseButton.Left);
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.Equal(2, openedCount);
                Assert.NotNull(Mouse.Captured);

                RaiseMouseButton(editor, Mouse.MouseUpEvent, MouseButton.Left);
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.Equal(2, openedCount);

                var window = Window.GetWindow(editor)!;
                MoveElementUnderCursor(window, outside);
                var popupCapture = Assert.IsAssignableFrom<UIElement>(Mouse.Captured);
                popupCapture.RaiseEvent(
                    RaiseMouseButtonArgs(
                        popupCapture,
                        Mouse.PreviewMouseDownEvent,
                        MouseButton.Left
                    )
                );
                Pump();

                Assert.False(flyout.IsOpen);
                Assert.True(editor.IsKeyboardFocused);

                var rightButton = RaiseMouseButton(editor, Mouse.MouseUpEvent, MouseButton.Right);
                Pump();

                Assert.False(rightButton.Handled);
                Assert.False(flyout.IsOpen);
                Assert.Equal(2, openedCount);
            });
        });
    }

    [Fact]
    public void FlyoutService_FocusedPanelRemainsOpenAndEscapeDoesNotReopenIt()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Text = "Editor" };
            var option = new Button { Content = "Suggestion" };
            var other = new Button { Content = "Other" };
            var root = new StackPanel { Children = { editor, other } };
            var flyout = new Flyout { Content = option };
            FlyoutService.SetAttachedFlyout(editor, flyout);
            FlyoutService.SetShowOnFocusOrClick(editor, true);

            WithWindow(root, flyout, () =>
            {
                Window.GetWindow(editor)!.Activate();
                editor.Focus();
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.True(option.IsKeyboardFocused);

                other.Focus();
                Pump();

                Assert.True(other.IsKeyboardFocused);
                Assert.False(flyout.IsOpen);

                editor.Focus();
                Pump();

                Assert.True(flyout.IsOpen);
                Assert.True(option.IsKeyboardFocused);

                var escape = new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(option)!,
                    0,
                    Key.Escape
                )
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                };
                option.RaiseEvent(escape);
                Pump();

                Assert.True(escape.Handled);
                Assert.False(flyout.IsOpen);
                Assert.True(editor.IsKeyboardFocused);
            });
        });
    }

    [Fact]
    public void FlyoutService_NestedContextMenuRemainsInsideManagedFocusScope()
    {
        WpfTestThread.Run(() =>
        {
            var editor = new TextBox { Text = "Owner" };
            var menuItem = new MenuItem { Header = "Copy" };
            var contextMenu = new ContextMenu { Items = { menuItem } };
            var panelEditor = new TextBox
            {
                Text = "Panel editor",
                ContextMenu = contextMenu,
            };
            var flyout = new Flyout { Content = panelEditor };
            FlyoutService.SetAttachedFlyout(editor, flyout);
            FlyoutService.SetShowOnFocusOrClick(editor, true);

            WithWindow(editor, flyout, () =>
            {
                Window.GetWindow(editor)!.Activate();
                editor.Focus();
                Pump();

                Assert.True(panelEditor.IsKeyboardFocused);
                contextMenu.PlacementTarget = panelEditor;
                contextMenu.IsOpen = true;
                Pump();
                menuItem.Focus();
                Pump();

                Assert.True(menuItem.IsKeyboardFocused);
                Assert.True(flyout.IsOpen);

                contextMenu.IsOpen = false;
                Pump();
            });
        });
    }

    [Fact]
    public void DropDownButton_UsesFlyoutOwnHostAndSynchronizesReplacement()
    {
        WpfTestThread.Run(() =>
        {
            var panel = new Flyout { Content = "Details", Placement = PlacementMode.Top };
            var button = new DropDownButton { Content = "Open", Flyout = panel };
            WithWindow(
                button,
                panel,
                () =>
                {
                    button.IsDropDownOpen = true;
                    Pump();
                    var popup = GetPopup(panel);
                    Assert.Same(panel, popup.Child);
                    Assert.Same(button, popup.PlacementTarget);
                    Assert.Equal(PlacementMode.Top, popup.Placement);
                    panel.Hide();
                    Pump();
                    Assert.False(button.IsDropDownOpen);
                    button.IsDropDownOpen = true;
                    Pump();
                    Assert.True(panel.IsOpen);
                    button.Flyout = new ContextMenu();
                    Pump();
                    Assert.False(panel.IsOpen);
                    Assert.False(button.IsDropDownOpen);
                    Assert.Null(panel.PlacementTarget);
                });
        });
    }

    [Fact]
    public void DropDownButton_PreservesExplicitTargetAndInnerClicksDoNotActivateOwner()
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var inner = new Button { Content = "Inside" };
            var panel = new Flyout { Content = inner, PlacementTarget = anchor };
            var button = new DropDownButton { Content = "Open", Flyout = panel };
            var root = new StackPanel { Children = { anchor, button } };
            var ownerClicks = 0;
            anchor.Click += (_, _) => ownerClicks++;
            button.Click += (_, _) => ownerClicks++;
            WithWindow(
                root,
                panel,
                () =>
                {
                    button.IsDropDownOpen = true;
                    Pump();
                    Assert.Same(anchor, GetPopup(panel).PlacementTarget);
                    inner.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, inner));
                    Assert.Equal(0, ownerClicks);
                    Assert.True(panel.IsOpen);
                    button.Flyout = null;
                    Assert.Same(anchor, panel.PlacementTarget);
                });
        });
    }

    [Fact]
    public void CloseThenImmediateReopen_DoesNotLoseNewRequest()
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var panel = new Flyout { Content = "Details" };
            WithWindow(
                anchor,
                panel,
                () =>
                {
                    panel.ShowAt(anchor);
                    Pump();
                    panel.Hide();
                    panel.Show();
                    Pump();
                    Assert.True(panel.IsOpen);
                    Assert.True(GetPopup(panel).IsOpen);
                });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CrossWindowRetarget_ReplacesNativeOwnerAndPreservesBindings(bool closeBeforeRetarget)
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "First" };
            var second = new Button { Content = "Second" };
            var other = new Window
            {
                Content = second, Width = 300, Height = 250, Left = 700,
                ShowActivated = false, ShowInTaskbar = false,
            };
            var state = new ToggleButton();
            var panel = new Flyout { Content = new TextBox { Text = "Edit" }, StaysOpen = true };
            panel.SetBinding(Flyout.IsOpenProperty, new Binding(nameof(ToggleButton.IsChecked)) { Source = state });
            WithWindow(anchor, panel, () =>
            {
                try
                {
                    other.Show();
                    Pump();
                    Window.GetWindow(anchor)!.Activate();
                    panel.ShowAt(anchor);
                    Pump();
                    var oldPopup = GetPopup(panel);
                    var oldSource = Assert.IsType<HwndSource>(PresentationSource.FromVisual(panel));
                    if (closeBeforeRetarget)
                    {
                        // Do not pump: the old HWND can still be waiting for WPF's deferred destruction.
                        panel.Hide();
                    }

                    panel.ShowAt(second);
                    Pump();
                    var newPopup = GetPopup(panel);
                    var newSource = Assert.IsType<HwndSource>(PresentationSource.FromVisual(panel));
                    Assert.NotSame(oldPopup, newPopup);
                    Assert.NotSame(oldSource, newSource);
                    Assert.Null(oldPopup.Child);
                    Assert.Null(oldPopup.PlacementTarget);
                    Assert.True(state.IsChecked);
                    Assert.True(BindingOperations.IsDataBound(panel, Flyout.IsOpenProperty));

                    Window.GetWindow(anchor)!.Close();
                    Pump();
                    Assert.False(newSource.IsDisposed);
                    Assert.Same(newSource, PresentationSource.FromVisual(panel));
                    Assert.True(panel.IsOpen);
                    Assert.True(newPopup.IsOpen);
                    second.IsEnabled = false;
                    Assert.False(panel.IsOpen);
                    Assert.False(state.IsChecked);
                }
                finally
                {
                    panel.Hide();
                    other.Close();
                    Pump();
                }
            });
        });
    }

    [Fact]
    public void InitialOpening_RetargetFromCallbackCompletesHostTransfer()
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "First" };
            var second = new Button { Content = "Second" };
            var other = new Window
            {
                Content = second, Width = 300, Height = 250, Left = 700,
                ShowActivated = false, ShowInTaskbar = false,
            };
            var panel = new Flyout { Content = new TextBox { Text = "Edit" }, StaysOpen = true };
            WithWindow(anchor, panel, () =>
            {
                try
                {
                    other.Show();
                    Pump();
                    var opened = 0;
                    var closed = 0;
                    panel.Closed += (_, _) => closed++;
                    panel.Opened += (_, _) =>
                    {
                        if (++opened == 1)
                        {
                            // The first CreateRootPopup call is still establishing its bindings here.
                            panel.ShowAt(second);
                        }
                    };
                    panel.ShowAt(anchor);
                    Pump();
                    Assert.Equal(2, opened);
                    Assert.Equal(0, closed);
                    Assert.Same(second, GetPopup(panel).PlacementTarget);
                    Window.GetWindow(anchor)!.Close();
                    Pump();
                    Assert.True(panel.IsOpen);
                    Assert.NotNull(PresentationSource.FromVisual(panel));
                    second.IsEnabled = false;
                    Assert.False(panel.IsOpen);
                }
                finally
                {
                    panel.Hide();
                    other.Close();
                    Pump();
                }
            });
        });
    }

    [Theory]
    [InlineData("disable")]
    [InlineData("unload")]
    [InlineData("deactivate")]
    public void FocusRestoration_ReentrantShowKeepsTargetLifetimeSubscriptions(string action)
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var root = new Grid { Children = { anchor } };
            var editor = new TextBox { Text = "Edit" };
            var panel = new Flyout { Content = editor, StaysOpen = true };
            WithWindow(root, panel, () =>
            {
                Window.GetWindow(anchor)!.Activate();
                anchor.Focus();
                panel.ShowAt(anchor);
                Pump();
                Assert.True(editor.IsKeyboardFocused);
                var reopen = true;
                anchor.GotKeyboardFocus += (_, _) =>
                {
                    if (reopen)
                    {
                        reopen = false;
                        panel.Show();
                    }
                };
                panel.Hide();
                Pump();
                Assert.True(panel.IsOpen);
                Assert.True(GetPopup(panel).IsOpen);

                var other = new Window { Width = 200, Height = 150, ShowInTaskbar = false };
                try
                {
                    if (action == "disable")
                    {
                        anchor.IsEnabled = false;
                    }
                    else if (action == "unload")
                    {
                        root.Children.Clear();
                    }
                    else
                    {
                        other.Show();
                        other.Activate();
                    }

                    Pump();
                    Assert.False(panel.IsOpen);
                    Assert.False(GetPopup(panel).IsOpen);
                }
                finally
                {
                    other.Close();
                }
            });
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialFocus_ClosingFromFocusHandlerDoesNotStealRestoredFocus(bool reopenExistingHost)
    {
        WpfTestThread.Run(() =>
        {
            var anchor = new Button { Content = "Anchor" };
            var editor = new TextBox { Text = "Edit" };
            var panel = new Flyout { Content = editor, PopupAnimation = PopupAnimation.None };
            WithWindow(anchor, panel, () =>
            {
                Window.GetWindow(anchor)!.Activate();
                anchor.Focus();
                if (reopenExistingHost)
                {
                    panel.ShowAt(anchor);
                    Pump();
                    panel.Hide();
                    Pump();
                }

                var opened = 0;
                var focusRequests = 0;
                panel.Opened += (_, _) => opened++;
                editor.GotKeyboardFocus += (_, _) =>
                {
                    focusRequests++;
                    panel.Hide();
                };
                panel.ShowAt(anchor);
                Pump();
                Assert.Equal(1, focusRequests);
                Assert.False(panel.IsOpen);
                Assert.False(GetPopup(panel).IsOpen);
                Assert.Equal(0, opened);
                Assert.True(anchor.IsKeyboardFocused);
            });
        });
    }

    private static Popup GetPopup(Flyout flyout) => Assert.IsType<Popup>(LogicalTreeHelper.GetParent(flyout));

    private static MouseButtonEventArgs RaiseMouseButton(
        UIElement target,
        RoutedEvent routedEvent,
        MouseButton button
    )
    {
        var mouseEvent = RaiseMouseButtonArgs(target, routedEvent, button);
        target.RaiseEvent(mouseEvent);
        return mouseEvent;
    }

    private static MouseButtonEventArgs RaiseMouseButtonArgs(
        UIElement target,
        RoutedEvent routedEvent,
        MouseButton button
    )
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, button)
        {
            RoutedEvent = routedEvent,
            Source = target,
        };
    }

    private static void MoveElementUnderCursor(Window window, FrameworkElement element)
    {
        Assert.True(GetCursorPos(out NativePoint cursorPosition));
        Point topLeft = element.PointToScreen(default);
        DpiScale dpi = VisualTreeHelper.GetDpi(element);
        window.Left +=
            (cursorPosition.X - topLeft.X - (element.ActualWidth * dpi.DpiScaleX / 2D))
            / dpi.DpiScaleX;
        window.Top +=
            (cursorPosition.Y - topLeft.Y - (element.ActualHeight * dpi.DpiScaleY / 2D))
            / dpi.DpiScaleY;
        Pump();

        Assert.True(GetCursorPos(out cursorPosition));
        Point localPosition = element.PointFromScreen(
            new Point(cursorPosition.X, cursorPosition.Y)
        );
        Assert.True(new Rect(element.RenderSize).Contains(localPosition));
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private static void Pump() => Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);

    private static T? FindVisual<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T result) return result;
            if (FindVisual<T>(child) is { } descendant) return descendant;
        }

        return null;
    }

    private static void WithWindow(UIElement content, Flyout flyout, Action action)
    {
        var window = new Window { Width = 500, Height = 360, Left = 150, Top = 150, ShowInTaskbar = false, Content = content };
        window.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute),
        });
        try
        {
            window.Show();
            Pump();
            action();
        }
        finally
        {
            flyout.Hide();
            Pump();
            window.Close();
            Pump();
        }
    }
}
