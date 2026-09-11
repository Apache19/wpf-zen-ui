// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Creates keyboard layouts for a <see cref="SoftKeyboard"/>.
/// </summary>
/// <remarks>
/// Providers own language- and input-method-specific key selection, placement, labels and actions.
/// The keyboard requests a complete snapshot when its input context changes; providers should use
/// that context to resolve Shift, display pages and IME modes together. Return null to decline a context;
/// use CompositeSoftKeyboardLayoutProvider to reuse other providers without copying their layouts.
/// The visual template only presents the resulting keys and does not interpret language rules or select layout variants.
/// </remarks>
public interface ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Creates an immutable layout for the supplied input context.
    /// </summary>
    /// <param name="context">
    /// The active language, input scope, display mode, Shift state, IME state and input-method profile.
    /// </param>
    /// <returns>
    /// A complete immutable snapshot, or null when this provider does not handle the context.
    /// </returns>
    SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context);
}
