// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UiNumberBox = Wpf.Zen.Ui.Controls.NumberBox;
using UiRichTextBox = Wpf.Zen.Ui.Controls.RichTextBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class TextInputFocusTests
{
    [Fact]
    public void NumberBoxKeyboardFocusLeavingScope_RestoresBackground()
    {
        RunOnStaThread(
            () =>
            {
                var numberBox = new UiNumberBox();

                VerifyBackgroundRestoredWhenKeyboardFocusLeavesScope(numberBox, "ContentBorder");
            }
        );
    }

    [Fact]
    public void RichTextBoxKeyboardFocusLeavingScope_RestoresBackground()
    {
        RunOnStaThread(
            () =>
            {
                var richTextBox = new UiRichTextBox();

                VerifyBackgroundRestoredWhenKeyboardFocusLeavesScope(richTextBox, "MainBorder");
            }
        );
    }

    private static void VerifyBackgroundRestoredWhenKeyboardFocusLeavesScope(
        Control textInput,
        string borderName
    )
    {
        var otherControl = new Button();
        var textInputScope = CreateFocusScope(textInput);
        var otherScope = CreateFocusScope(otherControl);
        var root = new StackPanel();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary { Source = CreateThemeUri() }
        );
        root.Children.Add(textInputScope);
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

            _ = textInput.ApplyTemplate();
            var contentBorder = Assert.IsType<Border>(
                textInput.Template.FindName(borderName, textInput)
            );
            var normalBackground = contentBorder.Background;

            Assert.True(textInput.Focus());
            Assert.True(textInput.IsKeyboardFocused);
            Assert.True(textInput.IsFocused);
            Assert.NotSame(normalBackground, contentBorder.Background);

            Assert.True(otherControl.Focus());
            Assert.False(textInput.IsKeyboardFocused);
            Assert.True(textInput.IsFocused);
            Assert.Same(normalBackground, contentBorder.Background);
        }
        finally
        {
            window.Close();
        }
    }

    private static StackPanel CreateFocusScope(UIElement child)
    {
        var scope = new StackPanel();
        FocusManager.SetIsFocusScope(scope, true);
        scope.Children.Add(child);

        return scope;
    }

    private static Uri CreateThemeUri()
    {
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        return System.IO.Packaging.PackUriHelper.Create(
            new Uri("application:///", UriKind.Absolute),
            new Uri("/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Relative)
        );
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