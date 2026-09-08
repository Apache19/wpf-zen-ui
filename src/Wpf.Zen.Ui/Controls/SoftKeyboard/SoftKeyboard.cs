// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Wpf.Zen.Ui.AutomationPeers;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a touch-oriented keyboard that participates in application layout while sending input
/// to a focused WPF text editor.
/// </summary>
/// <remarks>
/// <para>
/// The control and every key in its default template are non-focusable. Input is delivered through
/// the Windows keyboard stream so WPF selection, editing commands, undo, keyboard layouts, and TSF
/// input methods continue to behave as they do with a hardware keyboard.
/// </para>
/// <para>
/// Set <see cref="IsOpen"/> explicitly for deterministic application layout. Alternatively set
/// <see cref="AutoShowMode"/> to open after touch/pen focus or every supported editor focus.
/// <see cref="TargetScope"/> separates keyboard ownership when multiple forms, Pages, or dialogs are
/// loaded on the same Dispatcher.
/// </para>
/// <para>
/// The default template provides optional <c>PART_PreviousCandidatePageButton</c> and
/// <c>PART_NextCandidatePageButton</c> parts. Custom templates may omit them, while the read-only
/// candidate page properties remain available for a replacement paging surface.
/// </para>
/// </remarks>
[TemplatePart(Name = KeyRowsPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = CandidatesPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = PreviousCandidatePageButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = NextCandidatePageButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = EmojiPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = InputMethodsPartName, Type = typeof(ItemsControl))]
public class SoftKeyboard : Control
{
    private const string KeyRowsPartName = "PART_KeyRows";
    private const string CandidatesPartName = "PART_Candidates";
    private const string PreviousCandidatePageButtonPartName = "PART_PreviousCandidatePageButton";
    private const string NextCandidatePageButtonPartName = "PART_NextCandidatePageButton";
    private const string EmojiPartName = "PART_Emoji";
    private const string InputMethodsPartName = "PART_InputMethods";

    private readonly ObservableCollection<SoftKeyboardCandidate> _candidates = [];
    private readonly ObservableCollection<SoftKeyboardInputMethod> _inputMethods = [];
    private readonly ObservableCollection<SoftKeyboardEmoji> _emojis = [];
    private readonly ObservableCollection<string> _alternateValues = [];
    private readonly LinkedList<SoftKeyboardEmoji> _recentEmoji = [];
    private readonly SoftKeyboardPlatformServices _platform;
    private SoftKeyboardDispatcherSession? _session;
    private Window? _window;
    private SoftKeyboardKey? _heldKey;
    private SoftKeyboardEmoji? _heldEmoji;
    private SoftKeyboardInputMethod? _preferredImeInputMethod;
    private DispatcherTimer? _holdTimer;
    private DispatcherTimer? _repeatTimer;
    private bool _didRepeatOrOpenAlternates;
    private object? _suppressClickItem;
    private int _lastShiftClickTick;
    private int _lastInputPaneHideTick;
    private bool _systemKeyboardHiddenByThisControl;

    static SoftKeyboard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(typeof(SoftKeyboard))
        );
        FocusableProperty.OverrideMetadata(typeof(SoftKeyboard), new FrameworkPropertyMetadata(false));
        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>Initializes a new instance of the <see cref="SoftKeyboard"/> class.</summary>
    public SoftKeyboard()
        : this(SoftKeyboardPlatformServices.Default)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="SoftKeyboard"/> class with replaceable platform boundaries.</summary>
    /// <param name="platform">The native service boundary.</param>
    internal SoftKeyboard(SoftKeyboardPlatformServices platform)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        Candidates = new ReadOnlyObservableCollection<SoftKeyboardCandidate>(_candidates);
        InputMethods = new ReadOnlyObservableCollection<SoftKeyboardInputMethod>(_inputMethods);
        Emojis = new ReadOnlyObservableCollection<SoftKeyboardEmoji>(_emojis);
        AlternateValues = new ReadOnlyObservableCollection<string>(_alternateValues);

        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnButtonClick));
        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnPreviewMouseDown), true);
        AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(OnPreviewMouseUp), true);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnIsVisibleChanged;
        RefreshEmoji();
        RebuildLayout();
    }

    #region Dependency properties

    /// <summary>Identifies the <see cref="IsOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(SoftKeyboard),
        new FrameworkPropertyMetadata(
            false,
            FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            OnIsOpenChanged
        )
    );

    /// <summary>
    /// Gets or sets a value indicating whether the keyboard surface is open and participates in layout.
    /// </summary>
    /// <remarks>The default is <see langword="false"/>. The closed default template measures to zero.</remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Identifies the <see cref="Target"/> dependency property.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(IInputElement),
        typeof(SoftKeyboard),
        new PropertyMetadata(null, OnTargetChanged)
    );

    /// <summary>
    /// Gets or sets an editor that remains the keyboard's explicit input target.
    /// </summary>
    /// <remarks>
    /// When this value is <see langword="null"/>, the keyboard follows the last supported editor
    /// focused inside <see cref="TargetScope"/>. An explicit target must still be enabled and editable.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public IInputElement? Target
    {
        get => (IInputElement?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Identifies the <see cref="TargetScope"/> dependency property.</summary>
    public static readonly DependencyProperty TargetScopeProperty = DependencyProperty.Register(
        nameof(TargetScope),
        typeof(DependencyObject),
        typeof(SoftKeyboard),
        new PropertyMetadata(null, OnTargetScopeChanged)
    );

    /// <summary>
    /// Gets or sets the visual or logical subtree whose editors belong to this keyboard.
    /// </summary>
    /// <remarks>A <see langword="null"/> value uses the containing <see cref="Window"/>.</remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public DependencyObject? TargetScope
    {
        get => (DependencyObject?)GetValue(TargetScopeProperty);
        set => SetValue(TargetScopeProperty, value);
    }

    /// <summary>Identifies the <see cref="AutoShowMode"/> dependency property.</summary>
    public static readonly DependencyProperty AutoShowModeProperty = DependencyProperty.Register(
        nameof(AutoShowMode),
        typeof(SoftKeyboardAutoShowMode),
        typeof(SoftKeyboard),
        new PropertyMetadata(SoftKeyboardAutoShowMode.Disabled),
        static value => value is SoftKeyboardAutoShowMode mode && Enum.IsDefined(typeof(SoftKeyboardAutoShowMode), mode)
    );

    /// <summary>Gets or sets which editor-focus transitions open the keyboard automatically.</summary>
    [Bindable(true)]
    [Category("Behavior")]
    public SoftKeyboardAutoShowMode AutoShowMode
    {
        get => (SoftKeyboardAutoShowMode)GetValue(AutoShowModeProperty);
        set => SetValue(AutoShowModeProperty, value);
    }

    /// <summary>Identifies the <see cref="AutoHideMode"/> dependency property.</summary>
    public static readonly DependencyProperty AutoHideModeProperty = DependencyProperty.Register(
        nameof(AutoHideMode),
        typeof(SoftKeyboardAutoHideMode),
        typeof(SoftKeyboard),
        new PropertyMetadata(SoftKeyboardAutoHideMode.FocusLeavesScope),
        static value => value is SoftKeyboardAutoHideMode mode && Enum.IsDefined(typeof(SoftKeyboardAutoHideMode), mode)
    );

    /// <summary>Gets or sets when an automatically opened keyboard closes.</summary>
    [Bindable(true)]
    [Category("Behavior")]
    public SoftKeyboardAutoHideMode AutoHideMode
    {
        get => (SoftKeyboardAutoHideMode)GetValue(AutoHideModeProperty);
        set => SetValue(AutoHideModeProperty, value);
    }

    /// <summary>Identifies the <see cref="LayoutProvider"/> dependency property.</summary>
    public static readonly DependencyProperty LayoutProviderProperty = DependencyProperty.Register(
        nameof(LayoutProvider),
        typeof(ISoftKeyboardLayoutProvider),
        typeof(SoftKeyboard),
        new PropertyMetadata(DefaultSoftKeyboardLayoutProvider.Default, OnLayoutContextChanged)
    );

    /// <summary>Gets or sets the provider that creates key rows for the active input context.</summary>
    [Bindable(true)]
    [Category("Behavior")]
    public ISoftKeyboardLayoutProvider LayoutProvider
    {
        get => (ISoftKeyboardLayoutProvider)GetValue(LayoutProviderProperty);
        set => SetValue(LayoutProviderProperty, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>Identifies the <see cref="TargetAdapter"/> dependency property.</summary>
    public static readonly DependencyProperty TargetAdapterProperty = DependencyProperty.Register(
        nameof(TargetAdapter),
        typeof(ISoftKeyboardTargetAdapter),
        typeof(SoftKeyboard),
        new PropertyMetadata(null, OnTargetAdapterChanged)
    );

    /// <summary>
    /// Gets or sets an optional adapter for a custom editor. Built-in WPF text editors remain supported
    /// when this adapter does not recognize the active element.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public ISoftKeyboardTargetAdapter? TargetAdapter
    {
        get => (ISoftKeyboardTargetAdapter?)GetValue(TargetAdapterProperty);
        set => SetValue(TargetAdapterProperty, value);
    }

    /// <summary>Identifies the <see cref="EmojiSearchText"/> dependency property.</summary>
    public static readonly DependencyProperty EmojiSearchTextProperty = DependencyProperty.Register(
        nameof(EmojiSearchText),
        typeof(string),
        typeof(SoftKeyboard),
        new PropertyMetadata(string.Empty, OnEmojiFilterChanged)
    );

    /// <summary>
    /// Gets or sets text used to filter Emoji by their invariant searchable names.
    /// </summary>
    /// <remarks>The default is an empty string. The property does not record or persist search text.</remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public string EmojiSearchText
    {
        get => (string)GetValue(EmojiSearchTextProperty);
        set => SetValue(EmojiSearchTextProperty, value ?? string.Empty);
    }

    /// <summary>Identifies the <see cref="EmojiCategory"/> dependency property.</summary>
    public static readonly DependencyProperty EmojiCategoryProperty = DependencyProperty.Register(
        nameof(EmojiCategory),
        typeof(SoftKeyboardEmojiCategory?),
        typeof(SoftKeyboard),
        new PropertyMetadata(null, OnEmojiFilterChanged)
    );

    /// <summary>Gets or sets the Emoji category to display, or <see langword="null"/> to display all categories.</summary>
    [Bindable(true)]
    [Category("Behavior")]
    public SoftKeyboardEmojiCategory? EmojiCategory
    {
        get => (SoftKeyboardEmojiCategory?)GetValue(EmojiCategoryProperty);
        set => SetValue(EmojiCategoryProperty, value);
    }

    private static readonly DependencyPropertyKey ActiveTargetPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActiveTarget),
            typeof(IInputElement),
            typeof(SoftKeyboard),
            new PropertyMetadata(null, OnActiveTargetChanged)
        );

    /// <summary>Identifies the read-only <see cref="ActiveTarget"/> dependency property.</summary>
    public static readonly DependencyProperty ActiveTargetProperty = ActiveTargetPropertyKey.DependencyProperty;

    /// <summary>Gets the editor to which the next key will be delivered.</summary>
    public IInputElement? ActiveTarget => (IInputElement?)GetValue(ActiveTargetProperty);

    private static readonly DependencyPropertyKey DisplayModePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(DisplayMode),
            typeof(SoftKeyboardDisplayMode),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardDisplayMode.Letters, OnLayoutContextChanged)
        );

    /// <summary>Identifies the read-only <see cref="DisplayMode"/> dependency property.</summary>
    public static readonly DependencyProperty DisplayModeProperty = DisplayModePropertyKey.DependencyProperty;

    /// <summary>Gets the keyboard surface currently displayed.</summary>
    public SoftKeyboardDisplayMode DisplayMode => (SoftKeyboardDisplayMode)GetValue(DisplayModeProperty);

    private static readonly DependencyPropertyKey ShiftStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ShiftState),
            typeof(SoftKeyboardShiftState),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardShiftState.Off, OnLayoutContextChanged)
        );

    /// <summary>Identifies the read-only <see cref="ShiftState"/> dependency property.</summary>
    public static readonly DependencyProperty ShiftStateProperty = ShiftStatePropertyKey.DependencyProperty;

    /// <summary>Gets the virtual Shift state.</summary>
    public SoftKeyboardShiftState ShiftState => (SoftKeyboardShiftState)GetValue(ShiftStateProperty);

    private static readonly DependencyPropertyKey CurrentLayoutPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CurrentLayout),
            typeof(SoftKeyboardLayout),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="CurrentLayout"/> dependency property.</summary>
    public static readonly DependencyProperty CurrentLayoutProperty = CurrentLayoutPropertyKey.DependencyProperty;

    /// <summary>Gets the immutable key layout currently rendered by the default template.</summary>
    public SoftKeyboardLayout CurrentLayout => (SoftKeyboardLayout)GetValue(CurrentLayoutProperty);

    private static readonly DependencyPropertyKey ActiveInputMethodPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActiveInputMethod),
            typeof(SoftKeyboardInputMethod),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>Identifies the read-only <see cref="ActiveInputMethod"/> dependency property.</summary>
    public static readonly DependencyProperty ActiveInputMethodProperty = ActiveInputMethodPropertyKey.DependencyProperty;

    /// <summary>Gets the active Windows input method when profile enumeration is available.</summary>
    public SoftKeyboardInputMethod? ActiveInputMethod =>
        (SoftKeyboardInputMethod?)GetValue(ActiveInputMethodProperty);

    private static readonly DependencyPropertyKey ImeStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ImeState),
            typeof(InputMethodState),
            typeof(SoftKeyboard),
            new PropertyMetadata(InputMethodState.DoNotCare)
        );

    /// <summary>Identifies the read-only <see cref="ImeState"/> dependency property.</summary>
    public static readonly DependencyProperty ImeStateProperty = ImeStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the active Windows input method is composing native text or passing through
    /// direct alphanumeric input.
    /// </summary>
    /// <remarks>
    /// <see cref="InputMethodState.DoNotCare"/> indicates that the active text service does not
    /// expose a controllable open state. The default template's ABC button requests the opposite
    /// state and leaves this value unchanged when the input method rejects the request.
    /// </remarks>
    public InputMethodState ImeState => (InputMethodState)GetValue(ImeStateProperty);

    private static readonly DependencyPropertyKey CandidatePresentationStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CandidatePresentationState),
            typeof(SoftKeyboardCandidatePresentationState),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardCandidatePresentationState.None)
        );

    /// <summary>Identifies the read-only <see cref="CandidatePresentationState"/> dependency property.</summary>
    public static readonly DependencyProperty CandidatePresentationStateProperty =
        CandidatePresentationStatePropertyKey.DependencyProperty;

    /// <summary>Gets how the active input method's candidate UI is being presented.</summary>
    public SoftKeyboardCandidatePresentationState CandidatePresentationState =>
        (SoftKeyboardCandidatePresentationState)GetValue(CandidatePresentationStateProperty);

    private static readonly DependencyPropertyKey SystemKeyboardSuppressionStatePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SystemKeyboardSuppressionState),
            typeof(SoftKeyboardSystemKeyboardSuppressionState),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardSystemKeyboardSuppressionState.NotRequested)
        );

    /// <summary>Identifies the read-only <see cref="SystemKeyboardSuppressionState"/> dependency property.</summary>
    public static readonly DependencyProperty SystemKeyboardSuppressionStateProperty =
        SystemKeyboardSuppressionStatePropertyKey.DependencyProperty;

    /// <summary>Gets the result of the most recent Windows input-pane hide request.</summary>
    public SoftKeyboardSystemKeyboardSuppressionState SystemKeyboardSuppressionState =>
        (SoftKeyboardSystemKeyboardSuppressionState)GetValue(SystemKeyboardSuppressionStateProperty);

    private static readonly DependencyPropertyKey HasCandidatesPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(HasCandidates),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="HasCandidates"/> dependency property.</summary>
    public static readonly DependencyProperty HasCandidatesProperty = HasCandidatesPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether the embedded candidate list contains values.</summary>
    public bool HasCandidates => (bool)GetValue(HasCandidatesProperty);

    private static readonly DependencyPropertyKey CandidatePageNumberPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CandidatePageNumber),
            typeof(uint),
            typeof(SoftKeyboard),
            new PropertyMetadata(0U)
        );

    /// <summary>Identifies the read-only <see cref="CandidatePageNumber"/> dependency property.</summary>
    public static readonly DependencyProperty CandidatePageNumberProperty =
        CandidatePageNumberPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the one-based page number reported by the active candidate UI, or zero when no page is active.
    /// </summary>
    public uint CandidatePageNumber => (uint)GetValue(CandidatePageNumberProperty);

    private static readonly DependencyPropertyKey CandidatePageCountPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CandidatePageCount),
            typeof(uint),
            typeof(SoftKeyboard),
            new PropertyMetadata(0U)
        );

    /// <summary>Identifies the read-only <see cref="CandidatePageCount"/> dependency property.</summary>
    public static readonly DependencyProperty CandidatePageCountProperty = CandidatePageCountPropertyKey.DependencyProperty;

    /// <summary>Gets the number of pages reported by the active candidate UI, or zero when inactive.</summary>
    public uint CandidatePageCount => (uint)GetValue(CandidatePageCountProperty);

    private static readonly DependencyPropertyKey CanGoToPreviousCandidatePagePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanGoToPreviousCandidatePage),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="CanGoToPreviousCandidatePage"/> dependency property.</summary>
    public static readonly DependencyProperty CanGoToPreviousCandidatePageProperty =
        CanGoToPreviousCandidatePagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the active input method exposes a previous candidate page.
    /// </summary>
    /// <remarks>The value is <see langword="false"/> when no embedded candidate list is active.</remarks>
    public bool CanGoToPreviousCandidatePage => (bool)GetValue(CanGoToPreviousCandidatePageProperty);

    private static readonly DependencyPropertyKey CanGoToNextCandidatePagePropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(CanGoToNextCandidatePage),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="CanGoToNextCandidatePage"/> dependency property.</summary>
    public static readonly DependencyProperty CanGoToNextCandidatePageProperty =
        CanGoToNextCandidatePagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the active input method exposes a following candidate page.
    /// </summary>
    /// <remarks>The value is <see langword="false"/> when no embedded candidate list is active.</remarks>
    public bool CanGoToNextCandidatePage => (bool)GetValue(CanGoToNextCandidatePageProperty);

    private static readonly DependencyPropertyKey IsAlternatePopupOpenPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsAlternatePopupOpen),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="IsAlternatePopupOpen"/> dependency property.</summary>
    public static readonly DependencyProperty IsAlternatePopupOpenProperty =
        IsAlternatePopupOpenPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether long-press alternatives are visible.</summary>
    public bool IsAlternatePopupOpen => (bool)GetValue(IsAlternatePopupOpenProperty);

    #endregion

    /// <summary>Gets the candidates supplied by the current TSF input method.</summary>
    public ReadOnlyObservableCollection<SoftKeyboardCandidate> Candidates { get; }

    /// <summary>Gets the enabled Windows input methods.</summary>
    public ReadOnlyObservableCollection<SoftKeyboardInputMethod> InputMethods { get; }

    /// <summary>Gets the Emoji entries shown by the default Emoji surface.</summary>
    public ReadOnlyObservableCollection<SoftKeyboardEmoji> Emojis { get; }

    /// <summary>Gets the alternatives offered for the currently held key or Emoji.</summary>
    public ReadOnlyObservableCollection<string> AlternateValues { get; }

    /// <summary>Changes the visible keyboard surface.</summary>
    /// <param name="displayMode">The surface to display.</param>
    public void ShowDisplayMode(SoftKeyboardDisplayMode displayMode)
    {
        if (!Enum.IsDefined(typeof(SoftKeyboardDisplayMode), displayMode))
        {
            throw new ArgumentOutOfRangeException(nameof(displayMode));
        }

        SetValue(DisplayModePropertyKey, displayMode);
        CloseAlternates();

        if (displayMode == SoftKeyboardDisplayMode.InputMethods)
        {
            RefreshInputMethods();
        }
        else if (displayMode == SoftKeyboardDisplayMode.Emoji)
        {
            RefreshEmoji();
        }
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SoftKeyboardAutomationPeer(this);

    internal void OnSessionFocusChanged(IInputElement? focusedElement, bool touchOrPenInitiated)
    {
        if (!IsLoaded)
        {
            return;
        }

        if (focusedElement is DependencyObject dependencyObject && IsWithinEffectiveScope(dependencyObject))
        {
            ISoftKeyboardTargetAdapter? focusedAdapter = GetAdapter(focusedElement);
            bool isEditableTarget = focusedAdapter?.CanReceiveInput(focusedElement) == true;
            if (Target is null && isEditableTarget)
            {
                SetValue(ActiveTargetPropertyKey, focusedElement);
            }

            bool shouldOpen = AutoShowMode == SoftKeyboardAutoShowMode.AnyFocus
                || (AutoShowMode == SoftKeyboardAutoShowMode.TouchAndPen && touchOrPenInitiated);
            if (shouldOpen && isEditableTarget)
            {
                SetCurrentValue(IsOpenProperty, true);
            }

            if (IsOpen)
            {
                RefreshImeState();
                RequestSystemKeyboardHide();
                _platform.Candidates.Refresh(this);
                QueueCandidateBridgeRefresh();
            }

            return;
        }

        if (AutoHideMode == SoftKeyboardAutoHideMode.FocusLeavesScope
            && AutoShowMode != SoftKeyboardAutoShowMode.Disabled)
        {
            SetCurrentValue(IsOpenProperty, false);
        }

        // Focus outside the effective scope cannot be a valid target for this keyboard. Release the
        // local suppression marker immediately, but do not request TryShow for another window or
        // scope—the new focus owner and Windows input policy must decide what UI it needs.
        ReleaseSystemKeyboardSuppression(tryRestore: false);
    }

    internal void OnSessionInputLanguageChanged()
    {
        RebuildLayout();
        RefreshInputMethods();
        RefreshImeState();
    }

    internal void ReplaceCandidates(
        IEnumerable<SoftKeyboardCandidate> candidates,
        SoftKeyboardCandidatePresentationState presentationState,
        uint pageNumber = 0,
        uint pageCount = 0
    )
    {
        SoftKeyboardCandidate[] snapshot = candidates.ToArray();
        bool sameCandidates = snapshot.Length == _candidates.Count;
        for (int index = 0; sameCandidates && index < snapshot.Length; index++)
        {
            SoftKeyboardCandidate current = _candidates[index];
            SoftKeyboardCandidate replacement = snapshot[index];
            sameCandidates = current.Index == replacement.Index
                && current.Text == replacement.Text
                && current.IsSelected == replacement.IsSelected;
        }

        // Some TIPs do not raise UpdateUIElement reliably, so the bridge polls while composing.
        // Keeping equal immutable items in place is essential: clearing the collection every tick
        // destroys the Button below the pointer between MouseDown and MouseUp, which both flashes
        // its hover state and prevents Click from being raised.
        if (sameCandidates
            && CandidatePresentationState == presentationState
            && CandidatePageNumber == pageNumber
            && CandidatePageCount == pageCount)
        {
            return;
        }

        if (!sameCandidates)
        {
            _candidates.Clear();
            foreach (SoftKeyboardCandidate candidate in snapshot)
            {
                _candidates.Add(candidate);
            }
        }

        SetValue(HasCandidatesPropertyKey, _candidates.Count > 0);
        SetValue(CandidatePageNumberPropertyKey, pageNumber);
        SetValue(CandidatePageCountPropertyKey, pageCount);
        SetValue(CanGoToPreviousCandidatePagePropertyKey, pageNumber > 1);
        SetValue(CanGoToNextCandidatePagePropertyKey, pageNumber > 0 && pageNumber < pageCount);
        SetValue(CandidatePresentationStatePropertyKey, presentationState);
    }

    private static void OnIsOpenChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        SoftKeyboardAutomationPeer.RaiseIsOpenChanged(
            keyboard,
            (bool)eventArgs.OldValue,
            (bool)eventArgs.NewValue
        );
        if ((bool)eventArgs.NewValue)
        {
            keyboard.ResolveTarget();
            keyboard.RebuildLayout();
            keyboard.RefreshInputMethods();
            keyboard.RefreshImeState();
            keyboard.RequestSystemKeyboardHide();
            keyboard._platform.Candidates.Refresh(keyboard);
        }
        else
        {
            keyboard.CancelHeldKey();
            keyboard.CloseAlternates();
            keyboard._platform.Candidates.ReleasePresentation(keyboard);
            keyboard.ReplaceCandidates(Array.Empty<SoftKeyboardCandidate>(), SoftKeyboardCandidatePresentationState.None);
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
    }

    private static void OnTargetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SoftKeyboard)dependencyObject).ResolveTarget();
    }

    private static void OnTargetAdapterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SoftKeyboard)dependencyObject).ResolveTarget();
    }

    private static void OnTargetScopeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        keyboard.ResolveTarget();

        if (keyboard.ActiveTarget is DependencyObject activeTarget && !keyboard.IsWithinEffectiveScope(activeTarget))
        {
            keyboard.SetValue(ActiveTargetPropertyKey, null);
        }
    }

    private static void OnActiveTargetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        keyboard.RebuildLayout();
        if (eventArgs.NewValue is null)
        {
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: false);
        }
        else
        {
            keyboard.RequestSystemKeyboardHide();
        }
    }

    private static void OnLayoutContextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SoftKeyboard)dependencyObject).RebuildLayout();
    }

    private static void OnEmojiFilterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SoftKeyboard)dependencyObject).RefreshEmoji();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        _session = SoftKeyboardDispatcherSession.GetForCurrentDispatcher();
        _session.Register(this);
        _platform.Candidates.Register(this);
        InputMethod.Current.StateChanged += OnInputMethodStateChanged;
        _window = Window.GetWindow(this);
        if (_window is not null)
        {
            _window.Activated += OnWindowActivated;
            _window.Deactivated += OnWindowDeactivated;
        }

        ResolveTarget();
        RefreshImeState();
        if (IsOpen)
        {
            RequestSystemKeyboardHide();
            _platform.Candidates.Refresh(this);
            QueueCandidateBridgeRefresh();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs eventArgs)
    {
        // Unloading commonly means the Window/Page itself is going away. Showing a system keyboard
        // for a disappearing HWND would be intrusive and can target the next foreground window, so
        // only discard this control's suppression ownership here.
        ReleaseSystemKeyboardSuppression(tryRestore: false);
        _session?.Unregister(this);
        _platform.Candidates.Unregister(this);
        InputMethod.Current.StateChanged -= OnInputMethodStateChanged;
        _session = null;
        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        CancelHeldKey();
        CloseAlternates();
        ReplaceCandidates(Array.Empty<SoftKeyboardCandidate>(), SoftKeyboardCandidatePresentationState.None);
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (!IsVisible)
        {
            CancelHeldKey();
            CloseAlternates();
            _platform.Candidates.ReleasePresentation(this);
            ReleaseSystemKeyboardSuppression(tryRestore: true);
            return;
        }

        // IsVisible includes ancestor visibility, unlike the local Visibility value. Reacquire the
        // still-active TSF element when a Page/dialog region becomes visible without requiring the
        // user to type another character.
        if (IsLoaded && IsOpen && ActiveTarget is not null)
        {
            _platform.Candidates.Refresh(this);
            RequestSystemKeyboardHide();
        }
    }

    private void OnWindowActivated(object? sender, EventArgs eventArgs)
    {
        if (IsOpen)
        {
            RequestSystemKeyboardHide();
        }
    }

    private void OnInputMethodStateChanged(object sender, InputMethodStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsImeStateChanged || eventArgs.IsImeConversionModeChanged)
        {
            RefreshImeState();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs eventArgs)
    {
        // SendInput targets the foreground thread. Ending every pending gesture here prevents a
        // held key from continuing into another process during an Alt+Tab or tablet app switch.
        CancelHeldKey();
        CloseAlternates();
        ReleaseSystemKeyboardSuppression(tryRestore: false);
    }

    private void OnButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not ButtonBase button)
        {
            return;
        }

        if (_didRepeatOrOpenAlternates && ReferenceEquals(button.DataContext, _suppressClickItem))
        {
            _didRepeatOrOpenAlternates = false;
            _suppressClickItem = null;
            eventArgs.Handled = true;
            return;
        }

        switch (button.DataContext)
        {
            case SoftKeyboardKey key:
                InvokeKey(key);
                break;
            case SoftKeyboardCandidate candidate:
                CommitCandidate(candidate);
                break;
            case SoftKeyboardInputMethod inputMethod:
                ActivateInputMethod(inputMethod);
                break;
            case SoftKeyboardEmoji emoji:
                InsertEmoji(emoji);
                break;
            case string alternate when IsAlternatePopupOpen:
                _ = TrySendText(alternate);
                CloseAlternates();
                break;
            default:
                if (button.Name == PreviousCandidatePageButtonPartName)
                {
                    MoveCandidatePage(-1);
                }
                else if (button.Name == NextCandidatePageButtonPartName)
                {
                    MoveCandidatePage(1);
                }
                else if (button.Tag is SoftKeyboardDisplayMode mode)
                {
                    ShowDisplayMode(mode);
                }
                else if (button.Tag is SoftKeyboardKeyKind.ToggleIme)
                {
                    ToggleIme();
                }

                break;
        }

        eventArgs.Handled = true;
    }

    private void MoveCandidatePage(int delta)
    {
        if ((delta < 0 && !CanGoToPreviousCandidatePage)
            || (delta > 0 && !CanGoToNextCandidatePage)
            || !PrepareTargetForInput())
        {
            return;
        }

        _ = _platform.Candidates.TryMovePage(this, delta);
        RequestSystemKeyboardHide();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton != MouseButton.Left
            || eventArgs.OriginalSource is not DependencyObject source
            || FindButton(source) is not { } button)
        {
            return;
        }

        _heldKey = button.DataContext as SoftKeyboardKey;
        _heldEmoji = button.DataContext as SoftKeyboardEmoji;
        if (_heldKey is null && _heldEmoji is null)
        {
            return;
        }

        _didRepeatOrOpenAlternates = false;
        _suppressClickItem = null;
        _holdTimer ??= new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(GetInitialRepeatDelay()),
        };
        _holdTimer.Tick -= OnHoldTimerTick;
        _holdTimer.Tick += OnHoldTimerTick;
        _holdTimer.Start();
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs eventArgs)
    {
        if (eventArgs.ChangedButton == MouseButton.Left)
        {
            if (IsAlternatePopupOpen
                && InputHitTest(eventArgs.GetPosition(this)) is DependencyObject hit
                && FindButton(hit)?.DataContext is string alternate)
            {
                _ = TrySendText(alternate);
                CloseAlternates();
            }

            _suppressClickItem = (object?)_heldKey ?? _heldEmoji;
            CancelHeldKey(keepRepeatMarker: true);
        }
    }

    private void OnHoldTimerTick(object? sender, EventArgs eventArgs)
    {
        _holdTimer?.Stop();
        if (_heldKey is null && _heldEmoji is null)
        {
            return;
        }

        IReadOnlyList<string> alternates = _heldKey?.Alternates
            ?? _heldEmoji?.Alternates
            ?? Array.Empty<string>();
        if (alternates.Count > 0)
        {
            _alternateValues.Clear();
            foreach (string alternate in alternates)
            {
                _alternateValues.Add(alternate);
            }

            _didRepeatOrOpenAlternates = true;
            SetValue(IsAlternatePopupOpenPropertyKey, true);
            return;
        }

        if (_heldKey is null || !IsRepeatable(_heldKey))
        {
            return;
        }

        _didRepeatOrOpenAlternates = true;
        InvokeKey(_heldKey);
        _repeatTimer ??= new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(GetRepeatInterval()),
        };
        _repeatTimer.Tick -= OnRepeatTimerTick;
        _repeatTimer.Tick += OnRepeatTimerTick;
        _repeatTimer.Start();
    }

    private void OnRepeatTimerTick(object? sender, EventArgs eventArgs)
    {
        if (_heldKey is null || !IsRepeatable(_heldKey) || !IsOpen || !IsWindowForeground())
        {
            CancelHeldKey();
            return;
        }

        InvokeKey(_heldKey);
    }

    private void InvokeKey(SoftKeyboardKey key)
    {
        switch (key.Kind)
        {
            case SoftKeyboardKeyKind.VirtualKey:
                bool shifted = ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;
                if (TrySendVirtualKey(key.Key, shifted)
                    && ShiftState == SoftKeyboardShiftState.Once
                    && IsCharacterKey(key.Key))
                {
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
                }

                break;
            case SoftKeyboardKeyKind.Text:
                if (TrySendText(key.Text ?? string.Empty) && ShiftState == SoftKeyboardShiftState.Once)
                {
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
                }

                break;
            case SoftKeyboardKeyKind.Shift:
                ToggleShift();
                break;
            case SoftKeyboardKeyKind.ChangeDisplayMode:
                ShowDisplayMode(key.DisplayMode);
                break;
            case SoftKeyboardKeyKind.InputMethods:
                ShowDisplayMode(SoftKeyboardDisplayMode.InputMethods);
                break;
            case SoftKeyboardKeyKind.ToggleIme:
                ToggleIme();
                break;
            case SoftKeyboardKeyKind.Close:
                SetCurrentValue(IsOpenProperty, false);
                break;
        }
    }

    private void ToggleShift()
    {
        int now = Environment.TickCount;
        int elapsed = unchecked(now - _lastShiftClickTick);
        _lastShiftClickTick = now;

        SoftKeyboardShiftState next = ShiftState == SoftKeyboardShiftState.Once
            && elapsed is >= 0 and <= 500
            ? SoftKeyboardShiftState.Locked
            : ShiftState switch
            {
                SoftKeyboardShiftState.Off => SoftKeyboardShiftState.Once,
                _ => SoftKeyboardShiftState.Off,
            };
        SetValue(ShiftStatePropertyKey, next);
    }

    private void ToggleIme()
    {
        // IME state belongs to the focused TSF document, so target validation and focus restoration
        // must happen before reading or changing WPF's Dispatcher-scoped InputMethod.Current.
        ShowDisplayMode(SoftKeyboardDisplayMode.Letters);
        if (!PrepareTargetForInput())
        {
            return;
        }

        RefreshInputMethods();
        InputMethodState current = _platform.InputMethods.GetImeState();
        if (current == InputMethodState.On)
        {
            if (_platform.InputMethods.TrySetImeState(InputMethodState.Off))
            {
                SetValue(ImeStatePropertyKey, InputMethodState.Off);
                ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            }
        }
        else
        {
            // ImeState only controls the already active text service. If the process currently uses
            // an ordinary English keyboard layout, setting it to On is a no-op. Activate an enabled
            // TIP first, preferring the one most recently observed in this keyboard session.
            SoftKeyboardInputMethod? inputMethod = ActiveInputMethod ?? FindPreferredImeInputMethod();
            if (inputMethod is not null
                && (ActiveInputMethod is not null || _platform.InputMethods.TryActivate(inputMethod))
                && _platform.InputMethods.TrySetImeState(InputMethodState.On))
            {
                _preferredImeInputMethod = inputMethod;
                SetValue(ActiveInputMethodPropertyKey, inputMethod);
                SetValue(ImeStatePropertyKey, InputMethodState.On);
                QueueInputMethodStateRefresh();
            }
        }

        RequestSystemKeyboardHide();
    }

    private bool TrySendVirtualKey(Key key, bool shift)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        bool sent = _platform.Input.SendVirtualKey(key, shift);
        if (sent)
        {
            QueueCandidateBridgeRefresh();
        }

        RequestSystemKeyboardHide();
        return sent;
    }

    private bool TrySendText(string text)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        bool sent = _platform.Input.SendUnicode(text);
        if (sent)
        {
            QueueCandidateBridgeRefresh();
        }

        RequestSystemKeyboardHide();
        return sent;
    }

    private bool PrepareTargetForInput()
    {
        IInputElement? target = Target ?? ActiveTarget;
        ISoftKeyboardTargetAdapter? adapter = target is null ? null : GetAdapter(target);
        if (target is null
            || target is not DependencyObject targetObject
            || adapter is null
            || !IsWithinEffectiveScope(targetObject)
            || !adapter.CanReceiveInput(target)
            || !IsWindowForeground())
        {
            return false;
        }

        if (!adapter.TryFocus(target) || !ReferenceEquals(Keyboard.FocusedElement, target))
        {
            return false;
        }

        SetValue(ActiveTargetPropertyKey, target);
        // The candidate sink must be advised before the injected key can open a TSF candidate UI.
        // This also retries the normal focus path, where WPF may have created TextServicesHost only
        // after the GotKeyboardFocus preprocessing callback completed.
        _platform.Candidates.Refresh(this);
        return IsWindowForeground();
    }

    private void QueueCandidateBridgeRefresh()
    {
        // WPF initializes TextStore at DispatcherPriority.Background after the editor's first text
        // view layout. ContextIdle is intentionally lower, so the bridge sees the ThreadManager even
        // when focus and keyboard loading happen during the same layout pass. The same deferred path
        // runs after SendInput so a normal-mode TIP candidate element can be discovered and embedded.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (IsLoaded && IsVisible && IsOpen && ActiveTarget is not null)
                {
                    _platform.Candidates.Refresh(this);
                }
            })
        );
    }

    private bool IsWindowForeground()
    {
        Window? window = _window ?? Window.GetWindow(this);
        if (window is null)
        {
            return false;
        }

        IntPtr host = new WindowInteropHelper(window).Handle;
        return _platform.Input.IsHostForeground(host);
    }

    private void ResolveTarget()
    {
        if (Target is not null)
        {
            bool valid = Target is DependencyObject targetObject
                && IsWithinEffectiveScope(targetObject)
                && GetAdapter(Target) is { } targetAdapter
                && targetAdapter.CanReceiveInput(Target);
            SetValue(ActiveTargetPropertyKey, valid ? Target : null);
            return;
        }

        if (Keyboard.FocusedElement is { } focused
            && focused is DependencyObject dependencyObject
            && IsWithinEffectiveScope(dependencyObject)
            && GetAdapter(focused) is { } adapter
            && adapter.CanReceiveInput(focused))
        {
            SetValue(ActiveTargetPropertyKey, focused);
        }
    }

    private ISoftKeyboardTargetAdapter? GetAdapter(IInputElement target)
    {
        if (TargetAdapter?.IsSupported(target) == true)
        {
            return TargetAdapter;
        }

        return SoftKeyboardTargetAdapter.Default.IsSupported(target)
            ? SoftKeyboardTargetAdapter.Default
            : null;
    }

    private DependencyObject? GetEffectiveScope() => TargetScope ?? _window ?? Window.GetWindow(this);

    private bool IsWithinEffectiveScope(DependencyObject element)
    {
        DependencyObject? scope = GetEffectiveScope();
        if (scope is null)
        {
            return false;
        }

        if (ReferenceEquals(scope, element))
        {
            return true;
        }

        for (DependencyObject? current = element; current is not null; current = GetParent(current))
        {
            if (ReferenceEquals(current, scope))
            {
                return true;
            }
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject element)
    {
        if (element is Visual or System.Windows.Media.Media3D.Visual3D)
        {
            DependencyObject? visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }

    private void RebuildLayout()
    {
        ISoftKeyboardTargetAdapter? adapter = ActiveTarget is null ? null : GetAdapter(ActiveTarget);
        InputScopeNameValue inputScope = ActiveTarget is not null && adapter is not null
            ? adapter.GetInputScope(ActiveTarget)
            : InputScopeNameValue.Default;
        CultureInfo culture = InputLanguageManager.Current.CurrentInputLanguage;

        try
        {
            SetValue(
                CurrentLayoutPropertyKey,
                LayoutProvider.CreateLayout(new SoftKeyboardLayoutContext(culture, inputScope, DisplayMode, ShiftState))
            );
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A consumer-supplied provider must not prevent the keyboard from closing or from exposing
            // diagnostics. Falling back also keeps templates from receiving a null layout.
            SetValue(
                CurrentLayoutPropertyKey,
                DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                    new SoftKeyboardLayoutContext(culture, inputScope, DisplayMode, ShiftState)
                )
            );
        }
    }

    private void RefreshInputMethods()
    {
        _platform.InputMethods.Refresh(_inputMethods, out SoftKeyboardInputMethod? active);
        SetValue(ActiveInputMethodPropertyKey, active);
        if (active is not null)
        {
            _preferredImeInputMethod = active;
        }
    }

    private void RefreshImeState()
    {
        SetValue(ImeStatePropertyKey, _platform.InputMethods.GetImeState());
    }

    private void ActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        if (_platform.InputMethods.TryActivate(inputMethod))
        {
            _preferredImeInputMethod = inputMethod;
            _ = _platform.InputMethods.TrySetImeState(InputMethodState.On);
            RefreshInputMethods();
            RefreshImeState();
            SetValue(DisplayModePropertyKey, SoftKeyboardDisplayMode.Letters);
            RebuildLayout();
            QueueInputMethodStateRefresh();
        }
    }

    private SoftKeyboardInputMethod? FindPreferredImeInputMethod()
    {
        SoftKeyboardInputMethod? remembered = _preferredImeInputMethod is null
            ? null
            : _inputMethods.FirstOrDefault(inputMethod =>
                inputMethod.ClassId == _preferredImeInputMethod.ClassId
                && inputMethod.ProfileId == _preferredImeInputMethod.ProfileId
                && inputMethod.LanguageId == _preferredImeInputMethod.LanguageId
            );
        if (remembered is not null)
        {
            return remembered;
        }

        // The first invocation has no process-local history. Prefer a TIP matching the application's
        // UI culture, then its formatting culture, before falling back to Windows enumeration order.
        // This makes a Chinese application select Microsoft Pinyin without hard-coding a vendor or
        // preventing Japanese, Korean, and third-party profiles from using the same control.
        ushort uiLanguage = unchecked((ushort)CultureInfo.CurrentUICulture.LCID);
        ushort formattingLanguage = unchecked((ushort)CultureInfo.CurrentCulture.LCID);
        return _inputMethods.FirstOrDefault(inputMethod => SamePrimaryLanguage(inputMethod.LanguageId, uiLanguage))
            ?? _inputMethods.FirstOrDefault(inputMethod => SamePrimaryLanguage(inputMethod.LanguageId, formattingLanguage))
            ?? _inputMethods.FirstOrDefault();
    }

    private void QueueInputMethodStateRefresh()
    {
        // Profile activation is allowed to complete asynchronously. Re-reading both the active
        // profile and open compartment prevents the optimistic button state from becoming stale.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (IsLoaded)
                {
                    RefreshInputMethods();
                    RefreshImeState();
                    RebuildLayout();
                    QueueCandidateBridgeRefresh();
                }
            })
        );
    }

    private static bool SamePrimaryLanguage(ushort left, ushort right) =>
        (left & 0x03FF) == (right & 0x03FF);

    private void CommitCandidate(SoftKeyboardCandidate candidate)
    {
        // The TSF bridge replaces this handler with an active candidate behavior object. Retaining
        // the candidate click path here keeps the template independent from COM callback lifetimes.
        if (_session is not null && _platform.Candidates.TryCommit(this, candidate.Index))
        {
            ReplaceCandidates(Array.Empty<SoftKeyboardCandidate>(), SoftKeyboardCandidatePresentationState.None);
        }
    }

    private void InsertEmoji(SoftKeyboardEmoji emoji)
    {
        if (!TrySendText(emoji.Text))
        {
            return;
        }

        LinkedListNode<SoftKeyboardEmoji>? existing = _recentEmoji.Find(emoji);
        if (existing is not null)
        {
            _recentEmoji.Remove(existing);
        }

        _recentEmoji.AddFirst(emoji);
        while (_recentEmoji.Count > 16)
        {
            _recentEmoji.RemoveLast();
        }
    }

    private void RefreshEmoji()
    {
        _emojis.Clear();
        string search = EmojiSearchText.Trim();
        bool MatchesSearch(SoftKeyboardEmoji emoji) =>
            search.Length == 0 || emoji.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        bool Matches(SoftKeyboardEmoji emoji) =>
            (EmojiCategory is null || emoji.Category == EmojiCategory)
            && MatchesSearch(emoji);

        if (EmojiCategory is null or SoftKeyboardEmojiCategory.Recent)
        {
            foreach (SoftKeyboardEmoji recent in _recentEmoji.Where(MatchesSearch))
            {
                _emojis.Add(recent);
            }
        }

        if (EmojiCategory != SoftKeyboardEmojiCategory.Recent)
        {
            foreach (SoftKeyboardEmoji emoji in SoftKeyboardEmojiCatalog.Items.Where(Matches))
            {
                _emojis.Add(emoji);
            }
        }
    }

    private void RequestSystemKeyboardHide()
    {
        if (!IsSystemKeyboardSuppressionRequired)
        {
            ReleaseSystemKeyboardSuppression(tryRestore: true);
            return;
        }

        int now = Environment.TickCount;
        int elapsed = unchecked(now - _lastInputPaneHideTick);
        if (_lastInputPaneHideTick != 0 && elapsed is >= 0 and < 250)
        {
            return;
        }

        _lastInputPaneHideTick = now;

        Window? window = _window ?? Window.GetWindow(this);
        IntPtr handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        SoftKeyboardSystemKeyboardSuppressionState result = _platform.InputPane.TryHide(handle);
        if (result == SoftKeyboardSystemKeyboardSuppressionState.Hidden)
        {
            _systemKeyboardHiddenByThisControl = true;
        }

        SetValue(SystemKeyboardSuppressionStatePropertyKey, result);
    }

    /// <summary>
    /// Gets a value indicating whether this control currently has enough live context to suppress
    /// the HWND input pane.
    /// </summary>
    /// <remarks>
    /// IsVisible includes ancestor visibility. Requiring the target to remain keyboard-focused avoids
    /// hiding Windows input UI while focus is on a command, another form, or another application; a
    /// soft-key invocation can still restore the remembered editor before requesting suppression.
    /// </remarks>
    internal bool IsSystemKeyboardSuppressionRequired
    {
        get
        {
            if (!IsLoaded
                || !IsVisible
                || !IsOpen
                || ActiveTarget is not DependencyObject targetObject
                || !ReferenceEquals(Keyboard.FocusedElement, ActiveTarget)
                || !IsWithinEffectiveScope(targetObject)
                || GetAdapter(ActiveTarget)?.CanReceiveInput(ActiveTarget) != true)
            {
                return false;
            }

            return IsWindowForeground();
        }
    }

    /// <summary>
    /// Ends this control's ownership of a successful InputPane hide request and optionally lets
    /// Windows restore the pane for the still-focused editor.
    /// </summary>
    private void ReleaseSystemKeyboardSuppression(bool tryRestore)
    {
        bool shouldRestore = tryRestore
            && _systemKeyboardHiddenByThisControl
            && (_session?.IsSystemKeyboardSuppressionRequiredByOther(this) != true)
            && CanRestoreSystemKeyboard();

        _systemKeyboardHiddenByThisControl = false;
        _lastInputPaneHideTick = 0;
        SetValue(
            SystemKeyboardSuppressionStatePropertyKey,
            SoftKeyboardSystemKeyboardSuppressionState.NotRequested
        );

        if (!shouldRestore)
        {
            return;
        }

        Window? window = _window ?? Window.GetWindow(this);
        IntPtr handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        _ = _platform.InputPane.TryShow(handle);
    }

    /// <summary>
    /// Validates the conservative subset of WPF's own TipTsfHelper.Show preconditions available to
    /// the control without reflecting private stylus policy or cached WinRT state.
    /// </summary>
    private bool CanRestoreSystemKeyboard()
    {
        if (ActiveTarget is not DependencyObject targetObject
            || !ReferenceEquals(Keyboard.FocusedElement, ActiveTarget)
            || !IsWithinEffectiveScope(targetObject)
            || GetAdapter(ActiveTarget)?.CanReceiveInput(ActiveTarget) != true)
        {
            return false;
        }

        return IsWindowForeground();
    }

    private void CancelHeldKey(bool keepRepeatMarker = false)
    {
        _holdTimer?.Stop();
        _repeatTimer?.Stop();
        _heldKey = null;
        _heldEmoji = null;
        if (!keepRepeatMarker)
        {
            _didRepeatOrOpenAlternates = false;
            _suppressClickItem = null;
        }
    }

    private void CloseAlternates()
    {
        SetValue(IsAlternatePopupOpenPropertyKey, false);
        _alternateValues.Clear();
    }

    private static ButtonBase? FindButton(DependencyObject source)
    {
        for (DependencyObject? current = source; current is not null; current = GetParent(current))
        {
            if (current is ButtonBase button)
            {
                return button;
            }
        }

        return null;
    }

    private static bool IsRepeatable(SoftKeyboardKey key) =>
        key.Kind == SoftKeyboardKeyKind.VirtualKey
        && key.Key is Key.Back or Key.Delete or Key.Left or Key.Right or Key.Up or Key.Down;

    private static bool IsCharacterKey(Key key) => key is >= Key.A and <= Key.Z;

    private static double GetInitialRepeatDelay() => (SystemParameters.KeyboardDelay + 1) * 250d;

    private static double GetRepeatInterval()
    {
        // Win32 exposes speed as 0..31. Its documented effective range is approximately
        // 2.5..30 repeats per second, so this interpolation follows the user's system setting.
        double repeatsPerSecond = 2.5d + (SystemParameters.KeyboardSpeed * 27.5d / 31d);
        return 1000d / repeatsPerSecond;
    }
}
