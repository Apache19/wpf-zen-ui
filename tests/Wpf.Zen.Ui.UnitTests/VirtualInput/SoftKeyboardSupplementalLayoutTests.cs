// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;
using Wpf.Zen.Ui.VirtualInput.Extensibility;
using Wpf.Zen.Ui.VirtualInput.Extensions;
using Wpf.Zen.Ui.VirtualInput.Input;
using Wpf.Zen.Ui.VirtualInput.Layouts;

namespace Wpf.Zen.Ui.UnitTests.VirtualInput;

/// <summary>
/// Checks supplemental layout availability for recognized input environments.
/// </summary>
public class SoftKeyboardSupplementalLayoutTests
{
    /// <summary>
    /// Unrecognized preferences leave only the two built-in choices.
    /// </summary>
    [Theory]
    [InlineData("ja-JP", null, 2)]
    [InlineData("zh-TW", 0x0404, 2)]
    [InlineData("zh-TW", 0xF042, 2)]
    [InlineData("zh-TW", null, 2)]
    [InlineData("zh-CN", 0x0804, 2)]
    public void LayoutChoices_RequireAnImplementedRecognizedEnvironment(string culture, int? id, int choices)
    {
        var context = Context(culture, id);

        Assert.Equal(choices, LayoutOptions(context).Count);
    }

    /// <summary>
    /// Combines the built-in choices with layouts advertised by the default extensions.
    /// </summary>
    private static IReadOnlyList<SoftKeyboardLayoutOption> LayoutOptions(SoftKeyboardLayoutContext context)
    {
        var extensions = DefaultSoftKeyboardExtensionProvider.Default.GetExtensions(
            new SoftKeyboardExtensionContext(context)
        );

        var layouts = extensions
            .Where(extension => extension.SupplementalLayouts is not null)
            .SelectMany(extension => extension.SupplementalLayouts!.GetLayouts(context));

        return
        [
            new SoftKeyboardLayoutOption(SoftKeyboardSupplementalLayouts.Automatic, "Automatic"),
            new SoftKeyboardLayoutOption(SoftKeyboardSupplementalLayouts.Standard, "Standard"),
            .. layouts.Select(layout => new SoftKeyboardLayoutOption(layout.Id, layout.DisplayName))
        ];
    }

    /// <summary>
    /// Builds an input profile with the requested native touch-layout preference.
    /// </summary>
    private static SoftKeyboardLayoutContext Context(
        string culture,
        int? preference,
        string layout = SoftKeyboardSupplementalLayouts.Automatic,
        InputScopeNameValue scope = InputScopeNameValue.Default)
    {
        var language = CultureInfo.GetCultureInfo(culture);

        var profile = new SoftKeyboardInputMethod(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (ushort)language.LCID,
            culture,
            true,
            preference is { } id
                ? new SoftKeyboardTouchLayoutPreference(
                    SoftKeyboardTouchLayoutType.Optimized, (ushort)id
                )
                : null
        );

        return new SoftKeyboardLayoutContext(
            language,
            scope,
            SoftKeyboardPages.Automatic,
            SoftKeyboardShiftState.Off,
            profile,
            supplementalLayout: layout
        );
    }
}
