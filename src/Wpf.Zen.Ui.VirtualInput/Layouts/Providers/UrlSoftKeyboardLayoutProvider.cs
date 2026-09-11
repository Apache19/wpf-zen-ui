// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;
using static Wpf.Zen.Ui.VirtualInput.Layouts.Internal.KeyboardLayoutParts;

namespace Wpf.Zen.Ui.VirtualInput.Layouts.Providers;

/// <summary>
/// Creates the complete built-in URL address entry page.
/// </summary>
/// <remarks>
/// Owns every key and its placement. Physical letter keys and address literals are optional reusable
/// material, not a base layout to patch. Handles Automatic and Letters for its input scope; other
/// pages are deliberately declined so the enclosing language composition can choose their layouts.
/// This provider can also be reused independently in an application-defined composition.
/// </remarks>
public sealed class UrlSoftKeyboardLayoutProvider : ISoftKeyboardLayoutProvider
{
    /// <summary>
    /// Gets the shared, stateless provider.
    /// </summary>
    public static UrlSoftKeyboardLayoutProvider Default { get; } = new();

    /// <inheritdoc />
    public SoftKeyboardLayout? CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (context.InputScope != InputScopeNameValue.Url
            || context.Page is not (SoftKeyboardPages.Automatic or SoftKeyboardPages.Letters))
        {
            return null;
        }

        var rows = PhysicalKeyboardKeys.CreateRows(context);

        // Explicit geometry belongs to this page even when some other built-in pages use the same
        // arrangement. Changing another page's row composition must not implicitly alter this one.
        return new SoftKeyboardLayoutBuilder(20)
            .AddRow(0, rows[0], 2)
            .AddRow(1, rows[1], 2, startColumn: 1)
            .AddRow(
                2,
                [
                    new SoftKeyboardKey(SoftKeyboardKeyKind.Shift, GetShiftContent(context.ShiftState), "Shift"),
                    .. rows[2],
                    new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, BackspaceIcon, "Backspace", Key.Back, isRepeatEnabled: true),
                ],
                [2, 2, 2, 2, 2, 2, 2, 2, 4])
            .AddRow(
                3,
                [
                    SoftKeyboardKey.Navigate(SoftKeyboardPages.Symbols, "123", "Numbers and symbols"),
                    CreateImeKey(context),
                    new SoftKeyboardKey(SoftKeyboardKeyKind.Text, "http", "http", text: "http", alternates: ["https"], alternateHint: "https"),
                    SoftKeyboardKey.FromText(":"),
                    new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, SpaceIcon, "Space", Key.Space),
                    AddressKeyboardKeys.Domain(context.Culture),
                    AddressKeyboardKeys.Period(),
                    SoftKeyboardKey.FromText("/"),
                    new SoftKeyboardKey(SoftKeyboardKeyKind.InputMethods, InputMethodsIcon, "Input methods"),
                    new SoftKeyboardKey(SoftKeyboardKeyKind.VirtualKey, EnterIcon, "Enter", Key.Enter),
                ],
                [2, 2, 2, 2, 2, 2, 2, 2, 2, 2])
            .Build();
    }
}
