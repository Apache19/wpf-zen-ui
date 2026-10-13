// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.Controls;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;
using NativeButton = System.Windows.Controls.Button;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.UnitTests.Controls;

public partial class ContentDialogTests
{
    /// <summary>
    /// ExplicitCommit alone retains native prefix search. Native Enter consumes an open
    /// drop-down, but reaches the default button when the drop-down is closed.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void ComboBoxPrefix_UsesNativeEnterWithExplicitCommit(bool open, int expectedSubmissions)
    {
        Run(scope =>
        {
            int submissions = 0;
            var comboBox = new UiComboBox
            {
                IsEditable = true,
                IsTextSearchEnabled = true,
                SearchInteraction = ComboBoxSearchInteraction.ExplicitCommit,
                ItemsSource = new[] { "Alpha", "Beta" },
            };
            var button = new NativeButton { IsDefault = true, Content = "Submit" };
            button.Click += (_, _) => submissions++;
            var dialog = scope.CreateDialog();
            dialog.Content = comboBox;
            dialog.Footer = button;
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            var editor = Assert.IsAssignableFrom<NativeTextBox>(
                comboBox.Template.FindName("PART_EditableTextBox", comboBox));
            Assert.Same(editor, Keyboard.Focus(editor));
            editor.Text = "Al";
            comboBox.IsDropDownOpen = open;
            Drain();

            Assert.Equal(ComboBoxTextSearchMode.Prefix, comboBox.TextSearchMode);
            ProcessPreviewEnter(editor);
            Assert.Equal(expectedSubmissions, submissions);
            dialog.Close();
            Pump(task);
        });
    }

    /// <summary>
    /// Exercises preview-to-bubble input promotion with the real dialog and ComboBox templates.
    /// Search must consume Enter even when no candidate matches, while ordinary form input
    /// must retain the dialog's default-button behavior.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.ExplicitCommit, false)]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.ExplicitCommit, true)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.ExplicitCommit, false)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.ExplicitCommit, true)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.ExplicitCommit, false)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.ExplicitCommit, true)]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.AutoSelect, false)]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.AutoSelect, true)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.AutoSelect, false)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.AutoSelect, true)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.AutoSelect, false)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.AutoSelect, true)]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.Filter, false)]
    [InlineData(ComboBoxTextSearchMode.Contains, ComboBoxSearchInteraction.Filter, true)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.Filter, false)]
    [InlineData(ComboBoxTextSearchMode.Tokens, ComboBoxSearchInteraction.Filter, true)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.Filter, false)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, ComboBoxSearchInteraction.Filter, true)]
    public void ComboBoxSearch_ConsumesEnterBeforeDialogDefaultButton(
        ComboBoxTextSearchMode mode, ComboBoxSearchInteraction interaction, bool open)
    {
        Run(scope =>
        {
            int submissions = 0;
            var comboBox = new UiComboBox
            {
                IsEditable = true,
                TextSearchMode = mode,
                SearchInteraction = interaction,
                ItemsSource = new[] { "Alpha", "Beta" },
            };
            var plainEditor = new NativeTextBox();
            var body = new System.Windows.Controls.StackPanel();
            body.Children.Add(comboBox);
            body.Children.Add(plainEditor);
            var button = new NativeButton { Content = "Submit", IsDefault = true };
            button.Click += (_, _) => submissions++;
            var dialog = scope.CreateDialog();
            dialog.Content = body;
            dialog.Footer = button;
            var task = scope.Host.ShowAsync(dialog);
            Drain();
            var editor = Assert.IsAssignableFrom<NativeTextBox>(
                comboBox.Template.FindName("PART_EditableTextBox", comboBox));
            Assert.Same(editor, Keyboard.Focus(editor));
            editor.Text = "Alpha";
            comboBox.IsDropDownOpen = open;
            Drain();

            ProcessPreviewEnter(editor);
            Assert.Equal("Alpha", comboBox.SelectedItem);
            Assert.Equal(0, submissions);

            editor.Text = "No match";
            comboBox.IsDropDownOpen = open;
            Drain();
            ProcessPreviewEnter(editor);
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal("No match", editor.Text);
            Assert.Equal(0, submissions);

            Assert.Same(plainEditor, Keyboard.Focus(plainEditor));
            ProcessPreviewEnter(plainEditor);
            Assert.Equal(1, submissions);
            dialog.Close();
            Pump(task);
        });
    }

    /// <summary>
    /// Lets InputManager promote an unhandled preview event through the normal WPF pipeline.
    /// </summary>
    private static void ProcessPreviewEnter(Control target)
    {
        var source = PresentationSource.FromVisual(target);
        var input = new KeyEventArgs(
            Keyboard.PrimaryDevice, source, Environment.TickCount, Key.Enter)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        InputManager.Current.ProcessInput(input);
    }
}
