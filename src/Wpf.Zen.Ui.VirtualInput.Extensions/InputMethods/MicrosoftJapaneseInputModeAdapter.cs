// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;

/// <summary>
/// Interprets Microsoft Japanese IME observations and overrides its icon with a textual key cap.
/// Fallback execution changes the open compartment while preserving selected conversion flags.
/// </summary>
internal sealed class MicrosoftJapaneseInputModeAdapter : ISoftKeyboardInputModeAdapter
{
    /// <summary>
    /// Gets the shared stateless Microsoft Japanese IME adapter.
    /// </summary>
    internal static MicrosoftJapaneseInputModeAdapter Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardInputModeContribution GetContribution(
        SoftKeyboardInputMethod inputMethod,
        SoftKeyboardInputObservation observation)
    {
        if (DecodeOpen(inputMethod.ClassId, observation) is not { } enabled
            || DecodeJapaneseMode(enabled, observation.ConversionValue) is not { } mode)
        {
            return SoftKeyboardInputModeContribution.Empty;
        }

        return new SoftKeyboardInputModeContribution(
            CreateJapanesePresentation(inputMethod.ClassId, mode.Id),
            new SoftKeyboardInputModeTransition(enabled ? "disable" : "enable"),
            mode.Compact ? JapaneseCompactLayoutProvider.Default : null,
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

        // The core resolves the transition from the latest observation after platform discovery.
        // Accepted writes do not become synthetic observations or predict the next key cap.
        return SoftKeyboardTsfInputMode.TrySetKeyboardOpenValue(
            inputMethod, transition.Id == "enable" ? 1 : 0);
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

        return observation.OpenValue == 1 ? true : observation.OpenValue == 0 ? false : null;
    }

    /// <summary>
    /// Decodes Microsoft Japanese conversion flags only while its open compartment is one.
    /// The binary transition uses the observed open integer, so full-width Latin input
    /// can still be switched off and the selected conversion remains intact when reopening.
    /// </summary>
    private static (string Id, bool Compact)? DecodeJapaneseMode(bool enabled, int? conversion)
    {
        if (!enabled)
        {
            return ("direct", false);
        }

        return conversion switch
        {
            0x09 => ("hiragana", true),
            0x0B => ("full-width-katakana", true),
            0x08 => ("full-width-alphanumeric", false),
            0x03 => ("half-width-katakana", true),
            0x00 => ("half-width-alphanumeric", false),
            _    => null,
        };
    }

    /// <summary>
    /// Creates the textual cap that identifies Microsoft Japanese IME's effective input mode.
    /// </summary>
    private static SoftKeyboardInputModeButton CreateJapanesePresentation(
        Guid classId, string mode)
    {
        var (content, automationName) = mode switch
        {
            "hiragana"                => ("あ", "平假名输入"),
            "full-width-katakana"     => ("カ", "全角片假名输入"),
            "half-width-katakana"     => ("ｶ", "半角片假名输入"),
            "full-width-alphanumeric" => ("Ａ", "全角字母数字输入"),
            _                         => ("A", "字母数字输入"),
        };

        return new SoftKeyboardInputModeButton(
            $"known-tip:{classId:D}:{mode}", content, automationName);
    }
}
