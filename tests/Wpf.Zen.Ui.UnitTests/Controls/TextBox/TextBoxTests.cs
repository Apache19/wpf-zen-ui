// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

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
    public void UseTextBlockAppearance_WithCustomStyle_ForcesReadOnlyAndRestoresBaseValue()
    {
        RunOnStaThread(
            () =>
            {
                var textBox = new UiTextBox
                {
                    Style = new Style(typeof(UiTextBox)),
                    IsReadOnly = false,
                    UseTextBlockAppearance = true,
                };

                Assert.True(textBox.IsReadOnly);

                textBox.IsReadOnly = false;
                Assert.True(textBox.IsReadOnly);

                textBox.UseTextBlockAppearance = false;
                Assert.False(textBox.IsReadOnly);
            }
        );
    }

    [Fact]
    public void UseTextBlockAppearance_WhenBaseValueIsReadOnly_DoesNotMakeTextBoxEditable()
    {
        RunOnStaThread(
            () =>
            {
                var textBox = new UiTextBox { IsReadOnly = true };

                textBox.UseTextBlockAppearance = true;
                textBox.UseTextBlockAppearance = false;

                Assert.True(textBox.IsReadOnly);
            }
        );
    }

    [Fact]
    public void AutoSelect_WhenKeyboardFocusIsReceived_SelectsAllText()
    {
        RunOnStaThread(
            () =>
            {
                var textBox = new UiTextBox { AutoSelect = true, Text = "Text" };
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
                    Assert.Equal(0, textBox.SelectionStart);
                    Assert.Equal(textBox.Text.Length, textBox.SelectionLength);

                    Assert.True(otherControl.Focus());
                    textBox.Select(1, 2);

                    // Logical focus remains in textBoxScope and a partial selection already exists.
                    // The second entry therefore verifies both that the behavior follows keyboard
                    // focus rather than only GotFocus and that it always replaces the old selection.
                    Assert.True(textBox.Focus());
                    Assert.Equal(0, textBox.SelectionStart);
                    Assert.Equal(textBox.Text.Length, textBox.SelectionLength);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

    [Fact]
    public void AutoSelect_WhenSelectionAlreadyExists_SelectsAllText()
    {
        RunOnStaThread(
            () =>
            {
                var textBox = new UiTextBox { AutoSelect = true, Text = "Text" };
                textBox.Select(1, 2);
                var window = new Window
                {
                    Content = textBox,
                    ShowInTaskbar = false,
                    WindowStyle = WindowStyle.None,
                };

                try
                {
                    window.Show();

                    Assert.True(textBox.Focus());
                    Assert.Equal(0, textBox.SelectionStart);
                    Assert.Equal(textBox.Text.Length, textBox.SelectionLength);
                }
                finally
                {
                    window.Close();
                }
            }
        );
    }

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
        WpfTestThread.Run(action);
    }
}
