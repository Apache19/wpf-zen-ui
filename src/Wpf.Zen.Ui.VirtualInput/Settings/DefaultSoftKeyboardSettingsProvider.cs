// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Settings;

/// <summary>
/// Provides persistent opening/closing policies and recognized per-input-method layout choices.
/// </summary>
public sealed class DefaultSoftKeyboardSettingsProvider : ISoftKeyboardSettingsProvider
{
    /// <summary>
    /// Gets the shared stateless settings policy.
    /// </summary>
    public static DefaultSoftKeyboardSettingsProvider Default { get; } = new();

    /// <inheritdoc />
    public IEnumerable<SoftKeyboardSetting> CreateSettings(SoftKeyboardSettingsContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        yield return new SoftKeyboardSetting(
            "keyboard",
            "键盘开关",
            children:
            [
                new SoftKeyboardSetting(
                    "open.auto",
                    "自动打开",
                    k => k.SetCurrentValue(SoftKeyboard.AutoShowModeProperty, SoftKeyboardAutoShowMode.AnyFocus)
                )
                {
                    RadioGroup = "open",
                    IsChecked  = context.AutoShowMode == SoftKeyboardAutoShowMode.AnyFocus
                },
                new SoftKeyboardSetting(
                    "open.manual",
                    "手动打开",
                    k => k.SetCurrentValue(SoftKeyboard.AutoShowModeProperty, SoftKeyboardAutoShowMode.Disabled)
                )
                {
                    RadioGroup = "open",
                    IsChecked  = context.AutoShowMode == SoftKeyboardAutoShowMode.Disabled
                },
                new SoftKeyboardSetting("open.close.separator")
                {
                    IsSeparator = true
                },
                new SoftKeyboardSetting(
                    "close.auto",
                    "自动关闭",
                    k => k.SetCurrentValue(SoftKeyboard.AutoHideModeProperty, SoftKeyboardAutoHideMode.FocusLeavesScope)
                )
                {
                    RadioGroup = "close",
                    IsChecked  = context.AutoHideMode == SoftKeyboardAutoHideMode.FocusLeavesScope
                },
                new SoftKeyboardSetting(
                    "close.manual",
                    "手动关闭",
                    k => k.SetCurrentValue(SoftKeyboard.AutoHideModeProperty, SoftKeyboardAutoHideMode.Manual)
                )
                {
                    RadioGroup = "close",
                    IsChecked  = context.AutoHideMode == SoftKeyboardAutoHideMode.Manual
                }
            ]);

        if (context.AvailableLayouts.Count > 2)
        {
            yield return new SoftKeyboardSetting("layout.separator")
            {
                IsSeparator = true
            };

            yield return new SoftKeyboardSetting(
                "layout",
                "键盘布局",
                children:
                context.AvailableLayouts.Select(
                    option => new SoftKeyboardSetting(
                        "layout." + option.Id,
                        option.DisplayName,
                        k => k.SelectSupplementalLayout(option.Id))
                    {
                        RadioGroup = "layout",
                        IsChecked  = context.Layout.SupplementalLayout == option.Id,
                    }
                )
            );
        }

        if (context.Layout.InputMethod is not null)
        {
            yield return new SoftKeyboardSetting("candidates.separator")
            {
                IsSeparator = true
            };

            yield return new SoftKeyboardSetting(
                "candidates",
                "候选显示",
                children:
                [
                    CandidateChoice(SoftKeyboardCandidateMode.Automatic, "自动（兼容性优先）", context),
                    CandidateChoice(SoftKeyboardCandidateMode.Native, "输入法浮窗", context),
                    CandidateChoice(SoftKeyboardCandidateMode.Embedded, "键盘内嵌（尝试）", context)
                ]
            );
        }
    }

    private static SoftKeyboardSetting CandidateChoice(
        SoftKeyboardCandidateMode mode,
        string label,
        SoftKeyboardSettingsContext context)
    {
        return new SoftKeyboardSetting(
            "candidates." + mode,
            label,
            k => k.SetCurrentValue(SoftKeyboard.CandidateModeProperty, mode)
        )
        {
            RadioGroup = "candidates",
            IsChecked  = context.CandidateMode == mode,
        };
    }
}
