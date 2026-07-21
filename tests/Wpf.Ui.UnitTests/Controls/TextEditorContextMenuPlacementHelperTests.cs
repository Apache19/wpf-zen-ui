// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using UiTextBox = Wpf.Ui.Controls.TextBox;

namespace Wpf.Ui.UnitTests.Controls;

public class TextEditorContextMenuPlacementHelperTests
{
    [Fact]
    public void KeyboardInvokedContextMenu_RestoresPlacement_WhenClosed()
    {
        RunOnStaThread(
            () =>
            {
                var originalPlacementTarget = new Button();
                var owner = new Button
                {
                    ContextMenu = new ContextMenu
                    {
                        Placement = PlacementMode.Bottom,
                        PlacementTarget = originalPlacementTarget,
                        HorizontalOffset = 7d,
                        VerticalOffset = 11d,
                    },
                };
                var contextMenu = owner.ContextMenu;
                var eventArgs = CreateKeyboardContextMenuEventArgs(owner);

                var opened = InvokeTryOpenKeyboardInvokedMenu(
                    owner,
                    eventArgs,
                    () => new Rect(23d, 3d, 1d, 12d)
                );

                Assert.True(opened);
                Assert.True(eventArgs.Handled);
                Assert.True(contextMenu.IsOpen);
                Assert.Equal(PlacementMode.RelativePoint, contextMenu.Placement);
                Assert.Same(owner, contextMenu.PlacementTarget);
                Assert.Equal(23d, contextMenu.HorizontalOffset);
                Assert.Equal(15d, contextMenu.VerticalOffset);

                contextMenu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));

                Assert.Equal(PlacementMode.Bottom, contextMenu.Placement);
                Assert.Same(originalPlacementTarget, contextMenu.PlacementTarget);
                Assert.Equal(7d, contextMenu.HorizontalOffset);
                Assert.Equal(11d, contextMenu.VerticalOffset);
            }
        );
    }

    private static ContextMenuEventArgs CreateKeyboardContextMenuEventArgs(object source)
    {
        var constructor = typeof(ContextMenuEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(object), typeof(bool), typeof(double), typeof(double)],
            modifiers: null
        );

        Assert.NotNull(constructor);

        return (ContextMenuEventArgs)constructor.Invoke([source, true, -1d, -1d]);
    }

    private static bool InvokeTryOpenKeyboardInvokedMenu(
        Control owner,
        ContextMenuEventArgs eventArgs,
        Func<Rect> getPositionRect)
    {
        var helperType = typeof(UiTextBox).Assembly.GetType(
            "Wpf.Ui.Controls.TextEditorContextMenuPlacementHelper"
        );

        Assert.NotNull(helperType);

        var method = helperType.GetMethod(
            "TryOpenKeyboardInvokedMenu",
            BindingFlags.Public | BindingFlags.Static
        );

        Assert.NotNull(method);

        return (bool)method.Invoke(null, [owner, eventArgs, getPositionRect])!;
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(
            () =>
            {
                try
                {
                    action();
                }
                catch (Exception caughtException)
                {
                    exception = caughtException;
                }
            }
        );

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
