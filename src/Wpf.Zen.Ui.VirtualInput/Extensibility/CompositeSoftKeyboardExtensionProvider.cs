// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Extensibility;

/// <summary>
/// Concatenates several extension providers while preserving their declaration order.
/// </summary>
public sealed class CompositeSoftKeyboardExtensionProvider : ISoftKeyboardExtensionProvider
{
    private readonly ISoftKeyboardExtensionProvider[] _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSoftKeyboardExtensionProvider"/> class.
    /// </summary>
    /// <param name="providers">Providers to query in precedence order.</param>
    public CompositeSoftKeyboardExtensionProvider(params ISoftKeyboardExtensionProvider[] providers)
    {
        _providers = providers is null
            ? throw new ArgumentNullException(nameof(providers))
            : [.. providers];

        if (_providers.Any(static provider => provider is null))
        {
            throw new ArgumentException("An extension provider cannot be null.", nameof(providers));
        }
    }

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardExtension> GetExtensions(SoftKeyboardExtensionContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        foreach (var provider in _providers)
        {
            foreach (var extension in provider.GetExtensions(context))
            {
                yield return extension;
            }
        }
    }
}