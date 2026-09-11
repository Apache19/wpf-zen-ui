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
    private void ExecuteCustomAction(ISoftKeyboardKeyAction action)
    {
        if (!PrepareTargetForInput())
        {
            return;
        }

        var context = new SoftKeyboardActionContext(this);
        try
        {
            action.Execute(context);
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
}
