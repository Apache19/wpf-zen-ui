// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using NativeComboBox = System.Windows.Controls.ComboBox;
using NativeListBox = System.Windows.Controls.ListBox;
using NativeTextBox = System.Windows.Controls.TextBox;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Replaces the native editable-text search entry point while retaining WPF selection, text
/// extraction, binding, navigation, and composition behavior.
/// </summary>
/// <remarks>
/// WPF's ComboBox.TextUpdated and TextSearch.FindMatchingPrefix are private/internal and assume
/// prefix completion. The bridge uses UpdatingText to preserve input during live selection,
/// and SelectedItemUpdated to display even an already-selected match on commit.
/// Both net472 and modern WPF require these members; changes to WPF internals must be verified
/// against editable search tests before supporting a new runtime.
/// </remarks>
internal sealed class ComboBoxSearchController
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic   = BindingFlags.Static | BindingFlags.NonPublic;

    private static readonly MethodInfo NativeTextChangedMethod = GetRequiredMethod(
        typeof(NativeComboBox), "OnEditableTextBoxTextChanged", PrivateInstance
    );

    private static readonly Func<NativeComboBox, bool> GetUpdatingText = CreateFlagGetter("UpdatingText");
    private static readonly Func<NativeComboBox, bool> GetUpdatingSelection = CreateFlagGetter("UpdatingSelectedItem");
    private static readonly Action<NativeComboBox, bool> SetUpdatingText = CreateTextFlagSetter();
    private static readonly Func<ItemsControl, object?, string> GetItemText = CreateItemTextReader();
    private static readonly Func<TextBoxBase, bool> IsComposing = CreateCompositionReader();
    private static readonly Action<NativeComboBox> UpdateSelectedItem = CreateSelectionUpdater();

    private static readonly MethodInfo ItemInfoFromIndex = GetRequiredMethod(
        typeof(ItemsControl), "ItemInfoFromIndex", PrivateInstance
    );

    private static readonly PropertyInfo HighlightedInfo = typeof(NativeComboBox).GetProperty(
        "HighlightedInfo", PrivateInstance
    )!;

    private static readonly PropertyInfo HighlightedElement = typeof(NativeComboBox).GetProperty(
        "HighlightedElement", PrivateInstance
    )!;

    private NativeListBox?       _results;
    private DispatcherOperation? _highlightOperation;
    private bool                 _refreshing;

    private readonly ComboBox                _owner;
    private readonly TextChangedEventHandler _nativeTextChanged;

    private NativeTextBox?       _editor;
    private DispatcherOperation? _compositionOperation;
    private bool                 _waitingForComposition;

    /// <summary>
    /// Creates an owner-bound native handler so it can be restored without losing event identity.
    /// </summary>
    internal ComboBoxSearchController(ComboBox owner)
    {
        _owner = owner;
        _nativeTextChanged = (TextChangedEventHandler)NativeTextChangedMethod.CreateDelegate(
            typeof(TextChangedEventHandler), owner
        );
    }

    /// <summary>
    /// Replaces only the native TextChanged handler; native selection and input handlers remain.
    /// </summary>
    internal void Attach(NativeTextBox editor)
    {
        if (ReferenceEquals(_editor, editor))
        {
            return;
        }

        Detach(false);

        _editor  = editor;
        _results = _owner.Template.FindName("PART_SearchResults", _owner) as NativeListBox;

        _owner.DropDownClosed           += OnDropDownClosed;
        _owner.PreviewMouseLeftButtonUp += OnItemMouseUp;

        if (_results is not null)
        {
            _results.PreviewMouseLeftButtonUp += OnCandidateMouseUp;
            _results.SelectionChanged         += OnCandidateSelectionChanged;
        }

        editor.TextChanged      -= _nativeTextChanged;
        editor.TextChanged      += OnEditorTextChanged;
        editor.PreviewTextInput += OnEditorPreviewTextInput;
    }

    /// <summary>
    /// Cancels pending composition work and releases the previous template's editor.
    /// Native handling is restored only when switching modes on a still-active template.
    /// </summary>
    internal void Detach(bool restoreNativeHandler)
    {
        _compositionOperation?.Abort();
        _compositionOperation  = null;
        _waitingForComposition = false;

        if (_editor is null)
        {
            return;
        }

        _editor.TextChanged             -= OnEditorTextChanged;
        _editor.PreviewTextInput        -= OnEditorPreviewTextInput;
        _owner.DropDownClosed           -= OnDropDownClosed;
        _owner.PreviewMouseLeftButtonUp -= OnItemMouseUp;

        ResetPresentation();

        if (_results is not null)
        {
            _results.PreviewMouseLeftButtonUp -= OnCandidateMouseUp;
            _results.SelectionChanged         -= OnCandidateSelectionChanged;

            _results = null;
        }

        if (restoreNativeHandler)
        {
            _editor.TextChanged += _nativeTextChanged;
        }

        _editor = null;
    }

    /// <summary>
    /// Leaves disabled search and non-editable/read-only behavior in WPF's native pipeline.
    /// </summary>
    private void OnEditorTextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsCustomSearchActive)
        {
            ResetPresentation();
            _nativeTextChanged(sender, e);
            return;
        }

        SynchronizeEditorText();
    }

    private bool IsCustomSearchActive
    {
        get => _owner.IsEnabled
            && _owner.IsEditable
            && !_owner.IsReadOnly
            && _owner.IsTextSearchEnabled
            && _owner.TextSearchMode != ComboBoxTextSearchMode.Prefix;
    }

    /// <summary>
    /// Schedules a final read after IME commit. Some compositions commit without a final
    /// TextChanged event; deferral lets WPF finish updating its TextStore and undo transaction.
    /// </summary>
    private void OnEditorPreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!IsCustomSearchActive || _editor is null)
        {
            return;
        }

        if (!_waitingForComposition && !IsComposing(_editor))
        {
            return;
        }

        _compositionOperation?.Abort();
        _compositionOperation = _owner.Dispatcher.BeginInvoke(
            DispatcherPriority.Input,
            new Action(OnCompositionCompleted)
        );
    }

    /// <summary>
    /// Rechecks current mode and editor state when queued input processing resumes.
    /// </summary>
    private void OnCompositionCompleted()
    {
        _compositionOperation = null;
        if (IsCustomSearchActive)
        {
            SynchronizeEditorText();
        }
    }

    /// <summary>
    /// Synchronizes input under WPF's text-update guard and refreshes the chosen presentation.
    /// This preserves the caret, undo history, and Text binding while live results change.
    /// </summary>
    private void SynchronizeEditorText()
    {
        if (_editor is null || _refreshing || GetUpdatingText(_owner))
        {
            return;
        }

        if (GetUpdatingSelection(_owner))
        {
            ResetPresentation();
            return;
        }

        if (IsComposing(_editor))
        {
            _waitingForComposition = true;
            return;
        }

        _waitingForComposition = false;

        var query = _editor.Text;

        SetUpdatingText(_owner, true);

        try
        {
            _owner.SetCurrentValue(NativeComboBox.TextProperty, query);
        }
        finally
        {
            SetUpdatingText(_owner, false);
        }

        RefreshPresentation(query);
    }

    /// <summary>
    /// Clears transient candidates on dismissal without committing their provisional selection.
    /// </summary>
    private void OnDropDownClosed(object? sender, EventArgs e)
    {
        ResetPresentation();
    }

    /// <summary>
    /// Ends provisional search when application code changes text or selection. Updates made
    /// by the active search retain their presentation under the native text-update guard.
    /// </summary>
    internal void OnOwnerValueChanged()
    {
        if (!_refreshing && !GetUpdatingText(_owner))
        {
            ResetPresentation();
        }
    }

    /// <summary>
    /// Releases pending popup work and candidate references when state or source changes.
    /// </summary>
    internal void ResetPresentation()
    {
        _highlightOperation?.Abort();
        _highlightOperation = null;

        if (_owner.IsTextSearchFiltering)
        {
            HighlightedInfo.SetValue(_owner, null);

            _owner.SetSearchFiltering(false);
            _owner.SetCurrentValue(NativeComboBox.IsDropDownOpenProperty, false);
        }

        if (_results is not null)
        {
            _results.ItemsSource = null;
        }
    }

    /// <summary>
    /// Presents live results without changing the query or the source collection's view.
    /// </summary>
    private void RefreshPresentation(string query)
    {
        if (_owner.SearchInteraction == ComboBoxSearchInteraction.ExplicitCommit)
        {
            return;
        }

        _refreshing = true;

        try
        {
            var candidates = FindCandidates(query);
            var best       = FindBestCandidate(candidates);

            if (_owner.SearchInteraction == ComboBoxSearchInteraction.AutoSelect)
            {
                HighlightedInfo.SetValue(_owner, null);
                SetUpdatingText(_owner, true);

                try
                {
                    var index = best?.Index ?? -1;
                    _owner.SetCurrentValue(Selector.SelectedIndexProperty, index);
                }
                finally
                {
                    SetUpdatingText(_owner, false);
                }
            }
            else
            {
                if (_results is null)
                {
                    throw new InvalidOperationException(
                        "Filter search requires a PART_SearchResults ListBox in the ComboBox template."
                    );
                }

                _results.ItemsSource  = candidates;
                _results.SelectedItem = best;

                _owner.SetSearchFiltering(true);

                HighlightedInfo.SetValue(_owner, null);
            }

            OpenDropDownForQuery(query);

            _highlightOperation?.Abort();
            _highlightOperation = _owner.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
            {
                _highlightOperation = null;

                if (!_owner.IsDropDownOpen || !IsCustomSearchActive)
                {
                    return;
                }

                if (_owner.IsTextSearchFiltering)
                {
                    // Native popup opening queues navigation to the committed selection.
                    // Clear that highlight after layout; our ListBox owns provisional navigation.
                    HighlightedInfo.SetValue(_owner, null);

                    if (_results?.SelectedItem is SearchCandidate candidate)
                    {
                        _results.ScrollIntoView(candidate);
                    }
                }
                else if (best is not null && best.Index < _owner.Items.Count)
                {
                    var info = ItemInfoFromIndex.Invoke(_owner, new object[]
                    {
                        best.Index
                    });

                    HighlightedInfo.SetValue(_owner, info);

                    var container = _owner.ItemContainerGenerator.ContainerFromIndex(best.Index);
                    if (container is FrameworkElement element)
                    {
                        element.BringIntoView();
                    }
                }
                else
                {
                    HighlightedInfo.SetValue(_owner, null);
                }
            }));
        }
        finally
        {
            _refreshing = false;
        }
    }

    /// <summary>
    /// Opens live results for nonempty input while preserving the editor's current selection.
    /// Empty input leaves the drop-down's existing open or closed state unchanged.
    /// </summary>
    private void OpenDropDownForQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query) || _owner.IsDropDownOpen || _editor is null)
        {
            return;
        }

        var editor = _editor;

        var selectionStart  = editor.SelectionStart;
        var selectionLength = editor.SelectionLength;

        // WPF's OnIsDropDownOpenChanged synchronously calls SelectAll on the editable text box.
        // Search-driven opening must retain the insertion point for the next input operation.
        _owner.SetCurrentValue(NativeComboBox.IsDropDownOpenProperty, true);
        editor.Select(selectionStart, selectionLength);
    }

    /// <summary>
    /// Completes same-item clicks in live selection mode even when selection does not change.
    /// </summary>
    private void OnItemMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_owner.SearchInteraction != ComboBoxSearchInteraction.AutoSelect || !CanCommit)
        {
            return;
        }

        var source    = e.OriginalSource as DependencyObject;
        // todo: source 可能为 null，ItemsControl.ContainerFromElement 不允许第二个参数为 null。
        var container = ItemsControl.ContainerFromElement(_owner, source);
        if (container is System.Windows.Controls.ComboBoxItem item && item.IsEnabled)
        {
            var index = _owner.ItemContainerGenerator.IndexFromContainer(item);
            TryCommit(index);
            e.Handled = true;
        }
    }

    /// <summary>
    /// Commits a clicked candidate while keeping keyboard focus in the editable text box.
    /// </summary>
    private void OnCandidateMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_owner.IsTextSearchFiltering || _results is null)
        {
            return;
        }

        var source    = e.OriginalSource as DependencyObject;
        var container = ItemsControl.ContainerFromElement(_results, source) as System.Windows.Controls.ListBoxItem;
        if (container is not null && CanCommit)
        {
            var item = _results.ItemContainerGenerator.ItemFromContainer(container);
            if (item is SearchCandidate candidate)
            {
                TryCommit(candidate.Index);
            }

            _editor?.Focus();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Keeps provisional ListBox selection events inside the template instead of exposing them
    /// to handlers listening for the ComboBox's committed SelectionChanged event.
    /// </summary>
    private void OnCandidateSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        e.Handled = true;
    }

    /// <summary>
    /// Commits plain Enter before WPF can commit an unrelated highlighted popup item.
    /// An Enter used by an active IME composition remains available to the input method.
    /// </summary>
    internal void HandlePreviewKeyDown(KeyEventArgs e)
    {
        if (e.Handled || !CanCommit || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (_owner.IsTextSearchFiltering)
        {
            if (e.Key == Key.Escape || e.Key == Key.Tab)
            {
                ResetPresentation();
                e.Handled = e.Key == Key.Escape;
                return;
            }

            if (e.Key == Key.Up || e.Key == Key.Down || e.Key == Key.PageUp || e.Key == Key.PageDown)
            {
                var count = _results!.Items.Count;
                if (count > 0)
                {
                    var step = e.Key == Key.Up || e.Key == Key.PageUp ? -1 : 1;
                    if (e.Key == Key.PageUp || e.Key == Key.PageDown)
                    {
                        step *= 10;
                    }

                    var requestedIndex = _results.SelectedIndex + step;
                    var boundedIndex   = Math.Min(count - 1, requestedIndex);
                    var index          = Math.Max(0, boundedIndex);

                    _results.SelectedIndex = index;
                    _results.ScrollIntoView(_results.SelectedItem);
                }

                e.Handled = true;
                return;
            }
        }

        if (e.Key != Key.Enter)
        {
            return;
        }

        TryCommit();
        e.Handled = true;
    }

    /// <summary>
    /// Gets whether this editor can commit without interrupting composition or synchronization.
    /// </summary>
    private bool CanCommit => IsCustomSearchActive
        && _editor is not null
        && !IsComposing(_editor)
        && !GetUpdatingText(_owner)
        && !GetUpdatingSelection(_owner);

    /// <summary>
    /// Selects the best match and lets WPF display its full text. An unsuccessful explicit commit
    /// clears the previous selection while preserving the query, and reports false to the caller.
    /// </summary>
    internal bool TryCommit(int? requestedIndex = null)
    {
        if (!CanCommit)
        {
            return false;
        }

        _compositionOperation?.Abort();
        _compositionOperation  = null;
        _waitingForComposition = false;

        var query      = _editor!.Text;
        var matchIndex = FindBestMatch(query);

        if (_owner.IsTextSearchFiltering && _results?.SelectedItem is SearchCandidate candidate)
        {
            matchIndex = candidate.Index;
        }
        else if (_owner.SearchInteraction == ComboBoxSearchInteraction.AutoSelect
            && _owner.IsDropDownOpen)
        {
            var highlighted = HighlightedElement.GetValue(_owner) as DependencyObject;
            if (highlighted is not null)
            {
                matchIndex = _owner.ItemContainerGenerator.IndexFromContainer(highlighted);
            }
        }

        if (requestedIndex.HasValue)
        {
            matchIndex = requestedIndex.Value;
        }

        if (matchIndex >= 0 && matchIndex < _owner.Items.Count)
        {
            var item      = _owner.Items[matchIndex];
            var container = _owner.ItemContainerGenerator.ContainerFromIndex(matchIndex);
            if (!IsEnabledItem(item) || !IsEnabledItem(container))
            {
                matchIndex = -1;
            }
        }
        else
        {
            matchIndex = -1;
        }

        ResetPresentation();

        if (matchIndex < 0)
        {
            SetUpdatingText(_owner, true);
            try
            {
                _owner.SetCurrentValue(NativeComboBox.TextProperty, query);
                _owner.SetCurrentValue(Selector.SelectedIndexProperty, -1);
            }
            finally
            {
                SetUpdatingText(_owner, false);
            }

            return false;
        }

        // Assigning the same index raises no SelectionChanged event. Explicitly refreshing that
        // case is essential: the editor still contains the query rather than the selected label.
        if (_owner.SelectedIndex == matchIndex)
        {
            UpdateSelectedItem(_owner);
        }
        else
        {
            _owner.SetCurrentValue(Selector.SelectedIndexProperty, matchIndex);
        }

        _owner.SetCurrentValue(NativeComboBox.IsDropDownOpenProperty, false);
        return _owner.SelectedIndex == matchIndex;
    }

    /// <summary>
    /// Chooses the strongest enabled candidate. Equal ranks preserve collection order.
    /// </summary>
    private int FindBestMatch(string query)
    {
        var candidates = FindCandidates(query);
        var best       = FindBestCandidate(candidates);
        return best?.Index ?? -1;
    }

    /// <summary>
    /// Resolves enabled candidates in source order using WPF's native search-label extraction.
    /// </summary>
    private List<SearchCandidate> FindCandidates(string query)
    {
        CultureInfo culture;

        try
        {
            culture = _owner.Language.GetSpecificCulture();
        }
        catch (InvalidOperationException)
        {
            culture = CultureInfo.CurrentCulture;
        }

        var matcher = new ComboBoxTextMatcher(
            query, _owner.TextSearchMode, culture, _owner.IsTextSearchCaseSensitive
        );

        var candidates = new List<SearchCandidate>();

        for (var index = 0; index < _owner.Items.Count; index++)
        {
            var item      = _owner.Items[index];
            var container = _owner.ItemContainerGenerator.ContainerFromIndex(index);
            if (!IsEnabledItem(item) || !IsEnabledItem(container))
            {
                continue;
            }

            var text = GetItemText(_owner, item);
            var rank = matcher.GetRank(text);
            if (rank >= 0)
            {
                candidates.Add(new SearchCandidate(index, text, rank));
            }
        }

        return candidates;
    }

    /// <summary>
    /// Picks the strongest candidate, preserving source order for equal match ranks.
    /// </summary>
    private static SearchCandidate? FindBestCandidate(List<SearchCandidate> candidates)
    {
        SearchCandidate? best = null;
        foreach (var candidate in candidates)
        {
            if (best is null || candidate.Rank < best.Rank)
            {
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Holds a provisional source index and a text label without reparenting UIElement items.
    /// </summary>
    private sealed class SearchCandidate
    {
        /// <summary>
        /// Creates a candidate for the current source snapshot.
        /// </summary>
        internal SearchCandidate(int index, string text, int rank)
        {
            Index = index;
            Text  = text;
            Rank  = rank;
        }

        /// <summary>
        /// Gets the search label displayed by the candidate list.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Gets the source index used for final selection.
        /// </summary>
        internal int Index { get; }

        /// <summary>
        /// Gets the match strength, where smaller numbers are better matches.
        /// </summary>
        internal int Rank { get; }
    }

    /// <summary>
    /// Applies WPF's keyboard-selection enabled check to items and generated containers.
    /// </summary>
    private static bool IsEnabledItem(object? item)
    {
        if (item is DependencyObject dependencyObject)
        {
            return (bool)dependencyObject.GetValue(UIElement.IsEnabledProperty);
        }

        return true;
    }

    /// <summary>
    /// Resolves required WPF methods with an explicit diagnostic for unsupported runtimes.
    /// </summary>
    private static MethodInfo GetRequiredMethod(Type type, string name, BindingFlags flags)
    {
        return type.GetMethod(name, flags)
            ?? throw new NotSupportedException($"Custom ComboBox search requires {type.FullName}.{name}.");
    }

    /// <summary>
    /// Binds a native reentrancy-flag getter once, avoiding reflection invocation per keystroke.
    /// </summary>
    private static Func<NativeComboBox, bool> CreateFlagGetter(string name)
    {
        var getter = GetRequiredMethod(typeof(NativeComboBox), "get_" + name, PrivateInstance);
        return (Func<NativeComboBox, bool>)getter.CreateDelegate(typeof(Func<NativeComboBox, bool>));
    }

    /// <summary>
    /// Binds the guard shared by native selection and editable-text synchronization.
    /// </summary>
    private static Action<NativeComboBox, bool> CreateTextFlagSetter()
    {
        var setter = GetRequiredMethod(typeof(NativeComboBox), "set_UpdatingText", PrivateInstance);
        return (Action<NativeComboBox, bool>)setter.CreateDelegate(typeof(Action<NativeComboBox, bool>));
    }

    /// <summary>
    /// Reuses native label synchronization when committing the item that is already selected.
    /// </summary>
    private static Action<NativeComboBox> CreateSelectionUpdater()
    {
        var method = GetRequiredMethod(typeof(NativeComboBox), "SelectedItemUpdated", PrivateInstance);
        return (Action<NativeComboBox>)method.CreateDelegate(typeof(Action<NativeComboBox>));
    }

    /// <summary>
    /// Reuses WPF's attached text, binding-path, XML, content, and ToString fallback rules.
    /// </summary>
    private static Func<ItemsControl, object?, string> CreateItemTextReader()
    {
        var method = GetRequiredMethod(typeof(TextSearch), "GetPrimaryTextFromItem", PrivateStatic);
        return (Func<ItemsControl, object?, string>)method.CreateDelegate(
            typeof(Func<ItemsControl, object?, string>)
        );
    }

    /// <summary>
    /// Reads WPF's effective TSF composition state, including compositions without public events.
    /// </summary>
    private static Func<TextBoxBase, bool> CreateCompositionReader()
    {
        var helper = typeof(NativeComboBox).Assembly.GetType("MS.Internal.Helper", true)!;
        var method = helper.GetMethod(
            "IsComposing", PrivateStatic, null, new[]
            {
                typeof(TextBoxBase)
            }, null
        ) ?? throw new NotSupportedException("Custom ComboBox search requires WPF Helper.IsComposing.");

        return (Func<TextBoxBase, bool>)method.CreateDelegate(typeof(Func<TextBoxBase, bool>));
    }
}
