// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensibility;

/// <summary>
/// Describes the immutable environment used to select input-method extensions.
/// </summary>
public sealed class SoftKeyboardExtensionContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardExtensionContext"/> class.
    /// </summary>
    /// <param name="layout">The current layout context.</param>
    public SoftKeyboardExtensionContext(SoftKeyboardLayoutContext layout)
    {
        Layout = layout ?? throw new ArgumentNullException(nameof(layout));
    }

    /// <summary>
    /// Gets the current culture, editor scope, page, input state, and input-method profile.
    /// </summary>
    public SoftKeyboardLayoutContext Layout { get; }
}