// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Combines the built-in language and input-method adapters in stable precedence order.
/// </summary>
public sealed class DefaultSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private static readonly SoftKeyboardExtension CandidatePolicyExtension = new(
        candidateBehavior: WindowsCandidatePresentationBehavior.Default
    );

    private readonly CompositeSoftKeyboardExtensionProvider _provider = new(
        ChineseSoftKeyboardExtensionProvider.Default,
        JapaneseSoftKeyboardExtensionProvider.Default
    );

    /// <summary>
    /// Gets the shared default extension composition.
    /// </summary>
    public static DefaultSoftKeyboardExtensionProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        return _provider.GetExtensions(context).Append(CandidatePolicyExtension);
    }
}
