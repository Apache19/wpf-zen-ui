// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Uniquely identifies a navigation destination by its page type and optional variant.
/// </summary>
public sealed record NavigationKey
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NavigationKey"/> class.
    /// </summary>
    /// <param name="pageType">The type of page represented by this key.</param>
    /// <param name="variant">An optional variant that distinguishes multiple destinations using the same page type.</param>
    /// <exception cref="ArgumentNullException"><paramref name="pageType"/> is <see langword="null"/>.</exception>
    public NavigationKey(Type pageType, string? variant = null)
    {
        PageType = pageType ?? throw new ArgumentNullException(nameof(pageType));
        Variant  = string.IsNullOrWhiteSpace(variant) ? null : variant;
    }

    /// <summary>
    /// Gets the page type.
    /// </summary>
    public Type PageType { get; }

    /// <summary>
    /// Gets the optional page variant.
    /// </summary>
    public string? Variant { get; }

    /// <inheritdoc />
    public override string ToString()
    {
        return Variant is null ? PageType.FullName ?? PageType.Name : $"{PageType.FullName ?? PageType.Name} ({Variant})";
    }
}