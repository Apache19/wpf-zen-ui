// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui;

/// <summary>Represents one presentation-safe segment in a resolved semantic navigation path.</summary>
/// <remarks>
/// A segment is data, not a menu entry or a WPF control. Structural segments may intentionally
/// omit <see cref="Target"/> and remain visible without being clickable. This lets a navigation map
/// contain categories that organize pages but do not themselves have page content.
/// </remarks>
public sealed class BreadcrumbSegment
{
    /// <summary>Initializes a new instance of the <see cref="BreadcrumbSegment"/> class.</summary>
    /// <param name="key">A stable identity within the resolved path.</param>
    /// <param name="content">The value presented by a breadcrumb item template.</param>
    /// <param name="target">The optional destination activated when the segment is invoked.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public BreadcrumbSegment(string key, object? content, NavigationTarget? target = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("A breadcrumb segment key cannot be empty.", nameof(key));
        }

        Key = key;
        Content = content;
        Target = target;
    }

    /// <summary>Gets the stable identity of this segment within its path.</summary>
    public string Key { get; }

    /// <summary>Gets the presentation content for the segment.</summary>
    public object? Content { get; }

    /// <summary>Gets the optional navigation destination represented by this segment.</summary>
    public NavigationTarget? Target { get; }

    /// <summary>Gets a value indicating whether invoking this segment can request navigation.</summary>
    public bool IsNavigable => Target is not null;

    /// <inheritdoc />
    public override string ToString() => Content?.ToString() ?? string.Empty;
}
