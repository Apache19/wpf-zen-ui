// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.InputMethods;
using static Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods.KnownTextServices;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;

/// <summary>
/// Interprets known Chinese service observations and supplies their exact fallback writes.
/// Microsoft Pinyin uses the native conversion flag; other services use captured raw contracts.
/// Missing required observations contribute no button, layout, or transition.
/// </summary>
internal sealed class ChineseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Identifies TSF's TF_CONVERSIONMODE_NATIVE bit in the raw conversion compartment.
    /// Other bits describe independent settings such as full shape and symbol conversion.
    /// </summary>
    private const int NativeConversion = 0x0001;

    /// <summary>
    /// Gets the shared stateless Chinese adapter.
    /// </summary>
    internal static ChineseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod, SoftKeyboardInputObservation observation)
    {
        if (Decode(inputMethod.ClassId, observation) is not { } enabled)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var mode = enabled ? "chinese" : "alphanumeric";

        var button = new SoftKeyboardInputModeButton(
            id             : $"known-tip:{inputMethod.ClassId:D}:{mode}",
            content        : enabled ? "中" : "A",
            automationName : enabled ? "中文输入" : "字母数字输入"
        );

        return new SoftKeyboardInputModeContribution(
            button         : button, 
            transition     : new SoftKeyboardInputModeTransition(enabled ? "disable" : "enable"),
            layoutProvider : enabled ? ChineseCompactLayoutProvider.Default : null
        );
    }

    /// <summary>
    /// Interprets the mode-bearing observation required by each supported Chinese service.
    /// Microsoft Pinyin uses the native bit in TSF or IMM conversion flags, including when the
    /// accompanying open read is unavailable. All services consume the
    /// shared observations and interpret their own raw contracts, including non-Boolean open.
    /// </summary>
    private static bool? Decode(Guid classId, SoftKeyboardInputObservation state)
    {
        // 微软拼音
        if (classId == MicrosoftPinyin)
        {
            // Reject non-Boolean open integers and negative flags while preserving ancillary
            // conversion settings and independently unavailable open state.
            return state.OpenValue is null or 0 or 1
                && state.ConversionValue is { } value && value >= 0
                    ? (value & NativeConversion) != 0 : null;
        }

        var open = state.OpenValue;
        var conversion = state.ConversionValue;

        // 微软注音
        if (classId == MicrosoftBopomofo && open == 1)
        {
            return conversion == 1 ? true : conversion == 0 ? false : null;
        }

        // 讯飞输入法和微信输入法
        if (conversion == 0 && (classId == IFlytek || classId == WeChat))
        {
            var closed = classId == IFlytek ? 2 : 0;
            return open == 1 ? true : open == closed ? false : null;
        }

        // 其它输入法不解释
        return null;
    }

    /// <inheritdoc />
    public bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod, SoftKeyboardInputObservation observation,
        SoftKeyboardInputModeTransition transition)
    {
        var classId = inputMethod.ClassId;
        if (transition.Id is not ("enable" or "disable"))
        {
            return false;
        }

        // The keyboard has already attempted platform discovery. The fallback uses only a freshly
        // resolved observed contract; accepted writes never become synthetic observations.
        if (Decode(classId, observation) is null)
        {
            return false;
        }

        var enable = transition.Id == "enable";
        if (classId == MicrosoftPinyin || classId == MicrosoftBopomofo)
        {
            return SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                inputMethod, enable ? (classId == MicrosoftPinyin ? 1025 : 1) : 0);
        }

        // Third-party services use their exact observed open integers, including IFlytek's
        // non-Boolean closed value of two.
        return SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(
            inputMethod, enable ? 1 : (classId == IFlytek ? 2 : 0));
    }
}
