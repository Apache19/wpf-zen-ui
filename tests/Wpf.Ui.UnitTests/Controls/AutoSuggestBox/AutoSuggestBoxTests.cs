// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using NativeButton = System.Windows.Controls.Button;
using NativeListBox = System.Windows.Controls.ListBox;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;
using UiTextBox = Wpf.Ui.Controls.TextBox;
using UiTextChangeReason = Wpf.Ui.Controls.AutoSuggestionBoxTextChangeReason;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.UnitTests.Controls;

public class AutoSuggestBoxTests
{
    [Fact]
    public void FirstUserInput_OpensSuggestionPopup()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);

                Assert.True(textBox.Focus());
                textBox.Text = "a";

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DefaultPopup_IsNonStickyAndSynchronizesCloseState()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.False(popup.StaysOpen);

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);

                popup.SetCurrentValue(Popup.IsOpenProperty, false);

                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MovingHostWindow_ClosesOpenSuggestions()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);

                window.Left += 10;
                FlushDispatcher(window);

                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void RegainingKeyboardFocus_ReopensSuggestionsForNonEmptyQueryWithMatches()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, outsideButton) = CreateLoadedControl();

            try
            {
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(autoSuggestBox.IsSuggestionListOpen);

                Assert.True(outsideButton.Focus());
                Assert.False(autoSuggestBox.IsSuggestionListOpen);

                Assert.True(textBox.Focus());

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-match")]
    public void RegainingKeyboardFocus_DoesNotReopenSuggestionsWithoutNonEmptyMatchingQuery(string query)
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, outsideButton) = CreateLoadedControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = query;

                Assert.True(outsideButton.Focus());
                Assert.False(autoSuggestBox.IsSuggestionListOpen);

                Assert.True(textBox.Focus());

                Assert.False(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SelectingSuggestion_PreviewsTextWithoutFilteringOrRaisingSuggestionChosen()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var chosenItems = new List<object>();
                var changeReasons = new List<UiTextChangeReason>();
                autoSuggestBox.SuggestionChosen += (_, args) => chosenItems.Add(args.SelectedItem);
                autoSuggestBox.TextChanged += (_, args) => changeReasons.Add(args.Reason);

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                suggestionsList.SelectedItem = "Alpha";

                Assert.Equal("Alpha", textBox.Text);
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.Empty(chosenItems);
                Assert.Equal(UiTextChangeReason.SuggestionChosen, changeReasons[^1]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DisplayMemberPath_UsesWpfPropertyPathForFilteringAndSelection()
    {
        RunOnStaThread(() =>
        {
            var alpha = new Suggestion(new SuggestionDetails("Alpha"));
            var beta  = new Suggestion(new SuggestionDetails("Beta"));
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl(
                new[] { alpha, beta },
                "Details.Name"
            );

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "alp";

                Assert.Equal(new[] { alpha }, autoSuggestBox.Items.Cast<Suggestion>());

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                suggestionsList.SelectedItem = alpha;

                Assert.Equal("Alpha", textBox.Text);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void DownArrow_PreviewsFirstSuggestionWithoutCommittingIt()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var chosenItems = new List<object>();
                autoSuggestBox.SuggestionChosen += (_, args) => chosenItems.Add(args.SelectedItem);

                Assert.True(textBox.Focus());
                textBox.Text = "a";

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(suggestionsList.HasItems);

                var keyEvent = CreateKeyEvent(textBox, Key.Down);
                Assert.Equal(Key.Down, keyEvent.Key);
                autoSuggestBox.ProcessTextBoxPreviewKeyDown(keyEvent);

                Assert.Equal("Alpha", suggestionsList.SelectedItem);
                Assert.Equal("Alpha", textBox.Text);
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());
                Assert.Empty(chosenItems);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Enter_CommitsSelectedSuggestionExactlyOnce()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var chosenItems = new List<object>();
                autoSuggestBox.SuggestionChosen += (_, args) => chosenItems.Add(args.SelectedItem);

                Assert.True(textBox.Focus());
                textBox.Text = "a";

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                suggestionsList.SelectedItem = "Beta";
                Assert.True(suggestionsList.Focus());

                Assert.Empty(chosenItems);
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.Equal("Beta", suggestionsList.SelectedItem);

                var keyEvent = CreateKeyEvent(suggestionsList, Key.Enter);
                Assert.Equal(Key.Enter, keyEvent.Key);
                autoSuggestBox.ProcessSuggestionsListPreviewKeyDown(keyEvent);

                Assert.Equal(new object[] { "Beta" }, chosenItems);
                Assert.Equal("Beta", textBox.Text);
                Assert.True(textBox.IsKeyboardFocused);
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void MouseClick_CommitsClickedSuggestionExactlyOnce()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var chosenItems = new List<object>();
                autoSuggestBox.SuggestionChosen += (_, args) => chosenItems.Add(args.SelectedItem);

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                window.UpdateLayout();

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                var itemContainer = Assert.IsAssignableFrom<NativeListBoxItem>(
                    suggestionsList.ItemContainerGenerator.ContainerFromItem("Beta")
                );
                Assert.True(itemContainer.Focus());
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                var mouseEvent = new MouseButtonEventArgs(
                    Mouse.PrimaryDevice,
                    Environment.TickCount,
                    MouseButton.Left
                )
                {
                    RoutedEvent = UIElement.PreviewMouseLeftButtonUpEvent,
                    Source = itemContainer,
                };

                suggestionsList.RaiseEvent(mouseEvent);

                Assert.Equal(new object[] { "Beta" }, chosenItems);
                Assert.Equal("Beta", textBox.Text);
                Assert.True(textBox.IsKeyboardFocused);
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static (
        Window Window,
        TestAutoSuggestBox AutoSuggestBox,
        UiTextBox TextBox,
        NativeButton OutsideButton
    ) CreateLoadedControl(IList? originalItemsSource = null, string displayMemberPath = "")
    {
        var autoSuggestBox = new TestAutoSuggestBox
        {
            DisplayMemberPath   = displayMemberPath,
            OriginalItemsSource = originalItemsSource ?? new[] { "Alpha", "Beta" },
            Template            = CreateControlTemplate(),
        };
        var outsideButton = new NativeButton { Content = "Outside" };
        var root = new StackPanel();
        root.Children.Add(autoSuggestBox);
        root.Children.Add(outsideButton);

        var window = new Window
        {
            Width = 320,
            Height = 160,
            ShowInTaskbar = false,
            Content = root,
        };

        window.Show();
        FlushDispatcher(window);
        Assert.True(autoSuggestBox.IsLoaded);

        var textBox = Assert.IsAssignableFrom<UiTextBox>(
            autoSuggestBox.Template.FindName("PART_TextBox", autoSuggestBox)
        );

        return (window, autoSuggestBox, textBox, outsideButton);
    }

    private static (
        Window Window,
        AutoSuggestBox AutoSuggestBox,
        UiTextBox TextBox,
        Popup Popup
    ) CreateLoadedDefaultControl()
    {
        var autoSuggestBox = new AutoSuggestBox { OriginalItemsSource = new[] { "Alpha", "Beta" } };
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Ui;component/Themes/Generic.xaml",
                    UriKind.Absolute
                ),
            }
        );
        root.Children.Add(autoSuggestBox);

        var window = new Window
        {
            Width = 320,
            Height = 160,
            ShowInTaskbar = false,
            Content = root,
        };

        window.Show();
        FlushDispatcher(window);

        var textBox = Assert.IsAssignableFrom<UiTextBox>(
            autoSuggestBox.Template.FindName("PART_TextBox", autoSuggestBox)
        );
        var popup = Assert.IsType<Popup>(FindVisualDescendant<Popup>(autoSuggestBox));

        return (window, autoSuggestBox, textBox, popup);
    }

    private static T? FindVisualDescendant<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T result)
            {
                return result;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private static ControlTemplate CreateControlTemplate()
    {
        var root = new FrameworkElementFactory(typeof(Grid));
        var textBox = new FrameworkElementFactory(typeof(UiTextBox), "PART_TextBox");
        textBox.SetBinding(
            System.Windows.Controls.TextBox.TextProperty,
            new Binding(nameof(AutoSuggestBox.Text))
            {
                Mode = BindingMode.TwoWay,
                RelativeSource = RelativeSource.TemplatedParent,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            }
        );
        root.AppendChild(textBox);

        var suggestionsList = new FrameworkElementFactory(typeof(NativeListBox), "PART_SuggestionsList");
        suggestionsList.SetValue(Grid.RowProperty, 1);
        suggestionsList.SetBinding(
            ItemsControl.ItemsSourceProperty,
            new Binding(nameof(AutoSuggestBox.ItemsSource))
            {
                RelativeSource = RelativeSource.TemplatedParent,
            }
        );
        root.AppendChild(suggestionsList);

        return new ControlTemplate(typeof(AutoSuggestBox)) { VisualTree = root };
    }

    private static void FlushDispatcher(Window window)
    {
        var frame = new DispatcherFrame();
        _ = window.Dispatcher.BeginInvoke(
            DispatcherPriority.ApplicationIdle,
            new Action(() => frame.Continue = false)
        );
        Dispatcher.PushFrame(frame);
    }

    private static KeyEventArgs CreateKeyEvent(UIElement element, Key key)
    {
        var source = Assert.IsAssignableFrom<PresentationSource>(PresentationSource.FromVisual(element));
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = UIElement.PreviewKeyDownEvent,
        };
    }

    private sealed class TestAutoSuggestBox : AutoSuggestBox
    {
        public void ProcessTextBoxPreviewKeyDown(KeyEventArgs e)
        {
            OnTextBoxPreviewKeyDown(e);
        }

        public void ProcessSuggestionsListPreviewKeyDown(KeyEventArgs e)
        {
            OnSuggestionsListPreviewKeyDown(e);
        }
    }

    private sealed record Suggestion(SuggestionDetails Details);

    private sealed record SuggestionDetails(string Name);

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            ExceptionDispatchInfo.Capture(exception).Throw();
        }
    }
}
