// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop;

/// <summary>
/// Initializes optional WPF bridges once, caching compiled delegates and unavailable contracts.
/// </summary>
/// <remarks>
/// Factories may retain metadata and delegates, but must not capture a dispatcher, HWND, or COM
/// instance. Runtime failures belong to the individual request and must not invalidate the cache.
/// </remarks>
internal static class WpfReflectionBridge
{
    /// <summary>
    /// Creates a thread-safe lazy binding whose expected initialization failures become a cached
    /// null result. Diagnostics describe binding once rather than on every input refresh.
    /// </summary>
    internal static Lazy<T?> Create<T>(string name, Func<T?> factory) where T : class
    {
        return new Lazy<T?>(() =>
        {
            try
            {
                var binding = factory();
                Debug.WriteLine(
                    $"[SoftKeyboard.Reflection] WPF binding resolved; bridge={name}, "
                    + $"available={binding is not null}.");
                return binding;
            }
            catch (Exception exception) when (IsUnavailable(exception))
            {
                Debug.WriteLine(
                    $"[SoftKeyboard.Reflection] WPF binding unavailable; bridge={name}, "
                    + $"exception={exception.GetType().Name}.");
                return null;
            }
        });
    }

    /// <summary>
    /// Recognizes optional framework, delegate-binding, and native activation failures. Compiled
    /// calls expose the original exception instead of wrapping it in TargetInvocationException.
    /// </summary>
    internal static bool IsUnavailable(Exception exception)
    {
        return exception is TargetInvocationException
            or TypeInitializationException
            or MemberAccessException
            or ArgumentException
            or InvalidOperationException
            or System.Security.SecurityException
            or System.IO.IOException
            or COMException
            or DllNotFoundException
            or EntryPointNotFoundException
            or InvalidCastException
            or NotSupportedException
            or TypeLoadException;
    }
}
