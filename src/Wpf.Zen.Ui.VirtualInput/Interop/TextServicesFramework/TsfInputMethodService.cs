// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows.Input;
using Windows.Win32;
using Windows.Win32.UI.TextServices;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Enumerates and activates user-enabled Text Services Framework language profiles.
/// </summary>
/// <remarks>
/// Modern enumeration includes enabled TIPs and loaded keyboard layouts. Language settings alone
/// do not constitute an input method; unavailable discovery leaves the catalog empty.
/// </remarks>
internal sealed class TsfInputMethodService : ISoftKeyboardInputMethodBackend
{
    private const uint ProfileTypeInputProcessor = 0x0001;
    private const uint ProfileTypeKeyboardLayout = 0x0002;
    private const uint ActivateForProcess        = 0x10000000;

    private static readonly Guid ProfilesClassId    = new("33C53A50-F456-4884-B049-85FD643ECFED");
    private static readonly Guid KeyboardCategoryId = new("34745C63-B2F0-4784-8B67-5E12C8701A31");

    /// <inheritdoc />
    public void Refresh(
        ObservableCollection<SoftKeyboardInputMethod> destination,
        out SoftKeyboardInputMethod? active)
    {
        active = null;

        var profiles = TryEnumerateProfiles();
        destination.Clear();

        var sorted = profiles.OrderBy(
            static profile => profile.LanguageDisplayName,
            StringComparer.CurrentCulture
        ).ThenBy(static profile => profile.DisplayName, StringComparer.CurrentCulture);

        foreach (var profile in sorted)
        {
            destination.Add(profile);
            if (profile.IsActive)
            {
                active = profile;
            }
        }
    }

    /// <inheritdoc />
    public bool TryActivate(SoftKeyboardInputMethod profile)
    {
        if (profile.ClassId == Guid.Empty && profile.KeyboardLayout == IntPtr.Zero)
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

            var classId   = profile.ClassId;
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
                    profile.KeyboardLayout == IntPtr.Zero
                        ? ProfileTypeInputProcessor
                        : ProfileTypeKeyboardLayout,
                    profile.LanguageId,
                    in classId,
                    in profileId,
                    profile.KeyboardLayout,
                    ActivateForProcess
                );

                if (result == 0)
                {
                    return true;
                }
            }

            // A language-level fallback cannot select a particular layout within that language.
            if (profile.KeyboardLayout != IntPtr.Zero)
            {
                return false;
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

    /// <inheritdoc />
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

            // A TIP can keep the TSF keyboard-open compartment set while its conversion mode is
            // alphanumeric. For this control, "IME" means native composition is active, so expose
            // the combined state that matches what users see.
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

    /// <inheritdoc />
    public bool TrySetImeState(InputMethodState state)
    {
        if (state == InputMethodState.DoNotCare)
        {
            return false;
        }

        try
        {
            // WPF maps this public property to the TSF keyboard-open compartment and mirrors it to
            // IMM32 when required. A TIP may use a separate conversion compartment, so the open
            // state alone can report On while letters still pass through directly. Keep both
            // compartments aligned without synthesizing a locale-specific hotkey. Read the mode before changing
            // the open compartment: activating a TIP establishes that profile's current preferences,
            // including full/half shape and Roman input, and the keyboard must not replace them with
            // its own policy.
            var currentMode = InputMethod.Current.ImeConversionMode; // todo: InputMethod.Current 可能为 null
            var targetMode  = GetConversionModeForImeState(currentMode, state);

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
    /// <param name="state">The requested logical native-composition or direct-letter state.</param>
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

            var activeProfile = ReadActiveKeyboardProfile(profiles);
            var loadedLayouts = WindowsKeyboardLayouts.GetLoaded();

            for (var languageIndex = 0; languageIndex < languageCount; languageIndex++)
            {
                var languageId = unchecked(
                    (ushort)Marshal.ReadInt16(
                        languagesPointer,
                        languageIndex * sizeof(ushort)
                    )
                );

                if (profiles is ITfInputProcessorProfileMgr manager)
                {
                    AppendModernProfiles(profiles, manager, languageId, activeProfile, loadedLayouts, result);
                    continue;
                }

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

                        if (r < 0 || enabled == 0 || nativeProfile.CategoryId != KeyboardCategoryId)
                        {
                            continue;
                        }

                        var description = GetDescription(profiles, in nativeProfile);
                        // IsActive on a legacy language profile is not sufficient to distinguish
                        // the current keyboard category from a remembered per-language selection.
                        var isActive = activeProfile is { } current
                            && current.dwProfileType == ProfileTypeInputProcessor
                            && current.clsid == nativeProfile.ClassId
                            && current.guidProfile == nativeProfile.ProfileId
                            && current.langid == nativeProfile.LanguageId;

                        result.Add(
                            new SoftKeyboardInputMethod(
                                nativeProfile.ClassId,
                                nativeProfile.ProfileId,
                                nativeProfile.LanguageId,
                                description,
                                isActive,
                                isActive ? TsfTouchLayoutQuery.Query(nativeProfile.ClassId) : null
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

    /// <summary>
    /// Enumerates both profile kinds for one installed language. Substituted layouts are omitted
    /// because selecting them activates their replacement TIP rather than a separate keyboard.
    /// </summary>
    private static unsafe void AppendModernProfiles(
        ITfInputProcessorProfiles profiles,
        ITfInputProcessorProfileMgr manager,
        ushort languageId,
        TF_INPUTPROCESSORPROFILE? active,
        HashSet<IntPtr> loadedLayouts,
        List<SoftKeyboardInputMethod> destination)
    {
        if (manager.EnumProfiles(languageId, out var enumerator) < 0)
        {
            return;
        }

        try
        {
            while (true)
            {
                TF_INPUTPROCESSORPROFILE native = default;
                enumerator.Next(1, &native, out var fetched);

                if (fetched != 1)
                {
                    break;
                }

                if (!IsSelectableProfile(native, loadedLayouts))
                {
                    continue;
                }

                var isActive = active is { } current && IsSameProfile(native, current);
                var isLayout = native.dwProfileType == ProfileTypeKeyboardLayout;

                var legacy = new TfLanguageProfile
                {
                    ClassId    = native.clsid,
                    ProfileId  = native.guidProfile,
                    LanguageId = native.langid,
                };

                destination.Add(
                    new SoftKeyboardInputMethod(
                        isLayout ? Guid.Empty : native.clsid,
                        isLayout ? Guid.Empty : native.guidProfile,
                        native.langid,
                        isLayout
                            ? KeyboardLayoutNames.GetDisplayName((IntPtr)native.hkl, native.langid)
                            : GetDescription(profiles, in legacy),
                        isActive,
                        !isLayout && isActive ? TsfTouchLayoutQuery.Query(native.clsid) : null,
                        supportsImeToggle: !isLayout || PInvoke.ImmIsIME(native.hkl),
                        keyboardLayout: isLayout ? (IntPtr)native.hkl : IntPtr.Zero
                    )
                );
            }
        }
        finally
        {
            ReleaseComObject(enumerator);
        }
    }

    /// <summary>
    /// Filters disabled profiles, non-keyboard services, unloaded layouts, and TIP substitutes.
    /// Exact handles retain distinct layouts within one language; TIP identity uses its GUID pair.
    /// </summary>
    internal static bool IsSelectableProfile(TF_INPUTPROCESSORPROFILE profile, ISet<IntPtr> loadedLayouts)
    {
        if ((profile.dwFlags & PInvoke.TF_IPP_FLAG_ENABLED) == 0 || profile.langid == 0)
        {
            return false;
        }

        return profile.dwProfileType switch
        {
            ProfileTypeInputProcessor => profile.catid == KeyboardCategoryId
                && profile.clsid != Guid.Empty && profile.guidProfile != Guid.Empty,

            ProfileTypeKeyboardLayout => profile.hkl != default
                && loadedLayouts.Contains((IntPtr)profile.hkl)
                && (profile.dwFlags & PInvoke.TF_IPP_FLAG_SUBSTITUTEDBYINPUTPROCESSOR) == 0,

            _ => false,
        };
    }

    /// <summary>
    /// Matches the authoritative keyboard-category identity, including layouts in the same
    /// language.
    /// </summary>
    internal static bool IsSameProfile(TF_INPUTPROCESSORPROFILE left, TF_INPUTPROCESSORPROFILE right)
    {
        return left.dwProfileType == right.dwProfileType && left.langid == right.langid
            && (left.dwProfileType == ProfileTypeKeyboardLayout
                ? left.hkl == right.hkl
                : left.clsid == right.clsid && left.guidProfile == right.guidProfile);
    }

    /// <summary>
    /// Reads the current keyboard-category profile, including ordinary keyboard layouts.
    /// An unavailable query leaves the active TSF identity unknown instead of guessing a profile.
    /// </summary>
    private static TF_INPUTPROCESSORPROFILE? ReadActiveKeyboardProfile(ITfInputProcessorProfiles profiles)
    {
        if (profiles is not ITfInputProcessorProfileMgr manager)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(Marshal.SizeOf<TF_INPUTPROCESSORPROFILE>());
        try
        {
            return manager.GetActiveProfile(in KeyboardCategoryId, buffer) == 0
                ? Marshal.PtrToStructure<TF_INPUTPROCESSORPROFILE>(buffer)
                : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
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
}
