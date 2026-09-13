// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensibility;

/// <summary>
/// Resolves the ordered input-method extensions that apply to one immutable keyboard context.
/// </summary>
/// <remarks>
/// Providers must not mutate the keyboard, focus, input language, or operating-system input state.
/// Returning an empty sequence delegates every capability to the core implementation.
/// </remarks>
public interface ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Returns extensions in capability precedence order for the supplied context.
    /// </summary>
    /// <param name="context">The current input method, layout, and editor context.</param>
    /// <returns>An ordered sequence containing no null entries.</returns>
    IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context);
}