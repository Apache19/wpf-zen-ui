// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;

namespace Wpf.Ui.Appearance;

/// <summary>
/// Reads the Windows personalization accent color without taking a compile-time dependency on
/// a Windows Runtime projection.
/// </summary>
/// <remarks>
/// WPF UI still supports Windows 7, where Windows Runtime activation APIs do not exist. Keeping
/// the ABI boundary in this small, failure-tolerant provider lets the assembly load on Windows 7
/// while using the documented <c>Windows.UI.ViewManagement.UISettings</c> contract on Windows 10+
/// when it is available.
/// </remarks>
internal static class SystemAccentColorProvider
{
    private const string UISettingsRuntimeClass = "Windows.UI.ViewManagement.UISettings";

    /// <summary>
    /// Attempts to read the single personalization accent color reported by Windows.
    /// </summary>
    /// <param name="accentColor">Receives an opaque WPF color when the operation succeeds.</param>
    /// <returns><see langword="true"/> when <paramref name="accentColor"/> was obtained.</returns>
    internal static bool TryGetAccentColor(out Color accentColor)
    {
        accentColor = default;

        IntPtr className = IntPtr.Zero;
        object? instance = null;
        var shouldUninitialize = false;

        try
        {
            // RoInitialize is balanced only when this call succeeded. WPF normally initializes
            // the UI thread as an STA already; S_FALSE merely means that initialization existed.
            // RPC_E_CHANGED_MODE is also safe here because the thread already has a usable COM
            // apartment, so activation can proceed in that existing apartment.
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
                    UISettingsRuntimeClass,
                    UISettingsRuntimeClass.Length,
                    out className
                )
            );

            ThrowIfFailed(NativeMethods.RoActivateInstance(className, out instance));

            if (instance is not IUISettings3 settings)
            {
                return false;
            }

            var color = settings.GetColorValue(UIColorType.Accent);
            accentColor = Color.FromRgb(color.R, color.G, color.B);
            return true;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            // Missing WinRT entry points are expected on Windows 7. A missing IUISettings3
            // interface is likewise expected before Windows 10. ThemeManager deliberately falls
            // back to the documented library default instead of reading undocumented registry data.
            return false;
        }
        finally
        {
            ReleaseComObject(instance);

            if (className != IntPtr.Zero)
            {
                // Cleanup must never replace the useful activation result or its failure with a
                // secondary exception. A valid HSTRING can only have been created if this entry
                // point was available in the first place.
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

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException
            or DllNotFoundException
            or EntryPointNotFoundException
            or InvalidCastException
            or PlatformNotSupportedException
            or TypeLoadException;
    }

    private static void ReleaseComObject(object? instance)
    {
        if (instance is null || !Marshal.IsComObject(instance))
        {
            return;
        }

        try
        {
            _ = Marshal.FinalReleaseComObject(instance);
        }
        catch (InvalidComObjectException)
        {
            // Another RCW path may already have released the identity. This provider owns no
            // state after GetColorValue returns, so there is nothing else to clean up.
        }
    }

    private static class HResults
    {
        internal const int RpcEChangedMode = unchecked((int)0x80010106);
    }

    private enum ROInitType : uint
    {
        SingleThreaded = 0,
    }

    private enum UIColorType
    {
        Accent = 5,
    }

    private enum TrustLevel
    {
        BaseTrust,
        PartialTrust,
        FullTrust,
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct UIColor
    {
        internal readonly byte A;
        internal readonly byte R;
        internal readonly byte G;
        internal readonly byte B;
    }

    // IUISettings3 derives from IInspectable. The first three declarations preserve the
    // IInspectable vtable slots; only GetColorValue is invoked. Using IntPtr for HSTRING avoids
    // pretending that ordinary COM string marshaling understands the Windows Runtime HSTRING ABI.
    [Guid("03021BE4-5254-4781-8194-5168F7D06D7B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    private interface IUISettings3
    {
        void GetIids(out uint iidCount, out IntPtr iids);

        void GetRuntimeClassName(out IntPtr className);

        void GetTrustLevel(out TrustLevel trustLevel);

        UIColor GetColorValue(UIColorType desiredColor);
    }

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
        internal static extern int RoActivateInstance(
            IntPtr runtimeClassId,
            [MarshalAs(UnmanagedType.Interface)] out object instance
        );
    }
}
