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
namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class BreadcrumbBarFocusTests
{
    [Fact]
    public void KeyboardNavigation_TreatsPathAsOneCompositeFocusStop()
    {
        WpfTestThread.Run(() =>
        {
            var resources = new ResourceDictionary { Source = CreateResourceUri() };
            var before = new NativeButton { Content = "Before", Height = 24D };
            var after = new NativeButton { Content = "After", Height = 24D };
            var breadcrumbBar = new Wpf.Zen.Ui.Controls.BreadcrumbBar
            {
                Style = Assert.IsType<Style>(resources[typeof(Wpf.Zen.Ui.Controls.BreadcrumbBar)]),
            };
            breadcrumbBar.Items.Add("First");
            breadcrumbBar.Items.Add("Second");

            var root = new StackPanel();
            root.Children.Add(before);
            root.Children.Add(breadcrumbBar);
            root.Children.Add(after);

            var window = new Window
            {
                Content = root,
                Width = 320D,
                Height = 160D,
                ShowInTaskbar = false,
                WindowStyle = WindowStyle.None,
            };

            try
            {
                window.Show();
                window.UpdateLayout();

                ButtonBase firstButton = GetSegmentButton(breadcrumbBar, 0);
                ButtonBase secondButton = GetSegmentButton(breadcrumbBar, 1);

                Assert.Equal(
                    KeyboardNavigationMode.Once,
                    KeyboardNavigation.GetTabNavigation(breadcrumbBar)
                );
                Assert.Equal(
                    KeyboardNavigationMode.Contained,
                    KeyboardNavigation.GetDirectionalNavigation(breadcrumbBar)
                );

                Assert.True(before.Focus());
                Assert.True(before.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Assert.True(firstButton.IsKeyboardFocused);

                // Template buttons remain individually focusable, but Once makes the next Tab
                // traversal leave the complete breadcrumb path instead of visiting every segment.
                Assert.True(firstButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)));
                Assert.True(after.IsKeyboardFocused);

                Assert.True(after.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Assert.True(firstButton.IsKeyboardFocused);
                Assert.True(firstButton.MoveFocus(new TraversalRequest(FocusNavigationDirection.Previous)));
                Assert.True(before.IsKeyboardFocused);

                Assert.True(firstButton.Focus());
                RaiseArrowKey(firstButton, Key.Right);
                Assert.True(secondButton.IsKeyboardFocused);

                RaiseArrowKey(secondButton, Key.Left);
                Assert.True(firstButton.IsKeyboardFocused);

                RaiseArrowKey(firstButton, Key.Left);
                Assert.True(firstButton.IsKeyboardFocused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static ButtonBase GetSegmentButton(
        Wpf.Zen.Ui.Controls.BreadcrumbBar breadcrumbBar,
        int index)
    {
        var item = Assert.IsType<BreadcrumbBarItem>(
            breadcrumbBar.ItemContainerGenerator.ContainerFromIndex(index)
        );

        return Assert.IsAssignableFrom<ButtonBase>(item.Template.FindName("PART_Button", item));
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
