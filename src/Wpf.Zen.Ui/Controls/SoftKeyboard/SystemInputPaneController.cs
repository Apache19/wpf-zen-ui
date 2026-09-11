// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using Windows.Win32;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Requests that Windows show or hide the input pane associated with a WPF HWND.
/// </summary>
/// <remarks>
/// IInputPaneInterop is the documented desktop interop contract introduced in Windows 10 1607.
/// It scopes the request to the host HWND and avoids locating or manipulating TabTip windows. The
/// request is advisory: Windows can reject it when the application is not foreground or policy owns
/// the input experience. Every ABI and activation failure is therefore translated into a state value.
/// </remarks>
internal static class SystemInputPaneController
{
    private const string InputPaneRuntimeClass = "Windows.UI.ViewManagement.InputPane";
    private const int    TryShowVTableSlot     = 6;
    private const int    TryHideVTableSlot     = 7;

    private static readonly Guid InputPaneInteropId = new("75CF2C57-9195-4931-8332-F0B409E916AF");
    private static readonly Guid InputPane2Id       = new("8A6B3F26-7090-4793-944C-C3F2CDE26276");

    internal static SoftKeyboardSystemKeyboardSuppressionState TryHide(IntPtr window)
    {
        if (!TryInvoke(window, TryHideVTableSlot, out var accepted))
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
        return TryInvoke(window, TryShowVTableSlot, out var accepted) && accepted;
    }

    /// <summary>
    /// Invokes one of the two parameterless IInputPane2 visibility methods by SDK vtable slot.
    /// </summary>
    private static bool TryInvoke(IntPtr window, int methodSlot, out bool accepted)
    {
        accepted = false;

        if (window == IntPtr.Zero)
        {
            return false;
        }

        var className = IntPtr.Zero;
        var factoryPointer = IntPtr.Zero;
        var panePointer = IntPtr.Zero;
        var shouldUninitialize = false;

        try
        {
            var initializeResult = NativeMethods.RoInitialize(ROInitType.SingleThreaded);
            if (initializeResult >= 0)
            {
                shouldUninitialize = true;
            }
            else if (initializeResult != HResults.RpcEChangedMode)
            {
                Marshal.ThrowExceptionForHR(initializeResult);
            }

            ThrowIfFailed(
                NativeMethods.WindowsCreateString(
                    InputPaneRuntimeClass,
                    InputPaneRuntimeClass.Length,
                    out className
                )
            );

            var interopId = InputPaneInteropId;

            ThrowIfFailed(
                NativeMethods.RoGetActivationFactory(
                    className,
                    ref interopId,
                    out factoryPointer
                )
            );

            var inputPane2Id = InputPane2Id;

            var getForWindow = GetVTableMethod<GetForWindowDelegate>(factoryPointer, 6);

            ThrowIfFailed(
                getForWindow(
                    factoryPointer,
                    window,
                    ref inputPane2Id,
                    out panePointer
                )
            );

            var operation = GetVTableMethod<TryShowOrHideDelegate>(panePointer, methodSlot);

            ThrowIfFailed(operation(panePointer, out var operationAccepted));

            accepted = operationAccepted != 0;

            return true;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return false;
        }
        finally
        {
            // InputPane is a process-wide WinRT identity also used by WPF's TipTsfHelper. This code
            // deliberately invokes its ABI directly and never creates an RCW: releasing a shared RCW
            // can detach the wrapper cached by WPF and break every later focus operation. The raw
            // references returned specifically to this call are safe to balance here.
            if (panePointer != IntPtr.Zero)
            {
                _ = Marshal.Release(panePointer);
            }

            if (factoryPointer != IntPtr.Zero)
            {
                _ = Marshal.Release(factoryPointer);
            }

            if (className != IntPtr.Zero)
            {
                _ = NativeMethods.WindowsDeleteString(className);
            }

            if (shouldUninitialize)
            {
                NativeMethods.RoUninitialize();
            }
        }
    }

    private static void ThrowIfFailed(int result)
    {
        if (result < 0)
        {
            Marshal.ThrowExceptionForHR(result);
        }
    }

    private static TDelegate GetVTableMethod<TDelegate>(IntPtr instance, int slot)
        where TDelegate : Delegate
    {
        if (instance == IntPtr.Zero)
        {
            throw new COMException("The WinRT activation returned a null interface pointer.");
        }

        var vtable = Marshal.ReadIntPtr(instance);
        var method = Marshal.ReadIntPtr(vtable, checked(slot * IntPtr.Size));

        return Marshal.GetDelegateForFunctionPointer<TDelegate>(method);
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException
            or DllNotFoundException
            or EntryPointNotFoundException
            or InvalidCastException
            or PlatformNotSupportedException
            or TypeLoadException;
    }

    private static class HResults
    {
        internal const int RpcEChangedMode = unchecked((int)0x80010106);
    }

    private enum ROInitType : uint
    {
        SingleThreaded,
    }

    // Both interfaces derive from IInspectable, so their members start after the six IUnknown and
    // IInspectable slots. The delegates include the native `this` pointer and preserve HRESULTs so
    // unsupported Windows versions and policy rejection remain normal, recoverable outcomes.
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetForWindowDelegate(
        IntPtr thisPointer,
        IntPtr appWindow,
        ref Guid interfaceId,
        out IntPtr inputPane
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int TryShowOrHideDelegate(IntPtr thisPointer, out byte result);

    private static class NativeMethods
    {
        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int RoInitialize(ROInitType initType);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern void RoUninitialize();

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int WindowsCreateString(
            [MarshalAs(UnmanagedType.LPWStr)] string sourceString,
            int length,
            out IntPtr hstring
        );

        [DllImport("api-ms-win-core-winrt-string-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int WindowsDeleteString(IntPtr hstring);

        [DllImport("api-ms-win-core-winrt-l1-1-0.dll", CallingConvention = CallingConvention.StdCall)]
        internal static extern int RoGetActivationFactory(
            IntPtr activatableClassId,
            ref Guid interfaceId,
            out IntPtr factory
        );
    }
}