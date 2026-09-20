// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Windows.Win32.UI.Input.Ime;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using static Windows.Win32.UI.Input.Ime.IME_CONVERSION_MODE;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;

/// <summary>
/// Interprets Microsoft Japanese IME observations and overrides its icon with a textual key cap.
/// Fallback execution enters Hiragana from direct input and closes every enabled mode.
/// </summary>
internal sealed class MicrosoftJapaneseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Selects the language, script and width information used by the character-mode cap.
    /// </summary>
    private const IME_CONVERSION_MODE CharacterModeMask =
        IME_CMODE_NATIVE | IME_CMODE_KATAKANA | IME_CMODE_FULLSHAPE;

    /// <summary>
    /// Identifies input preferences retained when explicitly entering Hiragana.
    /// These flags do not change the character-mode cap.
    /// </summary>
    private const IME_CONVERSION_MODE InputStyleMask = IME_CMODE_ROMAN | IME_CMODE_SOFTKBD;

    /// <summary>
    /// Identifies conversion behavior described separately from the character-mode cap.
    /// These flags are cleared when explicitly selecting normal Hiragana conversion.
    /// </summary>
    private const IME_CONVERSION_MODE ConversionBehaviorMask =
        IME_CMODE_NOCONVERSION | IME_CMODE_FIXED;

    /// <summary>
    /// Identifies special input modes that do not establish an ordinary Japanese character cap.
    /// A known open state still permits closing them without interpreting their conversion.
    /// </summary>
    private const IME_CONVERSION_MODE SpecialModeMask =
        IME_CMODE_CHARCODE | IME_CMODE_HANJACONVERT | IME_CMODE_EUDC | IME_CMODE_SYMBOL;

    /// <summary>
    /// Writes open state independently of the retained character mode.
    /// </summary>
    private readonly Func<SoftKeyboardInputMethod, int, bool> _writeOpen;

    /// <summary>
    /// Selects the complete character-mode flags after the IME has opened.
    /// </summary>
    private readonly Func<SoftKeyboardInputMethod, int, bool> _writeConversion;

    /// <summary>
    /// Gets the shared stateless Microsoft Japanese IME adapter.
    /// </summary>
    internal static MicrosoftJapaneseInputModeAdapter Default { get; } = new(
        SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue,
        SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode
    );

    /// <summary>
    /// Binds the state interpreter to raw compartment writers for command execution.
    /// </summary>
    /// <param name="writeOpen">Writes the requested open integer and reports acceptance.</param>
    /// <param name="writeConversion">Writes the complete conversion integer.</param>
    internal MicrosoftJapaneseInputModeAdapter(
        Func<SoftKeyboardInputMethod, int, bool> writeOpen,
        Func<SoftKeyboardInputMethod, int, bool> writeConversion)
    {
        _writeOpen       = writeOpen;
        _writeConversion = writeConversion;
    }

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputObservation observation)
    {
        if (DecodeOpen(inputMethod.ClassId, observation) is not { } enabled)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        // Open alone determines the binary action, even if the character mode is unavailable.
        var transition = new SoftKeyboardInputModeTransition(enabled ? "disable" : "enable");
        if (DecodeJapaneseMode(enabled, observation.ConversionValue) is not { } mode)
        {
            return new SoftKeyboardInputModeContribution(transition: transition);
        }

        return new SoftKeyboardInputModeContribution(
            CreateJapanesePresentation(inputMethod.ClassId, mode.Id, observation.ConversionValue),
            transition,
            mode.Native ? JapaneseSoftKeyboardLayoutProvider.Default : null,
            overridePlatformButton: true
        );
    }

    /// <inheritdoc />
    public bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputObservation observation,
        SoftKeyboardInputModeTransition transition)
    {
        if (transition.Id is not ("enable" or "disable")
            || DecodeOpen(inputMethod.ClassId, observation) is null)
        {
            return false;
        }

        var enable        = transition.Id == "enable";
        var retainedStyle = observation.ConversionValue is >= 0
            ? (IME_CONVERSION_MODE)observation.ConversionValue.Value & InputStyleMask
            : 0;

        var conversion = IME_CMODE_NATIVE | IME_CMODE_FULLSHAPE | retainedStyle;

        Debug.WriteLine(
            "[SoftKeyboard.InputMode.Command] JAPANESE target resolved; "
            + $"profileId={inputMethod.ProfileId}, open={observation.OpenValue}, "
            + $"conversion={observation.ConversionValue?.ToString() ?? "<unavailable>"}, "
            + $"targetOpen={(enable ? 1 : 0)}, "
            + $"targetConversion={(enable ? $"0x{(uint)conversion:X8}" : "<preserved>")}."
        );

        if (!enable)
        {
            // Closing the IME preserves its retained conversion settings.
            return _writeOpen(inputMethod, 0);
        }

        // Opening may restore any retained mode. Select Hiragana explicitly afterwards,
        // preserving known input preferences and clearing special conversion behavior.
        if (!_writeOpen(inputMethod, 1))
        {
            return false;
        }

        return _writeConversion(inputMethod, unchecked((int)conversion));
    }

    /// <summary>
    /// Recognizes the Japanese open integer independently of retained conversion values.
    /// </summary>
    private static bool? DecodeOpen(Guid classId, SoftKeyboardInputObservation observation)
    {
        if (classId != KnownTextServices.MicrosoftJapanese)
        {
            return null;
        }

        return observation.OpenValue switch
        {
            1 => true,
            0 => false,
            _ => null
        };
    }

    /// <summary>
    /// Decodes Microsoft Japanese conversion flags only while its open compartment is one.
    /// Input style and conversion behavior are independent of the base character mode.
    /// Special input modes and undefined bits remain unknown; missing values never become zero.
    /// </summary>
    private static (string Id, bool Native)? DecodeJapaneseMode(bool enabled, int? conversion)
    {
        if (!enabled)
        {
            return ("direct", false);
        }

        if (conversion is not >= 0)
        {
            return null;
        }

        const IME_CONVERSION_MODE knownMask = CharacterModeMask | InputStyleMask | ConversionBehaviorMask | SpecialModeMask;

        var flags = (IME_CONVERSION_MODE)conversion.Value;
        if ((flags & ~knownMask) != 0 || (flags & SpecialModeMask) != 0)
        {
            return null;
        }

        var fullShape = (flags & IME_CMODE_FULLSHAPE) != 0;
        if ((flags & IME_CMODE_NATIVE) == 0)
        {
            // IMM defines Katakana as effective only under Native.
            return (fullShape ? "full-width-alphanumeric" : "half-width-alphanumeric", false);
        }

        if ((flags & IME_CMODE_KATAKANA) == 0)
        {
            // Hiragana has no separate half-width presentation.
            return ("hiragana", true);
        }

        return (fullShape ? "full-width-katakana" : "half-width-katakana", true);
    }

    /// <summary>
    /// Creates the textual cap that identifies Microsoft Japanese IME's effective input mode.
    /// </summary>
    private static SoftKeyboardInputModeButton CreateJapanesePresentation(
        Guid classId, string mode, int? conversion)
    {
        // todo: UI 自动化名称要本地化
        var (content, automationName) = mode switch
        {
            "hiragana"                => ("あ", "平假名输入"),
            "full-width-katakana"     => ("カ", "全角片假名输入"),
            "half-width-katakana"     => ("ｶ", "半角片假名输入"),
            "full-width-alphanumeric" => ("Ａ", "全角字母数字输入"),
            _                         => ("A", "字母数字输入"),
        };

        if (mode != "direct")
        {
            var behavior = (IME_CONVERSION_MODE)(conversion ?? 0) & ConversionBehaviorMask;
            automationName += behavior switch
            {
                IME_CMODE_NOCONVERSION => "（不转换）",
                IME_CMODE_FIXED        => "（固定转换）",
                ConversionBehaviorMask => "（不转换、固定转换）",
                _                      => string.Empty,
            };
        }

        return new SoftKeyboardInputModeButton(
            $"known-tip:{classId:D}:{mode}", content, automationName
        );
    }
}
