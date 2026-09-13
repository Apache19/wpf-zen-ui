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
    /// <param name="culture">The culture associated with the active input language.</param>
    /// <param name="inputScope">The semantic input scope requested by the active editor.</param>
    /// <param name="page">The surface requested by the keyboard.</param>
    /// <param name="shiftState">The current virtual Shift state.</param>
    /// <param name="imeState">The observed native/direct input state; unknown by default.</param>
    /// <param name="inputMethod">The active profile, allowing language- and vendor-specific layouts.</param>
    /// <param name="keyboardLayoutHandle">
    /// Borrowed Windows HKL used for key labels; zero captures the current thread's layout.
    /// </param>
    /// <param name="supplementalLayout">
    /// The user's supplemental letter surface, independent of the requested page.
    /// </param>
    public SoftKeyboardLayoutContext(
        CultureInfo culture,
        InputScopeNameValue inputScope,
        string page,
        SoftKeyboardShiftState shiftState,
        InputMethodState imeState = InputMethodState.DoNotCare,
        SoftKeyboardInputMethod? inputMethod = null,
        IntPtr keyboardLayoutHandle = default,
        string supplementalLayout = SoftKeyboardSupplementalLayouts.Automatic
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
        ImeState           = imeState;
        InputMethod        = inputMethod;
        SupplementalLayout = supplementalLayout;
    }

    /// <summary>
    /// Gets the culture of the active Windows input language.
    /// </summary>
    public CultureInfo Culture { get; }

    /// <summary>
    /// Gets the borrowed Windows HKL captured for this snapshot; providers must not unload it.
    /// </summary>
    /// <remarks>
    /// Key labels and scan-code mapping must use this handle rather than re-reading ambient thread state per key.
    /// </remarks>
    public IntPtr KeyboardLayoutHandle { get; }

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
    /// Gets the observed native/direct input state, never an optimistic toggle request.
    /// </summary>
    public InputMethodState ImeState { get; }

    /// <summary>
    /// Gets whether the active profile supports mode switching and has a known observed state.
    /// False when no profile is selected, the entry is a plain keyboard, or state is unavailable.
    /// </summary>
    public bool CanToggleIme => InputMethod?.SupportsImeToggle == true
        && ImeState is InputMethodState.On or InputMethodState.Off;

    /// <summary>
    /// Gets the active profile identity, or null when Windows exposes none.
    /// </summary>
    public SoftKeyboardInputMethod? InputMethod { get; }

    /// <summary>
    /// Gets the supplemental letter selection. Automatic resolves the recognized native preference.
    /// </summary>
    public string SupplementalLayout { get; }
}