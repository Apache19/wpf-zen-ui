// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui.VirtualInput.Layouts;

/// <summary>
/// Represents one immutable, flat collection of keys positioned on a shared logical grid.
/// </summary>
/// <remarks>
/// A layout deliberately exposes no row-container hierarchy. Each <see cref="SoftKeyboardKey"/>
/// carries its own <see cref="SoftKeyboardKey.Row"/>, <see cref="SoftKeyboardKey.Column"/>, and
/// <see cref="SoftKeyboardKey.ColumnSpan"/>. Every row shares <see cref="ColumnCount"/>, so omitted
/// leading or trailing cells remain empty instead of stretching a shorter row across the surface.
/// </remarks>
public sealed class SoftKeyboardLayout
{
    /// <summary>
    /// Gets the shared immutable layout used before a keyboard first requests its key surface.
    /// </summary>
    /// <remarks>
    /// Contains no keys and one logical column, so template bindings remain valid while deferred.
    /// </remarks>
    public static SoftKeyboardLayout Empty { get; } = new();

    /// <summary>
    /// Creates the empty snapshot without running key validation or initializing key factories.
    /// </summary>
    private SoftKeyboardLayout()
    {
        Keys = Array.Empty<SoftKeyboardKey>();
        ColumnCount = 1;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardLayout"/> class.
    /// </summary>
    /// <param name="keys">Positioned keys in reading order.</param>
    /// <param name="columnCount">
    /// The positive number of equal-width logical columns, or <see langword="null"/> to infer the
    /// smallest count containing every key. An empty inferred layout uses one column.
    /// </param>
    /// <remarks>
    /// Supplying the count explicitly lets a layout reserve empty trailing columns. Every key must
    /// fit completely inside the declared grid; overlapping keys are permitted so specialized
    /// providers can intentionally layer content.
    /// </remarks>
    public SoftKeyboardLayout(IEnumerable<SoftKeyboardKey> keys, int? columnCount = null)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (columnCount is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        var keyArray = keys.ToArray();
        if (keyArray.Any(static key => key is null))
        {
            throw new ArgumentException(
                "A keyboard layout cannot contain a null key.",
                nameof(keys)
            );
        }

        var requiredColumnCount64 = keyArray.Length == 0
            ? 1L
            : keyArray.Max(static key => (long)key.Column + key.ColumnSpan);

        if (requiredColumnCount64 > int.MaxValue)
        {
            throw new ArgumentException(
                "A key placement exceeds the supported logical grid range.",
                nameof(keys)
            );
        }

        var requiredColumnCount = (int)requiredColumnCount64;
        var resolvedColumnCount = columnCount ?? requiredColumnCount;

        if (requiredColumnCount > resolvedColumnCount)
        {
            throw new ArgumentException(
                "Every key must fit inside the declared logical column count.",
                nameof(columnCount)
            );
        }

        Keys        = new ReadOnlyCollection<SoftKeyboardKey>(keyArray);
        ColumnCount = resolvedColumnCount;
    }

    /// <summary>
    /// Gets keys in reading order. Items with the same row identifier retain this order.
    /// </summary>
    public IReadOnlyList<SoftKeyboardKey> Keys { get; }

    /// <summary>
    /// Gets the number of equal-width logical columns shared by every row in the layout.
    /// </summary>
    public int ColumnCount { get; }
}