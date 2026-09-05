// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Enumerates a control's logical-child snapshot without acquiring resources that require
/// disposal.
/// </summary>
/// <remarks>
/// WPF logical-tree walkers consume <see cref="IEnumerator"/> without a disposal contract. This
/// type therefore implements that interface directly instead of returning a boxed generic
/// collection enumerator, which would also implement <see cref="IDisposable"/> and incorrectly
/// require WPF to dispose the value returned by <c>LogicalChildren</c>.
/// </remarks>
internal sealed class LogicalChildrenEnumerator : IEnumerator
{
    private readonly IReadOnlyList<object> _children;
    private int _index = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="LogicalChildrenEnumerator"/> class over a
    /// private snapshot assembled by the owning control.
    /// </summary>
    internal LogicalChildrenEnumerator(IReadOnlyList<object> children)
    {
        _children = children;
    }

    /// <summary>
    /// Gets the current child and rejects positions before the first or after the last item.
    /// </summary>
    public object Current => _index >= 0 && _index < _children.Count
        ? _children[_index]
        : throw new InvalidOperationException("The enumerator is not positioned on a logical child.");

    /// <summary>Advances to the next child and remains past the end once exhausted.</summary>
    public bool MoveNext()
    {
        if (_index < _children.Count)
        {
            _index++;
        }

        return _index < _children.Count;
    }

    /// <summary>Restarts enumeration of the same logical-child snapshot.</summary>
    public void Reset() => _index = -1;
}
