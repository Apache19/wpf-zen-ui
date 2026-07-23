// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;

// ReSharper disable once CheckNamespace

namespace Wpf.Ui.Controls;

/// <summary>
/// Extends the <see cref="System.Windows.Controls.RichTextBox"/> control with additional properties.
/// </summary>
public class RichTextBox : System.Windows.Controls.RichTextBox
{
    private readonly TextEditorMouseInputHelper _mouseInputHelper;

    /// <summary>Identifies the <see cref="UseTextBlockAppearance"/> dependency property.</summary>
    public static readonly DependencyProperty UseTextBlockAppearanceProperty = DependencyProperty.Register(
        nameof(UseTextBlockAppearance),
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
    /// Initializes a new instance of the <see cref="RichTextBox"/> class.
    /// </summary>
    public RichTextBox()
    {
        _mouseInputHelper = new TextEditorMouseInputHelper(this);

        AddHandler(
            Mouse.QueryCursorEvent,
            new QueryCursorEventHandler(OnRichTextBoxQueryCursor),
            handledEventsToo: true
        );
    }

    /// <summary>
    /// Gets or sets a value indicating whether the control should use a plain selectable text appearance.
    /// </summary>
    public bool UseTextBlockAppearance
    {
        get => (bool)GetValue(UseTextBlockAppearanceProperty);
        set => SetValue(UseTextBlockAppearanceProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _mouseInputHelper.OnApplyTemplate(GetTemplateChild("PART_ContentHost") as ScrollViewer);
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        // TextBoxBase must establish focus and finish its native mouse-down work before the
        // padding bridge starts selection. In particular, doing this from PreviewMouseDown would
        // expose a non-native focus/event order to parent controls.
        base.OnMouseDown(e);

        if (
            e.ChangedButton is MouseButton.Left
            && _mouseInputHelper.TryForwardMouseDownFromContentHostPadding(e)
        )
        {
            e.Handled = true;
        }
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

    private void OnRichTextBoxQueryCursor(object sender, QueryCursorEventArgs e)
    {
        _mouseInputHelper.OnQueryCursor(e);
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
