// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Enumerates and activates user-enabled Text Services Framework language profiles.
/// </summary>
/// <remarks>
/// The service uses only the documented ITfInputProcessorProfiles contract. A machine can have no
/// TSF profiles or can deny COM activation; in that case WPF's installed input-language list remains
/// available as a language-level fallback and the keyboard's ordinary input path is unaffected.
/// </remarks>
internal sealed class TsfInputMethodService : ISoftKeyboardInputMethodBackend
{
    private const uint ProfileTypeInputProcessor = 0x0001;
    private const uint ActivateForProcess        = 0x10000000;

    private static readonly Guid ProfilesClassId = new("33C53A50-F456-4884-B049-85FD643ECFED");

    public void Refresh(
        ObservableCollection<SoftKeyboardInputMethod> destination,
        out SoftKeyboardInputMethod? active)
    {
        active = null;

        var profiles = TryEnumerateProfiles();
        if (profiles.Count == 0)
        {
            profiles = EnumerateWpfLanguages();
        }

        destination.Clear();

        var sorted = profiles.OrderBy(
            static profile => profile.DisplayName,
            StringComparer.CurrentCulture
        );

        foreach (var profile in sorted)
        {
            destination.Add(profile);
            if (profile.IsActive)
            {
                active = profile;
            }
        }
    }

    public bool TryActivate(SoftKeyboardInputMethod profile)
    {
        if (profile.ClassId == Guid.Empty)
        {
            try
            {
                InputLanguageManager.Current.CurrentInputLanguage = new CultureInfo(profile.LanguageId);
                return true;
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }

        ITfInputProcessorProfiles? profiles = null;

        try
        {
            profiles = CreateProfiles();

            if (profiles is null)
            {
                return false;
            }

            var classId = profile.ClassId;
            var profileId = profile.ProfileId;

            if (profiles.ChangeCurrentLanguage(profile.LanguageId) != 0)
            {
                return false;
            }

            // ITfInputProcessorProfiles predates per-process profile activation. On current Windows,
            // ITfInputProcessorProfileMgr is the authoritative API used by input switchers. Activating
            // for this process is important here: merely opening InputMethod.ImeState while an English
            // keyboard layout is active does not select Microsoft Pinyin (or any other TIP).
            if (profiles is ITfInputProcessorProfileMgr profileManager)
            {
                var result = profileManager.ActivateProfile(
                    ProfileTypeInputProcessor,
                    profile.LanguageId,
                    in classId,
                    in profileId,
                    IntPtr.Zero,
                    ActivateForProcess
                );

                if (result == 0)
                {
                    return true;
                }
            }

            // The legacy call remains a deliberate fallback for Windows versions and third-party
            // profile managers that do not expose the Vista-era interface. S_FALSE means the profile
            // is disabled and must not be reported as success even though its HRESULT is non-negative.
            return profiles.ActivateLanguageProfile(in classId, profile.LanguageId, in profileId) == 0;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return false;
        }
        finally
        {
            ReleaseComObject(profiles);
        }
    }

    public InputMethodState GetImeState()
    {
        try
        {
            // todo: InputMethod.Current 可能为 null
            var openState = InputMethod.Current.ImeState;
            if (openState != InputMethodState.On)
            {
                return openState;
            }

            // Microsoft Pinyin can keep the TSF keyboard-open compartment set while its own
            // Chinese/English mode is alphanumeric. For this control, "IME" means native
            // composition is active, so expose the combined state that matches what users see.
            var conversionMode = InputMethod.Current.ImeConversionMode;
            return (conversionMode & ImeConversionModeValues.Native) != 0
                ? InputMethodState.On
                : InputMethodState.Off;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return InputMethodState.DoNotCare;
        }
    }

    public bool TrySetImeState(InputMethodState state)
    {
        if (state == InputMethodState.DoNotCare)
        {
            return false;
        }

        try
        {
            // WPF maps this public property to the TSF keyboard-open compartment and mirrors it to
            // IMM32 when required. Microsoft Pinyin has a separate conversion compartment for its
            // Chinese/English mode, so the open state alone can report On while letters still pass
            // through directly. Keep both compartments aligned without synthesizing a locale-
            // specific hotkey such as Shift or Ctrl+Space. Read the conversion mode before changing
            // the open compartment: activating a TIP establishes that profile's current preferences,
            // including full/half shape and Roman input, and the keyboard must not replace them with
            // its own policy.
            var currentMode = InputMethod.Current.ImeConversionMode; // todo: InputMethod.Current 可能为 null
            var targetMode = GetConversionModeForImeState(currentMode, state);

            InputMethod.Current.ImeState = state;

            SetConversionMode(targetMode, state);
            return true;
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            return false;
        }
    }

    /// <summary>
    /// Produces the conversion mode for an IME open-state transition without inventing unrelated
    /// input preferences such as full shape, Roman input, symbol input, or fixed conversion.
    /// </summary>
    /// <param name="currentMode">The active TIP's conversion flags before the transition.</param>
    /// <param name="state">The requested logical Chinese/native or direct-letter state.</param>
    /// <returns>The same preferences with only the native-language dimension changed.</returns>
    /// <remarks>
    /// <see cref="ImeConversionModeValues.Alphanumeric"/> is WPF's managed sentinel for the native
    /// value zero. It is therefore removed when native conversion is enabled and restored when the
    /// language conversion bits are disabled. <see cref="ImeConversionModeValues.DoNotCare"/> is a
    /// dependency-property policy value rather than a native conversion flag and must never be sent
    /// to <see cref="InputMethod.ImeConversionMode"/>.
    /// </remarks>
    internal static ImeConversionModeValues GetConversionModeForImeState(
        ImeConversionModeValues currentMode,
        InputMethodState state)
    {
        var result = currentMode & ~ImeConversionModeValues.DoNotCare;
        if (state == InputMethodState.On)
        {
            return (result & ~ImeConversionModeValues.Alphanumeric) | ImeConversionModeValues.Native;
        }

        return (result & ~(ImeConversionModeValues.Native | ImeConversionModeValues.Katakana))
            | ImeConversionModeValues.Alphanumeric;
    }

    /// <summary>
    /// Applies a conversion transition through WPF while preserving flags orthogonal to the
    /// native/alphanumeric and full/half-shape dimensions.
    /// </summary>
    private static void SetConversionMode(ImeConversionModeValues targetMode, InputMethodState state)
    {
        var useFullShape = (targetMode & ImeConversionModeValues.FullShape) != 0;

        var primaryMode = state == InputMethodState.On
            ? ImeConversionModeValues.Native | (useFullShape ? ImeConversionModeValues.FullShape : 0)
            : useFullShape
                ? ImeConversionModeValues.FullShape
                : ImeConversionModeValues.Alphanumeric;

        // WPF's ImeConversionMode setter intentionally merges most flags into the existing TSF
        // compartment. It clears conflicting language/shape bits only for one of its canonical
        // primary modes (Native, Native|FullShape, FullShape, or Alphanumeric). Apply that canonical
        // mode first, then restore auxiliary preferences. A single combined write such as
        // Native|Roman can otherwise leave a stale FullShape bit behind in the native compartment.
        InputMethod.Current.ImeConversionMode = primaryMode; // todo: InputMethod.Current 可能为 null
        if (targetMode != primaryMode)
        {
            InputMethod.Current.ImeConversionMode = targetMode;
        }
    }

    private static List<SoftKeyboardInputMethod> TryEnumerateProfiles()
    {
        var result = new List<SoftKeyboardInputMethod>();

        ITfInputProcessorProfiles? profiles = null;

        var languagesPointer = IntPtr.Zero;

        try
        {
            profiles = CreateProfiles();
            if (profiles is null
                || profiles.GetLanguageList(out languagesPointer, out var languageCount) < 0)
            {
                return result;
            }

            for (var languageIndex = 0; languageIndex < languageCount; languageIndex++)
            {
                var languageId = unchecked(
                    (ushort)Marshal.ReadInt16(
                        languagesPointer,
                        languageIndex * sizeof(ushort)
                    )
                );

                if (profiles.EnumLanguageProfiles(languageId, out var enumerator) < 0)
                {
                    continue;
                }

                try
                {
                    while (enumerator.Next(1, out var nativeProfile, out var fetched) >= 0
                        && fetched == 1)
                    {
                        var r = profiles.IsEnabledLanguageProfile(
                            in nativeProfile.ClassId,
                            nativeProfile.LanguageId,
                            in nativeProfile.ProfileId,
                            out var enabled
                        );

                        if (r < 0 || enabled == 0)
                        {
                            continue;
                        }

                        var description = GetDescription(profiles, in nativeProfile);

                        result.Add(
                            new SoftKeyboardInputMethod(
                                nativeProfile.ClassId,
                                nativeProfile.ProfileId,
                                nativeProfile.LanguageId,
                                description,
                                nativeProfile.IsActive != 0
                            )
                        );
                    }
                }
                finally
                {
                    ReleaseComObject(enumerator);
                }
            }
        }
        catch (Exception exception) when (IsUnavailable(exception))
        {
            result.Clear();
        }
        finally
        {
            if (languagesPointer != IntPtr.Zero)
            {
                Marshal.FreeCoTaskMem(languagesPointer);
            }

            ReleaseComObject(profiles);
        }

        return result;
    }

    private static List<SoftKeyboardInputMethod> EnumerateWpfLanguages()
    {
        var result = new List<SoftKeyboardInputMethod>();
        var current = InputLanguageManager.Current.CurrentInputLanguage;

        if (InputLanguageManager.Current.AvailableInputLanguages is not { } languages)
        {
            return result;
        }

        foreach (var value in languages)
        {
            if (value is not CultureInfo culture)
            {
                continue;
            }

            result.Add(
                new SoftKeyboardInputMethod(
                    Guid.Empty,
                    Guid.Empty,
                    unchecked((ushort)culture.LCID),
                    culture.NativeName,
                    culture.Equals(current)
                )
            );
        }

        return result;
    }

    private static string GetDescription(ITfInputProcessorProfiles profiles, in TfLanguageProfile profile)
    {
        var r = profiles.GetLanguageProfileDescription(
            in profile.ClassId,
            profile.LanguageId,
            in profile.ProfileId,
            out var description
        );

        if (r >= 0 && !string.IsNullOrWhiteSpace(description))
        {
            return description!;
        }

        try
        {
            return new CultureInfo(profile.LanguageId).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return $"0x{profile.LanguageId:X4}";
        }
    }

    private static ITfInputProcessorProfiles? CreateProfiles()
    {
        var profilesType = Type.GetTypeFromCLSID(ProfilesClassId, throwOnError: false);
        return profilesType is null
            ? null
            : Activator.CreateInstance(profilesType) as ITfInputProcessorProfiles;
    }

    private static bool IsUnavailable(Exception exception)
    {
        return exception is COMException
            or InvalidCastException
            or InvalidOperationException
            or PlatformNotSupportedException
            or TypeLoadException;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is null || !Marshal.IsComObject(value))
        {
            return;
        }

        try
        {
            // Each activation/enumeration call owns one projected COM reference. Release exactly
            // that reference; FinalReleaseComObject could detach another keyboard's RCW when the
            // TSF profile manager exposes the same COM identity on multiple Dispatchers.
            _ = Marshal.ReleaseComObject(value);
        }
        catch (InvalidComObjectException)
        {
            // Enumeration owns no callback or persistent native state, so an RCW already released by
            // another projection path needs no additional cleanup.
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TfLanguageProfile
    {
        internal Guid   ClassId;
        internal ushort LanguageId;
        internal Guid   CategoryId;
        internal int    IsActive;
        internal Guid   ProfileId;
    }

    [Guid("3D61BF11-AC5F-42C8-A4CB-931BCC28C744")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    private interface IEnumTfLanguageProfiles
    {
        [PreserveSig]
        int Clone(out IEnumTfLanguageProfiles enumerator);

        [PreserveSig]
        int Next(uint count, out TfLanguageProfile profile, out uint fetched);

        [PreserveSig]
        int Reset();

        [PreserveSig]
        int Skip(uint count);
    }

    // Every member is declared in native vtable order even though only enumeration, description,
    // activation, and language switching are used. Removing an unused slot would redirect later calls.
    [Guid("1F02B6C5-7842-4EE6-8A0B-9A24183A95CA")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    private interface ITfInputProcessorProfiles
    {
        [PreserveSig]
        int Register(in Guid classId);

        [PreserveSig]
        int Unregister(in Guid classId);

        [PreserveSig]
        int AddLanguageProfile(
            in Guid classId,
            ushort languageId,
            in Guid profileId,
            IntPtr description,
            uint descriptionLength,
            IntPtr iconFile,
            uint iconFileLength,
            uint iconIndex
        );

        [PreserveSig]
        int RemoveLanguageProfile(in Guid classId, ushort languageId, in Guid profileId);

        [PreserveSig]
        int EnumInputProcessorInfo(out IntPtr enumerator);

        [PreserveSig]
        int GetDefaultLanguageProfile(ushort languageId, in Guid categoryId, out Guid classId, out Guid profileId);

        [PreserveSig]
        int SetDefaultLanguageProfile(ushort languageId, in Guid classId, in Guid profileId);

        [PreserveSig]
        int ActivateLanguageProfile(in Guid classId, ushort languageId, in Guid profileId);

        [PreserveSig]
        int GetActiveLanguageProfile(in Guid classId, out ushort languageId, out Guid profileId);

        [PreserveSig]
        int GetLanguageProfileDescription(
            in Guid classId,
            ushort languageId,
            in Guid profileId,
            [MarshalAs(UnmanagedType.BStr)] out string? description
        );

        [PreserveSig]
        int GetCurrentLanguage(out ushort languageId);

        [PreserveSig]
        int ChangeCurrentLanguage(ushort languageId);

        [PreserveSig]
        int GetLanguageList(out IntPtr languageIds, out uint count);

        [PreserveSig]
        int EnumLanguageProfiles(ushort languageId, out IEnumTfLanguageProfiles enumerator);

        [PreserveSig]
        int EnableLanguageProfile(in Guid classId, ushort languageId, in Guid profileId, int enable);

        [PreserveSig]
        int IsEnabledLanguageProfile(in Guid classId, ushort languageId, in Guid profileId, out int enabled);

        [PreserveSig]
        int EnableLanguageProfileByDefault(in Guid classId, ushort languageId, in Guid profileId, int enable);

        [PreserveSig]
        int SubstituteKeyboardLayout(in Guid classId, ushort languageId, in Guid profileId, IntPtr keyboardLayout);
    }

    // Every member is declared in SDK vtable order. Only ActivateProfile is used, but omitting later
    // declarations would make a future extension deceptively unsafe and harder to compare with msctf.idl.
    [Guid("71C6E74C-0F28-11D8-A82A-00065B84435C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    private interface ITfInputProcessorProfileMgr
    {
        [PreserveSig]
        int ActivateProfile(
            uint profileType,
            ushort languageId,
            in Guid classId,
            in Guid profileId,
            IntPtr keyboardLayout,
            uint flags
        );

        [PreserveSig]
        int DeactivateProfile(
            uint profileType,
            ushort languageId,
            in Guid classId,
            in Guid profileId,
            IntPtr keyboardLayout,
            uint flags
        );

        [PreserveSig]
        int GetProfile(
            uint profileType,
            ushort languageId,
            in Guid classId,
            in Guid profileId,
            IntPtr keyboardLayout,
            IntPtr profile
        );

        [PreserveSig]
        int EnumProfiles(ushort languageId, out IntPtr enumerator);

        [PreserveSig]
        int ReleaseInputProcessor(in Guid classId, uint flags);

        [PreserveSig]
        int RegisterProfile(
            in Guid classId,
            ushort languageId,
            in Guid profileId,
            IntPtr description,
            uint descriptionLength,
            IntPtr iconFile,
            uint iconFileLength,
            uint iconIndex,
            IntPtr substituteKeyboardLayout,
            uint preferredLayout,
            int enabledByDefault,
            uint flags
        );

        [PreserveSig]
        int UnregisterProfile(in Guid classId, ushort languageId, in Guid profileId, uint flags);

        [PreserveSig]
        int GetActiveProfile(in Guid categoryId, IntPtr profile);
    }
}