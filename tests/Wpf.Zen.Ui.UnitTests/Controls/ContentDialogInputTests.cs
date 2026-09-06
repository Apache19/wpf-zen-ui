// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InitialFocus_AcceptsFocusableTargetsOutsideTabOrder(bool use3D) => Run(scope =>
    {
        var model = new ModelUIElement3D { Focusable = true };
        KeyboardNavigation.SetIsTabStop(model, false);
        var viewport = new Viewport3D { Width = 100, Height = 100 };
        viewport.Children.Add(model);
        var editor = new TextBox { IsTabStop = false };
        var body = new StackPanel();
        body.Children.Add(viewport);
        body.Children.Add(editor);
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        dialog.InitialFocus = use3D ? model : editor;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(dialog.InitialFocus, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void Tab_FromFocused3DContentMovesToNextEditor() => Run(scope =>
    {
        var model = new ModelUIElement3D { Focusable = true };
        var viewport = new Viewport3D { Width = 100, Height = 100 };
        viewport.Children.Add(model);
        var editor = new TextBox();
        var body = new StackPanel();
        body.Children.Add(viewport);
        body.Children.Add(editor);
        var dialog = scope.CreateDialog();
        dialog.Content = body;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(model, Keyboard.Focus(model));

        InputManager.Current.ProcessInput(new KeyEventArgs(
            Keyboard.PrimaryDevice,
            PresentationSource.FromVisual(viewport),
            Environment.TickCount,
            Key.Tab)
        {
            RoutedEvent = Keyboard.KeyDownEvent,
        });
        Assert.Same(editor, Keyboard.FocusedElement);
        dialog.Close();
        Pump(task);
    });

    [Theory]
    [InlineData("Z")]
    [InlineData("\r")]
    [InlineData("\x001B")]
    public void AccessKeys_RespectNestedScope(string key) => Run(scope =>
    {
        int innerCalls = 0;
        int outerCalls = 0;
        var focusTarget = new Border { Focusable = true, Width = 10, Height = 10 };
        var inner = new Button { Content = "Inner", Command = new TestCommand(() => innerCalls++) };
        var outer = new Button { Content = "Outer", Command = new TestCommand(() => outerCalls++) };
        var group = new StackPanel();
        group.Children.Add(focusTarget);
        group.Children.Add(inner);
        // Menus and custom composite controls can establish an access-key scope independently
        // of keyboard focus scopes. An unconsumed key must stay in the originating access scope.
        AccessKeyManager.AddAccessKeyPressedHandler(group, (_, e) =>
        {
            e.Scope ??= group;
            e.Handled = true;
        });
        var dialog = scope.CreateDialog();
        dialog.IsDismissible = false;
        dialog.Content = group;
        dialog.Footer = outer;
        AccessKeyManager.Register(key, inner);
        AccessKeyManager.Register(key, outer);
        try
        {
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            Assert.Same(focusTarget, Keyboard.FocusedElement);
            if (key == "Z")
            {
                TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, focusTarget, key));
            }
            else
            {
                InputManager.Current.ProcessInput(new KeyEventArgs(
                    Keyboard.PrimaryDevice,
                    PresentationSource.FromVisual(focusTarget),
                    Environment.TickCount,
                    key == "\r" ? Key.Enter : Key.Escape)
                {
                    RoutedEvent = Keyboard.KeyDownEvent,
                });
            }

            Drain();
            Assert.Equal(1, innerCalls);
            Assert.Equal(0, outerCalls);
            Assert.False(task.IsCompleted);
            dialog.Close();
            Pump(task);
        }
        finally
        {
            AccessKeyManager.Unregister(key, inner);
            AccessKeyManager.Unregister(key, outer);
        }
    });

    [Theory]
    [InlineData("TextBox")]
    [InlineData("PasswordBox")]
    [InlineData("RichTextBox")]
    public void Editors_AcceptComposedUnicodeText(string editorType) => Run(scope =>
    {
        Control editor = editorType switch
        {
            "PasswordBox" => new PasswordBox(),
            "RichTextBox" => new RichTextBox(),
            _ => new TextBox(),
        };
        var dialog = scope.CreateDialog();
        dialog.Content = editor;
        var task = scope.Host.ShowAsync(dialog);
        Drain();
        Assert.Same(editor, Keyboard.FocusedElement);
        const string text = "a中文é";
        TextCompositionManager.StartComposition(new TextComposition(InputManager.Current, editor, text));
        Drain();
        var actual = editor switch
        {
            PasswordBox password => password.Password,
            RichTextBox rich => new TextRange(rich.Document.ContentStart, rich.Document.ContentEnd).Text.TrimEnd('\r', '\n'),
            TextBox plain => plain.Text,
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(text, actual);
        dialog.Close();
        Pump(task);
    });

    [Fact]
    public void EditorCommands_PreserveSelectionDeletionAndUndo() => Run(scope =>
    {
        var editor = new TextBox { Text = "original" };
        var dialog = scope.CreateDialog();
        dialog.Content = editor;
        var task = scope.Host.ShowAsync(dialog);
        Drain();

        Assert.True(ApplicationCommands.SelectAll.CanExecute(null, editor));
        ApplicationCommands.SelectAll.Execute(null, editor);
        Assert.Equal("original", editor.SelectedText);
        EditingCommands.Backspace.Execute(null, editor);
        Assert.Equal(string.Empty, editor.Text);
        Assert.True(ApplicationCommands.Undo.CanExecute(null, editor));
        ApplicationCommands.Undo.Execute(null, editor);
        Assert.Equal("original", editor.Text);
        dialog.Close();
        Pump(task);
    });
}
