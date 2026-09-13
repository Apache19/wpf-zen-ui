// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Connects a selectable layout identifier, presentation, automatic preference, and implementation.
/// </summary>
public sealed class SoftKeyboardSupplementalLayout
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardSupplementalLayout"/> class.
    /// </summary>
    /// <param name="id">The nonempty, case-sensitive identifier stored as the user's selection.</param>
    /// <param name="displayName">The nonempty default label exposed to settings providers.</param>
    /// <param name="layoutProvider">The provider that creates this layout when selected.</param>
    /// <param name="isAutomaticPreferred">Whether Automatic should choose this registration.</param>
    /// <param name="isApplicable">
    /// An optional context predicate. A null predicate makes the registration universally applicable.
    /// </param>
    public SoftKeyboardSupplementalLayout(
        string id,
        string displayName,
        ISoftKeyboardLayoutProvider layoutProvider,
        bool isAutomaticPreferred = false,
        Func<SoftKeyboardLayoutContext, bool>? isApplicable = null)
    {
        Id = !string.IsNullOrWhiteSpace(id)
            ? id
            : throw new ArgumentException("A supplemental layout identifier is required.", nameof(id));

        DisplayName = !string.IsNullOrWhiteSpace(displayName)
            ? displayName
            : throw new ArgumentException("A supplemental layout name is required.", nameof(displayName));

        LayoutProvider = layoutProvider ?? throw new ArgumentNullException(nameof(layoutProvider));
        IsAutomaticPreferred = isAutomaticPreferred;
        IsApplicable = isApplicable ?? (_ => true);
    }

    /// <summary>
    /// Gets the stable, case-sensitive selection identifier.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the default settings label.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the provider responsible for building this layout.
    /// </summary>
    public ISoftKeyboardLayoutProvider LayoutProvider { get; }

    /// <summary>
    /// Gets a value indicating whether Automatic prefers this layout in the current context.
    /// </summary>
    public bool IsAutomaticPreferred { get; }

    /// <summary>
    /// Gets the context predicate that controls catalog, validation, automatic selection, and generation.
    /// </summary>
    public Func<SoftKeyboardLayoutContext, bool> IsApplicable { get; }
}
