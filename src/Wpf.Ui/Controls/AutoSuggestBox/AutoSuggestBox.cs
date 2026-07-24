// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using System.Xml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Wpf.Ui.Input;
using NativeListBox = System.Windows.Controls.ListBox;
using NativeListBoxItem = System.Windows.Controls.ListBoxItem;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a text control that makes suggestions to users as they enter text using a keyboard. The app is notified when text has been changed by the user and is responsible for providing relevant suggestions for this control to display.
/// </summary>
/// <example>
/// <code lang="xml">
/// &lt;ui:AutoSuggestBox x:Name="AutoSuggestBox" PlaceholderText="Search"&gt;
///     &lt;ui:AutoSuggestBox.Icon&gt;
///         &lt;ui:IconSourceElement&gt;
///             &lt;ui:SymbolIconSource Symbol="Search24" /&gt;
///         &lt;/ui:IconSourceElement&gt;
///     &lt;/ui:AutoSuggestBox.Icon&gt;
/// &lt;/ui:AutoSuggestBox&gt;
/// </code>
/// </example>
[TemplatePart(Name = ElementTextBox, Type = typeof(TextBox))]
[TemplatePart(Name = ElementSuggestionsList, Type = typeof(NativeListBox))]
public class AutoSuggestBox : ItemsControl, IIconControl
{
    private const string ElementTextBox = "PART_TextBox";
    private const string ElementSuggestionsList = "PART_SuggestionsList";

    private static readonly DependencyProperty ItemTextValueProperty = DependencyProperty.Register(
        "ItemTextValue",
        typeof(object),
        typeof(AutoSuggestBox),
        new PropertyMetadata(null)
    );

    #region DP: OriginalItemsSource

    /// <summary>
    /// Identifies the <see cref="OriginalItemsSource"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty OriginalItemsSourceProperty = DependencyProperty.Register(
        nameof(OriginalItemsSource),
        typeof(IList),
        typeof(AutoSuggestBox),
        new PropertyMetadata(Array.Empty<object>())
    );

    /// <summary>
    /// Gets or sets your items here if you want to use the default filtering
    /// </summary>
    public IList OriginalItemsSource
    {
        get => (IList)GetValue(OriginalItemsSourceProperty);
        set => SetValue(OriginalItemsSourceProperty, value);
    }

    #endregion

    #region DP: IsSuggestionListOpen

    /// <summary>
    /// Identifies the <see cref="IsSuggestionListOpen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsSuggestionListOpenProperty = DependencyProperty.Register(
        nameof(IsSuggestionListOpen),
        typeof(bool),
        typeof(AutoSuggestBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether the drop-down portion of the <see cref="AutoSuggestBox"/> is open.
    /// </summary>
    public bool IsSuggestionListOpen
    {
        get => (bool)GetValue(IsSuggestionListOpenProperty);
        set => SetValue(IsSuggestionListOpenProperty, value);
    }

    #endregion

    #region DP: Text

    /// <summary>
    /// Identifies the <see cref="Text"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(AutoSuggestBox),
        new PropertyMetadata(string.Empty, OnTextChanged)
    );

    /// <summary>
    /// Gets or sets the text that is shown in the control.
    /// </summary>
    /// <remarks>
    /// This property is not typically set in XAML.
    /// </remarks>
    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    #endregion

    #region DP: PlaceholderText

    /// <summary>
    /// Identifies the <see cref="PlaceholderText"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PlaceholderTextProperty = DependencyProperty.Register(
        nameof(PlaceholderText),
        typeof(string),
        typeof(AutoSuggestBox),
        new PropertyMetadata(string.Empty)
    );

    /// <summary>
    /// Gets or sets the placeholder text to be displayed in the control.
    /// </summary>
    /// <remarks>
    /// The placeholder text to be displayed in the control. The default is an empty string.
    /// </remarks>
    public string PlaceholderText
    {
        get => (string)GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    #endregion

    #region DP: UpdateTextOnSelect

    /// <summary>
    /// Identifies the <see cref="UpdateTextOnSelect"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty UpdateTextOnSelectProperty = DependencyProperty.Register(
        nameof(UpdateTextOnSelect),
        typeof(bool),
        typeof(AutoSuggestBox),
        new PropertyMetadata(true)
    );

    /// <summary>
    /// Gets or sets a value indicating whether highlighting a suggestion updates the editable text part of the <see cref="AutoSuggestBox"/>.
    /// </summary>
    public bool UpdateTextOnSelect
    {
        get => (bool)GetValue(UpdateTextOnSelectProperty);
        set => SetValue(UpdateTextOnSelectProperty, value);
    }

    #endregion

    #region DP: MaxSuggestionListHeight

    /// <summary>
    /// Identifies the <see cref="MaxSuggestionListHeight"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MaxSuggestionListHeightProperty = DependencyProperty.Register(
        nameof(MaxSuggestionListHeight),
        typeof(double),
        typeof(AutoSuggestBox),
        new PropertyMetadata(0d)
    );

    /// <summary>
    /// Gets or sets the maximum height for the drop-down portion of the <see cref="AutoSuggestBox"/> control.
    /// </summary>
    public double MaxSuggestionListHeight
    {
        get => (double)GetValue(MaxSuggestionListHeightProperty);
        set => SetValue(MaxSuggestionListHeightProperty, value);
    }

    #endregion

    #region DP: Icon

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(IconElement),
        typeof(AutoSuggestBox),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets displayed <see cref="IconElement"/>.
    /// </summary>
    public IconElement? Icon
    {
        get => (IconElement?)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: FocusCommand

    /// <summary>
    /// Identifies the <see cref="FocusCommand"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusCommandProperty = DependencyProperty.Register(
        nameof(FocusCommand),
        typeof(ICommand),
        typeof(AutoSuggestBox),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets command used for focusing control.
    /// </summary>
    public ICommand FocusCommand => (ICommand)GetValue(FocusCommandProperty);

    #endregion

    #region DP: ClearButtonEnabled

    /// <summary>
    /// Identifies the <see cref="ClearButtonEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ClearButtonEnabledProperty = DependencyProperty.Register(
        nameof(ClearButtonEnabled),
        typeof(bool),
        typeof(AutoSuggestBox),
        new PropertyMetadata(false)
    );

    /// <summary>
    /// Gets or sets a value indicating whether to show the clear button when <see cref="AutoSuggestBox"/> is focused.
    /// </summary>
    public bool ClearButtonEnabled
    {
        get => (bool)GetValue(ClearButtonEnabledProperty);
        set => SetValue(ClearButtonEnabledProperty, value);
    }

    #endregion

    #region EVENT: QuerySubmitted

    /// <summary>
    /// Identifies the <see cref="QuerySubmitted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent QuerySubmittedEvent = EventManager.RegisterRoutedEvent(
        nameof(QuerySubmitted),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs>),
        typeof(AutoSuggestBox)
    );

    /// <summary>
    /// Occurs when the user submits a search query.
    /// </summary>
    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxQuerySubmittedEventArgs> QuerySubmitted
    {
        add => AddHandler(QuerySubmittedEvent, value);
        remove => RemoveHandler(QuerySubmittedEvent, value);
    }

    #endregion

    #region EVENT: SuggestionChosen

    /// <summary>Identifies the <see cref="SuggestionChosen"/> routed event.</summary>
    public static readonly RoutedEvent SuggestionChosenEvent = EventManager.RegisterRoutedEvent(
        nameof(SuggestionChosen),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<AutoSuggestBox, AutoSuggestBoxSuggestionChosenEventArgs>),
        typeof(AutoSuggestBox)
    );

    /// <summary>
    /// Occurs when the user commits a suggestion by pressing Enter or clicking it.
    /// </summary>
    /// <remarks>
    /// The event is raised before the control copies the committed item to <see cref="Text"/>. Set
    /// <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> to take responsibility for the
    /// text and suggestion source and suppress the control's automatic update.
    /// </remarks>
    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxSuggestionChosenEventArgs> SuggestionChosen
    {
        add => AddHandler(SuggestionChosenEvent, value);
        remove => RemoveHandler(SuggestionChosenEvent, value);
    }

    #endregion

    #region EVENT: SuggestionHighlighted

    /// <summary>
    /// Identifies the <see cref="SuggestionHighlighted"/> routed event.
    /// </summary>
    public static readonly RoutedEvent SuggestionHighlightedEvent = EventManager.RegisterRoutedEvent(
        nameof(SuggestionHighlighted),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<AutoSuggestBox, AutoSuggestBoxSuggestionHighlightedEventArgs>),
        typeof(AutoSuggestBox)
    );

    /// <summary>
    /// Occurs when keyboard or pointer navigation highlights a suggestion without committing it.
    /// </summary>
    /// <remarks>
    /// Set <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> to prevent the highlighted
    /// item from being copied to <see cref="Text"/> when <see cref="UpdateTextOnSelect"/> is enabled.
    /// </remarks>
    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxSuggestionHighlightedEventArgs> SuggestionHighlighted
    {
        add => AddHandler(SuggestionHighlightedEvent, value);
        remove => RemoveHandler(SuggestionHighlightedEvent, value);
    }

    #endregion

    #region EVENT: TextChanged

    /// <summary>Identifies the <see cref="TextChanged"/> routed event.</summary>
    public static readonly RoutedEvent TextChangedEvent = EventManager.RegisterRoutedEvent(
        nameof(TextChanged),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs>),
        typeof(AutoSuggestBox)
    );

    /// <summary>
    /// Occurs whenever the value of <see cref="Text"/> changes.
    /// </summary>
    /// <remarks>
    /// The event does not classify changes by their source. User input, a binding or another
    /// programmatic assignment, and suggestion preview or commitment are all ordinary text changes.
    /// Set <see cref="RoutedEventArgs.Handled"/> to <see langword="true"/> when supplying a custom
    /// suggestion source to suppress the built-in filtering pass for that change.
    /// </remarks>
    public event TypedEventHandler<AutoSuggestBox, AutoSuggestBoxTextChangedEventArgs> TextChanged
    {
        add => AddHandler(TextChangedEvent, value);
        remove => RemoveHandler(TextChangedEvent, value);
    }

    #endregion

    static AutoSuggestBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(AutoSuggestBox),
            new FrameworkPropertyMetadata(typeof(AutoSuggestBox))
        );
    }

    private TextBox? _textBox;
    private NativeListBox? _suggestionsList;

    private bool _isSynchronizingTextBox;
    private bool _isPreviewingSuggestion;
    private bool _hasSuggestionPreview;
    private bool _wasPreviewTextChangeHandled;
    private string _textBeforeSuggestionPreview = string.Empty;
    private HwndSource? _hookSource;
    private RECT? _hostWindowBounds;
    private bool _openSuggestionsOnMouseLeftButtonUp;
    private DispatcherOperation? _deferredSuggestionOpenOperation;

    public AutoSuggestBox()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        SetValue(FocusCommandProperty, new RelayCommand<object>(_ => Focus()));
    }

    public override void OnApplyTemplate()
    {
        ReleaseTemplateResources();

        base.OnApplyTemplate();

        _textBox = EnsureTemplateChild<TextBox>(ElementTextBox);
        _suggestionsList = EnsureTemplateChild<NativeListBox>(ElementSuggestionsList);

        AcquireTemplateResources();
    }

    protected override void OnGotFocus(RoutedEventArgs e)
    {
        base.OnGotFocus(e);
        _textBox?.Focus();
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        _textBox?.Focus();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        AddWindowHook();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelDeferredSuggestionOpen();
        RemoveWindowHook();
    }

    /// <inheritdoc cref="UIElement.Focus" />
    public new bool Focus()
    {
        return _textBox is not null && _textBox.Focus();
    }

    private T EnsureTemplateChild<T>(string name)
        where T : DependencyObject
    {
        return GetTemplateChild(name) as T ?? throw new ArgumentNullException(name);
    }

    /// <summary>
    /// Subscribes event handlers to the resolved template parts (<see cref="_textBox"/> and <see cref="_suggestionsList"/>).
    /// Each handler is unsubscribed first to prevent double subscription.
    /// </summary>
    private void AcquireTemplateResources()
    {
        if (_textBox != null)
        {
            _textBox.PreviewKeyDown += TextBoxOnPreviewKeyDown;
            _textBox.PreviewMouseLeftButtonDown += TextBoxOnPreviewMouseLeftButtonDown;
            _textBox.AddHandler(
                Mouse.MouseUpEvent,
                new MouseButtonEventHandler(TextBoxOnMouseUp),
                handledEventsToo: true
            );
            _textBox.TextChanged += TextBoxOnTextChanged;
            _textBox.GotKeyboardFocus += TextBoxOnGotKeyboardFocus;
            _textBox.LostKeyboardFocus += TextBoxOnLostKeyboardFocus;
        }

        if (_suggestionsList != null)
        {
            _suggestionsList.SelectionChanged += SuggestionsListOnSelectionChanged;
            _suggestionsList.PreviewKeyDown += SuggestionsListOnPreviewKeyDown;
            _suggestionsList.LostKeyboardFocus += SuggestionsListOnLostKeyboardFocus;
            _suggestionsList.PreviewMouseLeftButtonUp += SuggestionsListOnPreviewMouseLeftButtonUp;
        }
    }

    /// <summary>
    /// Unsubscribes event handlers from the resolved template parts.
    /// </summary>
    private void ReleaseTemplateResources()
    {
        if (_textBox != null)
        {
            _textBox.PreviewKeyDown -= TextBoxOnPreviewKeyDown;
            _textBox.PreviewMouseLeftButtonDown -= TextBoxOnPreviewMouseLeftButtonDown;
            _textBox.RemoveHandler(Mouse.MouseUpEvent, new MouseButtonEventHandler(TextBoxOnMouseUp));
            _textBox.TextChanged -= TextBoxOnTextChanged;
            _textBox.GotKeyboardFocus -= TextBoxOnGotKeyboardFocus;
            _textBox.LostKeyboardFocus -= TextBoxOnLostKeyboardFocus;
        }

        _openSuggestionsOnMouseLeftButtonUp = false;
        CancelDeferredSuggestionOpen();

        if (_suggestionsList != null)
        {
            _suggestionsList.SelectionChanged -= SuggestionsListOnSelectionChanged;
            _suggestionsList.PreviewKeyDown -= SuggestionsListOnPreviewKeyDown;
            _suggestionsList.LostKeyboardFocus -= SuggestionsListOnLostKeyboardFocus;
            _suggestionsList.PreviewMouseLeftButtonUp -= SuggestionsListOnPreviewMouseLeftButtonUp;
        }
    }

    /// <summary>
    /// Installs the <see cref="HwndSource"/> hook for tracking window activation and movement.
    /// Must be called after the control is in the visual tree (<see cref="FrameworkElement.Loaded"/>).
    /// </summary>
    private void AddWindowHook()
    {
        if (_hookSource is null && PresentationSource.FromVisual(this) is HwndSource source)
        {
            source.AddHook(Hook);
            _hookSource = source;
            _hostWindowBounds = TryGetWindowBounds(source.Handle, out var bounds) ? bounds : null;
        }
    }

    /// <summary>
    /// Removes the <see cref="HwndSource"/> hook installed by <see cref="AddWindowHook"/>.
    /// </summary>
    private void RemoveWindowHook()
    {
        _hookSource?.RemoveHook(Hook);
        _hookSource = null;

        _hostWindowBounds = null;
    }

    /// <summary>
    /// Method for <see cref="QuerySubmitted"/>.
    /// </summary>
    /// <param name="queryText">Currently submitted query text.</param>
    protected virtual void OnQuerySubmitted(string queryText)
    {
        var args = new AutoSuggestBoxQuerySubmittedEventArgs(QuerySubmittedEvent, this)
        {
            QueryText = queryText,
        };

        RaiseEvent(args);
    }

    /// <summary>
    /// Method for <see cref="SuggestionChosen"/>.
    /// </summary>
    /// <param name="selectedItem">Currently selected item.</param>
    protected virtual void OnSuggestionChosen(object selectedItem)
    {
        var hadSuggestionPreview = _hasSuggestionPreview;
        var textBeforeSuggestionPreview = _textBeforeSuggestionPreview;
        var args = new AutoSuggestBoxSuggestionChosenEventArgs(SuggestionChosenEvent, this)
        {
            SelectedItem = selectedItem,
        };

        RaiseEvent(args);

        if (UpdateTextOnSelect && !args.Handled)
        {
            var textChanged = UpdateText(selectedItem);

            // Highlighting may already have copied the selected item into Text while intentionally
            // retaining the broader result set for continued keyboard navigation. Committing that
            // same item therefore produces no second Text change, but it still has to finish the
            // deferred filtering pass against the query that existed before preview began.
            if (
                !textChanged
                && hadSuggestionPreview
                && textBeforeSuggestionPreview != Text
                && !_wasPreviewTextChangeHandled
            )
            {
                DefaultFiltering(Text);
            }
        }

        ResetSuggestionPreview();
    }

    /// <summary>
    /// Raises <see cref="SuggestionHighlighted"/> for a suggestion that is being previewed.
    /// </summary>
    /// <param name="highlightedItem">The suggestion highlighted by keyboard or pointer navigation.</param>
    protected virtual void OnSuggestionHighlighted(object highlightedItem)
    {
        var args = new AutoSuggestBoxSuggestionHighlightedEventArgs(SuggestionHighlightedEvent, this)
        {
            HighlightedItem = highlightedItem,
        };

        RaiseEvent(args);

        if (!UpdateTextOnSelect || args.Handled)
        {
            return;
        }

        if (!_hasSuggestionPreview)
        {
            _hasSuggestionPreview = true;
            _textBeforeSuggestionPreview = Text;
        }

        _isPreviewingSuggestion = true;

        try
        {
            _ = UpdateText(highlightedItem);
        }
        finally
        {
            _isPreviewingSuggestion = false;
        }
    }

    /// <summary>
    /// Raises <see cref="TextChanged"/> and applies the built-in filter when the event is not handled.
    /// </summary>
    /// <param name="text">The new value of <see cref="Text"/>.</param>
    protected virtual void OnTextChanged(string text)
    {
        var args = new AutoSuggestBoxTextChangedEventArgs(TextChangedEvent, this)
        {
            Text = text,
        };

        RaiseEvent(args);

        if (_isPreviewingSuggestion)
        {
            // A preview must not collapse the list while the user is navigating it. Remember whether
            // the application handled this change so committing an unchanged preview does not replace
            // an application-provided ItemsSource with the built-in filter.
            _wasPreviewTextChangeHandled = args.Handled;
            return;
        }

        if (!args.Handled)
        {
            DefaultFiltering(text);
        }
    }

    private void TextBoxOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        OnTextBoxPreviewKeyDown(e);
    }

    /// <summary>
    /// Handles keyboard input originating from the editable text box.
    /// </summary>
    /// <param name="e">The keyboard event data.</param>
    protected virtual void OnTextBoxPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            CloseSuggestions();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter)
        {
            var text = _textBox?.Text;

            if (IsSuggestionListOpen && _suggestionsList?.SelectedItem is { } selectedItem)
            {
                CommitSuggestion(selectedItem);
            }
            else if (text is not null && !string.IsNullOrWhiteSpace(text))
            {
                CloseSuggestions();
                OnQuerySubmitted(text);
            }

            e.Handled = true;
            return;
        }

        if (e.Key is Key.Down && IsSuggestionListOpen && _suggestionsList is { HasItems: true })
        {
            _ = _suggestionsList.Focus();

            if (_suggestionsList.SelectedIndex < 0)
            {
                _suggestionsList.SetCurrentValue(Selector.SelectedIndexProperty, 0);
            }

            e.Handled = true;
        }
    }

    private void TextBoxOnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        // Losing keyboard focus closes the non-sticky Popup, but that close does not invalidate
        // the filtered Items view. Reopen it when the editor is focused again so the user can
        // continue with the same query without first changing the text.
        //
        // HasItems is intentionally checked in addition to the query. It reflects both the built-in
        // filtering path and an ItemsSource supplied by a TextChanged handler, and prevents an empty
        // result Popup from being restored. An empty query is excluded even though DefaultFiltering
        // exposes OriginalItemsSource for it: showing that full, unfiltered list merely because focus
        // returned is not a suggestion-restoration scenario.
        if (!HasSuggestionsToRestore())
        {
            return;
        }

        if (_openSuggestionsOnMouseLeftButtonUp)
        {
            return;
        }

        // When an inactive owner window is clicked, WPF can restore keyboard focus while processing
        // window activation, before the TextBox receives PreviewMouseDown. Opening a non-sticky Popup
        // in that focus callback captures the activating click and prevents TextEditorMouse from
        // receiving its normal down/up pair. A null old focus identifies this restoration path. Give
        // the input queue one idle turn so the subsequent mouse event can claim the deferred open.
        if (e.OldFocus is null)
        {
            ScheduleDeferredSuggestionOpen();
            return;
        }

        // Keyboard navigation and ordinary programmatic focus have no unfinished pointer input, so
        // they retain the immediate behavior expected by the control contract.
        OpenSuggestionsIfAvailable();
    }

    private void TextBoxOnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (HasSuggestionsToRestore())
        {
            // This also covers the first click after owner-window activation: keyboard focus may
            // already have been restored, but the low-priority focus operation has not opened the
            // Popup yet. It is intentionally recorded even when the Popup is currently open because
            // StaysOpen=false may close it for this placement-target click before MouseUp. The
            // bubbling MouseUp handler will run after TextBoxBase releases capture and restore it.
            CancelDeferredSuggestionOpen();
            _openSuggestionsOnMouseLeftButtonUp = true;
        }
    }

    private void TextBoxOnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton is not MouseButton.Left || !_openSuggestionsOnMouseLeftButtonUp)
        {
            return;
        }

        _openSuggestionsOnMouseLeftButtonUp = false;

        // TextEditorMouse handles MouseUp and releases its selection capture before instance
        // handlers registered with handledEventsToo run. Do not open until that native cleanup is
        // observable; otherwise Popup.StaysOpen=false steals capture and leaves caret/selection input
        // in a partially completed state.
        if (_textBox?.IsMouseCaptured == true || Mouse.LeftButton is MouseButtonState.Pressed)
        {
            ScheduleDeferredSuggestionOpen();
            return;
        }

        OpenSuggestionsIfAvailable();
    }

    private void TextBoxOnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsFocusWithinSuggestions(e.NewFocus))
        {
            return;
        }

        _openSuggestionsOnMouseLeftButtonUp = false;
        CancelDeferredSuggestionOpen();
        CloseSuggestions();
    }

    private void TextBoxOnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isSynchronizingTextBox)
        {
            return;
        }

        // A change originating in the editor is the start of a new query, even when the template's
        // TwoWay binding has already copied the value to Text before this handler runs. Explicitly
        // assigning with SetCurrentValue also keeps custom templates functional without replacing a
        // consumer's Text binding.
        ResetSuggestionPreview();
        ClearSuggestionSelection();
        SetCurrentValue(TextProperty, _textBox!.Text);

        if (!IsLoaded)
        {
            // The TextBox binding can raise TextChanged while the control template is still being initialized.
            // WPF coerces Popup.IsOpen to false until a Popup in the visual tree is loaded, while the source
            // IsSuggestionListOpen value would remain true. The first real keystroke would then write true again
            // without changing the source property, so the template binding would not retry opening the Popup.
            // A user cannot interact with this TextBox before it is loaded; ignoring only that initialization-time
            // request keeps the source and Popup states aligned and lets the first user input open the list even
            // when filtering produces no suggestions.
            return;
        }

        SetCurrentValue(IsSuggestionListOpenProperty, true);
    }

    private void SuggestionsListOnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (IsFocusWithinControl(e.NewFocus))
        {
            return;
        }

        CloseSuggestions();
    }

    private void SuggestionsListOnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        OnSuggestionsListPreviewKeyDown(e);
    }

    /// <summary>
    /// Handles keyboard input originating from the suggestions list.
    /// </summary>
    /// <param name="e">The keyboard event data.</param>
    protected virtual void OnSuggestionsListPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
        {
            _ = _textBox?.Focus();
            CloseSuggestions();
            e.Handled = true;
            return;
        }

        if (e.Key is Key.Enter && _suggestionsList?.SelectedItem is { } selectedItem)
        {
            CommitSuggestion(selectedItem);
            e.Handled = true;
        }
    }

    private void SuggestionsListOnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (
            _suggestionsList is null
            || e.OriginalSource is not DependencyObject source
            || ContainerFromElement(_suggestionsList, source) is not NativeListBoxItem container
        )
        {
            return;
        }

        var selectedItem = _suggestionsList.ItemContainerGenerator.ItemFromContainer(container);

        if (ReferenceEquals(selectedItem, DependencyProperty.UnsetValue))
        {
            return;
        }

        _suggestionsList.SetCurrentValue(Selector.SelectedItemProperty, selectedItem);
        CommitSuggestion(selectedItem);
        e.Handled = true;
    }

    private void SuggestionsListOnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suggestionsList!.SelectedItem is null)
        {
            return;
        }

        PreviewSuggestion(_suggestionsList.SelectedItem);
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
    {
        var message = (uint)msg;

        if (message == PInvoke.WM_NCACTIVATE)
        {
            // WM_NCACTIVATE is sent for both activation and deactivation. The previous implementation
            // closed on both values, so a delayed activation notification during cold startup could
            // immediately undo the first IsSuggestionListOpen=true transition.
            var isDeactivating = wparam == IntPtr.Zero;

            if (isDeactivating && IsSuggestionListOpen)
            {
                SetCurrentValue(IsSuggestionListOpenProperty, false);
            }

            return IntPtr.Zero;
        }

        if (message == PInvoke.WM_WINDOWPOSCHANGED)
        {
            var previousBounds = _hostWindowBounds;
            var hasBounds = TryGetWindowBounds(hwnd, out var currentBounds);
            var boundsChanged =
                hasBounds && previousBounds.HasValue && !AreSameBounds(previousBounds.Value, currentBounds);

            if (hasBounds)
            {
                _hostWindowBounds = currentBounds;
            }

            // Popup does not automatically follow its owner while the HWND is moving. Preserve the
            // original close-on-move behavior, but ignore z-order/activation notifications whose
            // WM_WINDOWPOSCHANGED payload leaves the owner's actual bounds unchanged.
            if (boundsChanged && IsSuggestionListOpen)
            {
                SetCurrentValue(IsSuggestionListOpenProperty, false);
            }
        }

        return IntPtr.Zero;
    }

    private static bool TryGetWindowBounds(IntPtr hwnd, out RECT bounds)
    {
        if (hwnd == IntPtr.Zero)
        {
            bounds = default;
            return false;
        }

        return PInvoke.GetWindowRect((HWND)hwnd, out bounds);
    }

    private static bool AreSameBounds(RECT left, RECT right)
    {
        return left.left == right.left
            && left.top == right.top
            && left.right == right.right
            && left.bottom == right.bottom;
    }

    private void PreviewSuggestion(object selectedItem)
    {
        OnSuggestionHighlighted(selectedItem);
    }

    private void CommitSuggestion(object selectedItem)
    {
        _ = _textBox?.Focus();
        CloseSuggestions();
        OnSuggestionChosen(selectedItem);
    }

    private void CloseSuggestions()
    {
        SetCurrentValue(IsSuggestionListOpenProperty, false);
    }

    private void OpenSuggestionsIfAvailable()
    {
        if (_textBox?.IsKeyboardFocusWithin == true && HasSuggestionsToRestore())
        {
            SetCurrentValue(IsSuggestionListOpenProperty, true);
        }
    }

    private void ScheduleDeferredSuggestionOpen()
    {
        if (_deferredSuggestionOpenOperation?.Status is DispatcherOperationStatus.Pending)
        {
            return;
        }

        // ContextIdle runs after queued input without delaying keyboard/programmatic focus for a
        // visible amount of time. If the mouse is still held at that point, MouseUp becomes the only
        // safe completion signal because a non-sticky Popup must not compete with TextEditor capture.
        _deferredSuggestionOpenOperation = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                _deferredSuggestionOpenOperation = null;

                if (_textBox?.IsKeyboardFocusWithin != true || !HasSuggestionsToRestore())
                {
                    return;
                }

                if (_textBox.IsMouseCaptured || Mouse.LeftButton is MouseButtonState.Pressed)
                {
                    _openSuggestionsOnMouseLeftButtonUp = true;
                    return;
                }

                OpenSuggestionsIfAvailable();
            })
        );
    }

    private void CancelDeferredSuggestionOpen()
    {
        if (_deferredSuggestionOpenOperation?.Status is DispatcherOperationStatus.Pending)
        {
            _deferredSuggestionOpenOperation.Abort();
        }

        _deferredSuggestionOpenOperation = null;
    }

    private bool HasSuggestionsToRestore()
    {
        // HasItems covers both the built-in filter and ItemsSource values populated by a
        // TextChanged handler. An empty query still represents the unfiltered source rather than a
        // previously produced suggestion set, so focus alone must not expose the complete list.
        return !string.IsNullOrEmpty(_textBox?.Text) && HasItems;
    }

    private void ClearSuggestionSelection()
    {
        _suggestionsList?.SetCurrentValue(Selector.SelectedItemProperty, null);
    }

    private bool UpdateText(object selectedItem)
    {
        var newText = GetStringFromObj(selectedItem) ?? string.Empty;

        if (Text == newText)
        {
            return false;
        }

        SetCurrentValue(TextProperty, newText);
        return true;
    }

    private void SynchronizeTextBox(string text)
    {
        if (_textBox is null || _textBox.Text == text)
        {
            return;
        }

        _isSynchronizingTextBox = true;

        try
        {
            _textBox.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, text);
        }
        finally
        {
            _isSynchronizingTextBox = false;
        }
    }

    /// <summary>
    /// Ends suggestion preview without changing the displayed text. The committed text is retained;
    /// only the query snapshot used to decide whether filtering was deferred is discarded.
    /// </summary>
    private void ResetSuggestionPreview()
    {
        _hasSuggestionPreview = false;
        _wasPreviewTextChangeHandled = false;
        _textBeforeSuggestionPreview = string.Empty;
    }

    private bool IsFocusWithinControl(IInputElement? element)
    {
        return IsFocusWithin(_textBox, element) || IsFocusWithin(_suggestionsList, element);
    }

    private bool IsFocusWithinSuggestions(IInputElement? element)
    {
        return IsFocusWithin(_suggestionsList, element);
    }

    private static bool IsFocusWithin(DependencyObject? root, IInputElement? element)
    {
        return root is Visual visual
            && element is DependencyObject dependencyObject
            && (ReferenceEquals(root, dependencyObject) || visual.IsAncestorOf(dependencyObject));
    }

    private void DefaultFiltering(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            SetCurrentValue(ItemsSourceProperty, OriginalItemsSource);
            return;
        }

        var splitText = text.Split(' ');
        var suitableItems = OriginalItemsSource
            .Cast<object>()
            .Where(item =>
            {
                var itemText = GetStringFromObj(item);
                return splitText.All(key =>
                    itemText?.Contains(key, StringComparison.OrdinalIgnoreCase) ?? false
                );
            })
            .ToList();

        SetCurrentValue(ItemsSourceProperty, suitableItems);
    }

    private string GetStringFromObj(object? obj)
    {
        if (obj is null)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(DisplayMemberPath))
        {
            return obj.ToString() ?? string.Empty;
        }

        var binding = new Binding { Mode = BindingMode.OneWay, Source = obj };

        // WPF treats DisplayMemberPath as an XPath for XML items and as a PropertyPath
        // for CLR items. This mirrors TextSearch.CreateBindingExpression, so filtering
        // and selection use the same member resolution rules as the suggestions list.
        if (obj is XmlNode)
        {
            binding.XPath = DisplayMemberPath;
            binding.Path = new PropertyPath("/InnerText");
        }
        else
        {
            binding.Path = new PropertyPath(DisplayMemberPath);
        }

        try
        {
            _ = BindingOperations.SetBinding(this, ItemTextValueProperty, binding);

            var value = GetValue(ItemTextValueProperty);
            return value == DependencyProperty.UnsetValue ? string.Empty : value?.ToString() ?? string.Empty;
        }
        finally
        {
            BindingOperations.ClearBinding(this, ItemTextValueProperty);
        }
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (AutoSuggestBox)d;
        var newText = (string)e.NewValue;

        if (!self._isPreviewingSuggestion)
        {
            self.ResetSuggestionPreview();
        }

        self.SynchronizeTextBox(newText);
        self.OnTextChanged(newText);
    }
}
