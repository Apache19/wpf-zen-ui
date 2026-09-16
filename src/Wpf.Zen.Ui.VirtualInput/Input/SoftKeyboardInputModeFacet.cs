// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Input;

/// <summary>
/// Represents one independently resolved part of an input-mode capability.
/// </summary>
/// <typeparam name="T">The reference type carried by the facet.</typeparam>
/// <remarks>
/// An unhandled facet allows the next resolver to provide a value. A handled facet stops fallback,
/// including when its value is null and the resolver intentionally suppresses that capability.
/// </remarks>
public readonly struct SoftKeyboardInputModeFacet<T>
    where T : class
{
    private SoftKeyboardInputModeFacet(bool isHandled, T? value)
    {
        IsHandled = isHandled;
        Value     = value;
    }

    /// <summary>
    /// Gets a facet that leaves resolution to the next provider.
    /// </summary>
    public static SoftKeyboardInputModeFacet<T> Unhandled => default;

    /// <summary>
    /// Gets a handled facet that intentionally supplies no capability.
    /// </summary>
    public static SoftKeyboardInputModeFacet<T> None => new(true, null);

    /// <summary>
    /// Gets whether this facet stops fallback to subsequent providers.
    /// </summary>
    public bool IsHandled { get; }

    /// <summary>
    /// Gets the resolved value, or null when the resolver intentionally supplied no value.
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Creates a handled facet containing a resolved value.
    /// </summary>
    /// <param name="value">The resolved value.</param>
    /// <returns>A handled facet containing <paramref name="value"/>.</returns>
    public static SoftKeyboardInputModeFacet<T> FromValue(T value)
    {
        return new SoftKeyboardInputModeFacet<T>(
            true,
            value ?? throw new ArgumentNullException(nameof(value))
        );
    }
}
