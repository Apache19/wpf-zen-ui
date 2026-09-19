// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Reflection;
using Expression = System.Linq.Expressions.Expression;

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
    private static readonly Lazy<Func<HwndSource, bool, bool>?> InvokeInputPane =
        WpfReflectionBridge.Create("InputPane", CreateInvoker);

    /// <summary>
    /// Asks Windows to hide its input pane for the specified WPF window.
    /// </summary>
    /// <returns>
    /// Unavailable when the host or WPF bridge cannot be used; otherwise Hidden or Rejected
    /// according to WPF's result. WPF reports COM failures during hiding as a rejected request.
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
    /// <param name="window">The HWND whose focused editor may need the system input pane.</param>
    /// <returns>
    /// <see langword="true"/> when Windows accepted the advisory show request.
    /// </returns>
    /// <remarks>
    /// The caller performs the same foreground and focused-editor checks used by the suppression
    /// lifecycle. Windows may reject the request when a physical keyboard,
    /// user setting, or input policy says the touch keyboard is inappropriate.
    /// </remarks>
    internal static bool TryShow(IntPtr window)
    {
        return TryInvoke(window, show: true, out var accepted) && accepted;
    }

    /// <summary>
    /// Resolves the live WPF host and contains optional framework integration failures.
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
        catch (Exception exception) when (WpfReflectionBridge.IsUnavailable(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Resolves the runtime contract without retaining a window or a thread-affine WinRT object.
    /// </summary>
    private static Func<HwndSource, bool, bool>? CreateInvoker()
    {
        var paneType = typeof(HwndSource).Assembly.GetType(
            "MS.Internal.WindowsRuntime.Windows.UI.ViewManagement.InputPane"
        );

        return CreateInvoker(paneType);
    }

    /// <summary>
    /// Compiles the complete pane contract once, adapting its private type to IDisposable.
    /// Each invocation acquires and releases one wrapper on the calling dispatcher.
    /// </summary>
    internal static Func<HwndSource, bool, bool>? CreateInvoker(Type? paneType)
    {
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

        var sourceParameter = Expression.Parameter(typeof(HwndSource), "source");
        var paneParameter   = Expression.Parameter(typeof(IDisposable), "pane");
        var showParameter   = Expression.Parameter(typeof(bool), "show");
        var typedPane       = Expression.Convert(paneParameter, paneType);

        var acquire = Expression.Lambda<Func<HwndSource, IDisposable?>>(
            Expression.Convert(
                Expression.Call(getForWindow, sourceParameter),
                typeof(IDisposable)
            ),
            sourceParameter
        ).Compile();

        var request = Expression.Lambda<Func<IDisposable, bool, bool>>(
            Expression.Condition(
                showParameter,
                Expression.Call(typedPane, tryShow),
                Expression.Call(typedPane, tryHide)),
            paneParameter,
            showParameter
        ).Compile();

        return (source, show) =>
        {
#if DEBUG
            var  started = Stopwatch.GetTimestamp();
            long acquired;
            long requested;
#endif
            bool accepted;

            // Match WPF's TipTsfHelper: acquire and dispose one wrapper per request. InputPane
            // releases its pane RCW itself; never release its shared activation factory or cache
            // the wrapper across HWND lifetimes or dispatcher threads.
            using (var pane = acquire(source))
            {
                if (pane is null)
                {
                    throw new PlatformNotSupportedException();
                }

#if DEBUG
                acquired = Stopwatch.GetTimestamp();
#endif
                accepted = request(pane, show);
#if DEBUG
                requested = Stopwatch.GetTimestamp();
#endif
            }

#if DEBUG
            var completed         = Stopwatch.GetTimestamp();
            var totalMilliseconds = (completed - started) * 1000.0 / Stopwatch.Frequency;

            if (totalMilliseconds >= 16)
            {
                Debug.WriteLine(
                    $"[SoftKeyboard.SystemInputPane] Slow INPUT PANE request; show={show}, "
                    + $"accepted={accepted}, "
                    + $"acquireMs={(acquired - started) * 1000.0 / Stopwatch.Frequency:F3}, "
                    + $"requestMs={(requested - acquired) * 1000.0 / Stopwatch.Frequency:F3}, "
                    + $"disposeMs={(completed - requested) * 1000.0 / Stopwatch.Frequency:F3}."
                );
            }
#endif
            return accepted;
        };
    }
}