// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.InputMethods;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Preserves service-specific presentation contracts when reorganizing extension capabilities.
/// </summary>
public class ExtensionInputModeAdapterTests
{
    /// <summary>
    /// Chinese services retain their distinct raw contracts and never override platform buttons.
    /// </summary>
    /// <param name="classId">The service identity from the supported contract.</param>
    /// <param name="open">The observed open integer.</param>
    /// <param name="conversion">The observed conversion integer.</param>
    /// <param name="enabled">Whether the contract selects Chinese presentation and layout.</param>
    [Theory]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", 1, 1025, true)]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", 1, 0, false)]
    [InlineData("B115690A-EA02-48D5-A231-E3578D2FDF80", 1, 1, true)]
    [InlineData("B115690A-EA02-48D5-A231-E3578D2FDF80", 1, 0, false)]
    [InlineData("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643", 1, 0, true)]
    [InlineData("B722B5D7-0C4C-4933-A7B9-DF8C91F2C643", 2, 0, false)]
    [InlineData("86598FB9-66A2-463E-B9C2-AEB906D477AD", 1, 0, true)]
    [InlineData("86598FB9-66A2-463E-B9C2-AEB906D477AD", 0, 0, false)]
    public void ChineseContracts_PreserveButtonLayoutAndTransition(
        string classId, int open, int conversion, bool enabled)
    {
        var profile = new SoftKeyboardInputMethod(
            Guid.Parse(classId), Guid.NewGuid(), 0x0804, "Chinese service", true);
        var observation = new SoftKeyboardInputObservation(true, open, true, conversion);

        var contribution = ChineseInputModeAdapter.Default.GetContribution(profile, observation);

        Assert.Equal(enabled ? "中" : "A", contribution.Button?.Content);
        Assert.Equal(enabled ? "disable" : "enable", contribution.Transition?.Id);
        Assert.Same(enabled ? ChineseCompactLayoutProvider.Default : null,
            contribution.LayoutProvider);
        Assert.False(contribution.OverridePlatformButton);
        Assert.Same(SoftKeyboardInputModeContribution.Empty,
            MicrosoftJapaneseInputModeAdapter.Default.GetContribution(profile, observation));
    }

    /// <summary>
    /// Pinyin keeps its fallback button when only conversion is notified or ancillary bits vary.
    /// </summary>
    /// <param name="openObserved">Whether this generation received an open notification.</param>
    /// <param name="open">The notified open value, including an unreadable compartment.</param>
    /// <param name="conversion">The raw native, symbol, and full-shape flag combination.</param>
    /// <param name="enabled">Whether Chinese mode is indicated by the native flag.</param>
    [Theory]
    [InlineData(false, null, 1025, true)]
    [InlineData(false, null, 0, false)]
    [InlineData(true, null, 1025, true)]
    [InlineData(true, 0, 1025, true)]
    [InlineData(true, 1, 1, true)]
    [InlineData(true, 1, 0x0409, true)]
    [InlineData(true, 1, 0x0400, false)]
    [InlineData(true, 1, 0x0408, false)]
    public void PinyinConversion_ResolvesWithoutOpenAndIgnoresAncillaryFlags(
        bool openObserved, int? open, int conversion, bool enabled)
    {
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftPinyin, Guid.NewGuid(), 0x0804, "Microsoft Pinyin", true);
        var observation = new SoftKeyboardInputObservation(openObserved, open, true, conversion);

        var contribution = ChineseInputModeAdapter.Default.GetContribution(profile, observation);

        Assert.Equal(enabled ? "中" : "A", contribution.Button?.Content);
        Assert.Equal(enabled ? "disable" : "enable", contribution.Transition?.Id);
        Assert.Same(enabled ? ChineseCompactLayoutProvider.Default : null,
            contribution.LayoutProvider);
        Assert.False(contribution.OverridePlatformButton);
    }

    /// <summary>
    /// A supplied integer without a conversion notification cannot create a Pinyin fallback.
    /// </summary>
    [Fact]
    public void PinyinWithoutConversionNotification_ContributesNoCapabilities()
    {
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftPinyin, Guid.NewGuid(), 0x0804, "Microsoft Pinyin", true);
        var observation = new SoftKeyboardInputObservation(true, 1, false, 1025);

        Assert.Same(SoftKeyboardInputModeContribution.Empty,
            ChineseInputModeAdapter.Default.GetContribution(profile, observation));
    }

    /// <summary>
    /// Japanese textual overrides distinguish character mode from physical input style.
    /// </summary>
    /// <param name="open">The observed Japanese open integer.</param>
    /// <param name="conversion">The observed conversion integer, or an unavailable field.</param>
    /// <param name="cap">The expected textual key cap.</param>
    /// <param name="native">Whether this conversion selects native Japanese input.</param>
    [Theory]
    [InlineData(0, null, "A", false)]
    [InlineData(0, 0x19, "A", false)]
    [InlineData(1, 0x09, "あ", true)]
    [InlineData(1, 0x19, "あ", true)]
    [InlineData(1, 0x0B, "カ", true)]
    [InlineData(1, 0x1B, "カ", true)]
    [InlineData(1, 0x03, "ｶ", true)]
    [InlineData(1, 0x13, "ｶ", true)]
    [InlineData(1, 0x08, "Ａ", false)]
    [InlineData(1, 0x18, "Ａ", false)]
    [InlineData(1, 0x00, "A", false)]
    [InlineData(1, 0x10, "A", false)]
    public void JapaneseContracts_PreserveTextOverrideAndIndependentLayout(
        int open, int? conversion, string cap, bool native)
    {
        var profile = new SoftKeyboardInputMethod(
            Guid.Parse("03B5835F-F03C-411B-9CE2-AA23E1171E36"),
            Guid.NewGuid(), 0x0411, "Japanese service", true);
        var observation = new SoftKeyboardInputObservation(true, open, true, conversion);

        var contribution = MicrosoftJapaneseInputModeAdapter.Default.GetContribution(profile, observation);

        Assert.Equal(cap, contribution.Button?.Content);
        Assert.Equal(open == 1 ? "disable" : "enable", contribution.Transition?.Id);
        Assert.Same(native ? JapaneseSoftKeyboardLayoutProvider.Default : null,
            contribution.LayoutProvider);
        Assert.True(contribution.OverridePlatformButton);
        Assert.Same(SoftKeyboardInputModeContribution.Empty,
            ChineseInputModeAdapter.Default.GetContribution(profile, observation));
    }

    /// <summary>
    /// Unknown services and missing required or unsupported values leave presentation to the core.
    /// </summary>
    /// <param name="classId">A supported or unknown service identity.</param>
    /// <param name="open">The observed open integer, or an unavailable field.</param>
    /// <param name="conversion">The observed conversion integer, or an unavailable field.</param>
    [Theory]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", null, null)]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", 1, null)]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", 2, 1025)]
    [InlineData("81D4E9C9-1D3B-41BC-9E6C-4B40BF79E35E", 1, -1)]
    [InlineData("03B5835F-F03C-411B-9CE2-AA23E1171E36", null, 0x19)]
    [InlineData("03B5835F-F03C-411B-9CE2-AA23E1171E36", 2, 0x19)]
    [InlineData("00000000-0000-0000-0000-000000000001", 1, 1025)]
    public void UnrecognizedObservations_ContributeNoCapabilities(
        string classId, int? open, int? conversion)
    {
        var profile = new SoftKeyboardInputMethod(
            Guid.Parse(classId), Guid.NewGuid(), 0x0804, "Test service", true);
        var observation = new SoftKeyboardInputObservation(true, open, true, conversion);

        Assert.Same(SoftKeyboardInputModeContribution.Empty,
            ChineseInputModeAdapter.Default.GetContribution(profile, observation));
        Assert.Same(SoftKeyboardInputModeContribution.Empty,
            MicrosoftJapaneseInputModeAdapter.Default.GetContribution(profile, observation));
    }

    /// <summary>
    /// Direct input always enters Hiragana; closing any enabled mode leaves conversion intact.
    /// </summary>
    /// <param name="open">The observed open state.</param>
    /// <param name="conversion">The retained conversion flags.</param>
    [Theory]
    [InlineData(0, null)]
    [InlineData(0, 0x00)]
    [InlineData(0, 0x09)]
    [InlineData(0, 0x19)]
    [InlineData(0, 0x0B)]
    [InlineData(0, 0x18)]
    [InlineData(0, 0x03)]
    [InlineData(0, 0x99)]
    [InlineData(0, 0x999)]
    [InlineData(0, 0x239)]
    [InlineData(0, -1)]
    [InlineData(1, 0x09)]
    [InlineData(1, 0x19)]
    [InlineData(1, 0x0B)]
    [InlineData(1, 0x08)]
    [InlineData(1, 0x03)]
    [InlineData(1, 0x00)]
    [InlineData(1, null)]
    [InlineData(1, 0x04)]
    public void JapaneseSwitch_SelectsHiraganaOrClosesWithoutChangingConversion(
        int open, int? conversion)
    {
        var writes = new List<(string Field, int Value)>();
        var adapter = new MicrosoftJapaneseInputModeAdapter(
            (_, value) =>
            {
                writes.Add(("open", value));
                return true;
            },
            (_, value) =>
            {
                writes.Add(("conversion", value));
                return true;
            });
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftJapanese, Guid.NewGuid(), 0x0411, "Japanese", true);
        var observation = new SoftKeyboardInputObservation(true, open, true, conversion);
        var transition = Assert.IsType<SoftKeyboardInputModeTransition>(
            adapter.GetContribution(profile, observation).Transition);

        Assert.True(adapter.TrySwitchMode(profile, observation, transition));

        if (open == 0)
        {
            var expectedStyle = conversion is >= 0 ? conversion.Value & 0x90 : 0;
            Assert.Equal(new[] { ("open", 1), ("conversion", 0x09 | expectedStyle) },
                writes);
            var actual = new SoftKeyboardInputObservation(true, 1, true, writes[1].Value);
            Assert.Equal("あ", adapter.GetContribution(profile, actual).Button?.Content);
        }
        else
        {
            Assert.Equal(new[] { ("open", 0) }, writes);
        }
    }

    /// <summary>
    /// A rejected open stops the command, and a rejected conversion reports partial failure.
    /// </summary>
    /// <param name="openAccepted">Whether the open write succeeds.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void JapaneseSwitch_ReportsWriteFailure(bool openAccepted)
    {
        var conversionWrites = 0;
        var adapter = new MicrosoftJapaneseInputModeAdapter(
            (_, _) => openAccepted,
            (_, _) =>
            {
                conversionWrites++;
                return false;
            });
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftJapanese, Guid.NewGuid(), 0x0411, "Japanese", true);

        Assert.False(adapter.TrySwitchMode(profile,
            new SoftKeyboardInputObservation(true, 0, true, 0x19),
            new SoftKeyboardInputModeTransition("enable")));
        Assert.Equal(openAccepted ? 1 : 0, conversionWrites);
    }

    /// <summary>
    /// A known open state allows closing without guessing an unknown character-mode cap.
    /// </summary>
    /// <param name="conversion">An unavailable or unsupported conversion value.</param>
    [Theory]
    [InlineData(null)]
    [InlineData(0x04)]
    [InlineData(0x29)]
    [InlineData(0x49)]
    [InlineData(0x209)]
    [InlineData(0x409)]
    [InlineData(0x1029)]
    [InlineData(0x1009)]
    [InlineData(0x40000009)]
    [InlineData(-1)]
    public void JapaneseUnknownConversion_PreservesCloseAction(int? conversion)
    {
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftJapanese, Guid.NewGuid(), 0x0411, "Japanese", true);
        var contribution = MicrosoftJapaneseInputModeAdapter.Default.GetContribution(profile,
            new SoftKeyboardInputObservation(true, 1, true, conversion));

        Assert.Null(contribution.Button);
        Assert.Null(contribution.LayoutProvider);
        Assert.False(contribution.OverridePlatformButton);
        Assert.Equal("disable", contribution.Transition?.Id);
    }

    /// <summary>
    /// Character caps depend on script and width while input style and conversion behavior
    /// remain independent. Unsupported native-width combinations do not invent new caps.
    /// </summary>
    /// <param name="characterMode">The language, script and width bits.</param>
    /// <param name="cap">The effective character-mode cap.</param>
    /// <param name="name">The base accessibility description.</param>
    /// <param name="native">Whether the character mode uses native Japanese input.</param>
    [Theory]
    [InlineData(0x00, "A", "字母数字输入", false)]
    [InlineData(0x02, "A", "字母数字输入", false)]
    [InlineData(0x08, "Ａ", "全角字母数字输入", false)]
    [InlineData(0x0A, "Ａ", "全角字母数字输入", false)]
    [InlineData(0x01, "あ", "平假名输入", true)]
    [InlineData(0x09, "あ", "平假名输入", true)]
    [InlineData(0x03, "ｶ", "半角片假名输入", true)]
    [InlineData(0x0B, "カ", "全角片假名输入", true)]
    public void JapaneseFlags_SeparateCharacterModeStyleAndBehavior(
        int characterMode, string cap, string name, bool native)
    {
        var profile = new SoftKeyboardInputMethod(
            KnownTextServices.MicrosoftJapanese, Guid.NewGuid(), 0x0411, "Japanese", true);
        foreach (var inputStyle in new[] { 0, 0x10, 0x80, 0x90 })
        {
            foreach (var (behavior, suffix) in new[]
            {
                (0, ""), (0x100, "（不转换）"), (0x800, "（固定转换）"),
                (0x900, "（不转换、固定转换）"),
            })
            {
                var observation = new SoftKeyboardInputObservation(
                    true, 1, true, characterMode | inputStyle | behavior);
                var contribution = MicrosoftJapaneseInputModeAdapter.Default.GetContribution(
                    profile, observation);

                Assert.Equal(cap, contribution.Button?.Content);
                Assert.Equal(name + suffix, contribution.Button?.AutomationName);
                Assert.Equal("disable", contribution.Transition?.Id);
                Assert.True(contribution.OverridePlatformButton);
                Assert.Same(native ? JapaneseSoftKeyboardLayoutProvider.Default : null,
                    contribution.LayoutProvider);
            }
        }
    }
}
