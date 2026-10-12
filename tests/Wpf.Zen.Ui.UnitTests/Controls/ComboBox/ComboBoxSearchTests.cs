// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using Wpf.Zen.Ui.Controls;
using NativeComboBoxItem = System.Windows.Controls.ComboBoxItem;
using NativeTextBox = System.Windows.Controls.TextBox;
using TextBlock = System.Windows.Controls.TextBlock;
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
    /// Search-driven popup opening preserves the insertion point across consecutive text edits.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxSearchInteraction.AutoSelect)]
    [InlineData(ComboBoxSearchInteraction.Filter)]
    public void LiveSearch_OpeningPreservesConsecutiveInput(ComboBoxSearchInteraction interaction)
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("TextBlock", "TextBox");
            comboBox.SearchInteraction = interaction;
            using var source = LoadComboBox(comboBox);
            var editor = GetEditor(comboBox);

            InsertText(editor, "t");
            DrainDispatcher(comboBox);
            Assert.True(comboBox.IsDropDownOpen);
            Assert.Equal(1, editor.SelectionStart);
            Assert.Equal(0, editor.SelectionLength);

            InsertText(editor, "e");
            DrainDispatcher(comboBox);
            Assert.Equal("te", editor.Text);
            Assert.Equal("te", comboBox.Text);
            Assert.Equal(2, editor.SelectionStart);
            Assert.Equal(0, editor.SelectionLength);

            // Reopening after dismissal must preserve a middle-of-text insertion point too.
            comboBox.IsDropDownOpen = false;
            editor.Select(1, 0);
            InsertText(editor, "x");
            DrainDispatcher(comboBox);
            Assert.True(comboBox.IsDropDownOpen);
            Assert.Equal("txe", editor.Text);
            Assert.Equal(2, editor.SelectionStart);
            Assert.Equal(0, editor.SelectionLength);
            comboBox.IsDropDownOpen = false;
        });
    }

    /// <summary>
    /// Clearing a live query refreshes matches without opening or closing the drop-down.
    /// </summary>
    [Theory]
    [InlineData(ComboBoxSearchInteraction.AutoSelect, false, "")]
    [InlineData(ComboBoxSearchInteraction.AutoSelect, true, "")]
    [InlineData(ComboBoxSearchInteraction.Filter, false, "")]
    [InlineData(ComboBoxSearchInteraction.Filter, true, "")]
    [InlineData(ComboBoxSearchInteraction.AutoSelect, false, " \t ")]
    [InlineData(ComboBoxSearchInteraction.Filter, false, " \t ")]
    public void LiveSearch_ClearingRetainsDropDownState(
        ComboBoxSearchInteraction interaction, bool keepOpen, string emptyQuery)
    {
        WpfTestThread.Run(() =>
        {
            var comboBox = CreateComboBox("TextBlock", "TextBox");
            comboBox.SearchInteraction = interaction;
            comboBox.SelectedIndex = 1;
            using var source = LoadComboBox(comboBox);
            var editor = GetEditor(comboBox);
            editor.Text = "t";
            DrainDispatcher(comboBox);
            Assert.True(comboBox.IsDropDownOpen);
            comboBox.IsDropDownOpen = keepOpen;

            editor.Text = emptyQuery;
            DrainDispatcher(comboBox);
            Assert.Equal(keepOpen, comboBox.IsDropDownOpen);
            Assert.Equal(emptyQuery, editor.Text);
            Assert.Equal(emptyQuery, comboBox.Text);
            if (interaction == ComboBoxSearchInteraction.AutoSelect)
            {
                Assert.Equal(-1, comboBox.SelectedIndex);
            }
            else
            {
                Assert.Equal(1, comboBox.SelectedIndex);
                Assert.Empty(comboBox.SearchIndices);
            }

            editor.Text = "te";
            DrainDispatcher(comboBox);
            Assert.True(comboBox.IsDropDownOpen);
            comboBox.IsDropDownOpen = false;
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
            var parameters = new HwndSourceParameters("Filtered ComboBox test")
            {
                Width = 1, Height = 1, WindowStyle = 0,
            };
            using var source = new HwndSource(parameters);
            source.RootVisual = comboBox;
            editor.Text = "box";
            Assert.True(comboBox.IsTextSearchFiltering);
            Assert.Equal(new[] { 0, 2 }, comboBox.SearchIndices);
            Assert.Equal(3, comboBox.Items.Count);
            Assert.Equal(1, comboBox.SelectedIndex);
            var sharedView = CollectionViewSource.GetDefaultView(comboBox.ItemsSource);
            Assert.Null(sharedView.Filter);
            comboBox.Measure(new Size(300, 100));
            comboBox.Arrange(new Rect(0, 0, 300, 100));
            comboBox.UpdateLayout();
            Assert.True(RaiseKey(editor, source, Key.Down).Handled);
            Assert.True(((NativeComboBoxItem)comboBox.ItemContainerGenerator.ContainerFromIndex(2)).IsHighlighted);
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
            Assert.Empty(comboBox.SearchIndices);
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
            var selectionEvents = 0;
            comboBox.SelectionChanged += (_, _) => selectionEvents++;
            editor.Text = "box";
            comboBox.Dispatcher.Invoke(System.Windows.Threading.DispatcherPriority.Background, new Action(() => { }));
            Assert.True(comboBox.IsDropDownOpen);
            FrameworkElement container;
            if (interaction == ComboBoxSearchInteraction.Filter)
            {
                Assert.NotNull(comboBox.SearchPanel);
                Assert.Equal(1, comboBox.SelectedIndex);
                Assert.Equal(0, selectionEvents);
                var normalItems = (FrameworkElement)comboBox.Template.FindName("SearchItemsScrollViewer", comboBox);
                Assert.Equal(Visibility.Visible, normalItems.Visibility);
                Assert.Null(comboBox.ItemContainerGenerator.ContainerFromIndex(1));
                container = (FrameworkElement)comboBox.ItemContainerGenerator.ContainerFromIndex(2);
            }
            else
            {
                Assert.Null(comboBox.SearchPanel);
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
    /// Large results realize only viewport rows while retaining native item presentation.
    /// </summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Filter_VirtualizesNativeContainersAndPreservesPresentation(bool grouped, bool defaultTheme)
    {
        WpfTestThread.Run(() =>
        {
            const int count = 10000;
            var items = Enumerable.Range(0, count).Select(index => new SearchItem
            {
                Name = index == count - 1 ? "Item needle" : "Item " + index,
                Id = index,
            }).ToArray();
            var view = new ListCollectionView(items);
            if (grouped)
            {
                view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SearchItem.Group)));
            }

            var style = new Style(typeof(NativeComboBoxItem));
            style.Setters.Add(new Setter(FrameworkElement.HeightProperty, 24.0));
            style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(13, 2, 7, 2)));
            var tallLastRow = new DataTrigger
            {
                Binding = new Binding(nameof(SearchItem.Name)),
                Value = "Item needle",
            };
            tallLastRow.Setters.Add(new Setter(FrameworkElement.HeightProperty, 60.0));
            style.Triggers.Add(tallLastRow);
            var label = new FrameworkElementFactory(typeof(TextBlock));
            label.SetBinding(TextBlock.TextProperty, new Binding(nameof(SearchItem.Name)));
            var template = new DataTemplate { VisualTree = label };
            var originalPanel = new ItemsPanelTemplate(
                new FrameworkElementFactory(typeof(VirtualizingStackPanel))
            );
            var comboBox = new CountingComboBox
            {
                IsEditable = true,
                TextSearchMode = ComboBoxTextSearchMode.Contains,
                SearchInteraction = ComboBoxSearchInteraction.Filter,
                ItemsSource = view,
                ItemsPanel = originalPanel,
                ItemContainerStyle = style,
                ItemTemplate = template,
                SelectedIndex = 0,
                Template = CreateTemplate(),
            };
            TextSearch.SetTextPath(comboBox, nameof(SearchItem.Name));
            System.Windows.Controls.ScrollViewer.SetCanContentScroll(comboBox, true);
            VirtualizingPanel.SetIsVirtualizingWhenGrouping(comboBox, true);
            if (defaultTheme)
            {
                System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);
                var themeUri = new Uri(
                    "pack://application:,,,/Wpf.Zen.Ui;component/Themes/Generic.xaml", UriKind.Absolute
                );
                comboBox.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = themeUri });
                comboBox.ClearValue(Control.TemplateProperty);
                comboBox.MaxDropDownHeight = 100;
            }

            using var source = LoadComboBox(comboBox);
            var editor = GetEditor(comboBox);
            comboBox.GeneratedCount = 0;
            editor.Text = "Item";
            DrainDispatcher(comboBox);
            comboBox.UpdateLayout();
            Assert.Equal(count, comboBox.SearchIndices.Count);
            Assert.InRange(comboBox.GeneratedCount, 1, 30);
            var panel = Assert.IsType<ComboBoxSearchPanel>(comboBox.SearchPanel);
            Assert.InRange(panel.Children.Count, 1, 10);
            var firstContainer = comboBox.ItemContainerGenerator.ContainerFromIndex(0);
            var first = Assert.IsType<NativeComboBoxItem>(firstContainer);
            Assert.Same(comboBox, ItemsControl.ItemsControlFromItemContainer(first));
            Assert.Same(items[0], first.Content);
            Assert.Same(items[0], first.DataContext);
            Assert.Same(style, first.Style);
            Assert.Same(template, first.ContentTemplate);
            Assert.Equal(new Thickness(13, 2, 7, 2), first.Padding);

            panel.SetVerticalOffset(5000);
            comboBox.UpdateLayout();
            Assert.InRange(panel.Children.Count, 1, 10);
            Assert.Null(comboBox.ItemContainerGenerator.ContainerFromIndex(0));
            var middleContainer = comboBox.ItemContainerGenerator.ContainerFromIndex(5000);
            var middle = Assert.IsType<NativeComboBoxItem>(middleContainer);
            Assert.Same(items[5000], middle.Content);
            Assert.Equal(0, comboBox.SelectedIndex);
            Assert.Equal("Item", editor.Text);
            Assert.True(comboBox.IsTextSearchFiltering);

            var scrollbarMaximum = panel.ExtentHeight - panel.ViewportHeight;
            panel.SetVerticalOffset(scrollbarMaximum);
            panel.UpdateLayout();
            var bottomContainer = comboBox.ItemContainerGenerator.ContainerFromIndex(count - 1);
            var bottomItem = Assert.IsType<NativeComboBoxItem>(bottomContainer);
            var bottomTransform = bottomItem.TransformToAncestor(panel);
            var bottomBounds = bottomTransform.TransformBounds(new Rect(bottomItem.RenderSize));
            Assert.True(bottomBounds.Bottom <= panel.ActualHeight + 0.01);

            panel.SetVerticalOffset(double.PositiveInfinity);
            panel.UpdateLayout();
            Assert.InRange(panel.Children.Count, 2, 10);
            Assert.NotNull(comboBox.ItemContainerGenerator.ContainerFromIndex(count - 1));
            Assert.Equal("Item", editor.Text);

            comboBox.GeneratedCount = 0;
            editor.Text = "needle";
            DrainDispatcher(comboBox);
            comboBox.UpdateLayout();
            Assert.Equal(new[] { count - 1 }, comboBox.SearchIndices);
            Assert.InRange(comboBox.GeneratedCount, 1, 3);
            var lastContainer = comboBox.ItemContainerGenerator.ContainerFromIndex(count - 1);
            var last = Assert.IsType<NativeComboBoxItem>(lastContainer);
            Assert.Same(items[count - 1], last.Content);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Equal(count - 1, comboBox.SelectedIndex);
            Assert.Same(originalPanel, comboBox.ItemsPanel);
            Assert.Null(comboBox.GroupStyleSelector);
            Assert.Null(view.Filter);
            Assert.Equal(count, comboBox.Items.Count);
            Assert.Equal(grouped ? 1 : 0, view.GroupDescriptions.Count);
        });
    }

    /// <summary>
    /// Explicit native containers preserve their identity and local styling during filtering.
    /// </summary>
    [Fact]
    public void Filter_ReusesExplicitComboBoxItem()
    {
        WpfTestThread.Run(() =>
        {
            var item = new NativeComboBoxItem { Content = "TextBox", Padding = new Thickness(17) };
            var comboBox = CreateComboBox("TextBlock", item);
            comboBox.SearchInteraction = ComboBoxSearchInteraction.Filter;
            using var source = LoadComboBox(comboBox);
            var editor = GetEditor(comboBox);
            editor.Text = "box";
            DrainDispatcher(comboBox);
            Assert.Same(item, comboBox.ItemContainerGenerator.ContainerFromIndex(1));
            Assert.Same(comboBox, ItemsControl.ItemsControlFromItemContainer(item));
            Assert.Equal(new Thickness(17), item.Padding);
            Assert.True(comboBox.TryCommitTextSearch());
            Assert.Same(item, comboBox.SelectedItem);
        });
    }

    /// <summary>
    /// Keyboard navigation rechecks container availability when item state changes during search.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Filter_NavigationSkipsUnavailableItem(bool collapse)
    {
        WpfTestThread.Run(() =>
        {
            var first = new NativeComboBoxItem { Content = "Box A" };
            var second = new NativeComboBoxItem { Content = "Box B" };
            var third = new NativeComboBoxItem { Content = "Box C" };
            var comboBox = CreateComboBox(first, second, third);
            comboBox.SearchInteraction = ComboBoxSearchInteraction.Filter;
            using var source = LoadComboBox(comboBox);
            var editor = GetEditor(comboBox);
            editor.Text = "Box";
            DrainDispatcher(comboBox);
            Assert.True(first.IsHighlighted);
            if (collapse)
            {
                second.Visibility = Visibility.Collapsed;
            }
            else
            {
                second.IsEnabled = false;
            }

            Assert.True(RaiseKey(editor, source, Key.Down).Handled);
            Assert.True(third.IsHighlighted);
            Assert.True(RaiseKey(editor, source, Key.Up).Handled);
            Assert.True(first.IsHighlighted);
            third.IsEnabled = false;
            Assert.True(RaiseKey(editor, source, Key.Down).Handled);
            Assert.True(first.IsHighlighted);
            Assert.Equal(-1, comboBox.SelectedIndex);
            Assert.Equal("Box", editor.Text);
        });
    }

    /// <summary>
    /// Counts actual native container creation, independently of source matching work.
    /// </summary>
    private sealed class CountingComboBox : UiComboBox
    {
        /// <summary>
        /// Gets or sets the creation counter used to bound virtualization work.
        /// </summary>
        internal int GeneratedCount { get; set; }

        /// <summary>
        /// Records generated containers while retaining the native ComboBoxItem implementation.
        /// </summary>
        protected override DependencyObject GetContainerForItemOverride()
        {
            GeneratedCount++;
            return base.GetContainerForItemOverride();
        }
    }

    /// <summary>
    /// Attaches the control to an HWND and completes loading so WPF permits drop-down opening.
    /// </summary>
    private static HwndSource LoadComboBox(UiComboBox comboBox)
    {
        var parameters = new HwndSourceParameters("ComboBox live input test")
        {
            Width = 300,
            Height = 100,
            WindowStyle = 0,
        };
        var source = new HwndSource(parameters);
        source.RootVisual = comboBox;
        comboBox.Measure(new Size(300, 100));
        comboBox.Arrange(new Rect(0, 0, 300, 100));
        comboBox.UpdateLayout();
        DrainDispatcher(comboBox);
        return source;
    }

    /// <summary>
    /// Completes pending loading and popup navigation before checking editor state.
    /// </summary>
    private static void DrainDispatcher(UiComboBox comboBox)
    {
        comboBox.Dispatcher.Invoke(
            System.Windows.Threading.DispatcherPriority.Background, new Action(() => { })
        );
    }

    /// <summary>
    /// Replaces the selection and advances the caret in one text transaction, as typing does.
    /// TextChanged observes the final caret position when the transaction ends.
    /// </summary>
    private static void InsertText(NativeTextBox editor, string text)
    {
        var insertionStart = editor.SelectionStart;
        editor.BeginChange();
        try
        {
            editor.SelectedText = text;
            var caretIndex = insertionStart + text.Length;
            editor.Select(caretIndex, 0);
        }
        finally
        {
            editor.EndChange();
        }
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
        var scroll = new FrameworkElementFactory(typeof(System.Windows.Controls.ScrollViewer));
        scroll.SetValue(FrameworkElement.MaxHeightProperty, 100.0);
        var scrolling = new Binding("(ScrollViewer.CanContentScroll)")
        {
            RelativeSource = RelativeSource.TemplatedParent,
        };
        scroll.SetBinding(System.Windows.Controls.ScrollViewer.CanContentScrollProperty, scrolling);
        scroll.AppendChild(new FrameworkElementFactory(typeof(ItemsPresenter)));
        root.AppendChild(scroll);
        return new ControlTemplate(typeof(UiComboBox)) { VisualTree = root };
    }

    /// <summary>
    /// Resolves the current template's editor so replacement tests do not reuse stale parts.
    /// </summary>
    private static NativeTextBox GetEditor(UiComboBox comboBox)
    {
        object editor = comboBox.Template.FindName("PART_EditableTextBox", comboBox);
        return Assert.IsAssignableFrom<NativeTextBox>(editor);
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

        /// <summary>
        /// Gets the group key used to exercise native grouped container generation.
        /// </summary>
        public int Group => Id / 1000;
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
