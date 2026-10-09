// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Extends <see cref="System.Windows.Controls.ComboBox"/> with editable text-search modes,
/// interaction-state brushes, and a control-owned <see cref="CornerRadius"/>.
/// </summary>
/// <remarks>
/// <para>
/// The normal appearance continues to use <see cref="Control.Background"/>,
/// <see cref="Control.BorderBrush"/>, and <see cref="Control.Foreground"/>. State-specific
/// brushes are optional; a <see langword="null"/> state brush falls back to the corresponding
/// normal brush.
/// </para>
/// <para>
/// Editable combo boxes use the focused brushes while keyboard focus is within the embedded
/// editor. Non-editable combo boxes use the drop-down-open brushes while their popup is open.
/// <see cref="System.Windows.Controls.ComboBox.IsReadOnly"/> only affects the editable editor,
/// so a read-only editable combo box intentionally remains in the editable focus-state path.
/// </para>
/// </remarks>
public class ComboBox : System.Windows.Controls.ComboBox
{
    private System.Windows.Controls.TextBox? _searchEditor;
    private ComboBoxSearchController?        _searchController;

    /// <summary>
    /// Identifies the <see cref="TextSearchMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TextSearchModeProperty = DependencyProperty.Register(
        nameof(TextSearchMode),
        typeof(ComboBoxTextSearchMode),
        typeof(ComboBox),
        new PropertyMetadata(ComboBoxTextSearchMode.Prefix, OnTextSearchModeChanged),
        IsValidTextSearchMode
    );

    /// <summary>
    /// Gets or sets the matching rule. The default is native prefix completion while typing.
    /// Custom modes require IsEditable and IsTextSearchEnabled to be true and IsReadOnly false.
    /// </summary>
    /// <remarks>
    /// SearchInteraction determines live feedback and selection timing. Enter or
    /// TryCommitTextSearch confirms a candidate and displays its full text.
    /// Exact, prefix, substring, token, and subsequence matches have descending priority; ties use
    /// item order. Committing empty input or no match clears selection and retains the input.
    /// Candidate text follows
    /// TextSearch.Text, TextSearch.TextPath, and DisplayMemberPath, using WPF's fallback rules.
    /// Programmatic Text changes retain WPF's exact-match behavior. Programmatic selection and
    /// native item navigation still display the selected item's full text.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public ComboBoxTextSearchMode TextSearchMode
    {
        get => (ComboBoxTextSearchMode)GetValue(TextSearchModeProperty);
        set => SetValue(TextSearchModeProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="SearchInteraction"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SearchInteractionProperty = DependencyProperty.Register(
        nameof(SearchInteraction), typeof(ComboBoxSearchInteraction), typeof(ComboBox),
        new PropertyMetadata(ComboBoxSearchInteraction.ExplicitCommit, OnTextSearchModeChanged),
        IsValidSearchInteraction
    );

    /// <summary>
    /// Gets or sets how custom search presents and commits matches. Defaults to ExplicitCommit.
    /// This setting applies to Contains, Tokens, and Fuzzy; Prefix retains native behavior.
    /// </summary>
    /// <remarks>
    /// Filter displays matching native containers in the normal items presenter, retaining item
    /// styles, templates, data contexts, source indices, and committed selection. ItemsSource and
    /// its shared view remain intact. A virtualizing items panel realizes only visible matches.
    /// Custom templates must use an ItemsPresenter and bind their scroll viewer's CanContentScroll
    /// to ScrollViewer.CanContentScroll on this control.
    /// Escape or dismissal keeps the query and does not commit a provisional candidate.
    /// </remarks>
    public ComboBoxSearchInteraction SearchInteraction
    {
        get => (ComboBoxSearchInteraction)GetValue(SearchInteractionProperty);
        set => SetValue(SearchInteractionProperty, value);
    }

    private static readonly DependencyPropertyKey IsTextSearchFilteringPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsTextSearchFiltering), typeof(bool), typeof(ComboBox), new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the read-only <see cref="IsTextSearchFiltering"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsTextSearchFilteringProperty =
        IsTextSearchFilteringPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the popup currently presents provisional search results. Defaults to false.
    /// Matching items use the ComboBox's native container generator and item presentation.
    /// </summary>
    public bool IsTextSearchFiltering => (bool)GetValue(IsTextSearchFilteringProperty);

    /// <summary>
    /// Updates the template's provisional candidate presentation state.
    /// </summary>
    internal void SetSearchFiltering(bool value)
    {
        if (!value)
        {
            SearchPanel = null;
        }

        SetValue(IsTextSearchFilteringPropertyKey, value);
        CoerceValue(GroupStyleSelectorProperty);
        CoerceValue(ItemsPanelProperty);
        CoerceValue(ScrollViewer.CanContentScrollProperty);
    }

    /// <summary>
    /// Validates the supported interaction choices.
    /// </summary>
    private static bool IsValidSearchInteraction(object value)
    {
        return value is ComboBoxSearchInteraction.ExplicitCommit
            or ComboBoxSearchInteraction.AutoSelect or ComboBoxSearchInteraction.Filter;
    }

    /// <summary>
    /// Ends a pending presentation when editing or search becomes unavailable.
    /// </summary>
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == TextProperty || e.Property == SelectedItemProperty)
        {
            _searchController?.OnOwnerValueChanged();
        }

        if (e.Property == IsEditableProperty || e.Property == IsReadOnlyProperty
            || e.Property == IsEnabledProperty || e.Property == IsTextSearchEnabledProperty
            || e.Property == IsTextSearchCaseSensitiveProperty || e.Property == LanguageProperty
            || e.Property == DisplayMemberPathProperty || e.Property == TextSearch.TextPathProperty)
        {
            _searchController?.ResetPresentation();
        }
    }

    /// <summary>
    /// Handles search confirmation and provisional navigation before native ComboBox navigation.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        _searchController?.HandlePreviewKeyDown(e);
        if (!e.Handled)
        {
            base.OnPreviewKeyDown(e);
        }
    }

    /// <summary>
    /// Invalidates candidate indices when the source collection changes.
    /// </summary>
    protected override void OnItemsChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        base.OnItemsChanged(e);
        _searchController?.ResetPresentation();
    }

    /// <summary>
    /// Applies native item styles and templates while preserving an active editable query.
    /// </summary>
    protected override void PrepareContainerForItemOverride(DependencyObject? element, object? item)
    {
        var controller = _searchController;
        var previous   = controller?.BeginContainerUpdate() ?? false;
        try
        {
            base.PrepareContainerForItemOverride(element, item);
        }
        finally
        {
            controller?.EndContainerUpdate(previous);
        }
    }

    /// <summary>
    /// Prevents container virtualization from restoring the selected label over the query.
    /// </summary>
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        var controller = _searchController;
        var previous   = controller?.BeginContainerUpdate() ?? false;
        try
        {
            base.ClearContainerForItemOverride(element, item);
        }
        finally
        {
            controller?.EndContainerUpdate(previous);
        }
    }

    /// <summary>
    /// Confirms the highlighted live candidate, or matches the current text when no candidate
    /// presentation is active, and fills its full label. Suitable for application-defined triggers.
    /// </summary>
    /// <returns>
    /// True when a candidate was selected; false for no match, an active IME composition,
    /// unavailable editor, disabled/read-only control, disabled search, or native prefix mode.
    /// </returns>
    /// <remarks>
    /// A successful commit closes the drop-down. No match clears selection but retains input.
    /// Calls during composition or unavailable search leave text and selection unchanged.
    /// This method runs on the control's dispatcher thread and applies its template if needed.
    /// </remarks>
    public bool TryCommitTextSearch()
    {
        VerifyAccess();
        ApplyTemplate();
        return _searchController?.TryCommit() ?? false;
    }

    /// <summary>
    /// Reconnects editable search to the text box supplied by the current control template.
    /// </summary>
    public override void OnApplyTemplate()
    {
        _searchController?.Detach(false);
        base.OnApplyTemplate();
        _searchEditor = GetTemplateChild("PART_EditableTextBox") as System.Windows.Controls.TextBox;
        UpdateSearchController();
    }

    /// <summary>
    /// Rejects undefined search modes before they become effective dependency-property values.
    /// </summary>
    private static bool IsValidTextSearchMode(object value)
    {
        return value is ComboBoxTextSearchMode.Prefix
            or ComboBoxTextSearchMode.Contains
            or ComboBoxTextSearchMode.Tokens
            or ComboBoxTextSearchMode.Fuzzy;
    }

    /// <summary>
    /// Switches the editor between native completion and custom selection as settings change.
    /// </summary>
    private static void OnTextSearchModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var comboBox = (ComboBox)d;
        comboBox._searchController?.ResetPresentation();
        comboBox.UpdateSearchController();
    }

    /// <summary>
    /// Installs custom search only when requested, restoring WPF's handler for prefix mode.
    /// </summary>
    private void UpdateSearchController()
    {
        if (TextSearchMode == ComboBoxTextSearchMode.Prefix)
        {
            _searchController?.Detach(true);
            return;
        }

        if (_searchEditor is not null)
        {
            _searchController ??= new ComboBoxSearchController(this);
            _searchController.Attach(_searchEditor);
        }
    }

    /// <summary>
    /// Gets the active filtered items host after it connects to the native generator.
    /// </summary>
    internal ComboBoxSearchPanel? SearchPanel { get; set; }

    /// <summary>
    /// Gets the ordered source indices displayed by the active search.
    /// </summary>
    internal IReadOnlyList<int> SearchIndices
    {
        get => _searchController?.CandidateIndices ?? Array.Empty<int>();
    }

    /// <summary>
    /// Uses native container generation through the filtered virtualizing panel during search.
    /// The application's items-panel value and binding resume when filtering ends.
    /// </summary>
    private static object CoerceSearchItemsPanel(DependencyObject d, object baseValue)
    {
        var owner = (ComboBox)d;
        return owner.IsTextSearchFiltering ? ComboBoxSearchPanel.PanelTemplate : baseValue;
    }

    /// <summary>
    /// Connects the popup scroll viewer to the filtering panel's virtualized scrolling.
    /// </summary>
    private static object CoerceSearchScrolling(DependencyObject d, object baseValue)
    {
        var owner = (ComboBox)d;
        return owner.IsTextSearchFiltering ? true : baseValue;
    }

    /// <summary>
    /// Uses a flat, virtualized result presentation when the source view contains groups.
    /// Group definitions and the application's group styles resume after filtering ends.
    /// </summary>
    private static object CoerceSearchGroupStyle(DependencyObject d, object baseValue)
    {
        var owner = (ComboBox)d;
        return owner.IsTextSearchFiltering ? ComboBoxSearchPanel.GroupStyleSelector : baseValue;
    }

    #region DP: Icon

    /// <summary>
    /// Identifies the <see cref="Icon"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon),
        typeof(object),
        typeof(ComboBox),
        new PropertyMetadata(null)
    );

    /// <summary>
    /// Gets or sets the content displayed in the icon slot.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    #endregion

    #region DP: IconMargin

    /// <summary>
    /// Identifies the <see cref="IconMargin"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IconMarginProperty = DependencyProperty.Register(
        nameof(IconMargin),
        typeof(Thickness),
        typeof(ComboBox),
        new PropertyMetadata(default(Thickness))
    );

    /// <summary>
    /// Gets or sets the external margin of the icon slot. The visual default comes from the
    /// control's style, so a locally supplied value takes precedence over it.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Thickness IconMargin
    {
        get => (Thickness)GetValue(IconMarginProperty);
        set => SetValue(IconMarginProperty, value);
    }

    #endregion

    #region Interaction appearance

    /// <summary>
    /// Identifies the <see cref="MouseOverForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverForegroundProperty =
        DependencyProperty.Register(
            nameof(MouseOverForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverForeground
    {
        get => (Brush?)GetValue(MouseOverForegroundProperty);
        set => SetValue(MouseOverForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MouseOverBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverBackgroundProperty =
        DependencyProperty.Register(
            nameof(MouseOverBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBackground
    {
        get => (Brush?)GetValue(MouseOverBackgroundProperty);
        set => SetValue(MouseOverBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="MouseOverBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty MouseOverBorderBrushProperty =
        DependencyProperty.Register(
            nameof(MouseOverBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the pointer is over the closed combo box.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? MouseOverBorderBrush
    {
        get => (Brush?)GetValue(MouseOverBorderBrushProperty);
        set => SetValue(MouseOverBorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedForegroundProperty =
        DependencyProperty.Register(
            nameof(FocusedForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedForeground
    {
        get => (Brush?)GetValue(FocusedForegroundProperty);
        set => SetValue(FocusedForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedBackgroundProperty =
        DependencyProperty.Register(
            nameof(FocusedBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBackground
    {
        get => (Brush?)GetValue(FocusedBackgroundProperty);
        set => SetValue(FocusedBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="FocusedBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty FocusedBorderBrushProperty =
        DependencyProperty.Register(
            nameof(FocusedBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used by an editable combo box while keyboard focus is within
    /// its editor. A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? FocusedBorderBrush
    {
        get => (Brush?)GetValue(FocusedBorderBrushProperty);
        set => SetValue(FocusedBorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DropDownOpenForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DropDownOpenForegroundProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenForeground
    {
        get => (Brush?)GetValue(DropDownOpenForegroundProperty);
        set => SetValue(DropDownOpenForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DropDownOpenBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DropDownOpenBackgroundProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenBackground
    {
        get => (Brush?)GetValue(DropDownOpenBackgroundProperty);
        set => SetValue(DropDownOpenBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DropDownOpenBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DropDownOpenBorderBrushProperty =
        DependencyProperty.Register(
            nameof(DropDownOpenBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used by a non-editable combo box while its drop-down is open.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DropDownOpenBorderBrush
    {
        get => (Brush?)GetValue(DropDownOpenBorderBrushProperty);
        set => SetValue(DropDownOpenBorderBrushProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledForegroundProperty =
        DependencyProperty.Register(
            nameof(DisabledForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the foreground used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Foreground"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledForeground
    {
        get => (Brush?)GetValue(DisabledForegroundProperty);
        set => SetValue(DisabledForegroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledBackgroundProperty =
        DependencyProperty.Register(
            nameof(DisabledBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the background used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.Background"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBackground
    {
        get => (Brush?)GetValue(DisabledBackgroundProperty);
        set => SetValue(DisabledBackgroundProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="DisabledBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisabledBorderBrushProperty =
        DependencyProperty.Register(
            nameof(DisabledBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null, OnStateBrushChanged)
        );

    /// <summary>
    /// Gets or sets the border brush used while the combo box is disabled.
    /// A <see langword="null"/> value falls back to <see cref="Control.BorderBrush"/>.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? DisabledBorderBrush
    {
        get => (Brush?)GetValue(DisabledBorderBrushProperty);
        set => SetValue(DisabledBorderBrushProperty, value);
    }

    private static readonly DependencyPropertyKey VisualForegroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualForeground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualForeground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualForegroundProperty =
        VisualForegroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the foreground selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal foreground value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualForeground => (Brush?)GetValue(VisualForegroundProperty);

    private static readonly DependencyPropertyKey VisualBackgroundPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBackground),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBackground"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBackgroundProperty =
        VisualBackgroundPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the background selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal background value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBackground => (Brush?)GetValue(VisualBackgroundProperty);

    private static readonly DependencyPropertyKey VisualBorderBrushPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(VisualBorderBrush),
            typeof(Brush),
            typeof(ComboBox),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the read-only <see cref="VisualBorderBrush"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty VisualBorderBrushProperty =
        VisualBorderBrushPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the border brush selected for the current interaction state. This property is intended
    /// for control templates and does not replace the caller's normal border brush value.
    /// </summary>
    [Bindable(true)]
    [Category("Appearance")]
    public Brush? VisualBorderBrush => (Brush?)GetValue(VisualBorderBrushProperty);

    #endregion

    /// <summary>
    /// Registers native presentation defaults and filtering-specific scrolling coercion.
    /// </summary>
    static ComboBox()
    {
        GroupStyleSelectorProperty.OverrideMetadata(
            typeof(ComboBox), new FrameworkPropertyMetadata(null, null, CoerceSearchGroupStyle)
        );

        ItemsPanelProperty.OverrideMetadata(
            typeof(ComboBox), new FrameworkPropertyMetadata(
                ItemsPanelProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                null, CoerceSearchItemsPanel
            )
        );

        ScrollViewer.CanContentScrollProperty.OverrideMetadata(
            typeof(ComboBox), new FrameworkPropertyMetadata(false, null, CoerceSearchScrolling)
        );

        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(typeof(ComboBox))
        );

        // ----------------------------------------------------------------------------------------
        // Standard Control brushes remain the normal-state inputs. Metadata callbacks keep the
        // template-facing outputs synchronized when styles, bindings, or dynamic resources change.
        // ----------------------------------------------------------------------------------------

        BackgroundProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                BackgroundProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        BorderBrushProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                BorderBrushProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        ForegroundProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(
                ForegroundProperty.GetMetadata(typeof(System.Windows.Controls.ComboBox)).DefaultValue,
                OnStandardBrushChanged
            )
        );

        // -------------------------------------------------------------------------------------------
        // These inherited properties have no overridable CLR transition hooks. Overriding metadata
        // appends our callback to WPF's existing callbacks and preserves the framework's coercion and
        // two-way-binding behavior while making runtime mode changes update the visual outputs.
        // -------------------------------------------------------------------------------------------

        IsEditableProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );

        IsReadOnlyProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );

        IsDropDownOpenProperty.OverrideMetadata(
            typeof(ComboBox),
            new FrameworkPropertyMetadata(false, OnInteractionStateChanged)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ComboBox"/> class.
    /// </summary>
    public ComboBox()
    {
        UpdateVisualBrushes();
        IsEnabledChanged += OnComboBoxIsEnabledChanged;
    }

    /// <summary>
    /// Recomputes visual outputs when a normal-state brush changes through any value source.
    /// </summary>
    private static void OnStandardBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var comboBox = (ComboBox)d;
        comboBox.UpdateVisualBrushes();
    }

    /// <summary>
    /// Applies a changed optional state brush to the current interaction state.
    /// </summary>
    private static void OnStateBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var comboBox = (ComboBox)d;
        comboBox.UpdateVisualBrushes();
    }

    /// <summary>
    /// Refreshes visual outputs after editability, read-only state, or popup state changes.
    /// </summary>
    private static void OnInteractionStateChanged(
        DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        var comboBox = (ComboBox)d;
        comboBox.UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(MouseEventArgs e)
    {
        base.OnMouseEnter(e);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        UpdateVisualBrushes();
    }

    /// <inheritdoc />
    protected override void OnIsKeyboardFocusWithinChanged(DependencyPropertyChangedEventArgs e)
    {
        // WPF may close the popup in its base implementation when focus leaves. Updating after the
        // base call ensures both the focus state and any resulting IsDropDownOpen change are settled.
        base.OnIsKeyboardFocusWithinChanged(e);
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Applies or releases disabled-state brushes when effective enabled state changes.
    /// </summary>
    private void OnComboBoxIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateVisualBrushes();
    }

    /// <summary>
    /// Resolves all template-facing brushes in one pass. Disabled always wins. Editable controls
    /// behave like text input and therefore prefer keyboard focus over hover; their open popup is
    /// not a pressed visual state. Non-editable controls behave like toggle buttons, so an open
    /// popup wins over hover. Read-only does not form an independent visual state because WPF only
    /// applies it to the embedded editor; theme styles can specialize its focused brushes.
    /// </summary>
    private void UpdateVisualBrushes()
    {
        Brush? visualForeground  = Foreground;
        Brush? visualBackground  = Background;
        Brush? visualBorderBrush = BorderBrush;

        if (!IsEnabled)
        {
            visualForeground  = DisabledForeground ?? visualForeground;
            visualBackground  = DisabledBackground ?? visualBackground;
            visualBorderBrush = DisabledBorderBrush ?? visualBorderBrush;
        }
        else if (IsEditable && IsKeyboardFocusWithin)
        {
            visualForeground  = FocusedForeground ?? visualForeground;
            visualBackground  = FocusedBackground ?? visualBackground;
            visualBorderBrush = FocusedBorderBrush ?? visualBorderBrush;
        }
        else if (!IsEditable && IsDropDownOpen)
        {
            visualForeground  = DropDownOpenForeground ?? visualForeground;
            visualBackground  = DropDownOpenBackground ?? visualBackground;
            visualBorderBrush = DropDownOpenBorderBrush ?? visualBorderBrush;
        }
        else if (IsMouseOver)
        {
            visualForeground  = MouseOverForeground ?? visualForeground;
            visualBackground  = MouseOverBackground ?? visualBackground;
            visualBorderBrush = MouseOverBorderBrush ?? visualBorderBrush;
        }

        SetValue(VisualForegroundPropertyKey, visualForeground);
        SetValue(VisualBackgroundPropertyKey, visualBackground);
        SetValue(VisualBorderBrushPropertyKey, visualBorderBrush);
    }
}