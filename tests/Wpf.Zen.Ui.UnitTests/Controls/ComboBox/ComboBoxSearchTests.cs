// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Zen.Ui.Controls;
using NativeComboBoxItem = System.Windows.Controls.ComboBoxItem;
using NativeTextBox = System.Windows.Controls.TextBox;
using UiComboBox = Wpf.Zen.Ui.Controls.ComboBox;

namespace Wpf.Zen.Ui.UnitTests.Controls;

/// <summary>
/// Verifies matching rules and the WPF editor/selection boundary used by custom search.
/// </summary>
public class ComboBoxSearchTests
{
    /// <summary>
    /// Covers ranked matching, whitespace tokens, character order, and Unicode text elements.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxTextSearchMode.Contains, "box", "ComboBoxItem", 2)]
    [InlineData(ComboBoxTextSearchMode.Contains, "box item", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Tokens, "box item", "ComboBoxItem", 3)]
    [InlineData(ComboBoxTextSearchMode.Tokens, "item\tbox", "ComboBoxItem", 3)]
    [InlineData(ComboBoxTextSearchMode.Tokens, "box missing", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Tokens, "cbi", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "cbi", "ComboBoxItem", 4)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "cci", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "ibc", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "cb item", "ComboBoxItem", 4)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "矿山 编号", "露天矿山设备编号", 3)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "😀b", "😀AlphaBeta", 4)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, " \t ", "ComboBoxItem", -1)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "ComboBoxItem", "ComboBoxItem", 0)]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "combo", "ComboBoxItem", 1)]
    public void Matcher_RanksExpectedMatches(
        ComboBoxTextSearchMode mode, string query, string candidate, int expectedRank
    )
    {
        var matcher = new ComboBoxTextMatcher(query, mode, CultureInfo.InvariantCulture, false);
        Assert.Equal(expectedRank, matcher.GetRank(candidate));
    }

    /// <summary>
    /// Ensures language-specific casing follows the control rather than invariant casing rules.
    /// </summary>
    [Fact]
    public void Matcher_UsesSpecifiedCultureAndCaseSensitivity()
    {
        var turkish = CultureInfo.GetCultureInfo("tr-TR");
        var insensitive = new ComboBoxTextMatcher("ı", ComboBoxTextSearchMode.Contains, turkish, false);
        var sensitive = new ComboBoxTextMatcher("ı", ComboBoxTextSearchMode.Contains, turkish, true);
        Assert.Equal(0, insensitive.GetRank("I"));
        Assert.Equal(-1, sensitive.GetRank("I"));
    }

    /// <summary>
    /// Checks that editing preserves committed selection and confirmation updates bound values.
    /// </summary>
    [Fact]
    public void Commit_SelectsBestMatchAndSynchronizesFullTextAndBindings()
    {
        WpfTestThread.Run(() =>
        {
            var first = new SearchItem { Name = "Alpha ComboBoxItem", Id = 1 };
            var second = new SearchItem { Name = "ComboBoxItem", Id = 2 };
            var model = new SearchModel();
            var comboBox = CreateComboBox(first, second);
            comboBox.DisplayMemberPath = nameof(SearchItem.Name);
            comboBox.SelectedValuePath = nameof(SearchItem.Id);
            Bind(comboBox, Selector.SelectedItemProperty, model, nameof(SearchModel.Selection));
            Bind(comboBox, System.Windows.Controls.ComboBox.TextProperty, model, nameof(SearchModel.Query));
            var editor = GetEditor(comboBox);
            var referenceEditor = new NativeTextBox();

            editor.SelectedText = "box";
            referenceEditor.SelectedText = "box";
            Assert.Null(comboBox.SelectedItem);
            Assert.Null(model.Selection);
            Assert.Equal("box", editor.Text);
            Assert.Equal("box", comboBox.Text);
            Assert.Equal("box", model.Query);
            Assert.Equal(referenceEditor.SelectionStart, editor.SelectionStart);
            Assert.Equal(referenceEditor.SelectionLength, editor.SelectionLength);

            editor.Select(editor.Text.Length, 0);
            referenceEditor.Select(referenceEditor.Text.Length, 0);
            editor.SelectedText = " item";
            referenceEditor.SelectedText = " item";
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal("box item", editor.Text);
            Assert.Equal(referenceEditor.SelectionStart, editor.SelectionStart);
            Assert.Equal(referenceEditor.SelectionLength, editor.SelectionLength);

            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(first, comboBox.SelectedItem);
            Assert.Same(first, model.Selection);
            Assert.Equal(1, comboBox.SelectedValue);
            Assert.Equal(first.Name, editor.Text);
            Assert.Equal(first.Name, comboBox.Text);
            Assert.Equal(first.Name, model.Query);

            editor.Text = "ComboBoxItem";
            Assert.Same(first, comboBox.SelectedItem);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(second, comboBox.SelectedItem);
            Assert.Same(second, model.Selection);
            Assert.Equal(2, comboBox.SelectedValue);
            Assert.True(BindingOperations.IsDataBound(comboBox, Selector.SelectedItemProperty));
            Assert.True(BindingOperations.IsDataBound(comboBox, System.Windows.Controls.ComboBox.TextProperty));
            Assert.Equal(2, comboBox.Items.Count);
            Assert.False(comboBox.IsDropDownOpen);
        });
    }

    /// <summary>
    /// Checks priority and ensures unsuccessful or empty input clears selection only on commit.
    /// </summary>
    [Fact]
    public void Commit_PrefersPrefixAndPreservesUnmatchedInput()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("AlphaBox", "BoxItem");
            var editor = GetEditor(comboBox);
            editor.Text = "box";
            Assert.Null(comboBox.SelectedItem);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal("BoxItem", comboBox.SelectedItem);
            Assert.Equal("BoxItem", editor.Text);

            editor.Text = "missing";
            Assert.Equal("BoxItem", comboBox.SelectedItem);
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal("missing", editor.Text);

            editor.Text = "alpha";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal("AlphaBox", comboBox.SelectedItem);
            editor.Text = string.Empty;
            Assert.Equal("AlphaBox", comboBox.SelectedItem);
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal(string.Empty, comboBox.Text);
        });
    }

    /// <summary>
    /// Ensures custom search and native item selection resolve the same display/search text.
    /// </summary>
    [Fact]
    public void Editing_UsesNativeAttachedTextAndTextPathPrecedence()
    {
        WpfTestThread.Run(() =>
        {
            var attachedItem = new NativeComboBoxItem { Content = "Unrelated display" };
            TextSearch.SetText(attachedItem, "AttachedSearchLabel");
            var comboBox = CreateComboBox(attachedItem, new NativeComboBoxItem { Content = "ContentLabel" });
            var editor = GetEditor(comboBox);
            editor.Text = "search";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(attachedItem, comboBox.SelectedItem);
            Assert.Equal("AttachedSearchLabel", editor.Text);
            editor.Text = "content";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(1, comboBox.SelectedIndex);

            var dataComboBox = CreateComboBox(new SearchItem { Name = "Visible", SearchName = "HiddenSearch" });
            dataComboBox.DisplayMemberPath = nameof(SearchItem.Name);
            TextSearch.SetTextPath(dataComboBox, nameof(SearchItem.SearchName));
            var dataEditor = GetEditor(dataComboBox);
            dataEditor.Text = "hidden";
            Assert.True(dataComboBox.TryCommitTextSearch());
            Assert.Equal(0, dataComboBox.SelectedIndex);
            dataEditor.Text = "visible";
            Assert.False(dataComboBox.TryCommitTextSearch());
            Assert.Equal(-1, dataComboBox.SelectedIndex);
        });
    }

    /// <summary>
    /// Excludes disabled items and applies case sensitivity to subsequent edits.
    /// </summary>
    [Fact]
    public void Editing_RespectsCaseSensitivityAndDisabledCandidates()
    {
        WpfTestThread.Run(() =>
        {
            var disabledItem = new NativeComboBoxItem { Content = "box", IsEnabled = false };
            var enabledItem = new NativeComboBoxItem { Content = "ComboBoxItem" };
            var comboBox = CreateComboBox(disabledItem, enabledItem);
            var editor = GetEditor(comboBox);
            editor.Text = "box";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(enabledItem, comboBox.SelectedItem);
            comboBox.IsTextSearchCaseSensitive = true;
            editor.Text = "BOX";
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Null(comboBox.SelectedItem);
            editor.Text = "Box";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(enabledItem, comboBox.SelectedItem);
        });
    }

    /// <summary>
    /// Protects programmatic text and selection synchronization from recursive custom searches.
    /// </summary>
    [Fact]
    public void ProgrammaticSelectionAndText_KeepNativeSynchronization()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock");
            var editor = GetEditor(comboBox);
            editor.Text = "cbi";
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(0, comboBox.SelectedIndex);

            comboBox.SelectedIndex = 1;
            Assert.Equal("TextBlock", comboBox.Text);
            Assert.Equal("TextBlock", editor.Text);

            comboBox.Text = "ComboBoxItem";
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("ComboBoxItem", editor.Text);
            comboBox.Text = "box";
            Assert.Null(comboBox.SelectedItem);
            Assert.Equal("box", editor.Text);
        });
    }

    /// <summary>
    /// Exercises runtime mode changes and the inherited search-enable switch on one editor.
    /// </summary>
    [Fact]
    public void ModeChangesAndSearchDisable_RestoreNativeHandlingWithoutDuplicateCallbacks()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock");
            var editor = GetEditor(comboBox);
            comboBox.TextSearchMode = ComboBoxTextSearchMode.Prefix;
            editor.Text = "box";
            Assert.Null(comboBox.SelectedItem);
            editor.Text = "Combo";
            Assert.Equal(0, comboBox.SelectedIndex);

            comboBox.TextSearchMode = ComboBoxTextSearchMode.Fuzzy;
            comboBox.TextSearchMode = ComboBoxTextSearchMode.Contains;
            editor.Text = "Block";
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("Block", editor.Text);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("TextBlock", editor.Text);

            comboBox.IsTextSearchEnabled = false;
            editor.Text = "Box";
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("Box", comboBox.Text);

            comboBox.IsTextSearchEnabled = true;
            editor.Text = "Item";
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(0, comboBox.SelectedIndex);
        });
    }

    /// <summary>
    /// Ensures stale template parts cannot modify the control after its template is replaced.
    /// </summary>
    [Fact]
    public void TemplateReplacement_DetachesOldEditorAndConnectsNewEditor()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock");
            var oldEditor = GetEditor(comboBox);
            oldEditor.Text = "cbi";
            Assert.True(comboBox.TryCommitTextSearch());
            comboBox.Template = CreateTemplate();
            Assert.True(comboBox.ApplyTemplate());
            var editor = GetEditor(comboBox);
            Assert.NotSame(oldEditor, editor);

            editor.Text = "tb";
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(1, comboBox.SelectedIndex);
            oldEditor.Text = "cbi stale";
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("TextBlock", comboBox.Text);
        });
    }

    /// <summary>
    /// Checks the search hook with the library's actual template and embedded text-box type.
    /// </summary>
    [Fact]
    public void DefaultTheme_ConnectsEditableSearch()
    {
        WpfTestThread.Run(() =>
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            var themeUri = new Uri(
                "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute
            );
            var root = new Grid();
            root.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
            var comboBox = new UiComboBox
            {
                IsEditable = true,
                TextSearchMode = ComboBoxTextSearchMode.Fuzzy,
                ItemsSource = new[] { "ComboBoxItem", "TextBlock" },
            };
            root.Children.Add(comboBox);
            Assert.True(comboBox.ApplyTemplate());
            object part = comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            var editor = Assert.IsAssignableFrom<NativeTextBox>(part);
            editor.Text = "cbi";
            Assert.Null(comboBox.SelectedItem);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal("ComboBoxItem", comboBox.SelectedItem);
            Assert.Equal("ComboBoxItem", editor.Text);
        });
    }

    /// <summary>
    /// Verifies the routed Enter trigger and same-item label refresh in all custom modes.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxTextSearchMode.Contains, "Box")]
    [InlineData(ComboBoxTextSearchMode.Tokens, "box item")]
    [InlineData(ComboBoxTextSearchMode.Fuzzy, "cbi")]
    public void Enter_CommitsAndRefreshesAlreadySelectedItem(ComboBoxTextSearchMode mode, string query)
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock");
            comboBox.TextSearchMode = mode;
            var editor = GetEditor(comboBox);
            var parameters = new HwndSourceParameters("ComboBox search keyboard test")
            {
                Width = 1,
                Height = 1,
                WindowStyle = 0,
            };
            using var source = new HwndSource(parameters);
            source.RootVisual = comboBox;

            editor.Text = query;
            Assert.Equal(-1, comboBox.SelectedIndex);
            var enter = RaiseKey(editor, source, Key.Enter);
            Assert.True(enter.Handled);
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("ComboBoxItem", editor.Text);

            editor.Text = query;
            Assert.Equal(0, comboBox.SelectedIndex);
            RaiseKey(editor, source, Key.Enter);
            Assert.Equal("ComboBoxItem", editor.Text);
            Assert.Equal("ComboBoxItem", comboBox.Text);

            editor.Text = "missing";
            var imeKey = RaiseKey(editor, source, Key.ImeProcessed);
            Assert.False(imeKey.Handled);
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("missing", editor.Text);

            var noMatchEnter = RaiseKey(editor, source, Key.Enter);
            Assert.True(noMatchEnter.Handled);
            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal("missing", editor.Text);
        });
    }

    /// <summary>
    /// Ensures explicit triggers respect unavailable editing/search instead of changing selection.
    /// </summary>
    [Fact]
    public void Commit_IsInactiveForReadOnlyDisabledAndNativeModes()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock");
            var editor = GetEditor(comboBox);
            comboBox.SelectedIndex = 1;
            editor.Text = "cbi";
            comboBox.IsReadOnly = true;
            Assert.False(comboBox.TryCommitTextSearch());
            comboBox.IsReadOnly = false;
            comboBox.IsEnabled = false;
            Assert.False(comboBox.TryCommitTextSearch());
            comboBox.IsEnabled = true;
            comboBox.TextSearchMode = ComboBoxTextSearchMode.Prefix;
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("cbi", editor.Text);
        });
    }

    /// <summary>
    /// Live selection updates bound selection while keeping the query editable.
    /// </summary>
    [Fact]
    public void AutoSelect_PreservesQueryAndUpdatesSelection()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock", "TextBox");
            comboBox.SearchInteraction = ComboBoxSearchInteraction.AutoSelect;
            var editor = GetEditor(comboBox);
            editor.Text = "box";
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("box", editor.Text);
            Assert.Equal("box", comboBox.Text);
            editor.Text = "tb";
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("tb", editor.Text);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal("TextBlock", editor.Text);
            editor.Text = "missing";
            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal("missing", editor.Text);
        });
    }

    /// <summary>
    /// Filtering owns a provisional list and does not alter a shared view or committed selection.
    /// </summary>
    [Fact]
    public void Filter_NavigationCommitsCandidateAndEscapeRetainsQuery()
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("ComboBoxItem", "TextBlock", "TextBox");
            comboBox.SearchInteraction = ComboBoxSearchInteraction.Filter;
            comboBox.SelectedIndex = 1;
            var editor = GetEditor(comboBox);
            var results = (System.Windows.Controls.ListBox)comboBox.Template.FindName("PART_SearchResults", comboBox);
            var parameters = new HwndSourceParameters("Filtered ComboBox test")
            {
                Width = 1, Height = 1, WindowStyle = 0,
            };
            using var source = new HwndSource(parameters);
            source.RootVisual = comboBox;
            editor.Text = "box";
            Assert.True(comboBox.IsTextSearchFiltering);
            Assert.Equal(2, results.Items.Count);
            Assert.Equal(3, comboBox.Items.Count);
            Assert.Equal(1, comboBox.SelectedIndex);
            var sharedView = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
            Assert.Null(sharedView.Filter);
            Assert.True(RaiseKey(editor, source, Key.Down).Handled);
            Assert.Equal(1, results.SelectedIndex);
            Assert.Equal(1, comboBox.SelectedIndex);
            Assert.Equal("box", editor.Text);
            Assert.True(RaiseKey(editor, source, Key.Enter).Handled);
            Assert.Equal(2, comboBox.SelectedIndex);
            Assert.Equal("TextBox", editor.Text);
            Assert.False(comboBox.IsTextSearchFiltering);
            editor.Text = "cbi";
            Assert.True(RaiseKey(editor, source, Key.Escape).Handled);
            Assert.Equal(2, comboBox.SelectedIndex);
            Assert.Equal("cbi", editor.Text);
            Assert.False(comboBox.IsTextSearchFiltering);
            editor.Text = "missing";
            Assert.Empty(results.Items);
            Assert.Equal(2, comboBox.SelectedIndex);
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal("missing", editor.Text);
        });
    }

    /// <summary>
    /// Mode changes, source mutation, and disabling search release stale provisional candidates.
    /// </summary>
    [Fact]
    public void Filter_StateChangesClearCandidatePresentation()
    {
        WpfTestThread.Run(() =>
        {
            var items = new System.Collections.ObjectModel.ObservableCollection<string>
            {
                "ComboBoxItem", "TextBlock",
            };
            var comboBox = CreateComboBox();
            comboBox.ItemsSource = items;
            comboBox.SearchInteraction = ComboBoxSearchInteraction.Filter;
            var editor = GetEditor(comboBox);
            editor.Text = "box";
            Assert.True(comboBox.IsTextSearchFiltering);
            items.RemoveAt(0);
            Assert.False(comboBox.IsTextSearchFiltering);
            Assert.False(comboBox.TryCommitTextSearch());
            editor.Text = "tb";
            Assert.True(comboBox.IsTextSearchFiltering);
            comboBox.SearchInteraction = ComboBoxSearchInteraction.AutoSelect;
            Assert.False(comboBox.IsTextSearchFiltering);
            editor.Text = "block";
            Assert.Equal(0, comboBox.SelectedIndex);
            comboBox.SearchInteraction = ComboBoxSearchInteraction.Filter;
            editor.Text = "text";
            comboBox.Text = "TextBlock";
            Assert.False(comboBox.IsTextSearchFiltering);
            Assert.Equal("TextBlock", editor.Text);
            editor.Text = "tb";
            comboBox.IsTextSearchEnabled = false;
            Assert.False(comboBox.IsTextSearchFiltering);
            Assert.False(comboBox.TryCommitTextSearch());
        });
    }

    /// <summary>
    /// Exercises the real popup, template visibility, and mouse confirmation of a non-best match.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxSearchInteraction.Filter)]
    [InlineData(ComboBoxSearchInteraction.AutoSelect)]
    public void DefaultTheme_PopupCommitsClickedCandidate(ComboBoxSearchInteraction interaction)
    {
        WpfTestThread.Run(() =>
        {
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
            var themeUri = new Uri(
                "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute
            );
            var root = new Grid();
            root.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
            var comboBox = new UiComboBox
            {
                IsEditable = true,
                TextSearchMode = ComboBoxTextSearchMode.Fuzzy,
                SearchInteraction = interaction,
                ItemsSource = new[] { "ComboBoxItem", "TextBlock", "TextBox" },
                SelectedIndex = 1,
                Width = 280,
            };
            root.Children.Add(comboBox);
            var parameters = new HwndSourceParameters("ComboBox actual popup test")
            {
                Width = 300, Height = 100, WindowStyle = 0,
            };
            using var source = new HwndSource(parameters);
            source.RootVisual = root;
            root.Measure(new Size(300, 100));
            root.Arrange(new Rect(0, 0, 300, 100));
            root.UpdateLayout();
            comboBox.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            var editor = (NativeTextBox)comboBox.Template.FindName("PART_EditableTextBox", comboBox);
            var results = (System.Windows.Controls.ListBox)comboBox.Template.FindName("PART_SearchResults", comboBox);
            var selectionEvents = 0;
            comboBox.SelectionChanged += (_, _) => selectionEvents++;
            editor.Text = "box";
            comboBox.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            Assert.True(comboBox.IsDropDownOpen);
            FrameworkElement container;
            if (interaction == ComboBoxSearchInteraction.Filter)
            {
                Assert.True(results.IsVisible);
                Assert.Equal(1, comboBox.SelectedIndex);
                Assert.Equal(0, selectionEvents);
                var normalItems = (FrameworkElement)comboBox.Template.FindName("SearchItemsScrollViewer", comboBox);
                Assert.Equal(Visibility.Collapsed, normalItems.Visibility);
                results.UpdateLayout();
                container = (FrameworkElement)results.ItemContainerGenerator.ContainerFromIndex(1);
            }
            else
            {
                Assert.False(results.IsVisible);
                Assert.Equal(0, comboBox.SelectedIndex);
                Assert.Equal(1, selectionEvents);
                container = (FrameworkElement)comboBox.ItemContainerGenerator.ContainerFromIndex(0);
                var item = Assert.IsAssignableFrom<NativeComboBoxItem>(container);
                Assert.True(item.IsHighlighted);
            }

            Assert.NotNull(container);
            var mouse = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            {
                RoutedEvent = Mouse.PreviewMouseUpEvent,
            };
            container.RaiseEvent(mouse);
            Assert.True(mouse.Handled);
            var expectedIndex = interaction == ComboBoxSearchInteraction.Filter ? 2 : 0;
            var expectedText = interaction == ComboBoxSearchInteraction.Filter ? "TextBox" : "ComboBoxItem";
            Assert.Equal(expectedIndex, comboBox.SelectedIndex);
            Assert.Equal(expectedText, editor.Text);
            Assert.Equal(1, selectionEvents);
            Assert.False(comboBox.IsDropDownOpen);
            Assert.False(comboBox.IsTextSearchFiltering);
            editor.Text = "missing";
            comboBox.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            Assert.False(comboBox.TryCommitTextSearch());
            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal("missing", editor.Text);
            comboBox.IsDropDownOpen = false;
        });
    }

    /// <summary>
    /// Raises the editor's real routed preview event without injecting operating-system input.
    /// </summary>
    private static KeyEventArgs RaiseKey(NativeTextBox editor, PresentationSource source, Key key)
    {
        var args = new KeyEventArgs(Keyboard.PrimaryDevice, source, Environment.TickCount, key)
        {
            RoutedEvent = Keyboard.PreviewKeyDownEvent,
        };
        editor.RaiseEvent(args);
        return args;
    }

    /// <summary>
    /// Creates a minimal real WPF editor to exercise text events without requiring an HWND.
    /// </summary>
    private static UiComboBox CreateComboBox(params object[] items)
    {
        var comboBox = new UiComboBox
        {
            IsEditable = true,
            TextSearchMode = ComboBoxTextSearchMode.Fuzzy,
            ItemsSource = items,
            Template = CreateTemplate(),
        };
        Assert.True(comboBox.ApplyTemplate());
        return comboBox;
    }

    /// <summary>
    /// Supplies the named part that WPF ComboBox uses for native editable-text synchronization.
    /// </summary>
    private static ControlTemplate CreateTemplate()
    {
        var editor = new FrameworkElementFactory(typeof(NativeTextBox), "PART_EditableTextBox");
        var root = new FrameworkElementFactory(typeof(Grid));
        root.AppendChild(editor);
        var results = new FrameworkElementFactory(typeof(System.Windows.Controls.ListBox), "PART_SearchResults");
        root.AppendChild(results);
        return new ControlTemplate(typeof(UiComboBox)) { VisualTree = root };
    }

    /// <summary>
    /// Resolves the current template's editor so replacement tests do not reuse stale parts.
    /// </summary>
    private static NativeTextBox GetEditor(UiComboBox comboBox)
    {
        object editor = comboBox.Template.FindName("PART_EditableTextBox", comboBox);
        return Assert.IsType<NativeTextBox>(editor);
    }

    /// <summary>
    /// Installs a two-way binding that exposes updates synchronously to assertions.
    /// </summary>
    private static void Bind(DependencyObject target, DependencyProperty property, object source, string path)
    {
        var binding = new Binding(path)
        {
            Source = source,
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
        };
        BindingOperations.SetBinding(target, property, binding);
    }

    /// <summary>
    /// Provides distinct display, search, and selected-value members for WPF binding tests.
    /// </summary>
    public sealed class SearchItem
    {
        /// <summary>
        /// Gets or sets the displayed label; defaults to empty text.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets an alternate search label; defaults to empty text.
        /// </summary>
        public string SearchName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the value exposed through SelectedValuePath.
        /// </summary>
        public int Id { get; set; }
    }

    /// <summary>
    /// Receives immediate two-way selection and text updates from the control under test.
    /// </summary>
    public sealed class SearchModel
    {
        /// <summary>
        /// Gets or sets the selected candidate, or null when no query match exists.
        /// </summary>
        public object? Selection { get; set; }

        /// <summary>
        /// Gets or sets the editor's query text; defaults to empty text.
        /// </summary>
        public string Query { get; set; } = string.Empty;
    }
}
