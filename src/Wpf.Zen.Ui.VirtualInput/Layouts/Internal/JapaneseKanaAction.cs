// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

/// <summary>
/// Offers a kana or long-press variant through the keyboard's native Japanese input boundary.
/// </summary>
internal sealed class JapaneseKanaAction(string kana) : ISoftKeyboardAlternateKeyAction
{
    public void Execute(SoftKeyboardActionContext context) => context.TrySendJapaneseKana(kana);

    public void ExecuteAlternate(SoftKeyboardActionContext context, string alternative) => context.TrySendJapaneseKana(alternative);
}