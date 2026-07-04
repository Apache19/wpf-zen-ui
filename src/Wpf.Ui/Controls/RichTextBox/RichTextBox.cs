// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;

// ReSharper disable once CheckNamespace

namespace Wpf.Ui.Controls;

/// <summary>
/// Extends the <see cref="System.Windows.Controls.RichTextBox"/> control with additional properties.
/// </summary>
public class RichTextBox : System.Windows.Controls.RichTextBox
{
    /// <summary>Identifies the <see cref="IsTextSelectionEnabled"/> dependency property.</summary>
    public static readonly DependencyProperty IsTextSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsTextSelectionEnabled),
        typeof(bool),
        typeof(RichTextBox),
        new PropertyMetadata(false)
    );

    static RichTextBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(RichTextBox),
            new FrameworkPropertyMetadata(typeof(RichTextBox))
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the user can select text in the control.
    /// </summary>
    public bool IsTextSelectionEnabled
    {
        get => (bool)GetValue(IsTextSelectionEnabledProperty);
        set => SetValue(IsTextSelectionEnabledProperty, value);
    }

    /// <inheritdoc />
    protected override void OnContextMenuOpening(ContextMenuEventArgs e)
    {
        base.OnContextMenuOpening(e);

        _ = TextEditorContextMenuPlacementHelper.TryOpenKeyboardInvokedMenu(
            this,
            e,
            GetSelectionStartCharacterRect
        );
    }

    private Rect GetSelectionStartCharacterRect()
    {
        TextPointer position = Selection.Start;

        if (!position.HasValidLayout)
        {
            return Rect.Empty;
        }

        try
        {
            return position.GetCharacterRect(LogicalDirection.Forward);
        }
        catch (InvalidOperationException)
        {
            return Rect.Empty;
        }
    }
}
