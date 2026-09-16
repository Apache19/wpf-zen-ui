// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.Ime;

namespace Wpf.Zen.Ui.VirtualInput.Interop;

/// <summary>
/// Reads mode values from the IMM input context owned by one application window.
/// </summary>
internal static class ImmInputContextState
{
    /// <summary>
    /// Reads the open flag from the input context attached to <paramref name="window"/>.
    /// </summary>
    /// <remarks>
    /// The returned value belongs to the HWND that raised the IMM notification. The input context
    /// is borrowed only for the duration of this call and is always released to the same HWND.
    /// </remarks>
    internal static bool TryReadOpen(IntPtr window, out int open)
    {
        open = 0;

        var hwnd = new HWND(window);
        var context = PInvoke.ImmGetContext(hwnd);
        if (context.IsNull)
        {
            return false;
        }

        try
        {
            open = PInvoke.ImmGetOpenStatus(context) ? 1 : 0;
            return true;
        }
        finally
        {
            _ = PInvoke.ImmReleaseContext(hwnd, context);
        }
    }

    /// <summary>
    /// Reads the conversion bit field from the input context attached to
    /// <paramref name="window"/>.
    /// </summary>
    /// <remarks>
    /// The returned value belongs to the HWND that raised the IMM notification. The input context
    /// is borrowed only for the duration of this call and is always released to the same HWND.
    /// </remarks>
    internal static unsafe bool TryReadConversion(IntPtr window, out int conversion)
    {
        conversion = 0;

        var hwnd = new HWND(window);
        var context = PInvoke.ImmGetContext(hwnd);
        if (context.IsNull)
        {
            return false;
        }

        try
        {
            IME_CONVERSION_MODE conversionValue;
            IME_SENTENCE_MODE sentenceValue;
            if (!PInvoke.ImmGetConversionStatus(context, &conversionValue, &sentenceValue))
            {
                return false;
            }

            conversion = unchecked((int)conversionValue);
            return true;
        }
        finally
        {
            _ = PInvoke.ImmReleaseContext(hwnd, context);
        }
    }
}
