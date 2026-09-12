// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Internal;

internal static class SupplementalLayoutCatalog
{
    internal static IReadOnlyList<SoftKeyboardLayoutOption> GetOptions(SoftKeyboardLayoutContext context)
    {
        // todo：尝试从输入法或系统中读取，不要硬编码名称，再不然就采取本地化资源的方式实现。
        var options = new List<SoftKeyboardLayoutOption>
        {
            new(SoftKeyboardSupplementalLayouts.Automatic, "默认（输入法首选）")
        };

        if (context.InputMethod?.ClassId != Guid.Empty
            && context.InputMethod is not null
            && context.Culture.TwoLetterISOLanguageName == "ja")
        {
            options.Add(
                new SoftKeyboardLayoutOption(
                    SoftKeyboardSupplementalLayouts.JapaneseFifty,
                    "50音"
                )
            );

            options.Add(
                new SoftKeyboardLayoutOption(
                    SoftKeyboardSupplementalLayouts.JapaneseTwelve,
                    "12键"
                )
            );
        }
        else if (IsZhuyin(context))
        {
            options.Add(
                new SoftKeyboardLayoutOption(
                    SoftKeyboardSupplementalLayouts.Standard,
                    "英文键盘"
                )
            );

            options.Add(
                new SoftKeyboardLayoutOption(
                    SoftKeyboardSupplementalLayouts.Zhuyin,
                    "注音"
                )
            );
        }

        return options;
    }

    internal static bool IsZhuyin(SoftKeyboardLayoutContext context)
    {
        // LANGID alone also covers Changjie/Dayi and third-party schemes. Only the documented
        // phonetic layout preference establishes that our standard Zhuyin key mapping is relevant.
        return context.InputMethod is
            {
                LanguageId: 0x0404, TouchLayoutPreference:
                {
                    Id: 0x0404,
                    Type: SoftKeyboardTouchLayoutType.Classic or SoftKeyboardTouchLayoutType.Optimized
                }
            }

            // Modern Microsoft Bopomofo may not publish this Windows 8 function to a desktop host.
            // Its exact documented profile identity remains a valid phonetic capability declaration;
            // do not extend this fallback to arbitrary Taiwanese or similarly named input methods.
            || (context.InputMethod is { LanguageId: 0x0404, TouchLayoutPreference: null } profile
                && profile.ClassId == new Guid("B115690A-EA02-48D5-A231-E3578D2FDF80")
                && profile.ProfileId == new Guid("B2F9C502-1742-11D4-9790-0080C882687E"));
    }

    internal static string Resolve(SoftKeyboardLayoutContext context)
    {
        var selected = context.SupplementalLayout;
        if (selected == SoftKeyboardSupplementalLayouts.Automatic)
        {
            return IsZhuyin(context)
                ? SoftKeyboardSupplementalLayouts.Zhuyin
                : SoftKeyboardSupplementalLayouts.Standard;
        }

        return GetOptions(context).Any(option => option.Id == selected)
            ? selected
            : SoftKeyboardSupplementalLayouts.Standard;
    }
}