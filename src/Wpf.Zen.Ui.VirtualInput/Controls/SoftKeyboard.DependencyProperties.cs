// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.AutomationPeers;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Defines the control's dependency-property surface and property-change invariants.
/// </summary>
public partial class SoftKeyboard
{
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
            new PropertyMetadata(
                SoftKeyboardAutoShowMode.Disabled,
                (d, _) => ((SoftKeyboard)d).RefreshSettings()
            ),
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
            new PropertyMetadata(
                SoftKeyboardAutoHideMode.FocusLeavesScope,
                (d, _) => ((SoftKeyboard)d).RefreshSettings()
            ),
            static value => value is SoftKeyboardAutoHideMode mode
                && Enum.IsDefined(typeof(SoftKeyboardAutoHideMode), mode)
        );

    /// <summary>
    /// Gets or sets when the keyboard closes, independently of its automatic opening policy.
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
                null,
                OnLayoutContextChanged
            )
        );

    /// <summary>
    /// Gets or sets the provider that creates positioned keys for the active input context.
    /// </summary>
    /// <remarks>
    /// Defaults to null. A non-null application provider has priority over extensions and the
    /// built-in provider; use CompositeSoftKeyboardLayoutProvider for ordered partial overrides.
    /// Replacing this non-inherited property cancels key interactions and rebuilds the surface.
    /// LayoutError exposes provider failures while a usable built-in layout is retained.
    /// </remarks>
    [Bindable(true)]
    [Category("Behavior")]
    public ISoftKeyboardLayoutProvider? LayoutProvider
    {
        get => (ISoftKeyboardLayoutProvider?)GetValue(LayoutProviderProperty);
        set => SetValue(LayoutProviderProperty, value);
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

    #region Read-Only DP: Page

    private static readonly DependencyPropertyKey PagePropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(Page),
            typeof(string),
            typeof(SoftKeyboard),
            new PropertyMetadata(
                SoftKeyboardPages.Automatic,
                OnPageChanged
            )
        );

    /// <summary>
    /// Identifies the <see cref="Page"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty PageProperty
        = PagePropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the requested surface. The default, Automatic, follows the active editor input scope.
    /// </summary>
    /// <remarks>
    /// Page identifiers are case-sensitive and extensible. Changes rebuild the key surface; the
    /// property does not inherit and cannot be animated. An unhandled page falls back to Automatic
    /// and records a LayoutError, keeping navigation state consistent with the displayed layout.
    /// </remarks>
    public string Page
    {
        get => (string)GetValue(PageProperty);
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

    /// <summary>
    /// Gets how the active input method's candidate UI is being presented.
    /// </summary>
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

    #region Read-Only DP: SelectedCandidateIndex

    private static readonly DependencyPropertyKey SelectedCandidateIndexPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(SelectedCandidateIndex),
            typeof(uint?),
            typeof(SoftKeyboard),
            new PropertyMetadata(null)
        );

    /// <summary>
    /// Identifies the <see cref="SelectedCandidateIndex"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SelectedCandidateIndexProperty
        = SelectedCandidateIndexPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the input method's current candidate index, or null when none is current.
    /// </summary>
    /// <remarks>
    /// This is a backend index, not a position within the visible page. Changes update presentation only.
    /// </remarks>
    public uint? SelectedCandidateIndex => (uint?)GetValue(SelectedCandidateIndexProperty);

    #endregion

    #endregion

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        SoftKeyboardAutomationPeer.RaiseIsOpenChanged(
            keyboard,
            (bool)args.OldValue,
            (bool)args.NewValue
        );

        if ((bool)args.NewValue)
        {
            keyboard.ResolveTarget();
            keyboard.RebuildLayout();
            keyboard.RefreshInputMethods();
            keyboard.RefreshImeState();
            keyboard.RequestSystemKeyboardHide();

            keyboard.RequestCandidateRefresh();
        }
        else
        {
            keyboard._inputOperationVersion++;

            _ = keyboard.TryCancelActiveComposition();

            keyboard.CancelKeyInteraction();
            keyboard.CloseFlyouts();

            keyboard.ResetCandidateSession();
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: true);
        }
    }

    private static void OnTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        if (args.NewValue is IInputElement newTarget
            && keyboard.ActiveTarget is { } oldTarget
            && !ReferenceEquals(oldTarget, newTarget))
        {
            _ = keyboard.TryCancelActiveComposition(oldTarget);
        }

        keyboard.ResolveTarget();
    }

    private static void OnTargetAdapterChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        _ = keyboard.TryCancelActiveComposition();

        keyboard._inputOperationVersion++;

        keyboard.ResolveTarget();

        // The adapter can change the input scope without changing editor identity.
        keyboard.RebuildLayout();
    }

    private static void OnTargetScopeChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        if (keyboard.ActiveTarget is { } activeTarget and DependencyObject activeTargetObject
            && !keyboard.IsWithinEffectiveScope(activeTargetObject))
        {
            _ = keyboard.TryCancelActiveComposition(activeTarget);
        }

        keyboard.ResolveTarget();

        if (keyboard.ActiveTarget is DependencyObject resolvedTarget
            && !keyboard.IsWithinEffectiveScope(resolvedTarget))
        {
            keyboard.SetValue(ActiveTargetPropertyKey, null);
        }
    }

    private static void OnActiveTargetChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        keyboard._inputOperationVersion++;

        // A gesture belongs to the editor that was active when it started. Never let a held
        // Backspace or an unselected variant continue into a newly focused document.
        keyboard.CancelKeyInteraction();
        keyboard.CloseFlyouts();

        if (args.OldValue is IInputElement oldTarget
            && !ReferenceEquals(oldTarget, args.NewValue)
            && ReferenceEquals(keyboard._compositionTarget, oldTarget))
        {
            // GotKeyboardFocus can report the new editor only after Windows has moved focus. At that
            // point injecting Escape would target the new editor, so discard only our observation;
            // WPF and TSF own composition cleanup for an external focus transition.
            keyboard.ClearActiveComposition();
        }

        // A different editor starts with its semantic input scope; explicit navigation applies
        // only while that editor remains the target.
        keyboard.SetValue(PagePropertyKey, SoftKeyboardPages.Automatic);
        keyboard.RebuildLayout();
        keyboard.ResetCandidateSession();

        if (keyboard is
            {
                IsLoaded: true,
                IsOpen: true,
                IsVisible: true,
                IsEnabled: true,
                ActiveTarget: not null
            })
        {
            keyboard.RequestCandidateRefresh();
        }

        if (args.NewValue is null)
        {
            keyboard.ReleaseSystemKeyboardSuppression(tryRestore: false);
        }
        else
        {
            keyboard.RequestSystemKeyboardHide();
        }
    }

    private static void OnPageChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        var keyboard = (SoftKeyboard)d;

        keyboard.RebuildLayout();

        // The header always reserves space, but native candidate ownership still depends on the
        // live editor and IME state. Reevaluate it immediately after a surface transition.
        if (keyboard.CanPresentCandidates)
        {
            keyboard.RequestCandidateRefresh();
        }
        else
        {
            keyboard.ReleaseCandidatePresentation();
        }
    }

    private static void OnLayoutContextChanged(DependencyObject d, DependencyPropertyChangedEventArgs args)
    {
        ((SoftKeyboard)d).RebuildLayout();
    }
}
