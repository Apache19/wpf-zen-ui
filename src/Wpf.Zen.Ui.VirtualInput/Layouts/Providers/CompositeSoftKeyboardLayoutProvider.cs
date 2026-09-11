// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Queries independent providers in declaration order until one accepts the context.
/// </summary>
/// <remarks>
/// First non-null result wins, including an intentionally empty layout. Put vendor/profile overrides
/// before regional rules and general defaults. Exceptions indicate provider defects and propagate to
/// the keyboard's diagnostic/fallback boundary; they are not treated as an ordinary declined context.
/// The registration array is copied so a shared provider cannot change underneath another keyboard.
/// </remarks>
public sealed class CompositeSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    private readonly ISoftKeyboardLayoutProvider[] _providers;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeSoftKeyboardLayoutProvider"/> class.
    /// </summary>
    /// <param name="providers">
    /// Providers to query; neither the array nor its entries may be null.
    /// </param>
    public CompositeSoftKeyboardLayoutProvider(params ISoftKeyboardLayoutProvider[] providers)
    {
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        _providers = [.. providers];

        if (_providers.Any(provider => provider is null))
        {
            throw new ArgumentException("A provider cannot be null.", nameof(providers));
        }
    }

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        return _providers
            .Select(provider => provider.CreateLayout(context))
            .OfType<SoftKeyboardLayout>()
            .FirstOrDefault();
    }
}