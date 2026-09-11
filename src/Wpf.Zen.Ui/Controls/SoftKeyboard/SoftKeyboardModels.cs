// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

#pragma warning disable SA1402, SA1649 // The compact public data contract is intentionally discoverable in one source file.

/// <summary>Specifies how a <see cref="SoftKeyboard"/> reacts to editor focus.</summary>
public enum SoftKeyboardAutoShowMode
{
    /// <summary>The application controls <see cref="SoftKeyboard.IsOpen"/> explicitly.</summary>
    Disabled,

    /// <summary>The keyboard opens when touch or pen input gives focus to a supported editor.</summary>
    TouchAndPen,

    /// <summary>The keyboard opens whenever a supported editor receives keyboard focus.</summary>
    AnyFocus,
}

/// <summary>Specifies when an automatically opened <see cref="SoftKeyboard"/> closes.</summary>
public enum SoftKeyboardAutoHideMode
{
    /// <summary>The keyboard closes after keyboard focus leaves its configured target scope.</summary>
    FocusLeavesScope,

    /// <summary>The application or the keyboard's close key must close the keyboard.</summary>
    Manual,
}

/// <summary>Names the built-in keyboard pages. Custom providers may use any other nonempty, case-sensitive identifier.</summary>
/// <remarks>Use an application-qualified name for custom pages. Page navigation is owned by the keyboard,
/// so custom pages receive the same composition cancellation and target-reset behavior as built-in pages.</remarks>
public static class SoftKeyboardPages
{
    /// <summary>Selects the initial page from the editor input scope.</summary>
    public const string Automatic = "Automatic";

    /// <summary>Identifies the alphabetic page.</summary>
    public const string Letters = "Letters";

    /// <summary>Identifies the numeric page.</summary>
    public const string Numbers = "Numbers";

    /// <summary>Identifies the first symbol page.</summary>
    public const string Symbols = "Symbols";

    /// <summary>Identifies the second symbol page.</summary>
    public const string MoreSymbols = "MoreSymbols";

    /// <summary>Gets the immutable built-in catalog; this is not a restriction on custom identifiers.</summary>
    public static IReadOnlyList<string> BuiltInPages { get; } =
        Array.AsReadOnly([Automatic, Letters, Numbers, Symbols, MoreSymbols]);
}

/// <summary>Describes the virtual Shift modifier maintained by a <see cref="SoftKeyboard"/>.</summary>
public enum SoftKeyboardShiftState
{
    /// <summary>Shift is not active.</summary>
    Off,

    /// <summary>Shift applies to the next character key and is then cleared.</summary>
    Once,

    /// <summary>Shift remains active until the user toggles it off.</summary>
    Locked,
}

/// <summary>Describes how the current input method's candidate UI is being presented.</summary>
public enum SoftKeyboardCandidatePresentationState
{
    /// <summary>No candidate list is active.</summary>
    None,

    /// <summary>The candidate list is rendered inside the soft keyboard.</summary>
    Embedded,

    /// <summary>The input method cannot be controlled safely, so its native candidate window remains visible.</summary>
    NativeFallback,

    /// <summary>TSF candidate integration is unavailable on the current runtime.</summary>
    Unavailable,
}

/// <summary>Describes the result of suppressing the Windows input pane.</summary>
public enum SoftKeyboardSystemKeyboardSuppressionState
{
    /// <summary>
    /// The control owns no active suppression request. Windows may independently show or hide its pane.
    /// </summary>
    NotRequested,

    /// <summary>Windows accepted the request to hide its input pane.</summary>
    Hidden,

    /// <summary>The window-specific input-pane API is unavailable.</summary>
    Unavailable,

    /// <summary>Windows rejected the hide request, normally because the window is not foreground.</summary>
    Rejected,
}

/// <summary>Identifies the semantic action represented by a <see cref="SoftKeyboardKey"/>.</summary>
public enum SoftKeyboardKeyKind
{
    /// <summary>Sends a virtual key using the active Windows keyboard layout.</summary>
    VirtualKey,

    /// <summary>Inserts a Unicode string, used for symbols and key alternatives.</summary>
    Text,

    /// <summary>Changes the keyboard's Shift state.</summary>
    Shift,

    /// <summary>Changes the visible keyboard surface.</summary>
    ChangePage,

    /// <summary>Opens the input-method context menu.</summary>
    InputMethods,

    /// <summary>Toggles the active input method between native composition and direct alphanumeric input.</summary>
    ToggleIme,

    /// <summary>Closes the keyboard.</summary>
    Close,

    /// <summary>Invokes a custom action through the keyboard's validated input services.</summary>
    Custom,
}

/// <summary>
/// Supplies the stable information a layout provider needs to construct a keyboard surface.
/// </summary>
public sealed class SoftKeyboardLayoutContext
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardLayoutContext"/> class.</summary>
    /// <param name="culture">The culture associated with the active input language.</param>
    /// <param name="inputScope">The semantic input scope requested by the active editor.</param>
    /// <param name="page">The surface requested by the keyboard.</param>
    /// <param name="shiftState">The current virtual Shift state.</param>
    /// <param name="imeState">The observed native/direct input state; unknown by default.</param>
    /// <param name="inputMethod">The active profile, allowing language- and vendor-specific layouts.</param>
    /// <param name="keyboardLayoutHandle">Borrowed Windows HKL used for key labels; zero captures the current thread's layout.</param>
    public SoftKeyboardLayoutContext(
        CultureInfo culture,
        InputScopeNameValue inputScope,
        string page,
        SoftKeyboardShiftState shiftState,
        InputMethodState imeState = InputMethodState.DoNotCare,
        SoftKeyboardInputMethod? inputMethod = null,
        IntPtr keyboardLayoutHandle = default
    )
    {
        if (culture is null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        Culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());
        KeyboardLayoutHandle = keyboardLayoutHandle == IntPtr.Zero ? KeyboardLayoutLabels.GetCurrentLayout() : keyboardLayoutHandle;
        InputScope = inputScope;
        Page = !string.IsNullOrWhiteSpace(page) ? page : throw new ArgumentException("A page identifier is required.", nameof(page));
        ShiftState = shiftState;
        ImeState = imeState;
        InputMethod = inputMethod;
    }

    /// <summary>Gets the culture of the active Windows input language.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Gets the borrowed Windows HKL captured for this snapshot; providers must not unload it.</summary>
    /// <remarks>Key labels and scan-code mapping must use this handle rather than re-reading ambient thread state per key.</remarks>
    public IntPtr KeyboardLayoutHandle { get; }

    /// <summary>Gets the WPF input scope requested by the active target.</summary>
    public InputScopeNameValue InputScope { get; }

    /// <summary>Gets the surface the provider must create.</summary>
    public string Page { get; }

    /// <summary>Gets the current virtual Shift state.</summary>
    public SoftKeyboardShiftState ShiftState { get; }

    /// <summary>Gets the observed native/direct input state, never an optimistic toggle request.</summary>
    public InputMethodState ImeState { get; }

    /// <summary>Gets the active profile identity, or null when Windows exposes none.</summary>
    public SoftKeyboardInputMethod? InputMethod { get; }
}

/// <summary>Creates keyboard layouts for a <see cref="SoftKeyboard"/>.</summary>
/// <remarks>
/// Providers own language- and input-method-specific key selection, placement, labels and actions.
/// The keyboard requests a complete snapshot when its input context changes; providers should use
/// that context to resolve Shift, display pages and IME modes together. Return null to decline a context;
/// use CompositeSoftKeyboardLayoutProvider to reuse other providers without copying their layouts. The visual template only
/// presents the resulting keys and does not interpret language rules or select layout variants.
/// </remarks>
public interface ISoftKeyboardLayoutProvider
{
    /// <summary>Creates an immutable layout for the supplied input context.</summary>
    /// <param name="context">The active language, input scope, display mode, Shift state, IME state and input-method profile.</param>
    /// <returns>A complete immutable snapshot, or null when this provider does not handle the context.</returns>
    SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context);
}

/// <summary>
/// Adapts a custom WPF editor to the focus and input-scope contract used by <see cref="SoftKeyboard"/>.
/// </summary>
/// <remarks>
/// The keyboard still delivers ordinary keyboard input through Windows. An adapter declares eligibility,
/// read-only state, input scope, and how focus should be restored; it does not receive or store typed text.
/// </remarks>
public interface ISoftKeyboardTargetAdapter
{
    /// <summary>Returns whether this adapter recognizes <paramref name="target"/>.</summary>
    bool IsSupported(IInputElement target);

    /// <summary>Returns whether <paramref name="target"/> can currently accept user input.</summary>
    bool CanReceiveInput(IInputElement target);

    /// <summary>Gets the semantic input scope for <paramref name="target"/>.</summary>
    InputScopeNameValue GetInputScope(IInputElement target);

    /// <summary>Attempts to give keyboard focus to <paramref name="target"/>.</summary>
    bool TryFocus(IInputElement target);
}

/// <summary>Represents one immutable, flat collection of keys positioned on a shared logical grid.</summary>
/// <remarks>
/// A layout deliberately exposes no row-container hierarchy. Each <see cref="SoftKeyboardKey"/>
/// carries its own <see cref="SoftKeyboardKey.Row"/>, <see cref="SoftKeyboardKey.Column"/>, and
/// <see cref="SoftKeyboardKey.ColumnSpan"/>. Every row shares <see cref="ColumnCount"/>, so omitted
/// leading or trailing cells remain empty instead of stretching a shorter row across the surface.
/// </remarks>
public sealed class SoftKeyboardLayout
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardLayout"/> class.</summary>
    /// <param name="keys">Positioned keys in reading order.</param>
    /// <param name="columnCount">
    /// The positive number of equal-width logical columns, or <see langword="null"/> to infer the
    /// smallest count containing every key. An empty inferred layout uses one column.
    /// </param>
    /// <remarks>
    /// Supplying the count explicitly lets a layout reserve empty trailing columns. Every key must
    /// fit completely inside the declared grid; overlapping keys are permitted so specialized
    /// providers can intentionally layer content.
    /// </remarks>
    public SoftKeyboardLayout(IEnumerable<SoftKeyboardKey> keys, int? columnCount = null)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (columnCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        SoftKeyboardKey[] keyArray = keys.ToArray();
        if (keyArray.Any(static key => key is null))
        {
            throw new ArgumentException("A keyboard layout cannot contain a null key.", nameof(keys));
        }

        long requiredColumnCount64 = keyArray.Length == 0
            ? 1L
            : keyArray.Max(static key => (long)key.Column + key.ColumnSpan);
        if (requiredColumnCount64 > int.MaxValue)
        {
            throw new ArgumentException("A key placement exceeds the supported logical grid range.", nameof(keys));
        }

        int requiredColumnCount = (int)requiredColumnCount64;
        int resolvedColumnCount = columnCount ?? requiredColumnCount;
        if (requiredColumnCount > resolvedColumnCount)
        {
            throw new ArgumentException(
                "Every key must fit inside the declared logical column count.",
                nameof(columnCount)
            );
        }

        Keys = new ReadOnlyCollection<SoftKeyboardKey>(keyArray);
        ColumnCount = resolvedColumnCount;
    }

    /// <summary>
    /// Gets the positioned keys in reading order. Items with the same row identifier retain this order.
    /// </summary>
    public IReadOnlyList<SoftKeyboardKey> Keys { get; }

    /// <summary>
    /// Gets the number of equal-width logical columns shared by every row in the layout.
    /// </summary>
    public int ColumnCount { get; }
}

/// <summary>Describes one immutable key shown by a <see cref="SoftKeyboard"/>.</summary>
public sealed class SoftKeyboardKey
{
    /// <summary>Creates an unpositioned literal key suitable for adding to a layout builder row.</summary>
    /// <param name="text">The literal text, also used for the visible cap and automation name.</param>
    /// <returns>A text key using the keyboard's guarded Unicode input pipeline.</returns>
    public static SoftKeyboardKey FromText(string text)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Text, text, text, text: text);
    }

    /// <summary>Creates an unpositioned page-navigation key for a built-in or custom page.</summary>
    /// <param name="page">Destination page identifier.</param>
    /// <param name="content">Presentation data for the key cap.</param>
    /// <param name="automationName">Accessible description of the destination.</param>
    /// <returns>A navigation key that preserves composition cancellation.</returns>
    public static SoftKeyboardKey Navigate(string page, object content, string automationName)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.ChangePage, content, automationName, page: page);
    }

    /// <summary>Creates an unpositioned key with an application or language-specific function.</summary>
    /// <param name="action">Reusable custom action receiving guarded keyboard services.</param>
    /// <param name="content">Presentation data for the key cap.</param>
    /// <param name="automationName">Accessible description of the function.</param>
    /// <returns>A custom key with repetition disabled by default.</returns>
    public static SoftKeyboardKey FromAction(ISoftKeyboardKeyAction action, object content, string automationName)
    {
        return new SoftKeyboardKey(SoftKeyboardKeyKind.Custom, content, automationName, action: action);
    }

    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardKey"/> class.</summary>
    /// <param name="kind">The semantic action performed by the key.</param>
    /// <param name="content">
    /// The content displayed on the key. Strings are rendered as ordinary text; data objects such
    /// as <see cref="IconSource"/> are resolved through WPF data templates.
    /// </param>
    /// <param name="automationName">The descriptive name exposed to UI Automation.</param>
    /// <param name="key">The WPF virtual key for <see cref="SoftKeyboardKeyKind.VirtualKey"/>.</param>
    /// <param name="text">The Unicode value for <see cref="SoftKeyboardKeyKind.Text"/>.</param>
    /// <param name="page">The destination for <see cref="SoftKeyboardKeyKind.ChangePage"/>.</param>
    /// <param name="row">
    /// The nonnegative logical row identifier. Identifiers group and order keys; skipped identifiers
    /// do not reserve empty visual rows.
    /// </param>
    /// <param name="column">The zero-based logical column at which the key begins.</param>
    /// <param name="columnSpan">The positive number of logical columns occupied by the key.</param>
    /// <param name="alternates">Optional long-press Unicode alternatives.</param>
    /// <param name="isRepeatEnabled">Whether holding this key repeats its primary action. Defaults to false; alternatives take precedence.</param>
    /// <param name="alternateHint">Optional key-cap text previewing selected alternatives; null hides the hint. This is presentation only and does not define selectable values.</param>
    /// <param name="action">Required for a Custom key and forbidden for other kinds. Must not retain an editor or execution context.</param>
    /// <exception cref="ArgumentException">The key lacks presentation, accessible naming or input data,
    /// or an alternative is null or empty. Whitespace is valid input when its cap has a descriptive label.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The kind, virtual key or placement is invalid.</exception>
    public SoftKeyboardKey(
        SoftKeyboardKeyKind kind,
        object content,
        string automationName,
        Key key = Key.None,
        string? text = null,
        string page = SoftKeyboardPages.Letters,
        int row = 0,
        int column = 0,
        int columnSpan = 1,
        IEnumerable<string>? alternates = null,
        bool isRepeatEnabled = false,
        string? alternateHint = null,
        ISoftKeyboardKeyAction? action = null)
    {
        if (content is null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (content is string textContent && string.IsNullOrWhiteSpace(textContent))
        {
            throw new ArgumentException("Text key content cannot be empty.", nameof(content));
        }

        if (string.IsNullOrWhiteSpace(automationName))
        {
            throw new ArgumentException("An automation name is required.", nameof(automationName));
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (column < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(column));
        }

        if (columnSpan <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnSpan));
        }

        if (!Enum.IsDefined(typeof(SoftKeyboardKeyKind), kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if ((kind == SoftKeyboardKeyKind.Custom) != (action is not null))
        {
            throw new ArgumentException("Only custom keys must supply an action.", nameof(action));
        }

        // Validate executable payloads at the immutable model boundary, before a provider's layout
        // reaches a key cap. A visible but nonfunctional key must not silently enter the input path.
        if (kind == SoftKeyboardKeyKind.VirtualKey
            && (!Enum.IsDefined(typeof(Key), key) || KeyInterop.VirtualKeyFromKey(key) <= 0))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        if (kind == SoftKeyboardKeyKind.Text && string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("A text key requires nonempty input text.", nameof(text));
        }

        var alternateSnapshot = (alternates ?? Array.Empty<string>()).ToArray();
        if (alternateSnapshot.Any(string.IsNullOrEmpty))
        {
            throw new ArgumentException("Alternatives must contain nonempty input text.", nameof(alternates));
        }

        Action         = action;
        Kind           = kind;
        Content        = content;
        AutomationName = automationName;
        Key            = key;
        Text           = text;

        Page = !string.IsNullOrWhiteSpace(page)
            ? page
            : throw new ArgumentException("A page identifier is required.", nameof(page));

        Row             = row;
        Column          = column;
        ColumnSpan      = columnSpan;
        Alternates      = new ReadOnlyCollection<string>(alternateSnapshot);
        IsRepeatEnabled = isRepeatEnabled;
        AlternateHint   = string.IsNullOrWhiteSpace(alternateHint) ? null : alternateHint;
    }

    /// <summary>Gets the action performed by the key.</summary>
    public SoftKeyboardKeyKind Kind { get; }

    /// <summary>Gets the custom action, or null for a built-in key kind.</summary>
    /// <remarks>The action is reusable behavior, not mutable per-keyboard state. Execution receives a short-lived context.</remarks>
    public ISoftKeyboardKeyAction? Action { get; }

    /// <summary>Gets the visual content presented on the key.</summary>
    /// <remarks>
    /// This slot is designed for reusable presentation data rather than a shared visual element.
    /// Values such as <see cref="FluentIconSource"/> allow the keyboard's content presenter to
    /// create an independent visual for every key. Custom layout providers may supply their own
    /// data objects together with an implicit <see cref="System.Windows.DataTemplate"/>.
    /// </remarks>
    public object Content { get; }

    /// <summary>Gets the descriptive UI Automation name.</summary>
    public string AutomationName { get; }

    /// <summary>Gets the WPF virtual key, or <see cref="Key.None"/> for a non-virtual key action.</summary>
    public Key Key { get; }

    /// <summary>Gets the Unicode text inserted by a text key.</summary>
    public string? Text { get; }

    /// <summary>Gets the destination surface selected by a display-mode key.</summary>
    public string Page { get; }

    /// <summary>
    /// Gets the nonnegative logical row identifier consumed by the default keyboard item-container style.
    /// </summary>
    public int Row { get; }

    /// <summary>
    /// Gets the zero-based logical column at which the key begins. Keys are not automatically flowed
    /// after one another; layout providers assign this coordinate explicitly.
    /// </summary>
    public int Column { get; }

    /// <summary>
    /// Gets the positive number of equal-width logical columns occupied by the key.
    /// </summary>
    public int ColumnSpan { get; }

    /// <summary>Gets the long-press Unicode alternatives.</summary>
    public IReadOnlyList<string> Alternates { get; }

    /// <summary>Gets the optional preview displayed above the primary key content; defaults to null.</summary>
    /// <remarks>
    /// Providers may preview only a subset of alternatives to keep caps readable. The default template
    /// reserves a separate text row when this hint is present, so it cannot overlap the primary content.
    /// The hint never changes the primary action or the complete long-press list in <see cref="Alternates"/>.
    /// Whitespace-only hints are normalized to null. Custom templates may omit this presentation slot.
    /// </remarks>
    public string? AlternateHint { get; }

    /// <summary>Gets whether the key permits repeated primary requests while held. The layout provider owns this policy.</summary>
    public bool IsRepeatEnabled { get; }

    // Provider helpers position semantic key descriptions by cloning them. This keeps the public
    // model immutable and prevents a shared key instance from acquiring mutable placement state.
    internal SoftKeyboardKey WithPlacement(int row, int column, int columnSpan) =>
        new(Kind, Content, AutomationName, Key, Text, Page, row, column, columnSpan, Alternates, IsRepeatEnabled, AlternateHint, Action);
}

/// <summary>Represents one candidate supplied by the active Windows input method.</summary>
public sealed class SoftKeyboardCandidate
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardCandidate"/> class.</summary>
    /// <param name="index">The input method index, not the page-relative position.</param>
    /// <param name="text">The candidate text.</param>
    public SoftKeyboardCandidate(uint index, string text)
    {
        Index = index;
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    /// <summary>Gets the zero-based candidate index understood by the input method.</summary>
    public uint Index { get; }

    /// <summary>Gets the candidate text.</summary>
    public string Text { get; }
}

/// <summary>Describes one enabled Windows input method or keyboard layout.</summary>
public sealed class SoftKeyboardInputMethod
{
    internal SoftKeyboardInputMethod(Guid classId, Guid profileId, ushort languageId, string displayName, bool isActive)
    {
        ClassId = classId;
        ProfileId = profileId;
        LanguageId = languageId;
        DisplayName = displayName;
        IsActive = isActive;
    }

    /// <summary>Gets the TSF text-service class identifier.</summary>
    public Guid ClassId { get; }

    /// <summary>Gets the TSF language-profile identifier.</summary>
    public Guid ProfileId { get; }

    /// <summary>Gets the Windows language identifier.</summary>
    public ushort LanguageId { get; }

    /// <summary>Gets the localized profile name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets a value indicating whether this profile was active when the list was refreshed.</summary>
    public bool IsActive { get; }

    /// <inheritdoc />
    public override string ToString() => DisplayName;
}

#pragma warning restore SA1402, SA1649
