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
using Wpf.Zen.Ui.VirtualInput.AutomationPeers;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.VirtualInput.Controls;

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
/// The optional <c>PART_Candidates</c> list binds Candidates and SelectedCandidateIndex.
/// It owns containers and scrolling and reports CandidateInvoked without taking editor focus.
/// Keys use <see cref="SoftKeyboardKeyControl"/> with its Key property bound to the layout item.
/// Each cap owns its gesture and alternative popup; this control only coordinates cancellation
/// and executes semantic input requests against the active editor.
/// </para>
/// </remarks>
[TemplatePart(Name = KeysPartName, Type = typeof(ItemsControl))]
[TemplatePart(Name = CandidatesPartName, Type = typeof(SoftKeyboardCandidateList))]
[TemplatePart(Name = PreviousCandidatePageButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = NextCandidatePageButtonPartName, Type = typeof(ButtonBase))]
[TemplatePart(Name = SettingsButtonPartName, Type = typeof(Wpf.Zen.Ui.Controls.DropDownButton))]
public partial class SoftKeyboard : Control
{
    private const string KeysPartName                        = "PART_Keys";
    private const string CandidatesPartName                  = "PART_Candidates";
    private const string PreviousCandidatePageButtonPartName = "PART_PreviousCandidatePageButton";
    private const string NextCandidatePageButtonPartName     = "PART_NextCandidatePageButton";

    private readonly ObservableCollection<SoftKeyboardCandidate>   _candidates   = [];
    private readonly ObservableCollection<SoftKeyboardInputMethod> _inputMethods = [];
    private readonly SoftKeyboardPlatformServices                  _platform;

    private SoftKeyboardDispatcherSession? _session;
    private Window?                        _window;
    private TextComposition?               _activeTextComposition;
    private IInputElement?                 _compositionTarget;
    private bool                           _compositionCancellationRequested;
    private SoftKeyboardInputMethod?       _preferredImeInputMethod;
    private SoftKeyboardKeyControl?        _activeKeyControl;
    private UIElement?                     _inputMethodMenuAnchor;
    private int                            _inputOperationVersion;
    private int                            _lastShiftClickTick;
    private int                            _lastInputPaneHideTick;
    private DispatcherOperation?           _inputPaneHideOperation;
    private DispatcherTimer?               _inputPaneHideTimer;
    private bool                           _systemKeyboardHiddenByThisControl;

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

        InitializeSettings();

        AddHandler(
            SoftKeyboardCandidateList.CandidateInvokedEvent,
            new EventHandler<SoftKeyboardCandidateInvokedEventArgs>(OnCandidateInvoked)
        );

        Candidates   = [with(_candidates)];
        InputMethods = [with(_inputMethods)];

        // todo: 按钮事件是哪些按钮的事件？
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

    /// <summary>
    /// Gets the candidates supplied by the current TSF input method.
    /// </summary>
    public ReadOnlyObservableCollection<SoftKeyboardCandidate> Candidates { get; }

    /// <summary>
    /// Gets the enabled Windows input methods.
    /// </summary>
    public ReadOnlyObservableCollection<SoftKeyboardInputMethod> InputMethods { get; }

    /// <summary>
    /// Changes the visible keyboard surface and ends any pending long-press gesture.
    /// </summary>
    /// <remarks>
    /// Explicit letter, number, and symbol surfaces override the editor input scope. Automatic
    /// chooses from that scope again. A new active editor resets the mode to Automatic.
    /// If the active input method owns unconfirmed composition
    /// text, the control asks it to cancel that text before crossing the surface boundary.
    /// </remarks>
    /// <param name="page">The surface to display.</param>
    public void ShowPage(string page)
    {
        VerifyAccess();

        if (string.IsNullOrWhiteSpace(page))
        {
            throw new ArgumentOutOfRangeException(nameof(page));
        }

        if (page != Page && !TryCancelActiveComposition())
        {
            // Direct-text surfaces must not be entered while a TIP still owns mutable pre-edit
            // text. Keeping the current surface is the only non-destructive fallback when Windows
            // cannot accept the Escape request.
            return;
        }

        CancelKeyInteraction();
        SetValue(PagePropertyKey, page);
        CloseFlyouts();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer()
    {
        return new SoftKeyboardAutomationPeer(this);
    }

    /// <summary>
    /// Ends gestures belonging to the previous visual tree when the template changes.
    /// </summary>
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

    private void ClearActiveComposition()
    {
        _activeTextComposition            = null;
        _compositionTarget                = null;
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
        SettingsMenu?.SetCurrentValue(ContextMenu.IsOpenProperty, false);

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
