// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UiTextBox = Wpf.Zen.Ui.Controls.TextBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class TextBoxTests
{
    [Fact]
    public void KeyboardFocusLeavingScope_RefreshesVisualBrushesAndClearButton()
    {
        RunOnStaThread(
            () =>
            {
                var background = new SolidColorBrush(Colors.Red);
                var borderBrush = new SolidColorBrush(Colors.Green);
                var foreground = new SolidColorBrush(Colors.Blue);
                var focusedBackground = new SolidColorBrush(Colors.Orange);
                var focusedBorderBrush = new SolidColorBrush(Colors.Yellow);
                var focusedForeground = new SolidColorBrush(Colors.Purple);
                var textBox = new UiTextBox
                {
                    Text = "Text",
                    Background = background,
                    BorderBrush = borderBrush,
                    Foreground = foreground,
                    FocusedBackground = focusedBackground,
                    FocusedBorderBrush = focusedBorderBrush,
                    FocusedForeground = focusedForeground,
                };
                var otherControl = new Button();
                var textBoxScope = CreateFocusScope(textBox);
                var otherScope = CreateFocusScope(otherControl);
                var root = new StackPanel();
                root.Children.Add(textBoxScope);
                root.Children.Add(otherScope);

                var window = new Window
                {
                    Content = root,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();

                    Assert.True(textBox.Focus());
                    Assert.True(textBox.IsKeyboardFocused);
                    Assert.True(textBox.IsFocused);
                    Assert.Same(focusedBackground, textBox.VisualBackground);
                    Assert.Same(focusedBorderBrush, textBox.VisualBorderBrush);
                    Assert.Same(focusedForeground, textBox.VisualForeground);
                    Assert.True(textBox.IsClearButtonVisible);

                    Assert.True(otherControl.Focus());
                    Assert.False(textBox.IsKeyboardFocused);
                    Assert.True(textBox.IsFocused);
                    Assert.Same(background, textBox.VisualBackground);
                    Assert.Same(borderBrush, textBox.VisualBorderBrush);
                    Assert.Same(foreground, textBox.VisualForeground);
                    Assert.False(textBox.IsClearButtonVisible);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    private static StackPanel CreateFocusScope(UIElement child)
    {
        var scope = new StackPanel();
        FocusManager.SetIsFocusScope(scope, true);
        scope.Children.Add(child);

        return scope;
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