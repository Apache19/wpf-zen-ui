// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;
using Wpf.Zen.Ui.VirtualInput.Interop;

namespace Wpf.Zen.Ui.VirtualInput.InputMethods;

/// <summary>
/// Provides TSF mode operations for input-mode adapters on the focused WPF editor's dispatcher.
/// </summary>
/// <remarks>
/// These operations borrow WPF's initialized thread manager. They neither initialize TSF nor
/// change focus.
/// Callers must interpret the resulting platform values for the active input method's contract.
/// </remarks>
public static class SoftKeyboardTsfInputMode
{
    private const uint ButtonHidden   = 0x00000001;
    private const uint ButtonDisabled = 0x00000002;
    private const uint LeftClick      = 2;

    private static readonly Guid KeyboardOpenId       = new("58273AAD-01BB-4164-95C6-755BA0B5162D");
    private static readonly Guid KeyboardConversionId = new("CCF05DD8-4A87-11D7-A6E2-00065B84435C");
    private static readonly Guid InputModeItemId      = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    /// <summary>
    /// Reads the raw TSF input-mode compartments and discovers the active service's standard
    /// input-mode command.
    /// </summary>
    /// <param name="serviceId">The expected active TIP.</param>
    /// <returns>
    /// A snapshot when open or conversion state is readable; otherwise null. Missing compartments
    /// remain unavailable. Command discovery remains false when the service does not publish
    /// <c>GUID_LBI_INPUTMODE</c>.
    /// </returns>
    public static SoftKeyboardTsfInputModeState? GetState(Guid serviceId)
    {
        var open       = GetCompartmentInteger(KeyboardOpenId);
        var conversion = GetCompartmentInteger(KeyboardConversionId);

        if (open is null && conversion is null)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] TSF state read failed: open and conversion "
                + $"compartments are unavailable; serviceId={serviceId}."
            );
            return null;
        }

        var canSwitch = TryGetInputModeButton(
            serviceId,
            requireVisible: false,
            out var button
        );

        Debug.WriteLine(
            "[SoftKeyboard.InputMode] TSF state read; "
            + $"serviceId={serviceId}, openValue={FormatValue(open)}, "
            + $"conversionValue={FormatValue(conversion)}, inputModeCommand={canSwitch}."
        );

        ReleaseComObject(button);

        return new SoftKeyboardTsfInputModeState(open, conversion, canSwitch);
    }

    /// <summary>
    /// Reads the active service's standard TSF input-mode button and copies its current icon.
    /// </summary>
    /// <param name="serviceId">The expected active TIP.</param>
    /// <returns>
    /// A button descriptor when the item is visible, enabled, owned by the service, and supplies an
    /// icon; otherwise null.
    /// </returns>
    /// <remarks>
    /// The descriptor represents the language-bar command, not a decoded keyboard state. The icon
    /// is copied to a frozen WPF bitmap before the native handle is released.
    /// </remarks>
    public static SoftKeyboardInputModeButton? GetInputModeButton(Guid serviceId)
    {
        if (!TryGetInputModeButton(serviceId, requireVisible: true, out var button))
        {
            return null;
        }

        IntPtr icon = default;

        try
        {
            var iconResult = button!.GetIcon(out icon);
            if (iconResult < 0 || icon == IntPtr.Zero)
            {
                Debug.WriteLine(
                    "[SoftKeyboard.InputMode] Input-mode presentation unavailable: "
                    + $"icon lookup failed; serviceId={serviceId}, "
                    + $"hresult=0x{iconResult:X8}, iconHandle=0x{icon.ToInt64():X}."
                );
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions()
            );

            source.Freeze();

            return new SoftKeyboardInputModeButton(
                CreatePresentationId(serviceId, source),
                source,
                GetAutomationName(button)
            );
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] Input-mode presentation failed with exception; "
                + $"serviceId={serviceId}, exception={exception.GetType().FullName}, "
                + $"message='{exception.Message}'."
            );
            return null;
        }
        finally
        {
            if (icon != IntPtr.Zero)
            {
                _ = PInvoke.DestroyIcon(new HICON(icon));
            }

            ReleaseComObject(button);
        }
    }

    /// <summary>
    /// Reads the integer keyboard-open compartment without WPF's Boolean conversion.
    /// </summary>
    /// <returns>
    /// The native integer, or null when the host or compartment value is unavailable.
    /// </returns>
    public static int? GetKeyboardOpenValue()
    {
        return GetCompartmentInteger(KeyboardOpenId);
    }

    /// <summary>
    /// Reads the integer keyboard conversion compartment at its TSF notification boundary.
    /// </summary>
    /// <returns>The raw TSF bit field, or null when the value is unavailable.</returns>
    internal static int? GetKeyboardConversionValue()
    {
        return GetCompartmentInteger(KeyboardConversionId);
    }

    /// <summary>
    /// Gets whether the expected service currently publishes an enabled input-mode command.
    /// </summary>
    internal static bool CanInvokeInputModeButton(Guid serviceId)
    {
        var available = TryGetInputModeButton(
            serviceId,
            requireVisible: false,
            out var button
        );

        ReleaseComObject(button);
        return available;
    }

    /// <summary>
    /// Invokes only the active service's standard language-bar command.
    /// </summary>
    internal static bool TryInvokeInputModeButton(SoftKeyboardInputMethod inputMethod)
    {
        return TrySwitchInputMode(inputMethod.ClassId);
    }

    /// <summary>
    /// Writes the exact conversion integer selected by a service-specific adapter.
    /// </summary>
    /// <param name="inputMethod">The active text-service profile.</param>
    /// <param name="conversionMode">The complete replacement integer.</param>
    /// <returns>Whether TSF accepted the write.</returns>
    public static bool TrySetKeyboardConversionMode(
        SoftKeyboardInputMethod inputMethod, int conversionMode)
    {
        return TrySetCompartmentInteger(inputMethod, KeyboardConversionId, conversionMode);
    }

    /// <summary>
    /// Writes the exact open integer selected by a service-specific adapter.
    /// </summary>
    /// <param name="inputMethod">The active text-service profile.</param>
    /// <param name="openValue">The service-defined replacement integer.</param>
    /// <returns>Whether TSF accepted the write.</returns>
    public static bool TrySetKeyboardOpenValue(SoftKeyboardInputMethod inputMethod, int openValue)
    {
        return TrySetCompartmentInteger(inputMethod, KeyboardOpenId, openValue);
    }

    /// <summary>
    /// Transports an opaque integer to TSF without normalizing it through WPF properties.
    /// </summary>
    private static bool TrySetCompartmentInteger(
        SoftKeyboardInputMethod inputMethod, Guid compartmentId, int value)
    {
        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        try
        {
            if (inputMethod.ClassId == Guid.Empty
                || WpfTextServicesAccessor.TryGetThreadManager() is not ITfCompartmentMgr manager
                || manager.GetCompartment(in compartmentId, out var compartment) < 0)
            {
                return false;
            }

            object replacement = value;
            return compartment.SetValue(0, ref replacement) >= 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or COMException or PlatformNotSupportedException)
        {
            Debug.WriteLine($"[SoftKeyboard.InputMode] Compartment write failed: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    /// Invokes the current TSF language bar's standard input-mode button once.
    /// </summary>
    /// <param name="serviceId">
    /// The expected active TIP, whose language-bar item may be invoked.
    /// </param>
    /// <returns>True when an enabled button accepted the invocation; otherwise false.</returns>
    /// <remarks>
    /// This invokes the service-defined transition rather than setting an absolute Boolean state.
    /// An adapter must first read its native mode and determine whether the command applies. The
    /// keyboard restores editor focus and finishes composition before requesting the transition.
    /// </remarks>
    public static bool TrySwitchInputMode(Guid serviceId)
    {
        if (!TryGetInputModeButton(serviceId, requireVisible: false, out var button))
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] TSF command lookup failed; "
                + $"serviceId={serviceId}."
            );
            return false;
        }

        try
        {
            // GUID_LBI_INPUTMODE is the TSF input-mode command, even when the language bar itself
            // is hidden. The left-click action does not need a menu anchor or a synthesized hotkey.
            var rectangle = new TfInputModeRectangle();
            var result = button!.OnClick(LeftClick, default, in rectangle);
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE OnClick completed; "
                + $"serviceId={serviceId}, hresult=0x{result:X8}."
            );
            return result >= 0;
        }
        finally
        {
            ReleaseComObject(button);
        }
    }

    /// <summary>
    /// Reads an integer compartment without applying WPF's Boolean or managed-flag conversion.
    /// </summary>
    private static int? GetCompartmentInteger(Guid compartmentId)
    {
        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfCompartmentMgr manager
            || manager.GetCompartment(in compartmentId, out var compartment) < 0)
        {
            return null;
        }

        // Compartment RCWs can also be used by WPF's state-notification machinery. Keep their
        // managed lifetime intact; unlike enumeration snapshots, they belong to the TSF host.
        return compartment.GetValue(out var value) >= 0 && value is int integer ? integer : null;
    }

    /// <summary>
    /// Resolves the enabled standard input-mode command owned by the expected active service.
    /// </summary>
    private static bool TryGetInputModeButton(
        Guid serviceId,
        bool requireVisible,
        out ITfLangBarItemButton? button)
    {
        button = null;

        if (serviceId == Guid.Empty)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup rejected: "
                + "expected service ID is empty."
            );
            return false;
        }

        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfLangBarItemMgr manager)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup failed: "
                + $"language-bar manager is unavailable; serviceId={serviceId}."
            );
            return false;
        }

        var getResult = manager.GetItem(in InputModeItemId, out var item);
        if (getResult < 0)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup failed: item is unavailable; "
                + $"serviceId={serviceId}, hresult=0x{getResult:X8}."
            );
            return false;
        }

        if (item is not ITfLangBarItemButton candidate)
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup failed: "
                + $"item does not implement ITfLangBarItemButton; serviceId={serviceId}."
            );
            ReleaseComObject(item);
            return false;
        }

        var infoResult = candidate.GetInfo(out var info);
        var statusResult = candidate.GetStatus(out var status);
        var serviceMatches = infoResult >= 0 && info.ServiceId == serviceId;
        var isDisabled = statusResult >= 0 && (status & ButtonDisabled) != 0;
        var isHidden = statusResult >= 0 && (status & ButtonHidden) != 0;

        if (infoResult < 0 || !serviceMatches || statusResult < 0 || isDisabled
            || (requireVisible && isHidden))
        {
            Debug.WriteLine(
                "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup rejected; "
                + $"serviceId={serviceId}, itemServiceId="
                + $"{(infoResult >= 0 ? info.ServiceId : Guid.Empty)}, "
                + $"infoHresult=0x{infoResult:X8}, statusHresult=0x{statusResult:X8}, "
                + $"status=0x{status:X8}, disabled={isDisabled}, hidden={isHidden}, "
                + $"requireVisible={requireVisible}."
            );
            ReleaseComObject(candidate);
            return false;
        }

        Debug.WriteLine(
            "[SoftKeyboard.InputMode] GUID_LBI_INPUTMODE lookup succeeded; "
            + $"serviceId={serviceId}, status=0x{status:X8}, hidden={isHidden}, "
            + $"requireVisible={requireVisible}."
        );
        button = candidate;
        return true;
    }

    /// <summary>
    /// Formats a nullable TSF integer as both its decimal value and native hexadecimal bit field.
    /// </summary>
    private static string FormatValue(int? value)
    {
        return value is { } integer ? $"{integer} (0x{integer:X8})" : "<unavailable>";
    }

    /// <summary>
    /// Reads the language-bar item's stable description without interpreting its state text.
    /// </summary>
    private static string GetAutomationName(ITfLangBarItemButton button)
    {
        try
        {
            if (button.GetTooltipString(out var tooltip) >= 0
                && !string.IsNullOrWhiteSpace(tooltip))
            {
                return tooltip!;
            }

            if (button.GetInfo(out var info) >= 0 && !string.IsNullOrWhiteSpace(info.Description))
            {
                return info.Description;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // Accessible naming is best effort and must not discard a valid command icon.
        }

        return "Switch input mode";
    }

    /// <summary>
    /// Creates an identity that changes when the copied icon pixels change.
    /// </summary>
    private static string CreatePresentationId(Guid serviceId, BitmapSource source)
    {
        var stride = checked((source.PixelWidth * source.Format.BitsPerPixel + 7) / 8);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);

        const ulong offset = 14695981039346656037;
        const ulong prime  = 1099511628211;

        var hash = offset;
        foreach (var value in pixels)
        {
            hash ^= value;
            hash *= prime;
        }

        return $"tsf:{serviceId:D}:{source.PixelWidth}x{source.PixelHeight}:{hash:X16}";
    }

    /// <summary>
    /// Releases the reference returned by the language-bar item manager without detaching a
    /// shared RCW completely.
    /// </summary>
    private static void ReleaseComObject(object? value)
    {
        if (value is null || !Marshal.IsComObject(value))
        {
            return;
        }

        try
        {
            _ = Marshal.ReleaseComObject(value);
        }
        catch (InvalidComObjectException)
        {
            // A provider can expose the same identity through another projection path. The local
            // lookup owns no callback or native state after the synchronous operation completes.
        }
    }
}
