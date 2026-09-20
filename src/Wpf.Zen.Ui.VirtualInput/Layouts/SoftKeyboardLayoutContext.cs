// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Supplies the stable information a layout provider needs to construct a keyboard surface.
/// </summary>
public sealed class SoftKeyboardLayoutContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardLayoutContext"/> class.
    /// </summary>
    /// <remarks>
    /// Captures the current thread's Windows keyboard layout for <see cref="GetKeyLabel"/>.
    /// The culture describes the input language; it does not select a Windows keyboard layout.
    /// </remarks>
    /// <param name="culture">The culture associated with the active input language.</param>
    /// <param name="inputScope">The semantic input scope requested by the active editor.</param>
    /// <param name="page">The surface requested by the keyboard.</param>
    /// <param name="shiftState">The current virtual Shift state.</param>
    /// <param name="inputMethod">
    /// The active profile, allowing language- and vendor-specific layouts.
    /// </param>
    /// <param name="supplementalLayout">
    /// The user's supplemental letter surface, independent of the requested page.
    /// </param>
    /// <param name="inputModeButton">
    /// The independently discovered mode-command presentation, or null when unavailable.
    /// </param>
    public SoftKeyboardLayoutContext(
        CultureInfo culture,
        InputScopeNameValue inputScope,
        string page,
        SoftKeyboardShiftState shiftState,
        SoftKeyboardInputMethod? inputMethod = null,
        string supplementalLayout = SoftKeyboardSupplementalLayouts.Automatic,
        SoftKeyboardInputModeButton? inputModeButton = null
    )
        : this(culture, inputScope, page, shiftState, inputMethod, IntPtr.Zero,
            supplementalLayout, inputModeButton)
    {
    }

    /// <summary>
    /// Creates a layout snapshot using an existing native layout identity when supplied.
    /// </summary>
    /// <param name="culture">The culture associated with the active input language.</param>
    /// <param name="inputScope">The semantic input scope requested by the active editor.</param>
    /// <param name="page">The surface requested by the keyboard.</param>
    /// <param name="shiftState">The current virtual Shift state.</param>
    /// <param name="inputMethod">The active profile, or null when unavailable.</param>
    /// <param name="keyboardLayoutHandle">
    /// Borrowed Windows HKL used for key labels; zero captures the current thread's layout.
    /// </param>
    /// <param name="supplementalLayout">
    /// The user's supplemental letter surface, independent of the requested page.
    /// </param>
    /// <param name="inputModeButton">
    /// The mode-command presentation, or null when unavailable.
    /// </param>
    internal SoftKeyboardLayoutContext(
        CultureInfo culture,
        InputScopeNameValue inputScope,
        string page,
        SoftKeyboardShiftState shiftState,
        SoftKeyboardInputMethod? inputMethod,
        IntPtr keyboardLayoutHandle,
        string supplementalLayout = SoftKeyboardSupplementalLayouts.Automatic,
        SoftKeyboardInputModeButton? inputModeButton = null
    )
    {
        if (culture is null)
        {
            throw new ArgumentNullException(nameof(culture));
        }

        Culture = CultureInfo.ReadOnly((CultureInfo)culture.Clone());

        KeyboardLayoutHandle = keyboardLayoutHandle == IntPtr.Zero
            ? KeyboardLayoutLabels.GetCurrentLayout()
            : keyboardLayoutHandle;

        InputScope = inputScope;

        Page = !string.IsNullOrWhiteSpace(page)
            ? page
            : throw new ArgumentException("A page identifier is required.", nameof(page));

        ShiftState         = shiftState;
        InputMethod        = inputMethod;
        SupplementalLayout = supplementalLayout;
        InputModeButton    = inputModeButton;
    }

    /// <summary>
    /// Gets the culture of the active Windows input language.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets the borrowed Windows HKL captured for this snapshot; it must not be unloaded.
    /// </summary>
    /// <remarks>
    /// Key-label decoding uses this identity for every key in the snapshot. Internal copies retain
    /// it so that changing presentation data does not sample a different thread layout.
    /// </remarks>
    internal IntPtr KeyboardLayoutHandle { get; }

    /// <summary>
    /// Resolves a virtual key's display label using the captured Windows layout and Shift state.
    /// </summary>
    /// <param name="key">The WPF virtual key whose label is requested.</param>
    /// <param name="kana">
    /// Whether to translate with the Kana toggle set; defaults to false. This can supply a second
    /// legend on a physical key. The native layout determines the available mappings.
    /// </param>
    /// <returns>
    /// The printable single-character translation, or the key name when translation is unavailable.
    /// Fallback names are uppercase when Shift is active and lowercase otherwise.
    /// </returns>
    /// <remarks>
    /// Intended for custom layout providers. Translation preserves the Windows dead-key buffer and
    /// uses this snapshot's layout for every call. The label is presentation text, not IME output.
    /// </remarks>
    public string GetKeyLabel(Key key, bool kana = false)
    {
        var shifted = ShiftState is SoftKeyboardShiftState.Once or SoftKeyboardShiftState.Locked;
        return KeyboardLayoutLabels.GetLabel(key, shifted, KeyboardLayoutHandle, kana);
    }

    /// <summary>
    /// Gets the WPF input scope requested by the active target.
    /// </summary>
    public InputScopeNameValue InputScope { get; }

    /// <summary>
    /// Gets the surface the provider must create.
    /// </summary>
    public string Page { get; }

    /// <summary>
    /// Gets the current virtual Shift state.
    /// </summary>
    public SoftKeyboardShiftState ShiftState { get; }

    /// <summary>
    /// Gets whether the committed surface includes an input-method command button.
    /// </summary>
    public bool HasInputModeButton => InputMethod is not null && InputModeButton is not null;

    /// <summary>
    /// Gets the mode-command presentation independently from the interpreted mode state.
    /// </summary>
    public SoftKeyboardInputModeButton? InputModeButton { get; }

    /// <summary>
    /// Gets the active profile identity, or null when Windows exposes none.
    /// </summary>
    public SoftKeyboardInputMethod? InputMethod { get; }

    /// <summary>
    /// Gets the supplemental letter selection. Automatic resolves the recognized native preference.
    /// </summary>
    public string SupplementalLayout { get; }
}
