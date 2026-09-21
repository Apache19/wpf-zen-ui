// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

#if DEBUG
using System.Diagnostics;

namespace Wpf.Zen.Ui.VirtualInput.Infrastructure;

/// <summary>
/// Reports synchronous operations exceeding one 60 Hz frame during development.
/// </summary>
/// <remarks>
/// Times are inclusive of nested calls and must not be added together. Call sites are excluded
/// from Release builds. Control state is captured at entry; no editor content is recorded.
/// </remarks>
internal readonly struct SoftKeyboardPerformanceScope : IDisposable
{
    private readonly long    _started;
    private readonly string  _operation;
    private readonly bool?   _isVisible;
    private readonly bool?   _isLoaded;

    /// <summary>
    /// Starts timing a named operation, optionally recording its keyboard lifecycle state.
    /// </summary>
    internal SoftKeyboardPerformanceScope(string operation, SoftKeyboard? keyboard = null)
    {
        _operation = operation;
        _isVisible = keyboard?.IsVisible;
        _isLoaded  = keyboard?.IsLoaded;
        _started   = Stopwatch.GetTimestamp();
    }

    /// <summary>
    /// Writes one structured diagnostic when the operation takes at least 16 milliseconds.
    /// </summary>
    public void Dispose()
    {
        var elapsed = (Stopwatch.GetTimestamp() - _started) * 1000D / Stopwatch.Frequency;
        if (elapsed >= 16)
        {
            Debug.WriteLine(FormattableString.Invariant(
                $"[SoftKeyboard.Performance] Slow OPERATION; operation={_operation}, elapsedMs={elapsed:F3}, isVisible={_isVisible}, isLoaded={_isLoaded}.")
            );
        }
    }
}
#endif