// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media.Media3D;
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
/// The default template provides a <c>PART_Keys</c> items surface whose containers pass each key's
/// row, column and column span to <see cref="SoftKeyboardPanel"/>. It also provides optional
/// <c>PART_PreviousCandidatePageButton</c> and <c>PART_NextCandidatePageButton</c> parts. Custom
/// templates may omit the paging parts, while the read-only candidate page properties remain
/// available for a replacement paging surface.
/// Keys use <see cref="SoftKeyboardKeyControl"/> with its Key property bound to the layout item.
/// Each cap owns its gesture and alternative popup; this control only coordinates cancellation
/// and executes semantic input requests against the active editor.
/// </para>
/// </remarks>
[TemplatePart(Name = KeysPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = CandidatesPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = PreviousCandidatePageButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = NextCandidatePageButtonPartName, Type = typeof(ButtonBase))]
public partial class SoftKeyboard : Control
{
    private const string KeysPartName = "PART_Keys";
    private const string CandidatesPartName = "PART_Candidates";
    private const string PreviousCandidatePageButtonPartName = "PART_PreviousCandidatePageButton";
    private const string NextCandidatePageButtonPartName = "PART_NextCandidatePageButton";

    private readonly ObservableCollection<SoftKeyboardCandidate> _candidates = [];
    private readonly ObservableCollection<SoftKeyboardInputMethod> _inputMethods = [];
    private readonly SoftKeyboardPlatformServices _platform;
    private SoftKeyboardDispatcherSession? _session;
    private Window? _window;
    private TextComposition? _activeTextComposition;
    private IInputElement? _compositionTarget;
    private bool _compositionCancellationRequested;
    private SoftKeyboardInputMethod? _preferredImeInputMethod;
    private SoftKeyboardKeyControl? _activeKeyControl;
    private UIElement? _inputMethodMenuAnchor;
    private int _lastShiftClickTick;
    private int _lastInputPaneHideTick;
    private DispatcherOperation? _inputPaneHideOperation;
    private DispatcherTimer? _inputPaneHideTimer;
    private bool _systemKeyboardHiddenByThisControl;

    static SoftKeyboard()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(typeof(SoftKeyboard))
        );

        FocusableProperty.OverrideMetadata(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(false)
        );

        KeyboardNavigation.IsTabStopProperty.OverrideMetadata(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboard"/> class.
    /// </summary>
    public SoftKeyboard()
        : this(SoftKeyboardPlatformServices.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboard"/> class with replaceable platform boundaries.
    /// </summary>
    /// <param name="platform">The native service boundary.</param>
    internal SoftKeyboard(SoftKeyboardPlatformServices platform)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));

        Candidates   = new ReadOnlyObservableCollection<SoftKeyboardCandidate>(_candidates);
        InputMethods = new ReadOnlyObservableCollection<SoftKeyboardInputMethod>(_inputMethods);

        AddHandler(
            ButtonBase.ClickEvent,
            new RoutedEventHandler(OnButtonClick)
        );

        AddHandler(
            SoftKeyboardKeyControl.InteractionStartedEvent,
            new RoutedEventHandler(OnKeyInteractionStarted)
        );

        AddHandler(
            SoftKeyboardKeyControl.InputRequestedEvent,
            new EventHandler<SoftKeyboardInputRequestedEventArgs>(OnKeyInputRequested)
        );

        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;

        IsVisibleChanged += OnIsVisibleChanged;
        IsEnabledChanged += OnIsEnabledChanged;

        RebuildLayout();
    }

    #region Dependency properties

    #region DP: CornerRadius

    /// <summary>
    /// Identifies the <see cref="CornerRadius"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CornerRadiusProperty
        = Border.CornerRadiusProperty.AddOwner(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(new CornerRadius(8))
        );

    /// <summary>
    /// Gets or sets the outer surface corner radii. The default is 8 DIP.
    /// </summary>
    /// <remarks>
    /// The default template forwards this value to its border, which performs the required
    /// layout and rendering invalidation. The value is not inherited and supports animation;
    /// WPF's Border validation rejects negative or non-finite radii.
    /// </remarks>
    public CornerRadius CornerRadius
    {
        get => (CornerRadius)GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    #endregion

    #region DP: RowSpacing

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RowSpacingProperty
        = SoftKeyboardPanel.RowSpacingProperty.AddOwner(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(
                4D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    /// <summary>
    /// Gets or sets the requested distance between adjacent key rows. The default is 4 DIP.
    /// </summary>
    /// <remarks>
    /// The default template forwards this value to its <see cref="SoftKeyboardPanel"/>. The panel
    /// may reduce the effective gap under an exceptionally small finite height so it never lays out
    /// a row beyond the keyboard's assigned bounds.
    /// </remarks>
    [Bindable(true)]
    [Category("Layout")]
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    #endregion

    #region DP: ColumnSpacing

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpacingProperty
        = SoftKeyboardPanel.ColumnSpacingProperty.AddOwner(
            typeof(SoftKeyboard),
            new FrameworkPropertyMetadata(
                4D,
                FrameworkPropertyMetadataOptions.AffectsMeasure
            )
        );

    /// <summary>
    /// Gets or sets the requested gutter between adjacent logical key columns. The default is 4 DIP.
    /// </summary>
    /// <remarks>
    /// The default template forwards this value to its <see cref="SoftKeyboardPanel"/>. Adjacent key
    /// caps remain separated by one gutter, while a key spanning several columns also covers their
    /// intervening gutters. The panel may reduce the effective gutter under an exceptionally small
    /// finite width so the final key slot remains inside the keyboard's assigned bounds.
    /// </remarks>
    [Bindable(true)]
    [Category("Layout")]
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    #endregion

    #region DP: IsOpen

    /// <summary>
    /// Identifies the <see cref="IsOpen"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsOpenProperty
        = DependencyProperty.Register(
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
    /// <remarks>
    /// The default is <see langword="false"/>. The closed default template measures to zero.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    #endregion

    #region DP: Target

    /// <summary>
    /// Identifies the <see cref="Target"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetProperty
        = DependencyProperty.Register(
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

    #endregion

    #region DP: TargetScope

    /// <summary>
    /// Identifies the <see cref="TargetScope"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetScopeProperty
        = DependencyProperty.Register(
            nameof(TargetScope),
            typeof(DependencyObject),
            typeof(SoftKeyboard),
            new PropertyMetadata(null, OnTargetScopeChanged)
        );

    /// <summary>
    /// Gets or sets the visual or logical subtree whose editors belong to this keyboard.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> value uses the containing <see cref="Window"/>.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public DependencyObject? TargetScope
    {
        get => (DependencyObject?)GetValue(TargetScopeProperty);
        set => SetValue(TargetScopeProperty, value);
    }

    #endregion

    #region DP: AutoShowMode

    /// <summary>
    /// Identifies the <see cref="AutoShowMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AutoShowModeProperty
        = DependencyProperty.Register(
            nameof(AutoShowMode),
            typeof(SoftKeyboardAutoShowMode),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardAutoShowMode.Disabled),
            static value => value is SoftKeyboardAutoShowMode mode
                            && Enum.IsDefined(typeof(SoftKeyboardAutoShowMode), mode)
        );

    /// <summary>
    /// Gets or sets which editor-focus transitions open the keyboard automatically.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public SoftKeyboardAutoShowMode AutoShowMode
    {
        get => (SoftKeyboardAutoShowMode)GetValue(AutoShowModeProperty);
        set => SetValue(AutoShowModeProperty, value);
    }

    #endregion

    #region DP: AutoHideMode

    /// <summary>
    /// Identifies the <see cref="AutoHideMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AutoHideModeProperty
        = DependencyProperty.Register(
            nameof(AutoHideMode),
            typeof(SoftKeyboardAutoHideMode),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardAutoHideMode.FocusLeavesScope),
            static value => value is SoftKeyboardAutoHideMode mode
                            && Enum.IsDefined(typeof(SoftKeyboardAutoHideMode), mode)
        );

    /// <summary>
    /// Gets or sets when an automatically opened keyboard closes.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public SoftKeyboardAutoHideMode AutoHideMode
    {
        get => (SoftKeyboardAutoHideMode)GetValue(AutoHideModeProperty);
        set => SetValue(AutoHideModeProperty, value);
    }

    #endregion

    #region DP: LayoutProvider

    /// <summary>
    /// Identifies the <see cref="LayoutProvider"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty LayoutProviderProperty
        = DependencyProperty.Register(
            nameof(LayoutProvider),
            typeof(ISoftKeyboardLayoutProvider),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                DefaultSoftKeyboardLayoutProvider.Default,
                OnLayoutContextChanged
            )
        );

    /// <summary>
    /// Gets or sets the provider that creates positioned keys for the active input context.
    /// </summary>
    [Bindable(true)]
    [Category("Behavior")]
    public ISoftKeyboardLayoutProvider LayoutProvider
    {
        get => (ISoftKeyboardLayoutProvider)GetValue(LayoutProviderProperty);
        set => SetValue(
            LayoutProviderProperty,
            value ?? throw new ArgumentNullException(nameof(value))
        );
    }

    #endregion

    #region DP: TargetAdapter

    /// <summary>
    /// Identifies the <see cref="TargetAdapter"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty TargetAdapterProperty
        = DependencyProperty.Register(
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

    #endregion

    #region Read-Only DP: ActiveTarget

    private static readonly DependencyPropertyKey ActiveTargetPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(ActiveTarget),
            typeof(IInputElement),
            typeof(SoftKeyboard),
            new PropertyMetadata(null, OnActiveTargetChanged)
        );

    /// <summary>
    /// Identifies the <see cref="ActiveTarget"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActiveTargetProperty
        = ActiveTargetPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the editor to which the next key will be delivered.
    /// </summary>
    public IInputElement? ActiveTarget
    {
        get => (IInputElement?)GetValue(ActiveTargetProperty);
    }

    #endregion

    #region Read-Only DP: DisplayMode

    private static readonly DependencyPropertyKey DisplayModePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(DisplayMode),
            typeof(SoftKeyboardDisplayMode),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardDisplayMode.Automatic,
                OnDisplayModeChanged
            )
        );

    /// <summary>
    /// Identifies the <see cref="DisplayMode"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty DisplayModeProperty
        = DisplayModePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the requested surface. The default, Automatic, follows the active editor input scope.
    /// </summary>
    public SoftKeyboardDisplayMode DisplayMode
    {
        get => (SoftKeyboardDisplayMode)GetValue(DisplayModeProperty);
    }

    #endregion

    #region Read-Only DP: ShiftState

    private static readonly DependencyPropertyKey ShiftStatePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(ShiftState),
            typeof(SoftKeyboardShiftState),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardShiftState.Off,
                OnLayoutContextChanged
            )
        );

    /// <summary>
    /// Identifies the <see cref="ShiftState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ShiftStateProperty
        = ShiftStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the virtual Shift state.
    /// </summary>
    public SoftKeyboardShiftState ShiftState
    {
        get => (SoftKeyboardShiftState)GetValue(ShiftStateProperty);
    }

    #endregion

    #region Read-Only DP: CurrentLayout

    private static readonly DependencyPropertyKey CurrentLayoutPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CurrentLayout),
            typeof(SoftKeyboardLayout),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="CurrentLayout"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CurrentLayoutProperty
        = CurrentLayoutPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the immutable key layout currently rendered by the default template.
    /// </summary>
    public SoftKeyboardLayout CurrentLayout
    {
        get => (SoftKeyboardLayout)GetValue(CurrentLayoutProperty);
    }

    #endregion

    #region Read-Only DP: ActiveInputMethod

    private static readonly DependencyPropertyKey ActiveInputMethodPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(ActiveInputMethod),
            typeof(SoftKeyboardInputMethod),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="ActiveInputMethod"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActiveInputMethodProperty
        = ActiveInputMethodPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the active Windows input method when profile enumeration is available.
    /// </summary>
    public SoftKeyboardInputMethod? ActiveInputMethod
    {
        get => (SoftKeyboardInputMethod?)GetValue(ActiveInputMethodProperty);
    }

    #endregion

    #region Read-Only DP: ImeState

    private static readonly DependencyPropertyKey ImeStatePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(ImeState),
            typeof(InputMethodState),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                InputMethodState.DoNotCare,
                OnLayoutContextChanged
            )
        );

    /// <summary>
    /// Identifies the <see cref="ImeState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ImeStateProperty
        = ImeStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets whether the active Windows input method is composing native text or passing through
    /// direct alphanumeric input.
    /// </summary>
    /// <remarks>
    /// <see cref="InputMethodState.DoNotCare"/> indicates that the active text service does not
    /// expose a controllable open state. The default template's ABC button requests the opposite
    /// state and leaves this value unchanged when the input method rejects the request.
    /// </remarks>
    public InputMethodState ImeState
    {
        get => (InputMethodState)GetValue(ImeStateProperty);
    }

    #endregion

    #region Read-Only DP: CandidatePresentationState

    private static readonly DependencyPropertyKey CandidatePresentationStatePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CandidatePresentationState),
            typeof(SoftKeyboardCandidatePresentationState),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardCandidatePresentationState.None
            )
        );

    /// <summary>
    /// Identifies the <see cref="CandidatePresentationState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidatePresentationStateProperty
        = CandidatePresentationStatePropertyKey.DependencyProperty;

    /// <summary>Gets how the active input method's candidate UI is being presented.</summary>
    public SoftKeyboardCandidatePresentationState CandidatePresentationState
    {
        get => (SoftKeyboardCandidatePresentationState)GetValue(CandidatePresentationStateProperty);
    }

    #endregion

    #region Read-Only DP: SystemKeyboardSuppressionState

    private static readonly DependencyPropertyKey SystemKeyboardSuppressionStatePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(SystemKeyboardSuppressionState),
            typeof(SoftKeyboardSystemKeyboardSuppressionState),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardSystemKeyboardSuppressionState.NotRequested
            )
        );

    /// <summary>
    /// Identifies the <see cref="SystemKeyboardSuppressionState"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SystemKeyboardSuppressionStateProperty
        = SystemKeyboardSuppressionStatePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the result of the most recent Windows input-pane hide request.
    /// </summary>
    public SoftKeyboardSystemKeyboardSuppressionState SystemKeyboardSuppressionState
    {
        get => (SoftKeyboardSystemKeyboardSuppressionState)GetValue(SystemKeyboardSuppressionStateProperty);
    }

    #endregion

    #region Read-Only DP: HasCandidates

    private static readonly DependencyPropertyKey HasCandidatesPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(HasCandidates),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="HasCandidates"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty HasCandidatesProperty
        = HasCandidatesPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the embedded candidate list contains values.
    /// </summary>
    public bool HasCandidates
    {
        get => (bool)GetValue(HasCandidatesProperty);
    }

    #endregion

    #region Read-Only DP: CandidatePageNumber

    private static readonly DependencyPropertyKey CandidatePageNumberPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CandidatePageNumber),
            typeof(uint),
            typeof(SoftKeyboard),
            new PropertyMetadata(0U)
        );

    /// <summary>
    /// Identifies the <see cref="CandidatePageNumber"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidatePageNumberProperty
        = CandidatePageNumberPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the one-based page number reported by the active candidate UI, or zero when no page is active.
    /// </summary>
    public uint CandidatePageNumber
    {
        get => (uint)GetValue(CandidatePageNumberProperty);
    }

    #endregion

    #region Read-Only DP: CandidatePageCount

    private static readonly DependencyPropertyKey CandidatePageCountPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CandidatePageCount),
            typeof(uint),
            typeof(SoftKeyboard),
            new PropertyMetadata(0U)
        );

    /// <summary>
    /// Identifies the <see cref="CandidatePageCount"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CandidatePageCountProperty
        = CandidatePageCountPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the number of pages reported by the active candidate UI, or zero when inactive.
    /// </summary>
    public uint CandidatePageCount
    {
        get => (uint)GetValue(CandidatePageCountProperty);
    }

    #endregion

    #region Read-Only DP: CanGoToPreviousCandidatePage

    private static readonly DependencyPropertyKey CanGoToPreviousCandidatePagePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CanGoToPreviousCandidatePage),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="CanGoToPreviousCandidatePage"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanGoToPreviousCandidatePageProperty
        = CanGoToPreviousCandidatePagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the active input method exposes a previous candidate page.
    /// </summary>
    /// <remarks>
    /// The value is <see langword="false"/> when no embedded candidate list is active.
    /// </remarks>
    public bool CanGoToPreviousCandidatePage
    {
        get => (bool)GetValue(CanGoToPreviousCandidatePageProperty);
    }

    #endregion

    #region Read-Only DP: CanGoToNextCandidatePage

    private static readonly DependencyPropertyKey CanGoToNextCandidatePagePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(CanGoToNextCandidatePage),
            typeof(bool),
            typeof(SoftKeyboard),
            new PropertyMetadata(false)
        );

    /// <summary>
    /// Identifies the <see cref="CanGoToNextCandidatePage"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty CanGoToNextCandidatePageProperty
        = CanGoToNextCandidatePagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether the active input method exposes a following candidate page.
    /// </summary>
    /// <remarks>
    /// The value is <see langword="false"/> when no embedded candidate list is active.
    /// </remarks>
    public bool CanGoToNextCandidatePage
    {
        get => (bool)GetValue(CanGoToNextCandidatePageProperty);
    }

    #endregion

    #endregion

    /// <summary>Gets the candidates supplied by the current TSF input method.</summary>
    public ReadOnlyObservableCollection<SoftKeyboardCandidate> Candidates { get; }

    /// <summary>Gets the enabled Windows input methods.</summary>
    public ReadOnlyObservableCollection<SoftKeyboardInputMethod> InputMethods { get; }

    /// <summary>Changes the visible keyboard surface and ends any pending long-press gesture.</summary>
    /// <remarks>
    /// Explicit letter, number, and symbol surfaces override the editor input scope. Automatic
    /// chooses from that scope again. A new active editor resets the mode to Automatic.
    /// If the active input method owns unconfirmed composition
    /// text, the control asks it to cancel that text before crossing the surface boundary.
    /// </remarks>
    /// <param name="displayMode">The surface to display.</param>
    public void ShowDisplayMode(SoftKeyboardDisplayMode displayMode)
    {
        if (!Enum.IsDefined(typeof(SoftKeyboardDisplayMode), displayMode))
        {
            throw new ArgumentOutOfRangeException(nameof(displayMode));
        }

        if (displayMode != DisplayMode && !TryCancelActiveComposition())
        {
            // Direct-text surfaces must not be entered while a TIP still owns mutable pre-edit
            // text. Keeping the current surface is the only non-destructive fallback when Windows
            // cannot accept the Escape request.
            return;
        }

        CancelKeyInteraction();
        SetValue(DisplayModePropertyKey, displayMode);
        CloseFlyouts();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new SoftKeyboardAutomationPeer(this);
    }

    /// <summary>Ends gestures belonging to the previous visual tree when the template changes.</summary>
    /// <remarks>
    /// A theme or application can replace the template without unloading the control. The active
    /// key's interaction must not survive that replacement, even without mouse capture. Cleanup runs at
    /// template change rather than OnApplyTemplate because an empty template creates no tree
    /// and does not receive that callback.
    /// </remarks>
    /// <param name="oldTemplate">The previous template, or null.</param>
    /// <param name="newTemplate">The replacement template, or null.</param>
    protected override void OnTemplateChanged(ControlTemplate oldTemplate, ControlTemplate newTemplate)
    {
        CancelKeyInteraction();
        CloseFlyouts();

        base.OnTemplateChanged(oldTemplate, newTemplate);
    }

    internal void OnSessionFocusChanged(IInputElement? focusedElement, bool touchOrPenInitiated)
    {
        if (_inputMethodMenu?.IsOpen == true)
        {
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        if (focusedElement is DependencyObject dpObj && IsWithinEffectiveScope(dpObj))
        {
            var focusedAdapter   = GetAdapter(focusedElement);
            var isEditableTarget = focusedAdapter?.CanReceiveInput(focusedElement) == true;

            if (isEditableTarget && ReferenceEquals(focusedElement, _compositionTarget))
            {
                // A composition can survive temporary application deactivation or an external
                // focus move. GotKeyboardFocus is the first point at which Escape can again be
                // routed safely to its original editor.
                _ = TryCancelActiveComposition(focusedElement);
            }

            if (Target is null && isEditableTarget)
            {
                SetValue(ActiveTargetPropertyKey, focusedElement);
            }

            var shouldOpen = AutoShowMode == SoftKeyboardAutoShowMode.AnyFocus
                             || (AutoShowMode == SoftKeyboardAutoShowMode.TouchAndPen && touchOrPenInitiated);

            if (shouldOpen && isEditableTarget)
            {
                SetCurrentValue(IsOpenProperty, true);
            }

            if (IsOpen)
            {
                RefreshInputMethods();
                RefreshImeState();
                QueueInputMethodStateRefresh();
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
        _platform.Candidates.ReleasePresentation(this);
    }

    internal void OnSessionInputLanguageChanged()
    {
        RefreshInputMethods();
        RefreshImeState();
        RebuildLayout();
        QueueInputMethodStateRefresh();
    }

    /// <summary>
    /// Tracks the WPF composition associated with the active editor without taking ownership of it.
    /// </summary>
    /// <remarks>
    /// Candidate UI lifetime is not a reliable composition signal: a TIP can temporarily expose no
    /// candidates while its pre-edit range remains mutable. The matching final TextInput event ends
    /// this observation. Cancellation itself is still delegated to the TIP through Escape.
    /// </remarks>
    internal void OnSessionTextComposition(TextCompositionEventArgs eventArgs)
    {
        var composition       = eventArgs.TextComposition;
        var compositionSource = eventArgs.Source as IInputElement;

        if (eventArgs.RoutedEvent is not null
            && (eventArgs.RoutedEvent == TextCompositionManager.PreviewTextInputStartEvent
                || eventArgs.RoutedEvent == TextCompositionManager.PreviewTextInputUpdateEvent))
        {
            if (ReferenceEquals(compositionSource, ActiveTarget))
            {
                _activeTextComposition = composition;
                _compositionTarget = compositionSource;
                _compositionCancellationRequested = false;
            }

            return;
        }

        // TextStore.RaiseCompositionEvents creates a separate FrameworkTextComposition (including
        // FrameworkRichTextComposition) for every start/update/end record. Its final event belongs
        // to the editor's composition stream, not to the object retained from the previous record.
        // Match that stream by source and framework type; ordinary TextComposition instances still
        // require identity so unrelated direct text input cannot end a pending composition.
        if (eventArgs.RoutedEvent == TextCompositionManager.PreviewTextInputEvent
            && ReferenceEquals(compositionSource, _compositionTarget)
            && (ReferenceEquals(composition, _activeTextComposition)
                || (composition is FrameworkTextComposition
                    && _activeTextComposition is FrameworkTextComposition)))
        {
            ClearActiveComposition();
        }
    }

    internal void ReplaceCandidates(
        IEnumerable<SoftKeyboardCandidate> candidates,
        SoftKeyboardCandidatePresentationState presentationState,
        uint pageNumber = 0,
        uint pageCount = 0
    )
    {
        var snapshot       = candidates.ToArray();
        var sameCandidates = snapshot.Length == _candidates.Count;

        for (var index = 0; sameCandidates && index < snapshot.Length; index++)
        {
            var current     = _candidates[index];
            var replacement = snapshot[index];

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
            foreach (var candidate in snapshot)
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
            _ = keyboard.TryCancelActiveComposition();
            keyboard.CancelKeyInteraction();
            keyboard.CloseFlyouts();
            keyboard._platform.Candidates.ReleasePresentation(keyboard);
            keyboard.ReplaceCandidates(Array.Empty<SoftKeyboardCandidate>(), SoftKeyboardCandidatePresentationState.None);
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
    }

    private static void OnTargetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;

        if (eventArgs.NewValue is IInputElement newTarget
            && keyboard.ActiveTarget is { } oldTarget
            && !ReferenceEquals(oldTarget, newTarget))
        {
            _ = keyboard.TryCancelActiveComposition(oldTarget);
        }

        keyboard.ResolveTarget();
    }

    private static void OnTargetAdapterChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        _ = keyboard.TryCancelActiveComposition();
        keyboard.ResolveTarget();
    }

    private static void OnTargetScopeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        if (keyboard.ActiveTarget is { } activeTarget
            && activeTarget is DependencyObject activeTargetObject
            && !keyboard.IsWithinEffectiveScope(activeTargetObject))
        {
            _ = keyboard.TryCancelActiveComposition(activeTarget);
        }

        keyboard.ResolveTarget();

        if (keyboard.ActiveTarget is DependencyObject resolvedTarget && !keyboard.IsWithinEffectiveScope(resolvedTarget))
        {
            keyboard.SetValue(ActiveTargetPropertyKey, null);
        }
    }

    private static void OnActiveTargetChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;

        // A gesture belongs to the editor that was active when it started. Never let a held
        // Backspace or an unselected variant continue into a newly focused document.
        keyboard.CancelKeyInteraction();
        keyboard.CloseFlyouts();

        if (eventArgs.OldValue is IInputElement oldTarget
            && !ReferenceEquals(oldTarget, eventArgs.NewValue)
            && ReferenceEquals(keyboard._compositionTarget, oldTarget))
        {
            // GotKeyboardFocus can report the new editor only after Windows has moved focus. At that
            // point injecting Escape would target the new editor, so discard only our observation;
            // WPF and TSF own composition cleanup for an external focus transition.
            keyboard.ClearActiveComposition();
        }

        // A different editor starts with its semantic input scope; explicit navigation applies
        // only while that editor remains the target.
        keyboard.SetValue(DisplayModePropertyKey, SoftKeyboardDisplayMode.Automatic);
        keyboard.RebuildLayout();
        keyboard._platform.Candidates.ReleasePresentation(keyboard);

        if (eventArgs.NewValue is null)
        {
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: false);
        }
        else
        {
            keyboard.RequestSystemKeyboardHide();
        }
    }

    private static void OnDisplayModeChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        var keyboard = (SoftKeyboard)dependencyObject;
        keyboard.RebuildLayout();

        // The header always reserves space, but native candidate ownership still depends on the
        // live editor and IME state. Reevaluate it immediately after a surface transition.
        if (keyboard.CanPresentCandidates)
        {
            keyboard._platform.Candidates.Refresh(keyboard);
        }
        else
        {
            keyboard._platform.Candidates.ReleasePresentation(keyboard);
        }
    }

    private static void OnLayoutContextChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs eventArgs)
    {
        ((SoftKeyboard)dependencyObject).RebuildLayout();
    }

    private void OnLoaded(object sender, RoutedEventArgs eventArgs)
    {
        _session = SoftKeyboardDispatcherSession.GetForCurrentDispatcher();
        _session.Register(this);

        _platform.Candidates.Register(this);

        // todo: InputMethod.Current 有可能是空的
        InputMethod.Current.StateChanged += OnInputMethodStateChanged;

        StartInputStateObservation();

        // todo: 在没有 Window 的环境下（例如 WinForms/HostElement），如何让此机制生效？
        _window = Window.GetWindow(this);

        if (_window is not null)
        {
            _window.Activated   += OnWindowActivated;
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

        _ = TryCancelActiveComposition();
        ReleaseSystemKeyboardSuppression(tryRestore: false);
        _session?.Unregister(this);
        _platform.Candidates.Unregister(this);

        InputMethod.Current.StateChanged -= OnInputMethodStateChanged;

        _inputStateTimer?.Stop();

        _session = null;

        if (_window is not null)
        {
            _window.Activated -= OnWindowActivated;
            _window.Deactivated -= OnWindowDeactivated;
            _window = null;
        }

        CancelKeyInteraction();
        CloseFlyouts();

        ReplaceCandidates(
            Array.Empty<SoftKeyboardCandidate>(),
            SoftKeyboardCandidatePresentationState.None
        );

        ClearActiveComposition();
    }

    private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (!IsVisible)
        {
            _ = TryCancelActiveComposition();
            CancelKeyInteraction();
            CloseFlyouts();
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
            // Deactivation occurs after the HWND has lost foreground ownership, when SendInput is
            // no longer safe. Cancel any composition that survived the app switch now, then let the
            // queued refresh either reacquire a still-valid TSF element or observe its cancellation.
            _ = TryCancelActiveComposition();
            QueueInputMethodStateRefresh();
            RequestSystemKeyboardHide();
            QueueCandidateBridgeRefresh();
        }
    }

    private void OnInputMethodStateChanged(object sender, InputMethodStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsImeStateChanged || eventArgs.IsImeConversionModeChanged)
        {
            RefreshImeState();
            _platform.Candidates.Refresh(this);
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs eventArgs)
    {
        // SendInput targets the foreground thread. Ending every pending gesture here prevents a
        // held key from continuing into another process during an Alt+Tab or tablet app switch.
        CancelKeyInteraction();
        CloseFlyouts();
        ReleaseSystemKeyboardSuppression(tryRestore: false);
        _platform.Candidates.ReleasePresentation(this);
    }

    private void OnButtonClick(object sender, RoutedEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not ButtonBase button)
        {
            return;
        }

        switch (button.DataContext)
        {
            case SoftKeyboardCandidate candidate:
                CommitCandidate(candidate);
                break;

            case SoftKeyboardInputMethod inputMethod:
                ActivateInputMethod(inputMethod);
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
                else if (button.Tag is SoftKeyboardKeyKind.Close)
                {
                    CloseKeyboard();
                }
                else if (button.Tag is SoftKeyboardKeyKind.ToggleIme)
                {
                    ToggleIme();
                }
                else
                {
                    // Replacement templates may contain application commands. Only consume
                    // actions owned by the keyboard so those Click events can reach the host.
                    return;
                }

                break;
        }

        eventArgs.Handled = true;
    }

    private void MoveCandidatePage(int delta)
    {
        if (!CanPresentCandidates
            || (delta < 0 && !CanGoToPreviousCandidatePage)
            || (delta > 0 && !CanGoToNextCandidatePage)
            || !PrepareTargetForInput())
        {
            return;
        }

        _ = _platform.Candidates.TryMovePage(this, delta);
        RequestSystemKeyboardHide();
    }

    private void OnKeyInteractionStarted(object sender, RoutedEventArgs eventArgs)
    {
        if (eventArgs.OriginalSource is not SoftKeyboardKeyControl keyControl)
        {
            return;
        }

        eventArgs.Handled = true;
        if (!ReferenceEquals(_activeKeyControl, keyControl))
        {
            CancelKeyInteraction();
        }

        _activeKeyControl = keyControl;
        if (_inputMethodMenu is not null)
        {
            _inputMethodMenu.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        }

        if (!IsOpen || !IsEnabled || !IsVisible)
        {
            CancelKeyInteraction();
        }
    }

    private void OnKeyInputRequested(object? sender, SoftKeyboardInputRequestedEventArgs eventArgs)
    {
        eventArgs.Handled = true;

        if (eventArgs.OriginalSource is not SoftKeyboardKeyControl keyControl
            || !ReferenceEquals(_activeKeyControl, keyControl)
            || !ReferenceEquals(keyControl.Key, eventArgs.Key)
            || !IsOpen || !IsEnabled || !IsVisible)
        {
            return;
        }

        // The key owns the gesture, but each request still crosses the keyboard's normal
        // target/focus/IME validation boundary. No editor reference is stored on a key cap.
        _inputMethodMenuAnchor = keyControl;
        try
        {
            if (eventArgs.AlternateText is string text)
            {
                if (TrySendText(text) && ShiftState == SoftKeyboardShiftState.Once)
                    SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
            }
            else
            {
                InvokeKey(eventArgs.Key);
            }
        }
        finally
        {
            _inputMethodMenuAnchor = null;
        }
    }

    private void InvokeKey(SoftKeyboardKey key)
    {
        switch (key.Kind)
        {
            case SoftKeyboardKeyKind.VirtualKey:
                var shifted = ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;
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
                ShowInputMethodMenu();
                break;

            case SoftKeyboardKeyKind.ToggleIme:
                ToggleIme();
                break;

            case SoftKeyboardKeyKind.Close:
                CloseKeyboard();
                break;
        }
    }

    private void ToggleShift()
    {
        var now = Environment.TickCount;
        var elapsed = unchecked(now - _lastShiftClickTick);

        _lastShiftClickTick = now;

        var next = ShiftState == SoftKeyboardShiftState.Once
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
        RunAfterActiveCompositionCancellation(ToggleImeCore);
    }

    private void ToggleImeCore()
    {
        // IME state belongs to the focused TSF document, so target validation and focus restoration
        // must happen before reading or changing WPF's Dispatcher-scoped InputMethod.Current.
        if (!PrepareTargetForInput())
        {
            return;
        }

        RefreshInputMethods();

        var current = ReadInputState();
        if (current == InputMethodState.On)
        {
            if (TryChangeInputState(InputMethodState.Off))
            {
                _platform.Candidates.ReleasePresentation(this);
                ReplaceCandidates([], SoftKeyboardCandidatePresentationState.None);
            }
        }
        else
        {
            // ImeState only controls the already active text service. If the process currently uses
            // an ordinary English keyboard layout, setting it to On is a no-op. Activate an enabled
            // TIP first, preferring the one most recently observed in this keyboard session.
            var inputMethod = ActiveInputMethod ?? FindPreferredImeInputMethod();
            if (inputMethod is not null
                && (ActiveInputMethod is not null || _platform.InputMethods.TryActivate(inputMethod)))
            {
                _preferredImeInputMethod = inputMethod;
                SetValue(ActiveInputMethodPropertyKey, inputMethod);
                _ = TryChangeInputState(InputMethodState.On);
            }
        }

        RefreshImeState();
        QueueInputMethodStateRefresh();
        RequestSystemKeyboardHide();
    }

    private bool TrySendVirtualKey(Key key, bool shift)
    {
        if (!PrepareTargetForInput())
        {
            return false;
        }

        var sent = _platform.Input.SendVirtualKey(key, shift);
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

        // KEYEVENTF_UNICODE bypasses the active TIP. Escape is queued first so mutable pre-edit
        // text cannot be interleaved with symbols or long-press alternatives.
        if (!TryCancelActiveComposition())
        {
            return false;
        }

        var sent = _platform.Input.SendUnicode(text);
        if (sent)
        {
            QueueCandidateBridgeRefresh();
        }

        RequestSystemKeyboardHide();
        return sent;
    }

    private bool PrepareTargetForInput()
    {
        var target = Target ?? ActiveTarget;
        var adapter = target is null ? null : GetAdapter(target);

        if (!IsLoaded || !IsEnabled || !IsVisible || !IsOpen
            || target is null
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

        // Focus callbacks can disable, hide, or invalidate the editor or keyboard synchronously.
        return IsSystemKeyboardSuppressionRequired;
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
        var window = _window ?? Window.GetWindow(this);
        if (window is null)
        {
            return false;
        }

        var host = new WindowInteropHelper(window).Handle;
        return _platform.Input.IsHostForeground(host);
    }

    private void ResolveTarget()
    {
        if (Target is not null)
        {
            var valid = Target is DependencyObject targetObject
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
        var scope = GetEffectiveScope();
        if (scope is null)
        {
            return false;
        }

        if (ReferenceEquals(scope, element))
        {
            return true;
        }

        for (var current = element; current is not null; current = GetParent(current))
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
        if (element is Visual or Visual3D)
        {
            var visualParent = VisualTreeHelper.GetParent(element);
            if (visualParent is not null)
            {
                return visualParent;
            }
        }

        return LogicalTreeHelper.GetParent(element);
    }

    private void RebuildLayout()
    {
        CancelKeyInteraction();
        var adapter = ActiveTarget is null ? null : GetAdapter(ActiveTarget);
        var inputScope = ActiveTarget is not null && adapter is not null
            ? adapter.GetInputScope(ActiveTarget)
            : InputScopeNameValue.Default;
        var culture = InputLanguageManager.Current.CurrentInputLanguage;

        try
        {
            SetValue(
                CurrentLayoutPropertyKey,
                LayoutProvider.CreateLayout(new SoftKeyboardLayoutContext(culture, inputScope, DisplayMode, ShiftState, ImeState, ActiveInputMethod))
                    ?? throw new InvalidOperationException("A layout provider must return a layout.")
            );
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // A consumer-supplied provider must not prevent the keyboard from closing or from exposing
            // diagnostics. Falling back also keeps templates from receiving a null layout.
            SetValue(
                CurrentLayoutPropertyKey,
                DefaultSoftKeyboardLayoutProvider.Default.CreateLayout(
                    new SoftKeyboardLayoutContext(culture, inputScope, DisplayMode, ShiftState, ImeState, ActiveInputMethod)
                )
            );
        }
    }

    private void RefreshInputMethods()
    {
        _platform.InputMethods.Refresh(_inputMethods, out var active);

        var previous = ActiveInputMethod;

        SetValue(ActiveInputMethodPropertyKey, active);

        if (previous?.ProfileId != active?.ProfileId
            || previous?.ClassId != active?.ClassId
            || previous?.LanguageId != active?.LanguageId)
        {
            RebuildLayout();
        }

        if (active is not null)
        {
            _preferredImeInputMethod = active;
        }
    }

    private void RefreshImeState()
    {
        SetValue(ImeStatePropertyKey, ReadInputState());
        if (IsLoaded && !CanPresentCandidates)
        {
            _platform.Candidates.ReleasePresentation(this);
        }
    }

    private void ActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        RunAfterActiveCompositionCancellation(() => ActivateInputMethodCore(inputMethod));
    }

    private void ActivateInputMethodCore(SoftKeyboardInputMethod inputMethod)
    {
        if (_platform.InputMethods.TryActivate(inputMethod))
        {
            _preferredImeInputMethod = inputMethod;
            RefreshInputMethods();
            _ = TryChangeInputState(InputMethodState.On);
            RefreshImeState();
            RebuildLayout();
            QueueInputMethodStateRefresh();
        }
    }

    private SoftKeyboardInputMethod? FindPreferredImeInputMethod()
    {
        var remembered = _preferredImeInputMethod is null
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
        var uiLanguage = unchecked((ushort)CultureInfo.CurrentUICulture.LCID);
        var formattingLanguage = unchecked((ushort)CultureInfo.CurrentCulture.LCID);

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

    private static bool SamePrimaryLanguage(ushort left, ushort right)
    {
        return (left & 0x03FF) == (right & 0x03FF);
    }

    private void CommitCandidate(SoftKeyboardCandidate candidate)
    {
        // The TSF bridge replaces this handler with an active candidate behavior object. Retaining
        // the candidate click path here keeps the template independent from COM callback lifetimes.
        if (_session is not null && CanPresentCandidates && _platform.Candidates.TryCommit(this, candidate.Index))
        {
            ReplaceCandidates(Array.Empty<SoftKeyboardCandidate>(), SoftKeyboardCandidatePresentationState.None);
        }
    }

    private void RequestSystemKeyboardHide()
    {
        if (!IsSystemKeyboardSuppressionRequired)
        {
            ReleaseSystemKeyboardSuppression(tryRestore: true);
            return;
        }

        // UIElement.Focus calls TipTsfHelper.Show AFTER Keyboard.Focus has raised GotKeyboardFocus.
        // A hide inside that event alone loses to WPF's subsequent show. Background also runs after
        // the Input-priority operation that TipTsfHelper queues when dispatcher processing is disabled.
        // Coalesce requests, but never throttle away this final hide when editors change quickly.
        if (_inputPaneHideOperation is null)
        {
            _inputPaneHideOperation = Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _inputPaneHideOperation = null;
                HideSystemKeyboardIfRequired();
            }));
        }

        // Windows can finish its own focus tracking asynchronously, after WPF's dispatcher work.
        // Keep a low-frequency guard only for a live foreground editor. TryHide is advisory, so a
        // successful request is not a permanent policy and must not disable subsequent checks.
        _inputPaneHideTimer ??= new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            OnInputPaneHideTimerTick,
            Dispatcher
        );

        _inputPaneHideTimer.Start();

        var now = Environment.TickCount;
        var elapsed = unchecked(now - _lastInputPaneHideTick);
        if (_lastInputPaneHideTick != 0 && elapsed is >= 0 and < 250)
        {
            return;
        }

        _lastInputPaneHideTick = now;

        HideSystemKeyboardIfRequired();
    }

    /// <summary>
    /// Rechecks ownership at execution time so deferred work cannot hide another editor's input UI.
    /// No global Windows setting, WPF AppContext switch, or input-method state is changed.
    /// </summary>
    private void HideSystemKeyboardIfRequired()
    {
        if (!IsSystemKeyboardSuppressionRequired)
        {
            ReleaseSystemKeyboardSuppression(tryRestore: false);
            return;
        }

        var window = _window ?? Window.GetWindow(this);
        var handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;
        var result = _platform.InputPane.TryHide(handle);
        if (result == SoftKeyboardSystemKeyboardSuppressionState.Hidden)
        {
            _systemKeyboardHiddenByThisControl = true;
        }

        SetValue(SystemKeyboardSuppressionStatePropertyKey, result);
        if (result == SoftKeyboardSystemKeyboardSuppressionState.Unavailable)
        {
            // Unsupported WinRT platforms should not repeatedly attempt activation. A later focus
            // or visibility transition may retry; ordinary Windows rejection remains recoverable.
            _inputPaneHideTimer?.Stop();
        }
    }

    private void OnInputPaneHideTimerTick(object? sender, EventArgs eventArgs) => HideSystemKeyboardIfRequired();

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
                || !IsEnabled
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
        _inputPaneHideOperation?.Abort();
        _inputPaneHideOperation = null;
        _inputPaneHideTimer?.Stop();

        var shouldRestore = tryRestore
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

        var window = _window ?? Window.GetWindow(this);
        var handle = window is null ? IntPtr.Zero : new WindowInteropHelper(window).Handle;

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

    /// <summary>
    /// Gets a value indicating whether the live editor and visible surface can own candidates.
    /// Remembered targets may restore focus for a key, but must never capture another editor's IME UI.
    /// </summary>
    internal bool CanPresentCandidates
    {
        get =>
            ImeState == InputMethodState.On
            && ActiveInputMethod is not null
            && IsSystemKeyboardSuppressionRequired;
    }

    private void OnIsEnabledChanged(object sender, DependencyPropertyChangedEventArgs eventArgs)
    {
        if (!IsEnabled)
        {
            _ = TryCancelActiveComposition();
            CancelKeyInteraction();
            CloseFlyouts();
            _platform.Candidates.ReleasePresentation(this);
            ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
        else if (IsLoaded && IsOpen)
        {
            _platform.Candidates.Refresh(this);
            RequestSystemKeyboardHide();
        }
    }

    /// <summary>
    /// Asks the active TIP to discard its mutable pre-edit transaction by sending Escape to the
    /// editor that originated the matching WPF composition.
    /// </summary>
    /// <remarks>
    /// TSF TerminateComposition only removes the composing property and may preserve or commit the
    /// covered text. Escape retains the input method's own cancellation semantics (for example,
    /// Microsoft Pinyin removes its unconfirmed phonetic text). The request is sent only while the
    /// originating editor still owns keyboard focus and its Window owns the foreground; otherwise
    /// SendInput could affect another target.
    /// </remarks>
    private bool TryCancelActiveComposition(IInputElement? expectedTarget = null)
    {
        if (_activeTextComposition is null || _compositionTarget is null)
        {
            return true;
        }

        if (_compositionCancellationRequested)
        {
            // Escape is already ahead of any directly injected text in the Win32 input queue.
            return true;
        }

        var target = expectedTarget ?? _compositionTarget;
        if (!ReferenceEquals(target, _compositionTarget))
        {
            return true;
        }

        if (!ReferenceEquals(Keyboard.FocusedElement, target)
            || !IsWindowForeground()
            || !_platform.Input.SendVirtualKey(Key.Escape, shift: false))
        {
            return false;
        }

        // SendInput preserves packet order, so a directly following Unicode packet cannot overtake
        // Escape. Retain the WPF composition until its final event; if the TIP ignores the request,
        // reset the guard after input processing so the next semantic boundary can retry safely.
        _compositionCancellationRequested = true;
        var requestedComposition = _activeTextComposition;
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                if (ReferenceEquals(_activeTextComposition, requestedComposition))
                {
                    _compositionCancellationRequested = false;
                }
            })
        );
        return true;
    }

    /// <summary>
    /// Defers an operation that mutates TSF input-method state until WPF has observed the requested
    /// cancellation completing.
    /// </summary>
    /// <remarks>
    /// SendInput only places Escape in the foreground thread's input queue; it does not process the
    /// key before returning. Changing the active TIP or its open compartment in the same callback
    /// could therefore run ahead of cancellation and cause the outgoing TIP to preserve raw text.
    /// ContextIdle runs after ordinary input dispatch. If no final composition event arrives, the
    /// operation is deliberately abandoned instead of converting uncertain pre-edit text.
    /// </remarks>
    private void RunAfterActiveCompositionCancellation(Action operation)
    {
        if (_activeTextComposition is null)
        {
            operation();
            return;
        }

        if (!TryCancelActiveComposition())
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(() =>
            {
                // A still-active composition means either the TIP ignored Escape or a fresh
                // composition has already started. Neither case is safe for changing TSF state.
                if (_activeTextComposition is not null)
                {
                    return;
                }

                operation();
            })
        );
    }

    private void ClearActiveComposition()
    {
        _activeTextComposition = null;
        _compositionTarget = null;
        _compositionCancellationRequested = false;
    }

    private void CloseKeyboard()
    {
        if (TryCancelActiveComposition())
        {
            SetCurrentValue(IsOpenProperty, false);
        }
    }

    private void CancelKeyInteraction()
    {
        // Clear ownership before calling the child: closing its popup may synchronously raise
        // routed/lifecycle events. The child alone owns timers, capture and submission suppression.
        var keyControl = _activeKeyControl;
        _activeKeyControl = null;
        keyControl?.CancelInteraction();
    }

    private void CloseFlyouts()
    {
        CancelKeyInteraction();
        if (_inputMethodMenu is not null)
        {
            _inputMethodMenu.SetCurrentValue(ContextMenu.IsOpenProperty, false);
        }
    }

    private static bool IsCharacterKey(Key key)
    {
        return key is >= Key.A and <= Key.Z;
    }
}
