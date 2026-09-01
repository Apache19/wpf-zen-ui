// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using NativeButton = System.Windows.Controls.Button;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls.NavigationView;

[Collection(nameof(KeyboardFocusTestCollection))]
public class NavigationViewListFocusTests
{
    [Fact]
    public void TabNavigation_TreatsItemsAsOneCompositeFocusStop()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var before = new NativeButton { Content = "Before", Height = 24D };
            var first = new NativeButton { Content = "First", Height = 24D };
            var second = new NativeButton { Content = "Second", Height = 24D };
            var after = new NativeButton { Content = "After", Height = 24D };
            var list = new NavigationViewList
            {
                Focusable = false,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
            };
            list.Items.Add(first);
            list.Items.Add(second);

            var root = new StackPanel();
            root.Children.Add(before);
            root.Children.Add(list);
            root.Children.Add(after);

            var window = new Window
            {
                Content = root,
                Width = 240D,
                Height = 160D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                Assert.Equal(
                    KeyboardNavigationMode.Once,
                    KeyboardNavigation.GetTabNavigation(list)
                );
                Assert.Equal(
                    KeyboardNavigationMode.Contained,
                    KeyboardNavigation.GetDirectionalNavigation(list)
                );

                Assert.True(before.Focus());
                Assert.True(before.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Assert.True(first.IsKeyboardFocused);

                // The list is one Tab stop even though each item header remains independently
                // focusable for directional navigation.
                Assert.True(first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Assert.True(after.IsKeyboardFocused);

                Assert.True(after.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Assert.True(first.IsKeyboardFocused);
                Assert.True(first.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Assert.True(before.IsKeyboardFocused);

                Assert.True(first.Focus());
                KeyEventArgs down = CreateKeyEvent(first, Key.Down);
                first.RaiseEvent(down);
                Assert.True(down.Handled);
                Assert.True(second.IsKeyboardFocused);

                KeyEventArgs edgeDown = CreateKeyEvent(second, Key.Down);
                second.RaiseEvent(edgeDown);
                Assert.True(edgeDown.Handled);
                Assert.True(second.IsKeyboardFocused);

                KeyEventArgs up = CreateKeyEvent(second, Key.Up);
                second.RaiseEvent(up);
                Assert.True(up.Handled);
                Assert.True(first.IsKeyboardFocused);

                Assert.True(second.Focus());
                Assert.True(second.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Assert.True(after.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ArrowKeys_MoveBetweenNavigationItemHeaderButtons()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            Style itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var firstItem = new NavigationViewItem { Header = "First", Style = itemStyle };
            var secondItem = new NavigationViewItem { Header = "Second", Style = itemStyle };
            var list = new NavigationViewList
            {
                Focusable = false,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
            };
            list.Items.Add(firstItem);
            list.Items.Add(secondItem);

            var window = new Window
            {
                Content = list,
                Width = 240D,
                Height = 160D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                var firstHeader = Assert.IsAssignableFrom<ButtonBase>(
                    firstItem.Template.FindName("PART_HeaderButton", firstItem)
                );
                var secondHeader = Assert.IsAssignableFrom<ButtonBase>(
                    secondItem.Template.FindName("PART_HeaderButton", secondItem)
                );

                Assert.True(firstHeader.Focus());
                KeyEventArgs down = CreateKeyEvent(firstHeader, Key.Down);
                firstHeader.RaiseEvent(down);

                Assert.True(down.Handled);
                Assert.True(secondHeader.IsKeyboardFocused);

                KeyEventArgs up = CreateKeyEvent(secondHeader, Key.Up);
                secondHeader.RaiseEvent(up);

                Assert.True(up.Handled);
                Assert.True(firstHeader.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DownArrow_LeavesExpandedInlineBranchAtItsLastChild()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            Style itemStyle = Assert.IsType<Style>(resources[typeof(NavigationViewItem)]);
            var parentItem = new NavigationViewItem
            {
                Header = "Parent",
                IsExpanded = true,
                Style = itemStyle,
            };
            var firstChild = new NavigationViewItem { Header = "First child", Style = itemStyle };
            var lastChild = new NavigationViewItem { Header = "Last child", Style = itemStyle };
            var followingItem = new NavigationViewItem { Header = "Following", Style = itemStyle };
            parentItem.Items.Add(firstChild);
            parentItem.Items.Add(lastChild);

            var list = new NavigationViewList
            {
                Focusable = false,
                Style = Assert.IsType<Style>(resources[typeof(NavigationViewList)]),
            };
            list.Items.Add(parentItem);
            list.Items.Add(followingItem);

            var window = new Window
            {
                Content = list,
                Width = 240D,
                Height = 240D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                ButtonBase parentHeader = GetHeaderButton(parentItem);
                ButtonBase firstChildHeader = GetHeaderButton(firstChild);
                ButtonBase lastChildHeader = GetHeaderButton(lastChild);
                ButtonBase followingHeader = GetHeaderButton(followingItem);

                Assert.True(parentHeader.Focus());
                RaiseArrowKey(parentHeader, Key.Down);
                Assert.True(firstChildHeader.IsKeyboardFocused);

                RaiseArrowKey(firstChildHeader, Key.Down);
                Assert.True(lastChildHeader.IsKeyboardFocused);

                // The inline presenter is part of the outer list's directional scope. Its final
                // child must therefore continue to the root item following the expanded branch.
                RaiseArrowKey(lastChildHeader, Key.Down);
                Assert.True(followingHeader.IsKeyboardFocused);

                RaiseArrowKey(followingHeader, Key.Up);
                Assert.True(lastChildHeader.IsKeyboardFocused);

                // NavigationViewItem retains ownership of hierarchy-specific Left behavior.
                RaiseArrowKey(lastChildHeader, Key.Left);
                Assert.True(parentHeader.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ButtonBase GetHeaderButton(NavigationViewItem item)
    {
        return Assert.IsAssignableFrom<ButtonBase>(
            item.Template.FindName("PART_HeaderButton", item)
        );
    }

    private static void RaiseArrowKey(UIElement sourceElement, Key key)
    {
        KeyEventArgs keyEvent = CreateKeyEvent(sourceElement, key);
        sourceElement.RaiseEvent(keyEvent);
        Assert.True(keyEvent.Handled);
    }

    private static KeyEventArgs CreateKeyEvent(UIElement sourceElement, Key key)
    {
        PresentationSource source = Assert.IsAssignableFrom<PresentationSource>(
            PresentationSource.FromVisual(sourceElement)
        );

        return new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        };
    }

    private static Uri CreateResourceUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
    }
}
