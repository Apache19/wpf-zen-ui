// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Publishes supplemental layout choices and remembers selections per input profile.
/// </summary>
public partial class SoftKeyboard
{
    private readonly Dictionary<string, string> _supplementalSelections = new(StringComparer.Ordinal);

    private static readonly DependencyPropertyKey AvailableSupplementalLayoutsPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(AvailableSupplementalLayouts),
            typeof(IReadOnlyList<SoftKeyboardLayoutOption>),
            typeof(SoftKeyboard),
            new PropertyMetadata(Array.Empty<SoftKeyboardLayoutOption>())
        );

    /// <summary>
    /// Identifies the <see cref="AvailableSupplementalLayouts"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty AvailableSupplementalLayoutsProperty
        = AvailableSupplementalLayoutsPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets an immutable snapshot of layout choices applicable to the current input context.
    /// </summary>
    /// <remarks>
    /// Initially empty. Opening or RefreshLayout publishes Automatic, Standard and applicable
    /// extension layouts in declaration order. Closed changes retain the last generated snapshot.
    /// Updated during layout resolution; applications may bind their own selection UI to this
    /// property and apply an identifier with SelectSupplementalLayout. Previously read snapshots
    /// remain unchanged. Choices can become unavailable when the profile or input scope changes.
    /// </remarks>
    public IReadOnlyList<SoftKeyboardLayoutOption> AvailableSupplementalLayouts
        => (IReadOnlyList<SoftKeyboardLayoutOption>)GetValue(AvailableSupplementalLayoutsProperty);

    #region Read-Only DP: SelectedSupplementalLayout

    private static readonly DependencyPropertyKey SelectedSupplementalLayoutPropertyKey
        = DependencyProperty.RegisterReadOnly(
            nameof(SelectedSupplementalLayout),
            typeof(string),
            typeof(SoftKeyboard),
            new PropertyMetadata(SoftKeyboardSupplementalLayouts.Automatic
            )
        );

    /// <summary>
    /// Identifies the <see cref="SelectedSupplementalLayout"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty SelectedSupplementalLayoutProperty =
        SelectedSupplementalLayoutPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the remembered choice; Automatic follows the recognized input-method preference.
    /// </summary>
    /// <remarks>
    /// Use SelectSupplementalLayout to validate a change and cancel composition before replacing
    /// the letter surface.
    /// </remarks>
    public string SelectedSupplementalLayout => (string)GetValue(SelectedSupplementalLayoutProperty);

    #endregion

    /// <summary>
    /// Selects an available supplemental layout and remembers it for this keyboard's active input
    /// profile.
    /// </summary>
    /// <param name="id">An identifier from AvailableSupplementalLayouts.</param>
    /// <remarks>
    /// Selection lasts for the control's lifetime and is independent of symbol/number page
    /// navigation.
    /// Unknown choices throw; a composition that cannot be cancelled leaves the current choice
    /// intact. A pending native observation likewise postpones layout discovery; retry selection
    /// after the current layout snapshot is published.
    /// Application persistence can restore a choice after the relevant input profile becomes
    /// active.
    /// </remarks>
    public void SelectSupplementalLayout(string id)
    {
        VerifyAccess();

        // A choice obtained before closed-state configuration changes must be revalidated.
        if (_layoutDirty)
        {
            RefreshLayout();
        }

        if (_layoutDirty)
        {
            // A native operation still owns publication; leave the current selection intact.
            return;
        }

        if (AvailableSupplementalLayouts.All(option => option.Id != id))
        {
            throw new ArgumentException(
                "The supplemental layout is not available for the active input method.",
                nameof(id)
            );
        }

        var profile = GetSupplementalProfileKey();

        if (_activeTextComposition is not null && !PrepareTargetForInput())
        {
            return;
        }

        CandidateInteractionVersion++;

        RunAfterActiveCompositionCancellation(
            () =>
            {
                if (_layoutDirty)
                {
                    RefreshLayout();
                }

                if (_layoutDirty || profile != GetSupplementalProfileKey()
                    || AvailableSupplementalLayouts.All(option => option.Id != id))
                {
                    return;
                }

                _supplementalSelections[profile] = id;

                SetValue(SelectedSupplementalLayoutPropertyKey, id);
                ShowPage(SoftKeyboardPages.Letters);

                RefreshLayout();
            }
        );
    }

    /// <summary>
    /// Keys the selected layout by native profile identity, or language before discovery.
    /// </summary>
    private string GetSupplementalProfileKey()
    {
        return ActiveInputMethod is { } profile
            ? $"{profile.LanguageId:X4}/{profile.ClassId:D}/{profile.ProfileId:D}/{profile.KeyboardLayout}"
            : System.Windows.Input.InputLanguageManager.Current.CurrentInputLanguage.Name;
    }

    /// <summary>
    /// Reads the per-profile choice, defaulting to automatic layout selection.
    /// </summary>
    private string ReadSupplementalSelection()
    {
        return _supplementalSelections.TryGetValue(GetSupplementalProfileKey(), out var selection)
            ? selection
            : SoftKeyboardSupplementalLayouts.Automatic;
    }
}
