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

/// <summary>Identifies the surface currently presented by a <see cref="SoftKeyboard"/>.</summary>
public enum SoftKeyboardDisplayMode
{
    /// <summary>Displays alphabetic keys for the active Windows keyboard layout.</summary>
    Letters,

    /// <summary>Displays decimal and common numeric keys.</summary>
    Numbers,

    /// <summary>Displays punctuation and symbol keys.</summary>
    Symbols,

    /// <summary>Displays the built-in Emoji catalogue.</summary>
    Emoji,

    /// <summary>Displays the enabled Windows input methods.</summary>
    InputMethods,
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

    /// <summary>Inserts a Unicode string, used for symbols, alternates, and Emoji sequences.</summary>
    Text,

    /// <summary>Changes the keyboard's Shift state.</summary>
    Shift,

    /// <summary>Changes the visible keyboard surface.</summary>
    ChangeDisplayMode,

    /// <summary>Opens the embedded input-method picker.</summary>
    InputMethods,

    /// <summary>Toggles the active input method between native composition and direct alphanumeric input.</summary>
    ToggleIme,

    /// <summary>Closes the keyboard.</summary>
    Close,
}

/// <summary>Groups Emoji entries on the built-in Emoji surface.</summary>
public enum SoftKeyboardEmojiCategory
{
    /// <summary>Recently inserted Emoji, retained only for the current process.</summary>
    Recent,

    /// <summary>Faces and emotional gestures.</summary>
    Smileys,

    /// <summary>People, hands, and body gestures.</summary>
    People,

    /// <summary>Animals and nature.</summary>
    Animals,

    /// <summary>Food and drink.</summary>
    Food,

    /// <summary>Activities and celebrations.</summary>
    Activities,

    /// <summary>Travel and places.</summary>
    Travel,

    /// <summary>Objects and tools.</summary>
    Objects,

    /// <summary>Symbols and marks.</summary>
    Symbols,
}

/// <summary>
/// Supplies the stable information a layout provider needs to construct a keyboard surface.
/// </summary>
public sealed class SoftKeyboardLayoutContext
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardLayoutContext"/> class.</summary>
    /// <param name="culture">The culture associated with the active input language.</param>
    /// <param name="inputScope">The semantic input scope requested by the active editor.</param>
    /// <param name="displayMode">The surface requested by the keyboard.</param>
    /// <param name="shiftState">The current virtual Shift state.</param>
    public SoftKeyboardLayoutContext(
        CultureInfo culture,
        InputScopeNameValue inputScope,
        SoftKeyboardDisplayMode displayMode,
        SoftKeyboardShiftState shiftState
    )
    {
        Culture = culture ?? throw new ArgumentNullException(nameof(culture));
        InputScope = inputScope;
        DisplayMode = displayMode;
        ShiftState = shiftState;
    }

    /// <summary>Gets the culture of the active Windows input language.</summary>
    public CultureInfo Culture { get; }

    /// <summary>Gets the WPF input scope requested by the active target.</summary>
    public InputScopeNameValue InputScope { get; }

    /// <summary>Gets the surface the provider must create.</summary>
    public SoftKeyboardDisplayMode DisplayMode { get; }

    /// <summary>Gets the current virtual Shift state.</summary>
    public SoftKeyboardShiftState ShiftState { get; }
}

/// <summary>Creates keyboard layouts for a <see cref="SoftKeyboard"/>.</summary>
public interface ISoftKeyboardLayoutProvider
{
    /// <summary>Creates an immutable layout for the supplied input context.</summary>
    /// <param name="context">The active language, scope, display mode, and Shift state.</param>
    /// <returns>A complete keyboard layout.</returns>
    SoftKeyboardLayout CreateLayout(SoftKeyboardLayoutContext context);
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

/// <summary>Represents one immutable keyboard layout.</summary>
public sealed class SoftKeyboardLayout
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardLayout"/> class.</summary>
    /// <param name="rows">Rows in visual order.</param>
    public SoftKeyboardLayout(IEnumerable<SoftKeyboardRow> rows)
    {
        if (rows is null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        Rows = new ReadOnlyCollection<SoftKeyboardRow>(rows.ToArray());
    }

    /// <summary>Gets the keyboard rows in visual order.</summary>
    public IReadOnlyList<SoftKeyboardRow> Rows { get; }
}

/// <summary>Represents one immutable row of keys.</summary>
public sealed class SoftKeyboardRow
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardRow"/> class.</summary>
    /// <param name="keys">Keys in reading order.</param>
    public SoftKeyboardRow(IEnumerable<SoftKeyboardKey> keys)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        Keys = new ReadOnlyCollection<SoftKeyboardKey>(keys.ToArray());
    }

    /// <summary>Gets the keys in reading order.</summary>
    public IReadOnlyList<SoftKeyboardKey> Keys { get; }
}

/// <summary>Describes one immutable key shown by a <see cref="SoftKeyboard"/>.</summary>
public sealed class SoftKeyboardKey
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardKey"/> class.</summary>
    /// <param name="kind">The semantic action performed by the key.</param>
    /// <param name="label">The short label displayed on the key.</param>
    /// <param name="automationName">The descriptive name exposed to UI Automation.</param>
    /// <param name="key">The WPF virtual key for <see cref="SoftKeyboardKeyKind.VirtualKey"/>.</param>
    /// <param name="text">The Unicode value for <see cref="SoftKeyboardKeyKind.Text"/>.</param>
    /// <param name="displayMode">The destination for <see cref="SoftKeyboardKeyKind.ChangeDisplayMode"/>.</param>
    /// <param name="width">The relative width of the key within its row.</param>
    /// <param name="alternates">Optional long-press Unicode alternatives.</param>
    public SoftKeyboardKey(
        SoftKeyboardKeyKind kind,
        string label,
        string automationName,
        Key key = Key.None,
        string? text = null,
        SoftKeyboardDisplayMode displayMode = SoftKeyboardDisplayMode.Letters,
        double width = 1d,
        IEnumerable<string>? alternates = null
    )
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("A key label is required.", nameof(label));
        }

        if (string.IsNullOrWhiteSpace(automationName))
        {
            throw new ArgumentException("An automation name is required.", nameof(automationName));
        }

        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        Kind = kind;
        Label = label;
        AutomationName = automationName;
        Key = key;
        Text = text;
        DisplayMode = displayMode;
        Width = width;
        Alternates = new ReadOnlyCollection<string>((alternates ?? Array.Empty<string>()).ToArray());
    }

    /// <summary>Gets the action performed by the key.</summary>
    public SoftKeyboardKeyKind Kind { get; }

    /// <summary>Gets the short visual label.</summary>
    public string Label { get; }

    /// <summary>Gets the descriptive UI Automation name.</summary>
    public string AutomationName { get; }

    /// <summary>Gets the WPF virtual key, or <see cref="Key.None"/> for a non-virtual key action.</summary>
    public Key Key { get; }

    /// <summary>Gets the Unicode text inserted by a text key.</summary>
    public string? Text { get; }

    /// <summary>Gets the destination surface selected by a display-mode key.</summary>
    public SoftKeyboardDisplayMode DisplayMode { get; }

    /// <summary>Gets the relative width within the containing row.</summary>
    public double Width { get; }

    /// <summary>Gets the long-press Unicode alternatives.</summary>
    public IReadOnlyList<string> Alternates { get; }
}

/// <summary>Represents one candidate supplied by the active Windows input method.</summary>
public sealed class SoftKeyboardCandidate
{
    internal SoftKeyboardCandidate(uint index, string text, bool isSelected)
    {
        Index = index;
        Text = text;
        IsSelected = isSelected;
    }

    /// <summary>Gets the zero-based candidate index understood by the input method.</summary>
    public uint Index { get; }

    /// <summary>Gets the candidate text.</summary>
    public string Text { get; }

    /// <summary>Gets a value indicating whether the input method currently selects this candidate.</summary>
    public bool IsSelected { get; }
}

/// <summary>Describes one Unicode Emoji sequence offered by a <see cref="SoftKeyboard"/>.</summary>
public sealed class SoftKeyboardEmoji
{
    /// <summary>Initializes a new instance of the <see cref="SoftKeyboardEmoji"/> class.</summary>
    /// <param name="text">The Unicode sequence inserted when selected.</param>
    /// <param name="name">The searchable and accessible description.</param>
    /// <param name="category">The visual category.</param>
    /// <param name="alternates">Optional long-press variants such as skin tones.</param>
    public SoftKeyboardEmoji(
        string text,
        string name,
        SoftKeyboardEmojiCategory category,
        IEnumerable<string>? alternates = null
    )
    {
        if (string.IsNullOrEmpty(text))
        {
            throw new ArgumentException("Emoji text is required.", nameof(text));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("An Emoji name is required.", nameof(name));
        }

        Text = text;
        Name = name;
        Category = category;
        Alternates = new ReadOnlyCollection<string>((alternates ?? Array.Empty<string>()).ToArray());
    }

    /// <summary>Gets the Unicode sequence.</summary>
    public string Text { get; }

    /// <summary>Gets the searchable and accessible description.</summary>
    public string Name { get; }

    /// <summary>Gets the visual category.</summary>
    public SoftKeyboardEmojiCategory Category { get; }

    /// <summary>Gets the long-press variants.</summary>
    public IReadOnlyList<string> Alternates { get; }
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
