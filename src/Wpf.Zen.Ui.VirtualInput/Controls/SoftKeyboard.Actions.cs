// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Coordinates custom key execution with the standard input validation boundary.
/// </summary>
public partial class SoftKeyboard
{
    private void ExecuteCustomAction(ISoftKeyboardKeyAction action, string? alternative = null)
    {
        if (!PrepareTargetForInput())
        {
            return;
        }

        var context = new SoftKeyboardActionContext(this);
        try
        {
            if (alternative is not null
                && action is ISoftKeyboardAlternateKeyAction alternateAction)
            {
                alternateAction.ExecuteAlternate(context, alternative);
            }
            else
            {
                action.Execute(context);
            }
        }
        finally
        {
            // Retaining a context must never grant a later callback access to a different editor.
            context.Expire();
        }
    }

    internal bool SendActionText(string text)
    {
        var sent = TrySendText(text);
        if (sent && ShiftState == SoftKeyboardShiftState.Once)
        {
            SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
        }

        return sent;
    }

    internal bool SendActionKey(Key key, bool shift)
    {
        if (key == Key.None || !Enum.IsDefined(typeof(Key), key))
        {
            throw new ArgumentOutOfRangeException(nameof(key));
        }

        var sent = TrySendVirtualKey(key, shift);
        if (sent && IsCharacterKey(key) && ShiftState == SoftKeyboardShiftState.Once)
        {
            SetValue(ShiftStatePropertyKey, SoftKeyboardShiftState.Off);
        }

        return sent;
    }

    // todo: 通用模块里不应有专项功能，职责边界不清晰
    internal bool SendJapaneseKana(string kana)
    {
        if (!PrepareTargetForInput() || ImeState != InputMethodState.On
            || ActiveInputMethod is not { LanguageId: 0x0411 })
        {
            return false;
        }

        var window = Window.GetWindow(this);
        var keys = JapaneseImeInput.GetKanaKeys(kana, KeyboardLayoutLabels.GetCurrentLayout());

        if (keys.Length == 0)
        {
            return false;
        }

        if (window is null || !JapaneseImeInput.TryEnableKanaMode(new WindowInteropHelper(window).Handle))
        {
            return false;
        }

        foreach (var (key, shift) in keys)
        {
            if (!_platform.Input.SendVirtualKey(key, shift))
            {
                return false;
            }
        }

        QueueCandidateBridgeRefresh();
        RequestSystemKeyboardHide();

        return true;
    }
}
