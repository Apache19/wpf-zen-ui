// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;

/// <summary>
/// Applies a localized compact repertoire after the core's specialized InputScope layouts.
/// Derived providers supply only their language boundary and repertoire.
/// </summary>
internal abstract class LanguageCompactLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly CompactSoftKeyboardLayoutProvider _compact;

    /// <summary>
    /// Creates the compact layout generator from a language-owned repertoire.
    /// </summary>
    protected LanguageCompactLayoutProvider(SoftKeyboardCompactRepertoire repertoire)
    {
        _compact = new CompactSoftKeyboardLayoutProvider(repertoire);
    }

    /// <summary>
    /// Determines whether this language's repertoire applies to the active layout context.
    /// </summary>
    protected abstract bool Supports(SoftKeyboardLayoutContext context);

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (!Supports(context))
        {
            return null;
        }

        return UrlSoftKeyboardLayoutProvider.Default.CreateLayout(context)
            ?? EmailSoftKeyboardLayoutProvider.Default.CreateLayout(context)
            ?? NumberSoftKeyboardLayoutProvider.Default.CreateLayout(context)
            ?? _compact.CreateLayout(context);
    }
}
