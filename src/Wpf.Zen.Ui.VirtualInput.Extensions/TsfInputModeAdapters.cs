// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Interprets the standard TSF conversion flags used by Chinese input methods.
/// </summary>
internal sealed class ChineseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    internal static ChineseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputMode? GetMode(SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return null;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        return state is null ? null : Decode(state.OpenValue, state.ConversionValue, state.CanSwitch);
    }

    /// <summary>
    /// Converts the standard Chinese TSF values into a keyboard mode.
    /// </summary>
    internal static SoftKeyboardInputMode? Decode(int? open, int? conversion, bool canSwitch)
    {
        if (open is not (0 or 1) || conversion is not int conversionValue)
        {
            return null;
        }

        var native = open == 1 && (conversionValue & TsfConversionMode.Native) != 0;
        return new SoftKeyboardInputMode(
            native ? "chinese" : "alphanumeric",
            native ? "中" : "英",
            native ? "中文输入" : "英文输入",
            native,
            canSwitch
        );
    }

    /// <inheritdoc />
    public bool TrySwitchMode(SoftKeyboardInputMethod inputMethod, SoftKeyboardInputMode currentMode)
    {
        return currentMode.CanSwitch
            && SoftKeyboardTsfInputMode.TrySwitchInputMode(inputMethod.ClassId);
    }
}

/// <summary>
/// Interprets the standard TSF open and conversion values used by Japanese input methods.
/// </summary>
internal sealed class JapaneseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    internal static JapaneseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputMode? GetMode(SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return null;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        return state is null ? null : Decode(state.OpenValue, state.ConversionValue, state.CanSwitch);
    }

    /// <summary>
    /// Converts the standard Japanese TSF values into a precise keyboard mode.
    /// </summary>
    internal static SoftKeyboardInputMode? Decode(int? open, int? conversion, bool canSwitch)
    {
        if (open is not (0 or 1) || conversion is not int conversionValue)
        {
            return null;
        }

        if (open == 0)
        {
            return Mode("direct", "A", "直接输入", false, canSwitch);
        }

        var native    = (conversionValue & TsfConversionMode.Native) != 0;
        var katakana  = (conversionValue & TsfConversionMode.Katakana) != 0;
        var fullShape = (conversionValue & TsfConversionMode.FullShape) != 0;

        if (!native)
        {
            return fullShape
                ? Mode("full-width-alphanumeric", "Ａ", "全角字母数字", false, canSwitch)
                : Mode("half-width-alphanumeric", "A", "半角字母数字", false, canSwitch);
        }

        if (!katakana)
        {
            return Mode("hiragana", "あ", "平假名", true, canSwitch);
        }

        return fullShape
            ? Mode("full-width-katakana", "カ", "全角片假名", true, canSwitch)
            : Mode("half-width-katakana", "ｶ", "半角片假名", true, canSwitch);
    }

    /// <inheritdoc />
    public bool TrySwitchMode(SoftKeyboardInputMethod inputMethod, SoftKeyboardInputMode currentMode)
    {
        return currentMode.CanSwitch
            && SoftKeyboardTsfInputMode.TrySwitchInputMode(inputMethod.ClassId);
    }

    private static SoftKeyboardInputMode Mode(
        string id,
        string keyLabel,
        string displayName,
        bool native,
        bool canSwitch)
    {
        return new SoftKeyboardInputMode(id, keyLabel, displayName, native, canSwitch);
    }
}

/// <summary>
/// Defines the TSF conversion bits used by the language adapters.
/// </summary>
internal static class TsfConversionMode
{
    internal const int Native    = 0x0001;
    internal const int Katakana  = 0x0002;
    internal const int FullShape = 0x0008;
}
