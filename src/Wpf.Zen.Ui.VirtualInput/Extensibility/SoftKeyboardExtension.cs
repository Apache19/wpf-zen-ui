// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensibility;

/// <summary>
/// Groups independent capabilities contributed by one input-method extension.
/// </summary>
/// <remarks>
/// Every capability is optional. The keyboard resolves each capability independently, so a
/// language layout extension and a vendor candidate extension can apply at the same time.
/// </remarks>
public sealed class SoftKeyboardExtension
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardExtension"/> class.
    /// </summary>
    /// <param name="layoutProvider">An optional provider for ordinary keyboard pages.</param>
    /// <param name="supplementalLayouts">An optional provider for selectable supplemental layouts.</param>
    /// <param name="inputStateAdapter">An optional adapter for nonstandard native/direct state.</param>
    /// <param name="candidateBehavior">An optional TSF candidate behavior override.</param>
    public SoftKeyboardExtension(
        ISoftKeyboardLayoutProvider?             layoutProvider = null,
        ISoftKeyboardSupplementalLayoutProvider? supplementalLayouts = null,
        ISoftKeyboardInputStateAdapter?          inputStateAdapter = null,
        ISoftKeyboardTsfCandidateBehavior?       candidateBehavior = null)
    {
        LayoutProvider      = layoutProvider;
        SupplementalLayouts = supplementalLayouts;
        InputStateAdapter   = inputStateAdapter;
        CandidateBehavior   = candidateBehavior;
    }

    /// <summary>
    /// Gets the optional ordinary layout provider.
    /// </summary>
    public ISoftKeyboardLayoutProvider? LayoutProvider { get; }

    /// <summary>
    /// Gets the optional supplemental-layout catalog.
    /// </summary>
    public ISoftKeyboardSupplementalLayoutProvider? SupplementalLayouts { get; }

    /// <summary>
    /// Gets the optional input-state adapter.
    /// </summary>
    public ISoftKeyboardInputStateAdapter? InputStateAdapter { get; }

    /// <summary>
    /// Gets the optional candidate behavior override used by the core TSF source.
    /// </summary>
    public ISoftKeyboardTsfCandidateBehavior? CandidateBehavior { get; }
}
