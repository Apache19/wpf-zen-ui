// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Applies observed contracts only to explicitly identified Chinese text services.
/// </summary>
internal sealed class ChineseInputModeAdapter : KnownInputModeAdapter
{
    internal static ChineseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    protected override bool Supports(Guid classId)
    {
        return classId == Pinyin || classId == Bopomofo || classId == IFlytek || classId == WeChat;
    }
}

/// <summary>
/// Applies Microsoft's Japanese IME open contract while retaining its selected conversion mode.
/// </summary>
internal sealed class JapaneseInputModeAdapter : KnownInputModeAdapter
{
    internal static JapaneseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    protected override bool Supports(Guid classId)
    {
        return classId == Japanese;
    }
}

/// <summary>
/// Supplies service-specific state and fallback commands for verified ClassId values.
/// </summary>
/// <remarks>
/// Language-bar presentation and commands retain priority independently. Microsoft Japanese IME
/// uses a decoded textual cap because one native button icon cannot describe all conversion modes.
/// Unrecognized states contribute no fallback. Raw writes express observed targets; acceptance
/// does not confirm a mode change.
/// </remarks>
internal abstract class KnownInputModeAdapter : ISoftKeyboardObservedInputModeAdapter
{
    /// <summary>
    /// Identifies the verified Pinyin text service.
    /// </summary>
    protected static readonly Guid Pinyin = new("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E");

    /// <summary>
    /// Identifies the verified Bopomofo text service.
    /// </summary>
    protected static readonly Guid Bopomofo = new("B115690A-EA02-48D5-A231-E3578D2FDF80");

    /// <summary>
    /// Identifies the verified IFlytek text service.
    /// </summary>
    protected static readonly Guid IFlytek = new("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643");

    /// <summary>
    /// Identifies the verified WeChat text service.
    /// </summary>
    protected static readonly Guid WeChat = new("86598FB9-66A2-463E-B9C2-AEB906D477AD");

    /// <summary>
    /// Identifies the verified Japanese text service.
    /// </summary>
    protected static readonly Guid Japanese = new("03B5835F-F03C-411B-9CE2-AA23E1171E36");

    /// <summary>
    /// Restricts the adapter to the service identities owned by its extension provider.
    /// </summary>
    protected abstract bool Supports(Guid classId);

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(SoftKeyboardInputMethod inputMethod)
    {
        if (!Supports(inputMethod.ClassId))
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        return state is null
            ? SoftKeyboardInputModeContribution.Empty
            : GetContribution(inputMethod, state);
    }

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod, SoftKeyboardTsfInputModeState state)
    {
        if (!Supports(inputMethod.ClassId)
            || Decode(inputMethod.ClassId, state) is not { } enabled)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var japanese = inputMethod.ClassId == Japanese;
        var mode = japanese
            ? DecodeJapaneseMode(enabled, state.ConversionValue)
            : new SoftKeyboardInputMode(enabled ? "chinese" : "alphanumeric", enabled);
        // The Japanese language-bar icon identifies the command rather than the active conversion
        // mode. Use the decoded cap even when that icon is available, while CanSwitch continues to
        // select the native command as the transition route.
        var presentation = japanese
            ? CreateJapanesePresentation(inputMethod.ClassId, mode)
            : SoftKeyboardTsfInputMode.GetInputModeButton(inputMethod.ClassId)
              ?? new SoftKeyboardInputModeButton(
                  $"known-tip:{inputMethod.ClassId:D}:{mode.Id}",
                  enabled ? "中" : "A",
                  enabled ? "中文输入" : "字母数字输入");

        return new SoftKeyboardInputModeContribution(
            SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode),
            SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.FromValue(presentation),
            SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition>.FromValue(
                new SoftKeyboardInputModeTransition(enabled ? "disable" : "enable")));
    }

    /// <summary>
    /// Decodes Microsoft Japanese conversion flags only while its open compartment is one.
    /// The binary transition uses open rather than IsNativeInput, so full-width Latin input
    /// can still be switched off and the selected conversion remains intact when reopening.
    /// </summary>
    private static SoftKeyboardInputMode DecodeJapaneseMode(bool enabled, int? conversion)
    {
        if (!enabled)
        {
            return new SoftKeyboardInputMode("direct", false);
        }

        // Only name conversion values observed from Microsoft Japanese IME. Other nonzero values
        // still indicate Japanese input, but must not be mistaken for one of these specific modes.
        return conversion switch
        {
            0x09 => new SoftKeyboardInputMode("hiragana", true),
            0x0B => new SoftKeyboardInputMode("full-width-katakana", true),
            0x08 => new SoftKeyboardInputMode("full-width-alphanumeric", false),
            0x03 => new SoftKeyboardInputMode("half-width-katakana", true),
            0x00 => new SoftKeyboardInputMode("half-width-alphanumeric", false),
            _    => new SoftKeyboardInputMode("japanese", true),
        };
    }

    /// <summary>
    /// Creates the textual cap that identifies Microsoft Japanese IME's effective input mode.
    /// </summary>
    private static SoftKeyboardInputModeButton CreateJapanesePresentation(
        Guid classId, SoftKeyboardInputMode mode)
    {
        var (content, automationName) = mode.Id switch
        {
            "hiragana"                => ("あ", "平假名输入"),
            "full-width-katakana"     => ("カ", "全角片假名输入"),
            "half-width-katakana"     => ("ｶ", "半角片假名输入"),
            "full-width-alphanumeric" => ("Ａ", "全角字母数字输入"),
            "japanese"                => ("日", "日语输入"),
            _                         => ("A", "字母数字输入"),
        };

        return new SoftKeyboardInputModeButton(
            $"known-tip:{classId:D}:{mode.Id}", content, automationName);
    }

    /// <summary>
    /// Recognizes the captured service states without generalizing to other values or services.
    /// Japanese open state is independent of conversion, including retained values while closed.
    /// </summary>
    private static bool? Decode(Guid classId, SoftKeyboardTsfInputModeState state)
    {
        var open       = state.OpenValue;
        var conversion = state.ConversionValue;
        if (classId == Japanese)
        {
            return open == 1 ? true : open == 0 ? false : null;
        }

        if (classId == Pinyin && open == 1)
        {
            return conversion == 1025 ? true : conversion == 0 ? false : null;
        }

        if (classId == Bopomofo && open == 1)
        {
            return conversion == 1 ? true : conversion == 0 ? false : null;
        }

        if (conversion == 0 && (classId == IFlytek || classId == WeChat))
        {
            var closed = classId == IFlytek ? 2 : 0;
            return open == 1 ? true : open == closed ? false : null;
        }

        return null;
    }

    /// <inheritdoc />
    public bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod, SoftKeyboardInputModeTransition transition)
    {
        var classId = inputMethod.ClassId;
        if (!Supports(classId) || transition.Id is not ("enable" or "disable"))
        {
            return false;
        }

        // Recheck after editor focus restoration; a native command failure is not a request
        // to execute a second transition through compartments.
        var state = SoftKeyboardTsfInputMode.GetState(classId);
        if (state?.CanSwitch == true)
        {
            return SoftKeyboardTsfInputMode.TrySwitchInputMode(classId);
        }

        if (state is null || Decode(classId, state) is null)
        {
            return false;
        }

        var enable = transition.Id == "enable";
        if (classId == Pinyin || classId == Bopomofo)
        {
            return SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                inputMethod, enable ? (classId == Pinyin ? 1025 : 1) : 0);
        }

        // Japanese preserves its selected conversion. Third-party services use their exact
        // observed open integers, including IFlytek's non-Boolean closed value of two.
        return SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(
            inputMethod, enable ? 1 : (classId == IFlytek ? 2 : 0));
    }
}
