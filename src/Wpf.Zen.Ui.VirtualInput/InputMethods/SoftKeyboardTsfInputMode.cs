// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.InteropServices;
using System.Windows.Input;
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

    private const int ConversionNative       = 0x0001;
    private const int ConversionKatakana     = 0x0002;
    private const int ConversionFullShape    = 0x0008;
    private const int ConversionRoman        = 0x0010;
    private const int ConversionCharCode     = 0x0020;
    private const int ConversionNoConversion = 0x0100;
    private const int ConversionEudc         = 0x0200;
    private const int ConversionSymbol       = 0x0400;
    private const int ConversionFixed        = 0x0800;

    private static readonly Guid KeyboardOpenId       = new("58273AAD-01BB-4164-95C6-755BA0B5162D");
    private static readonly Guid KeyboardConversionId = new("CCF05DD8-4A87-11D7-A6E2-00065B84435C");
    private static readonly Guid InputModeItemId      = new("2C77A81E-41CC-4178-A3A7-5F8A987568E6");

    // Keys are Windows primary-language identifiers so every regional Chinese language shares the
    // same cap. Unlisted languages stay hidden until an extension supplies a stronger contract.
    private static readonly IReadOnlyDictionary<ushort, (string Label, string AutomationName)>
        OpenCloseLanguages = new Dictionary<ushort, (string, string)>
        {
            [0x04] = ("中", "Chinese input"),
            [0x11] = ("日", "Japanese input"),
            [0x12] = ("한", "Korean input"),
        };

    /// <summary>
    /// Reads the raw TSF input-mode compartments and discovers the active service's standard
    /// input-mode command.
    /// </summary>
    /// <param name="serviceId">The expected active TIP.</param>
    /// <returns>
    /// A snapshot when open or conversion state is readable; otherwise null. Conversion falls back
    /// to WPF's normalized IMM32-compatible value when no TSF compartment exists. Command discovery
    /// remains false when the service does not publish <c>GUID_LBI_INPUTMODE</c>.
    /// </returns>
    public static SoftKeyboardTsfInputModeState? GetState(Guid serviceId)
    {
        var open       = GetCompartmentInteger(KeyboardOpenId);
        var conversion = GetCompartmentInteger(KeyboardConversionId)
            ?? GetWpfKeyboardConversionMode();

        if (open is null && conversion is null)
        {
            return null;
        }

        var canSwitch = TryGetInputModeButton(
            serviceId,
            requireVisible: false,
            out var button
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
            if (button!.GetIcon(out icon) < 0 || icon == IntPtr.Zero)
            {
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
    /// Resolves the best mode command offered by the Windows input-method source.
    /// </summary>
    /// <param name="inputMethod">The active input method.</param>
    /// <returns>
    /// The service-owned language-bar button, or a keyboard-open fallback for a supported
    /// language; otherwise null.
    /// </returns>
    internal static SoftKeyboardInputModeButton? GetInputModeButton(
        SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return null;
        }

        var languageBarButton = GetInputModeButton(inputMethod.ClassId);
        if (languageBarButton is not null
            || !OpenCloseLanguages.ContainsKey(GetPrimaryLanguageId(inputMethod.LanguageId)))
        {
            return languageBarButton;
        }

        return CreateKeyboardOpenButton(inputMethod.LanguageId, GetKeyboardOpenValue());
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
    /// Reads the integer keyboard conversion compartment without assigning it to a profile.
    /// </summary>
    /// <returns>The raw TSF bit field, or null when the value is unavailable.</returns>
    /// <remarks>
    /// The compartment is thread-scoped. Callers must associate the value only with the active
    /// profile generation that received the corresponding conversion notification.
    /// </remarks>
    internal static int? GetKeyboardConversionValue()
    {
        return GetCompartmentInteger(KeyboardConversionId)
            ?? GetWpfKeyboardConversionMode();
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
    /// Invokes the service-owned language-bar command, falling back to the keyboard-open
    /// compartment for supported languages.
    /// </summary>
    /// <param name="inputMethod">The active input method.</param>
    /// <returns>
    /// True when one of the two command paths accepted the request; otherwise false.
    /// </returns>
    internal static bool TryInvokeInputModeButton(SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return false;
        }

        var languageBarButton = GetInputModeButton(inputMethod.ClassId);
        return languageBarButton is not null
            ? TrySwitchInputMode(inputMethod.ClassId)
            : TryToggleKeyboardOpen(inputMethod);
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
            return false;
        }

        try
        {
            // GUID_LBI_INPUTMODE is the TSF input-mode command, even when the language bar itself
            // is hidden. The left-click action does not need a menu anchor or a synthesized hotkey.
            var rectangle = new TfInputModeRectangle();
            return button!.OnClick(LeftClick, default, in rectangle) >= 0;
        }
        finally
        {
            ReleaseComObject(button);
        }
    }

    /// <summary>
    /// Gets whether an input method belongs to a language covered by the core binary
    /// keyboard-open policy.
    /// </summary>
    /// <param name="inputMethod">The active input method.</param>
    /// <returns>
    /// True when the language supports the keyboard-open fallback; otherwise false.
    /// </returns>
    /// <remarks>
    /// This capability does not describe conversion modes. Extensions may combine it with their
    /// own state interpretation while retaining the core open/close transition.
    /// </remarks>
    public static bool SupportsKeyboardOpenFallback(SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        return inputMethod.ClassId != Guid.Empty
            && OpenCloseLanguages.ContainsKey(GetPrimaryLanguageId(inputMethod.LanguageId));
    }

    /// <summary>
    /// Toggles WPF's keyboard-open state without reading or changing the conversion compartment.
    /// </summary>
    /// <param name="inputMethod">The active input method.</param>
    /// <returns>True when the state-change request was issued; otherwise false.</returns>
    /// <remarks>
    /// This operation is limited to languages covered by the core binary fallback policy. WPF
    /// writes <c>GUID_COMPARTMENT_KEYBOARD_OPENCLOSE</c> and mirrors the state to IMM32 when its
    /// compatibility path is active.
    /// </remarks>
    public static bool TryToggleKeyboardOpen(SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        if (inputMethod.ClassId == Guid.Empty
            || !OpenCloseLanguages.ContainsKey(GetPrimaryLanguageId(inputMethod.LanguageId)))
        {
            return false;
        }

        var openValue = GetKeyboardOpenValue();
        var currentInputMethod = InputMethod.Current;
        if (openValue is null || currentInputMethod is null)
        {
            return false;
        }

        try
        {
            currentInputMethod.ImeState = openValue == 0
                ? InputMethodState.On
                : InputMethodState.Off;

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or COMException
            or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Writes the keyboard conversion state used by the active WPF editor.
    /// </summary>
    /// <param name="inputMethod">The active input method.</param>
    /// <param name="conversionMode">The complete TSF conversion-mode bit field to write.</param>
    /// <returns>True when TSF accepted the conversion-mode value; otherwise false.</returns>
    /// <remarks>
    /// This method does not interpret the bit field. It writes the TSF compartment when available
    /// and otherwise uses WPF's normalized IMM32-compatible path. A service-specific adapter must
    /// derive a complete replacement value from a fresh observation and preserve unowned flags.
    /// </remarks>
    public static bool TrySetKeyboardConversionMode(
        SoftKeyboardInputMethod inputMethod,
        int conversionMode)
    {
        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        if (inputMethod.ClassId == Guid.Empty)
        {
            return false;
        }

        if (WpfTextServicesAccessor.TryGetThreadManager() is not ITfCompartmentMgr manager
            || manager.GetCompartment(in KeyboardConversionId, out var compartment) < 0)
        {
            return TrySetWpfKeyboardConversionMode(conversionMode);
        }

        try
        {
            object value = conversionMode;
            return compartment.SetValue(0, ref value) >= 0;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or COMException
            or PlatformNotSupportedException)
        {
            return false;
        }
    }

    /// <summary>
    /// Sends a dedicated East Asian conversion-mode key through the focused window's hardware
    /// keyboard input stream.
    /// </summary>
    /// <param name="inputMethod">The active input method expected to consume the key.</param>
    /// <param name="modeKey">A dedicated WPF conversion-mode key.</param>
    /// <returns>True when the complete key press was inserted; otherwise false.</returns>
    /// <remarks>
    /// Some text services publish writable TSF conversion compartments but do not treat a direct
    /// compartment write as an input-mode command. Their dedicated conversion keys update both
    /// the service's effective mode and its published compartments. Callers must already have
    /// validated and focused the intended editor before invoking this operation.
    /// </remarks>
    internal static bool TrySendKeyboardConversionModeKey(
        SoftKeyboardInputMethod inputMethod,
        Key modeKey)
    {
        if (inputMethod is null)
        {
            throw new ArgumentNullException(nameof(inputMethod));
        }

        if (inputMethod.ClassId == Guid.Empty || !IsConversionModeKey(modeKey))
        {
            return false;
        }

        return SoftKeyboardInputInjector.SendVirtualKey(
            modeKey,
            ModifierKeys.None,
            useScanCode: false
        );
    }

    /// <summary>
    /// Gets whether a key is one of the virtual-key commands reserved for East Asian conversion
    /// modes.
    /// </summary>
    private static bool IsConversionModeKey(Key key)
    {
        return key is Key.KanjiMode
            or Key.DbeAlphanumeric
            or Key.DbeKatakana
            or Key.DbeHiragana
            or Key.DbeSbcsChar
            or Key.DbeDbcsChar
            or Key.DbeRoman
            or Key.DbeNoRoman;
    }

    /// <summary>
    /// Reads WPF's normalized conversion state when the active TIP exposes no TSF compartment.
    /// </summary>
    /// <remarks>
    /// WPF uses the same TSF compartment when available and falls back to its IMM32 compatibility
    /// path for legacy services. The public flags use different numeric values and are therefore
    /// translated explicitly rather than cast.
    /// </remarks>
    private static int? GetWpfKeyboardConversionMode()
    {
        try
        {
            return InputMethod.Current is { } inputMethod
                ? FromWpfConversionMode(inputMethod.ImeConversionMode)
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException
            or COMException
            or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Writes WPF's normalized conversion state for a TIP using the IMM32 compatibility path.
    /// </summary>
    private static bool TrySetWpfKeyboardConversionMode(int conversionMode)
    {
        var inputMethod = InputMethod.Current;
        if (inputMethod is null)
        {
            return false;
        }

        try
        {
            inputMethod.ImeConversionMode = ToWpfConversionMode(conversionMode);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
            or InvalidOperationException
            or COMException
            or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static int FromWpfConversionMode(ImeConversionModeValues mode)
    {
        var value = 0;
        Add(ImeConversionModeValues.Native, ConversionNative);
        Add(ImeConversionModeValues.Katakana, ConversionKatakana);
        Add(ImeConversionModeValues.FullShape, ConversionFullShape);
        Add(ImeConversionModeValues.Roman, ConversionRoman);
        Add(ImeConversionModeValues.CharCode, ConversionCharCode);
        Add(ImeConversionModeValues.NoConversion, ConversionNoConversion);
        Add(ImeConversionModeValues.Eudc, ConversionEudc);
        Add(ImeConversionModeValues.Symbol, ConversionSymbol);
        Add(ImeConversionModeValues.Fixed, ConversionFixed);
        return value;

        void Add(ImeConversionModeValues flag, int nativeFlag)
        {
            if ((mode & flag) != 0)
            {
                value |= nativeFlag;
            }
        }
    }

    private static ImeConversionModeValues ToWpfConversionMode(int mode)
    {
        var value = ImeConversionModeValues.Alphanumeric;
        Add(ConversionNative, ImeConversionModeValues.Native);
        Add(ConversionKatakana, ImeConversionModeValues.Katakana);
        Add(ConversionFullShape, ImeConversionModeValues.FullShape);
        Add(ConversionRoman, ImeConversionModeValues.Roman);
        Add(ConversionCharCode, ImeConversionModeValues.CharCode);
        Add(ConversionNoConversion, ImeConversionModeValues.NoConversion);
        Add(ConversionEudc, ImeConversionModeValues.Eudc);
        Add(ConversionSymbol, ImeConversionModeValues.Symbol);
        Add(ConversionFixed, ImeConversionModeValues.Fixed);
        return value;

        void Add(int nativeFlag, ImeConversionModeValues flag)
        {
            if ((mode & nativeFlag) != 0)
            {
                value |= flag;
            }
        }
    }

    /// <summary>
    /// Creates a direct representation of the keyboard-open compartment for a language whose
    /// input method uses that binary contract.
    /// </summary>
    /// <param name="languageId">The Windows language identifier of the active input method.</param>
    /// <param name="openValue">
    /// The raw keyboard-open compartment value, or null when unavailable.
    /// </param>
    /// <returns>
    /// A stateful key-cap descriptor for a supported language and value; otherwise null.
    /// </returns>
    internal static SoftKeyboardInputModeButton? CreateKeyboardOpenButton(
        ushort languageId,
        int? openValue)
    {
        if (openValue is null
            || !OpenCloseLanguages.TryGetValue(GetPrimaryLanguageId(languageId), out var language))
        {
            return null;
        }

        var isOpen = openValue != 0;
        return new SoftKeyboardInputModeButton(
            $"tsf-openclose:{languageId:X4}:{(isOpen ? 1 : 0)}",
            isOpen ? language.Label : "A",
            isOpen ? language.AutomationName : "Direct input"
        );
    }

    /// <summary>
    /// Extracts the Windows primary-language identifier shared by regional language identifiers.
    /// </summary>
    private static ushort GetPrimaryLanguageId(ushort languageId)
    {
        return (ushort)(languageId & 0x03FF);
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

        if (serviceId == Guid.Empty
            || WpfTextServicesAccessor.TryGetThreadManager() is not ITfLangBarItemMgr manager
            || manager.GetItem(in InputModeItemId, out var item) < 0)
        {
            return false;
        }

        if (item is not ITfLangBarItemButton candidate)
        {
            ReleaseComObject(item);
            return false;
        }

        if (candidate.GetInfo(out var info) < 0
            || info.ServiceId != serviceId
            || candidate.GetStatus(out var status) < 0
            || (status & ButtonDisabled) != 0
            || (requireVisible && (status & ButtonHidden) != 0))
        {
            ReleaseComObject(candidate);
            return false;
        }

        button = candidate;
        return true;
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
