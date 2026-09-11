// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using System.Windows.Threading;

namespace Wpf.Zen.Ui.VirtualInput.Controls;

/// <summary>
/// Refreshes, selects, and observes Windows input-method profiles.
/// </summary>
public partial class SoftKeyboard
{
    private void RefreshInputMethods()
    {
        _platform.InputMethods.Refresh(_inputMethods, out var active);

        var previous = ActiveInputMethod;

        SetValue(ActiveInputMethodPropertyKey, active);

        if (previous?.ProfileId != active?.ProfileId
            || previous?.ClassId != active?.ClassId
            || previous?.LanguageId != active?.LanguageId)
        {
            RebuildLayout();
        }

        if (active is not null)
        {
            _preferredImeInputMethod = active;
        }
    }

    private void RefreshImeState()
    {
        SetValue(ImeStatePropertyKey, ReadInputState());

        if (IsLoaded && !CanPresentCandidates)
        {
            _platform.Candidates.ReleasePresentation(this);
        }
    }

    private void ActivateInputMethod(SoftKeyboardInputMethod inputMethod)
    {
        RunAfterActiveCompositionCancellation(() => ActivateInputMethodCore(inputMethod));
    }

    private void ActivateInputMethodCore(SoftKeyboardInputMethod inputMethod)
    {
        if (_platform.InputMethods.TryActivate(inputMethod))
        {
            _preferredImeInputMethod = inputMethod;

            RefreshInputMethods();

            _ = TryChangeInputState(InputMethodState.On);

            RefreshImeState();
            RebuildLayout();
            QueueInputMethodStateRefresh();
        }
    }

    private SoftKeyboardInputMethod? FindPreferredImeInputMethod()
    {
        var remembered = _preferredImeInputMethod is null
            ? null
            : _inputMethods.FirstOrDefault(
                inputMethod =>
                       inputMethod.ClassId    == _preferredImeInputMethod.ClassId
                    && inputMethod.ProfileId  == _preferredImeInputMethod.ProfileId
                    && inputMethod.LanguageId == _preferredImeInputMethod.LanguageId
            );

        if (remembered is not null)
        {
            return remembered;
        }

        // The first invocation has no process-local history. Prefer a TIP matching the application's
        // UI culture, then its formatting culture, before falling back to Windows enumeration order.
        // This makes a Chinese application select Microsoft Pinyin without hard-coding a vendor or
        // preventing Japanese, Korean, and third-party profiles from using the same control.
        var uiLanguage         = unchecked((ushort)CultureInfo.CurrentUICulture.LCID);
        var formattingLanguage = unchecked((ushort)CultureInfo.CurrentCulture.LCID);

        return _inputMethods.FirstOrDefault(inputMethod => SamePrimaryLanguage(inputMethod.LanguageId, uiLanguage))
            ?? _inputMethods.FirstOrDefault(inputMethod => SamePrimaryLanguage(inputMethod.LanguageId, formattingLanguage))
            ?? _inputMethods.FirstOrDefault();
    }

    private void QueueInputMethodStateRefresh()
    {
        // Profile activation is allowed to complete asynchronously. Re-reading both the active
        // profile and open compartment prevents the optimistic button state from becoming stale.
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.ContextIdle,
            new Action(
                () =>
                {
                    if (IsLoaded)
                    {
                        RefreshInputMethods();
                        RefreshImeState();
                        RebuildLayout();
                        QueueCandidateBridgeRefresh();
                    }
                }
            )
        );
    }

    private static bool SamePrimaryLanguage(ushort left, ushort right)
    {
        // todo: 0x03FF 这个掩码代表什么？
        return (left & 0x03FF) == (right & 0x03FF);
    }
}
