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
    private static readonly SoftKeyboardInputModeTransition ToggleNativeTransition =
        new("toggle-native");

    internal static ChineseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        return state is null ? SoftKeyboardInputModeContribution.Empty : Decode(inputMethod, state);
    }

    /// <summary>
    /// Converts Chinese TSF state and supplies a conversion-mode fallback only when the active
    /// service does not publish its standard input-mode command.
    /// </summary>
    /// <param name="inputMethod">The active Chinese input-method profile.</param>
    /// <param name="state">The raw TSF state observed for that profile.</param>
    /// <returns>The independently handled state, presentation, and transition facets.</returns>
    internal static SoftKeyboardInputModeContribution Decode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardTsfInputModeState state)
    {
        var fallback = !state.CanSwitch
            && state.OpenValue == 1
            && state.ConversionValue is not null
            && IsChineseTip(inputMethod);

        var mode = Decode(state.OpenValue, state.ConversionValue);

        if (mode is null)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var presentation = fallback
            ? SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.FromValue(
                new SoftKeyboardInputModeButton(
                    $"tsf-conversion-native:{inputMethod.LanguageId:X4}:{(mode.IsNativeInput ? 1 : 0)}",
                    mode.IsNativeInput ? "中" : "英",
                    mode.IsNativeInput ? "中文输入" : "英文输入"
                )
            )
            : SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.Unhandled;

        return new SoftKeyboardInputModeContribution(
            SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode),
            presentation,
            fallback
                ? SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition>.FromValue(
                    ToggleNativeTransition
                )
                : SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition>.Unhandled
        );
    }

    /// <summary>
    /// Converts the standard Chinese TSF values into a keyboard mode.
    /// </summary>
    internal static SoftKeyboardInputMode? Decode(int? open, int? conversion)
    {
        if (open is not (0 or 1) || conversion is not int conversionValue)
        {
            return null;
        }

        var native = open == 1 && (conversionValue & TsfConversionMode.Native) != 0;
        return new SoftKeyboardInputMode(
            native ? "chinese" : "alphanumeric",
            native
        );
    }

    /// <inheritdoc />
    public bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputModeTransition transition)
    {
        if (!Equals(transition, ToggleNativeTransition))
        {
            return false;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        if (state is null)
        {
            return false;
        }

        return !state.CanSwitch
            && state.OpenValue == 1
            && state.ConversionValue is int conversion
            && IsChineseTip(inputMethod)
            && SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(
                inputMethod,
                conversion ^ TsfConversionMode.Native
            );
    }

    /// <summary>
    /// Determines whether a profile is a Chinese text service rather than a keyboard layout.
    /// </summary>
    private static bool IsChineseTip(SoftKeyboardInputMethod inputMethod)
    {
        return inputMethod.ClassId != Guid.Empty && (inputMethod.LanguageId & 0x03FF) == 0x04;
    }
}

/// <summary>
/// Interprets the standard TSF open and conversion values used by Japanese input methods.
/// </summary>
internal sealed class JapaneseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    private static readonly SoftKeyboardInputModeTransition ToggleNativeTransition =
        new("toggle-native");

    internal static JapaneseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod)
    {
        if (inputMethod.ClassId == Guid.Empty)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        var state = SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId);
        return state is null ? SoftKeyboardInputModeContribution.Empty : Decode(inputMethod, state);
    }

    /// <summary>
    /// Converts Japanese TSF state and supplies only the facets missing from the active service.
    /// </summary>
    /// <param name="inputMethod">The active Japanese input-method profile.</param>
    /// <param name="state">The raw TSF state observed for that profile.</param>
    /// <returns>The independently handled state, presentation, and transition facets.</returns>
    internal static SoftKeyboardInputModeContribution Decode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardTsfInputModeState state)
    {
        var mode = Decode(state.OpenValue, state.ConversionValue);
        if (mode is null)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        if (state.CanSwitch)
        {
            return new SoftKeyboardInputModeContribution(
                mode: SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode)
            );
        }

        if (!SoftKeyboardTsfInputMode.SupportsKeyboardOpenFallback(inputMethod))
        {
            return new SoftKeyboardInputModeContribution(
                mode: SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode)
            );
        }

        return new SoftKeyboardInputModeContribution(
            SoftKeyboardInputModeFacet<SoftKeyboardInputMode>.FromValue(mode),
            SoftKeyboardInputModeFacet<SoftKeyboardInputModeButton>.FromValue(
                CreatePresentation(inputMethod, mode)
            ),
            SoftKeyboardInputModeFacet<SoftKeyboardInputModeTransition>.FromValue(
                ToggleNativeTransition
            )
        );
    }

    /// <summary>
    /// Converts the standard Japanese TSF values into a precise keyboard mode.
    /// </summary>
    internal static SoftKeyboardInputMode? Decode(int? open, int? conversion)
    {
        if (open is null || conversion is not int conversionValue)
        {
            return null;
        }

        if (open == 0)
        {
            return Mode("direct", false);
        }

        var native    = (conversionValue & TsfConversionMode.Native) != 0;
        var katakana  = (conversionValue & TsfConversionMode.Katakana) != 0;
        var fullShape = (conversionValue & TsfConversionMode.FullShape) != 0;

        if (!native)
        {
            return fullShape
                ? Mode("full-width-alphanumeric", false)
                : Mode("half-width-alphanumeric", false);
        }

        if (!katakana)
        {
            return Mode("hiragana", true);
        }

        return fullShape
            ? Mode("full-width-katakana", true)
            : Mode("half-width-katakana", true);
    }

    /// <inheritdoc />
    public bool TrySwitchMode(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputModeTransition transition)
    {
        if (!Equals(transition, ToggleNativeTransition)
            || SoftKeyboardTsfInputMode.GetState(inputMethod.ClassId) is not { } state
            || state.CanSwitch
            || state.ConversionValue is not int conversion)
        {
            return false;
        }

        var preserved = conversion
            & ~(TsfConversionMode.Native | TsfConversionMode.Katakana | TsfConversionMode.FullShape);
        var native = state.OpenValue != 0
            && (conversion & TsfConversionMode.Native) != 0;
        var next = native
            ? preserved
            : preserved | TsfConversionMode.Native | TsfConversionMode.FullShape;

        if (!SoftKeyboardTsfInputMode.TrySetKeyboardConversionMode(inputMethod, next))
        {
            return false;
        }

        return state.OpenValue != 0 || SoftKeyboardTsfInputMode.TryToggleKeyboardOpen(inputMethod);
    }

    /// <summary>
    /// Creates the textual mode-key presentation used when the TIP supplies no standard command.
    /// </summary>
    private static SoftKeyboardInputModeButton CreatePresentation(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputMode mode)
    {
        var (content, automationName) = mode.Id switch
        {
            "hiragana"                => ("あ", "平假名输入"),
            "full-width-katakana"     => ("カ", "全角片假名输入"),
            "half-width-katakana"     => ("ｶ", "半角片假名输入"),
            "full-width-alphanumeric" => ("Ａ", "全角字母数字输入"),
            _                          => ("A", "半角字母数字和直接输入"),
        };

        return new SoftKeyboardInputModeButton(
            $"tsf-japanese:{inputMethod.LanguageId:X4}:{mode.Id}",
            content,
            automationName
        );
    }

    private static SoftKeyboardInputMode Mode(string id, bool native)
    {
        return new SoftKeyboardInputMode(id, native);
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
