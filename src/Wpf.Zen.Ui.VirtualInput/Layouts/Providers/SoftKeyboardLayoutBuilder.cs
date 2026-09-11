// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Builds immutable grid layouts from readable rows without manual column arithmetic.
/// </summary>
/// <remarks>
/// A builder belongs to one layout construction. Build returns a snapshot; subsequent additions do
/// not mutate earlier snapshots. Placement validation is shared with the immutable layout model.
/// Rows may be staggered or have different widths; all use the same declared column grid.
/// </remarks>
public sealed class SoftKeyboardLayoutBuilder
{
    private readonly List<SoftKeyboardKey> _keys = new();
    private readonly int                   _columnCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SoftKeyboardLayoutBuilder"/> class.
    /// </summary>
    /// <param name="columnCount">
    /// Width of the logical grid, independent of physical pixels.
    /// </param>
    public SoftKeyboardLayoutBuilder(int columnCount)
    {
        if (columnCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnCount));
        }

        _columnCount = columnCount;
    }

    /// <summary>
    /// Adds an already positioned key, including any custom action and alternative data.
    /// </summary>
    /// <param name="key">The immutable key to include.</param>
    /// <returns>This builder for further additions.</returns>
    public SoftKeyboardLayoutBuilder Add(SoftKeyboardKey key)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        // todo: 这句什么意思？
        _ = new SoftKeyboardLayout([key], _columnCount);

        _keys.Add(key);

        return this;
    }

    /// <summary>
    /// Adds equal-width keys to one row, starting at the supplied column.
    /// </summary>
    /// <param name="row">Nonnegative row identifier.</param>
    /// <param name="keys">Unpositioned keys in reading order.</param>
    /// <param name="columnSpan">Positive width of every key; defaults to one.</param>
    /// <param name="startColumn">Leading grid offset; defaults to zero.</param>
    /// <returns>This builder for further additions.</returns>
    public SoftKeyboardLayoutBuilder AddRow(
        int row,
        IEnumerable<SoftKeyboardKey> keys,
        int columnSpan = 1,
        int startColumn = 0)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (columnSpan <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(columnSpan));
        }

        var snapshot = keys.ToArray();
        return AddRow(row, snapshot, Enumerable.Repeat(columnSpan, snapshot.Length), startColumn);
    }

    /// <summary>
    /// Adds a row with one explicit width per key. Failed additions leave this builder unchanged.
    /// </summary>
    /// <param name="row">Nonnegative row identifier.</param>
    /// <param name="keys">Unpositioned keys in reading order.</param>
    /// <param name="columnSpans">Exactly one positive width for each key.</param>
    /// <param name="startColumn">Leading grid offset; defaults to zero.</param>
    /// <returns>This builder for further additions.</returns>
    public SoftKeyboardLayoutBuilder AddRow(
        int row,
        IEnumerable<SoftKeyboardKey> keys,
        IEnumerable<int> columnSpans,
        int startColumn = 0)
    {
        if (keys is null)
        {
            throw new ArgumentNullException(nameof(keys));
        }

        if (columnSpans is null)
        {
            throw new ArgumentNullException(nameof(columnSpans));
        }

        if (row < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (startColumn < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(startColumn));
        }

        var snapshot = keys.ToArray();
        if (snapshot.Any(key => key is null))
        {
            throw new ArgumentException("A key cannot be null.", nameof(keys));
        }

        var positioned = new List<SoftKeyboardKey>();
        var column     = startColumn;

        using var spans = columnSpans.GetEnumerator();

        foreach (var key in snapshot)
        {
            if (!spans.MoveNext())
            {
                throw new ArgumentException("Every key requires a column span.", nameof(columnSpans));
            }

            positioned.Add(key.WithPlacement(row, column, spans.Current));
            column = checked(column + spans.Current);
        }

        if (spans.MoveNext())
        {
            throw new ArgumentException("There are more spans than keys.", nameof(columnSpans));
        }

        var layout = new SoftKeyboardLayout(positioned, _columnCount);
        _keys.AddRange(layout.Keys);
        return this;
    }

    /// <summary>Creates an immutable snapshot of all successfully added keys.</summary>
    /// <returns>A layout that does not share mutable collections with this builder.</returns>
    public SoftKeyboardLayout Build()
    {
        return new SoftKeyboardLayout(_keys, _columnCount);
    }
}