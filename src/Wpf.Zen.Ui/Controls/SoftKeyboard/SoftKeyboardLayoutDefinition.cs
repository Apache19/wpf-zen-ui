// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Declares a keyboard in XAML using rows, key spans and semantic actions.</summary>
/// <remarks>
/// Set this object as <see cref="SoftKeyboard.LayoutProvider"/> for a fixed layout, or call
/// <see cref="CreateLayout"/> from a context-aware provider after selecting a language definition.
/// Definitions are configuration objects, not live visual trees; configure them before assigning
/// the provider. Every call produces a validated immutable snapshot consumed by the keyboard.
/// </remarks>
[ContentProperty(nameof(Rows))]
public sealed class SoftKeyboardLayoutDefinition : ISoftKeyboardLayoutProvider
{
    /// <summary>Gets or sets the shared logical column count. Defaults to twenty half-key units.</summary>
    public int ColumnCount { get; set; } = 20;

    /// <summary>Gets the rows in visual order. Explicit offsets reserve empty leading cells.</summary>
    public Collection<SoftKeyboardRowDefinition> Rows { get; } = [];

    /// <inheritdoc />
    public SoftKeyboardLayout CreateLayout(SoftKeyboardLayoutContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        var keys = new List<SoftKeyboardKey>();
        for (int row = 0; row < Rows.Count; row++)
        {
            SoftKeyboardRowDefinition definition = Rows[row] ?? throw new InvalidOperationException("A row cannot be null.");
            int column = definition.Offset;
            foreach (SoftKeyboardKeyDefinition key in definition.Keys)
            {
                if (key is null)
                {
                    throw new InvalidOperationException("A key cannot be null.");
                }

                keys.Add(new SoftKeyboardKey(
                    key.Kind,
                    key.Content ?? key.Text ?? key.Key.ToString(),
                    key.AutomationName ?? key.Text ?? key.Key.ToString(),
                    key.Key,
                    key.Text,
                    key.DisplayMode,
                    row,
                    column,
                    key.Span,
                    key.Alternates));
                column = checked(column + key.Span);
            }
        }

        return new SoftKeyboardLayout(keys, ColumnCount);
    }
}
