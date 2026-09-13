// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Extensions;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

public class CandidateNumberCommitTests
{
    [Theory]
    [InlineData(0, 1, Key.D1)]
    [InlineData(4, 5, Key.D5)]
    [InlineData(8, 9, Key.D9)]
    [InlineData(0, 0, Key.None)]
    [InlineData(5, 5, Key.None)]
    [InlineData(0, 10, Key.None)]
    [InlineData(uint.MaxValue, 9, Key.None)]
    public void NumberShortcut_OnlyAcceptsPublishedPositionsInStandardNineCandidatePages(uint index, uint count, Key expected)
    {
        Key key = Key.None;

        bool available = count is > 0 and <= 9
            && index < count
            && WeaselCandidateBehavior.TryGetCandidateNumberKey((int)index, out key);

        Assert.Equal(expected != Key.None, available);
        Assert.Equal(expected, key);
    }
}
