// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Runtime.InteropServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop;

/// <summary>
/// Requests that Windows show or hide the input pane associated with a WPF HWND.
/// </summary>
/// <remarks>
/// WPF exposes no public input-pane API. This controller reflects PresentationCore's internal
/// InputPane wrapper, which owns WinRT activation, factory caching, and per-request COM cleanup.
/// Internal members can change between WPF versions; missing members or activation failures make
/// the service unavailable. Calls must run on the host's dispatcher thread. Windows remains
/// authoritative and may reject a visibility request because of focus, settings, or input policy.
/// </remarks>
internal static class SystemInputPaneController
{
    private static readonly Lazy<Func<HwndSource, bool, bool>?> InvokeInputPane = new(CreateInvoker);

    /// <summary>
    /// Asks Windows to hide its input pane for the specified WPF window.
    /// </summary>
    /// <returns>
    /// Unavailable when the host or WPF bridge cannot be used; otherwise Hidden or Rejected according
    /// to WPF's result. WPF also reports COM failures during the hide operation as a rejected request.
    /// </returns>
    internal static SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr window)
    {
        if (!TryInvoke(window, show: false, out var accepted))
        {
            return SoftKeyboardSystemKeyboardSuppressionState.Unavailable;
        }

        return accepted
            ? SoftKeyboardSystemKeyboardSuppressionState.Hidden
            : SoftKeyboardSystemKeyboardSuppressionState.Rejected;
    }

    /// <summary>
    /// Asks Windows to restore its input pane after this control previously hid it.
    /// </summary>
    /// <param name="window">The HWND whose focused editor may require the system input pane.</param>
    /// <returns><see langword="true"/> when Windows accepted the advisory show request.</returns>
    /// <remarks>
    /// The caller performs the same foreground and focused-editor checks used by the suppression
    /// lifecycle. Windows remains authoritative and can reject the request when a physical keyboard,
    /// user setting, or input policy says the touch keyboard is inappropriate.
    /// </remarks>
    internal static bool TryShow(IntPtr window)
    {
        return TryInvoke(window, show: true, out var accepted) && accepted;
    }

    /// <summary>
    /// Resolves the live WPF host and contains failures at the optional framework integration boundary.
    /// </summary>
    private static bool TryInvoke(IntPtr window, bool show, out bool accepted)
    {
        accepted = false;

        if (window == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var source = HwndSource.FromHwnd(window);
            if (source is null || source.IsDisposed || !source.Dispatcher.CheckAccess())
            {
                return false;
            }

            var invoke = InvokeInputPane.Value;
            if (invoke is null)
            {
                return false;
            }

            accepted = invoke(source, show);
            return true;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Caches the internal method contract without retaining a window or a thread-affine WinRT object.
    /// </summary>
    private static Func<HwndSource, bool, bool>? CreateInvoker()
    {
        var paneType = typeof(HwndSource).Assembly.GetType(
            "MS.Internal.WindowsRuntime.Windows.UI.ViewManagement.InputPane"
        );

        var getForWindow = paneType?.GetMethod(
            "GetForWindow",
            BindingFlags.Static | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(HwndSource)],
            modifiers: null
        );

        var tryShow = paneType?.GetMethod(
            "TryShow",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null
        );

        var tryHide = paneType?.GetMethod(
            "TryHide",
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: Type.EmptyTypes,
            modifiers: null
        );

        // Validate the complete contract before creating a pane so an incompatible WPF version
        // cannot leave us with an object whose ownership or return values we do not understand.
        if (paneType is null || !typeof(IDisposable).IsAssignableFrom(paneType)
            || getForWindow is null || getForWindow.ReturnType != paneType
            || tryShow?.ReturnType != typeof(bool) || tryHide?.ReturnType != typeof(bool))
        {
            return null;
        }

        return (source, show) =>
        {
            // Match WPF's TipTsfHelper: acquire and dispose one wrapper per request. InputPane
            // releases its pane RCW itself; never release its shared activation factory or cache
            // the wrapper across HWND lifetimes or dispatcher threads.
            using var pane = (IDisposable?)getForWindow.Invoke(null, [source]);

            if (pane is null)
            {
                throw new PlatformNotSupportedException();
            }

            return (bool)(show ? tryShow : tryHide).Invoke(pane, null)!;
        };
    }

    private static bool IsUnavailable(Exception exception)
    {
        // Reflection wraps activation and invocation failures. Keep them inside this optional
        // service so a framework/platform change cannot interrupt editor focus processing.
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
            or PlatformNotSupportedException
            or TypeLoadException;
    }
}
