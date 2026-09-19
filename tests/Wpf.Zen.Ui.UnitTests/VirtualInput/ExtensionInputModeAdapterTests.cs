// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.VirtualInput.Extensions.InputMethods;
using Wpf.Zen.Ui.VirtualInput.Extensions.Layouts;
using Wpf.Zen.Ui.VirtualInput.Input;

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
    /// Japanese textual overrides distinguish conversion modes independently of compact layout.
    /// </summary>
    /// <param name="open">The observed Japanese open integer.</param>
    /// <param name="conversion">The observed conversion integer, or an unavailable field.</param>
    /// <param name="cap">The expected textual key cap.</param>
    /// <param name="compact">Whether this conversion selects the Japanese compact layout.</param>
    [Theory]
    [InlineData(0, null, "A", false)]
    [InlineData(1, 0x09, "あ", true)]
    [InlineData(1, 0x0B, "カ", true)]
    [InlineData(1, 0x03, "ｶ", true)]
    [InlineData(1, 0x08, "Ａ", false)]
    [InlineData(1, 0x00, "A", false)]
    public void JapaneseContracts_PreserveTextOverrideAndIndependentLayout(
        int open, int? conversion, string cap, bool compact)
    {
        var profile = new SoftKeyboardInputMethod(
            Guid.Parse("03B5835F-F03C-411B-9CE2-AA23E1171E36"),
            Guid.NewGuid(), 0x0411, "Japanese service", true);
        var observation = new SoftKeyboardInputObservation(true, open, true, conversion);

        var contribution = MicrosoftJapaneseInputModeAdapter.Default.GetContribution(profile, observation);

        Assert.Equal(cap, contribution.Button?.Content);
        Assert.Equal(open == 1 ? "disable" : "enable", contribution.Transition?.Id);
        Assert.Same(compact ? JapaneseCompactLayoutProvider.Default : null,
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
    [InlineData("03B5835F-F03C-411B-9CE2-AA23E1171E36", 1, null)]
    [InlineData("03B5835F-F03C-411B-9CE2-AA23E1171E36", 1, 0x10)]
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
}
