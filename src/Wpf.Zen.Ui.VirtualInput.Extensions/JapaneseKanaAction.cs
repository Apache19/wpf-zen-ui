// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Extensions;

/// <summary>
/// Sends Japanese kana through the active Windows layout after selecting native kana-key mode.
/// </summary>
public sealed class JapaneseKanaAction : ISoftKeyboardAlternateKeyAction
{
    private readonly string _kana;

    /// <summary>
    /// Initializes a new instance of the <see cref="JapaneseKanaAction"/> class.
    /// </summary>
    /// <param name="kana">The nonempty hiragana text entered by the primary key.</param>
    public JapaneseKanaAction(string kana)
    {
        _kana = !string.IsNullOrEmpty(kana)
            ? kana
            : throw new ArgumentException("Kana text is required.", nameof(kana));
    }

    /// <inheritdoc />
    public void Execute(SoftKeyboardActionContext context)
    {
        Send(context, _kana);
    }

    /// <inheritdoc />
    public void ExecuteAlternate(SoftKeyboardActionContext context, string alternative)
    {
        Send(context, alternative);
    }

    private static void Send(SoftKeyboardActionContext context, string kana)
    {
        if (context.ImeState != InputMethodState.On
            || context.InputMethod is not { LanguageId: 0x0411 }
            || !JapaneseImeInput.TryEnableKanaMode(context.HostWindowHandle))
        {
            return;
        }

        foreach (var (key, shift) in JapaneseImeInput.GetKanaKeys(kana, context.KeyboardLayoutHandle))
        {
            if (!context.TrySendKey(key, shift ? ModifierKeys.Shift : ModifierKeys.None))
            {
                return;
            }
        }
    }
}
