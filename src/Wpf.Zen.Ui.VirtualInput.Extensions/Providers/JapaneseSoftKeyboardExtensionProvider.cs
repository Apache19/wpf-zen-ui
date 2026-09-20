// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.Extensions.Routing;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Registers a complete physical keyboard and the Japanese input-method adapter.
/// Physical keys remain available while the IME is closed or its mode is still unknown.
/// </summary>
public sealed class JapaneseSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    /// <summary>
    /// Gets the shared Japanese extension provider.
    /// </summary>
    public static JapaneseSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return ProfileLanguage.IsJapanese(context.Layout)
            ? [new SoftKeyboardExtension(
                layoutProvider: JapaneseSoftKeyboardLayoutProvider.Default,
                inputModeAdapter: MicrosoftJapaneseInputModeAdapter.Default)]
            : [];
    }
}
