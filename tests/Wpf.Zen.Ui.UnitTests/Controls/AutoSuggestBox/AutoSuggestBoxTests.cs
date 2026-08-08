// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Wpf.Zen.Ui.Controls;
using NativeButton = System.Windows.Controls.Button;
using NativeListBox = System.Windows.Controls.ListBox;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;
using UiTextBox = Wpf.Zen.Ui.Controls.TextBox;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.UnitTests.Controls;

[Collection(nameof(KeyboardFocusTestCollection))]
public class AutoSuggestBoxTests
{
    [Fact]
    public void SuggestionListReopening_IsDisabledByDefault()
    {
        RunOnStaThread(() =>
        {
            var autoSuggestBox = new AutoSuggestBox();

            Assert.True(autoSuggestBox.IsSuggestionListReopeningEnabled);
            Assert.True(
                (bool)AutoSuggestBox.IsSuggestionListReopeningEnabledProperty.DefaultMetadata.DefaultValue
            );
        });
    }

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
                // Popup tears down its HWND asynchronously at Input priority. Complete that teardown
                // before closing the owner so the next STA test does not inherit a disposed input provider.
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void DefaultPopup_WhenPlacedBelow_JoinsEditorAndSuggestionsBorders()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();
            var suggestionsBorder = Assert.IsType<Border>(popup.Child);

            try
            {
                // Keep both vertical candidates away from a monitor edge so WPF selects the declared Bottom
                // candidate deterministically instead of inheriting the test runner's last window position.
                window.Left = SystemParameters.WorkArea.Left + 100D;
                window.Top = SystemParameters.WorkArea.Top + 100D;
                FlushDispatcher(window);

                Assert.True(PopupPlacement.GetIsEnabled(popup));
                Assert.True(textBox.Focus());
                textBox.Text = "a";

                Assert.Equal(PopupPlacementDirection.Bottom, PopupPlacement.GetActualPlacement(popup));
                Assert.Equal(
                    PopupPlacementDirection.Bottom,
                    PopupPlacement.GetActualPlacement(suggestionsBorder)
                );
                Assert.Equal(new CornerRadius(4D, 4D, 0D, 0D), textBox.CornerRadius);
                Assert.Equal(new CornerRadius(0D, 0D, 8D, 8D), suggestionsBorder.CornerRadius);
                Assert.Equal(new Thickness(1D, 0D, 1D, 1D), suggestionsBorder.BorderThickness);

                popup.SetCurrentValue(Popup.IsOpenProperty, false);

                Assert.Equal(PopupPlacementDirection.None, PopupPlacement.GetActualPlacement(popup));
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void DefaultPopup_WhenBottomCandidateDoesNotFit_JoinsAboveEditor()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();
            var suggestionsBorder = Assert.IsType<Border>(popup.Child);

            try
            {
                // Put the placement target at the lower work-area edge. The suggestions surface cannot fit below,
                // so this exercises WPF's real Bottom-to-Top fallback instead of changing Popup.Placement to Top.
                window.Left = SystemParameters.WorkArea.Left + 100D;
                window.Top = SystemParameters.WorkArea.Bottom - window.ActualHeight;
                FlushDispatcher(window);

                Assert.True(textBox.Focus());
                textBox.Text = "a";

                Assert.Equal(PopupPlacementDirection.Top, PopupPlacement.GetActualPlacement(popup));
                Assert.Equal(new CornerRadius(0D, 0D, 4D, 4D), textBox.CornerRadius);
                Assert.Equal(new CornerRadius(8D, 8D, 0D, 0D), suggestionsBorder.CornerRadius);
                Assert.Equal(new Thickness(1D, 1D, 1D, 0D), suggestionsBorder.BorderThickness);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
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
    public void RegainingKeyboardFocusByMouse_ReopensNonStickyPopupAfterMouseUp()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();
            var outsideButton = new NativeButton { Content = "Outside" };
            Assert.IsType<Grid>(window.Content).Children.Add(outsideButton);

            try
            {
                Assert.False(popup.StaysOpen);

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(popup.IsOpen);

                Assert.True(outsideButton.Focus());
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);

                textBox.RaiseEvent(
                    CreateMouseButtonEvent(textBox, UIElement.PreviewMouseLeftButtonDownEvent)
                );
                Assert.True(textBox.Focus());

                // Opening while the initiating button is still down lets Popup capture the matching
                // mouse-up and interpret it as an outside click. The control therefore waits for the
                // release before restoring the previously filtered suggestions.
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);

                textBox.RaiseEvent(CreateMouseButtonEvent(textBox, Mouse.MouseUpEvent));

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void WindowActivationFocus_DoesNotOpenNonStickyPopupBeforeActivatingClickCompletes()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "a";
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                Keyboard.ClearFocus();

                // Owner-window activation restores focus before it dispatches the mouse-down that
                // caused activation. Reproduce that ordering and ensure the idle focus restoration
                // cannot let Popup capture the still-pending click.
                Assert.True(textBox.Focus());
                textBox.RaiseEvent(
                    CreateMouseButtonEvent(textBox, UIElement.PreviewMouseLeftButtonDownEvent)
                );
                FlushDispatcher(window);

                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);

                textBox.RaiseEvent(CreateMouseButtonEvent(textBox, Mouse.MouseUpEvent));

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void RegainingKeyboardFocusWithNoOldFocus_ReopensSuggestionsAfterInputQueueIsIdle()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "a";
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                Keyboard.ClearFocus();

                Assert.True(textBox.Focus());
                Assert.False(autoSuggestBox.IsSuggestionListOpen);

                FlushDispatcher(window);

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void TextBoxClick_RestoresPopupIfNonStickyCaptureClosesItDuringMouseDown()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(popup.IsOpen);

                textBox.RaiseEvent(
                    CreateMouseButtonEvent(textBox, UIElement.PreviewMouseLeftButtonDownEvent)
                );
                popup.SetCurrentValue(Popup.IsOpenProperty, false);

                Assert.False(autoSuggestBox.IsSuggestionListOpen);

                textBox.RaiseEvent(CreateMouseButtonEvent(textBox, Mouse.MouseUpEvent));

                Assert.True(autoSuggestBox.IsSuggestionListOpen);
                Assert.True(popup.IsOpen);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
                window.Close();
            }
        });
    }

    [Fact]
    public void DefaultPopup_UsesDismissalExclusionForAutoSuggestBoxOwner()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                // PreviewMouseDownOutsideCapturedElement carries the physical pointer position from
                // Popup's HWND. Move the test window under that pointer without moving the user's
                // cursor so the production cross-presentation-source bounds check is exercised.
                Assert.True(NativeMethods.GetCursorPos(out NativePoint cursorPosition));
                Point topLeft = autoSuggestBox.PointToScreen(default);
                DpiScale dpi = VisualTreeHelper.GetDpi(autoSuggestBox);
                window.Left +=
                    (cursorPosition.X - topLeft.X - (autoSuggestBox.ActualWidth * dpi.DpiScaleX / 2D))
                    / dpi.DpiScaleX;
                window.Top +=
                    (cursorPosition.Y - topLeft.Y - (autoSuggestBox.ActualHeight * dpi.DpiScaleY / 2D))
                    / dpi.DpiScaleY;
                FlushDispatcher(window);

                Assert.True(NativeMethods.GetCursorPos(out cursorPosition));
                topLeft = autoSuggestBox.PointToScreen(default);
                Point bottomRight = autoSuggestBox.PointToScreen(
                    new Point(autoSuggestBox.ActualWidth, autoSuggestBox.ActualHeight)
                );
                Assert.InRange((double)cursorPosition.X, topLeft.X, bottomRight.X);
                Assert.InRange((double)cursorPosition.Y, topLeft.Y, bottomRight.Y);

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.False(popup.StaysOpen);
                Assert.True(popup.IsOpen);
                Assert.Same(autoSuggestBox, PopupDismissal.GetExcludedElement(popup));

                popup.RaiseEvent(
                    CreateMouseButtonEvent(popup, Mouse.PreviewMouseDownOutsideCapturedElementEvent)
                );

                // The Popup remains open and releases PopupRoot capture for this owner click.
                Assert.True(popup.StaysOpen);
                Assert.True(popup.IsOpen);

                textBox.RaiseEvent(CreateMouseButtonEvent(textBox, Mouse.MouseUpEvent));

                Assert.False(popup.StaysOpen);
                Assert.True(popup.IsOpen);
                Assert.True(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                popup.SetCurrentValue(Popup.IsOpenProperty, false);
                FlushDispatcher(window);
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

    [Fact]
    public void RegainingKeyboardFocus_DoesNotReopenSuggestionsWhenReopeningIsDisabled()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, outsideButton) = CreateLoadedControl();

            try
            {
                autoSuggestBox.IsSuggestionListReopeningEnabled = false;

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.True(autoSuggestBox.IsSuggestionListOpen);

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
    public void TextBoxClick_DoesNotRestoreSuggestionsWhenReopeningIsDisabled()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, popup) = CreateLoadedDefaultControl();

            try
            {
                autoSuggestBox.IsSuggestionListReopeningEnabled = false;

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                popup.SetCurrentValue(Popup.IsOpenProperty, false);

                textBox.RaiseEvent(
                    CreateMouseButtonEvent(textBox, UIElement.PreviewMouseLeftButtonDownEvent)
                );
                textBox.RaiseEvent(CreateMouseButtonEvent(textBox, Mouse.MouseUpEvent));

                Assert.False(autoSuggestBox.IsSuggestionListOpen);
                Assert.False(popup.IsOpen);
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
    public void SelectingSuggestion_RaisesHighlightedAndPreviewsTextWithoutFilteringOrCommitting()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var chosenItems = new List<object>();
                var highlightedItems = new List<object>();
                var changedTexts = new List<string>();
                autoSuggestBox.SuggestionChosen += (_, args) => chosenItems.Add(args.SelectedItem);
                autoSuggestBox.SuggestionHighlighted += (_, args) =>
                    highlightedItems.Add(args.HighlightedItem);
                autoSuggestBox.TextChanged += (_, args) => changedTexts.Add(args.Text);

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
                Assert.Equal(new object[] { "Alpha" }, highlightedItems);
                Assert.Equal("Alpha", changedTexts[^1]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void SuggestionHighlighted_WhenHandled_DoesNotPreviewText()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var highlightedItems = new List<object>();
                autoSuggestBox.SuggestionHighlighted += (_, args) =>
                {
                    highlightedItems.Add(args.HighlightedItem);
                    args.Handled = true;
                };

                Assert.True(textBox.Focus());
                textBox.Text = "a";

                var suggestionsList = Assert.IsAssignableFrom<NativeListBox>(
                    autoSuggestBox.Template.FindName("PART_SuggestionsList", autoSuggestBox)
                );
                suggestionsList.SelectedItem = "Alpha";

                Assert.Equal(new object[] { "Alpha" }, highlightedItems);
                Assert.Equal("a", autoSuggestBox.Text);
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void ProgrammaticTextChange_RaisesTextChangedAndFiltersWithoutOpeningSuggestions()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                var changedTexts = new List<string>();
                autoSuggestBox.TextChanged += (_, args) => changedTexts.Add(args.Text);

                autoSuggestBox.Text = "Alpha";

                Assert.Equal("Alpha", textBox.Text);
                Assert.Equal(new[] { "Alpha" }, changedTexts);
                Assert.Equal(new[] { "Alpha" }, autoSuggestBox.Items.Cast<string>());
                Assert.False(autoSuggestBox.IsSuggestionListOpen);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CommittingSuggestion_FiltersAfterChosenEventUpdatesText()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                string? textObservedByChosenEvent = null;
                string[]? itemsObservedByChosenEvent = null;
                autoSuggestBox.SuggestionChosen += (_, _) =>
                {
                    textObservedByChosenEvent = autoSuggestBox.Text;
                    itemsObservedByChosenEvent = autoSuggestBox.Items.Cast<string>().ToArray();
                };

                Assert.True(textBox.Focus());
                textBox.Text = "a";
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());

                autoSuggestBox.ChooseSuggestion("Alpha");

                Assert.Equal("a", textObservedByChosenEvent);
                Assert.Equal(new[] { "Alpha", "Beta" }, itemsObservedByChosenEvent);
                Assert.Equal("Alpha", autoSuggestBox.Text);
                Assert.Equal(new[] { "Alpha" }, autoSuggestBox.Items.Cast<string>());
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void CommittingSuggestion_WithUnchangedText_DoesNotFilterAgain()
    {
        RunOnStaThread(() =>
        {
            var (window, autoSuggestBox, textBox, _) = CreateLoadedControl();

            try
            {
                Assert.True(textBox.Focus());
                textBox.Text = "Alpha";
                autoSuggestBox.ItemsSource = new[] { "Alpha", "Beta" };

                autoSuggestBox.ChooseSuggestion("Alpha");

                Assert.Equal("Alpha", autoSuggestBox.Text);
                Assert.Equal(new[] { "Alpha", "Beta" }, autoSuggestBox.Items.Cast<string>());
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
            var beta = new Suggestion(new SuggestionDetails("Beta"));
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
                Assert.Equal(new[] { "Beta" }, autoSuggestBox.Items.Cast<string>());
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
            DisplayMemberPath = displayMemberPath,
            IsSuggestionListReopeningEnabled = true,
            OriginalItemsSource = originalItemsSource ?? new[] { "Alpha", "Beta" },
            Template = CreateControlTemplate(),
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
        var autoSuggestBox = new AutoSuggestBox
        {
            IsSuggestionListReopeningEnabled = true,
            OriginalItemsSource = new[] { "Alpha", "Beta" },
        };
        var root = new Grid();
        root.Resources.MergedDictionaries.Add(
            new ResourceDictionary
            {
                Source = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml",
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

    private static MouseButtonEventArgs CreateMouseButtonEvent(UIElement source, RoutedEvent routedEvent)
    {
        return new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
        {
            RoutedEvent = routedEvent,
            Source = source,
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out NativePoint point);
    }

    private sealed class TestAutoSuggestBox : AutoSuggestBox
    {
        public void ChooseSuggestion(object selectedItem)
        {
            OnSuggestionChosen(selectedItem);
        }

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
        WpfTestThread.Run(action);
    }
}
