// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Wpf.Ui.Controls;

/// <summary>
/// Restores native text-editor mouse behavior for clicks that land in <see cref="TextBoxBase"/>
/// padding inside <c>PART_ContentHost</c>.
/// </summary>
internal sealed class TextEditorMouseInputHelper
{
    // WPF creates an internal render scope and stores it in TextBoxBase.RenderScope. The scope is
    // the real text layout/input surface, but the property is internal, so reflection is the only
    // practical way for derived controls to find it without re-templating TextBoxBase internals.
    private static readonly PropertyInfo? RenderScopePropertyInfo = typeof(TextBoxBase).GetProperty(
        "RenderScope",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    // TextEditor owns the native caret, selection, drag-selection, auto-scroll and cursor behavior
    // for TextBoxBase. The public API exposes pieces of that behavior, but not the mouse pipeline
    // entry points we need for padding-area input forwarding.
    private static readonly Type? TextEditorType = typeof(TextBoxBase).Assembly.GetType(
        "System.Windows.Documents.TextEditor"
    );

    // TextEditorMouse contains the internal implementation that translates mouse points into caret
    // movement, selection changes and IBeam/Arrow cursor selection.
    private static readonly Type? TextEditorMouseType = typeof(TextBoxBase).Assembly.GetType(
        "System.Windows.Documents.TextEditorMouse"
    );

    // TextEditor._GetTextEditor(TextBoxBase) returns the internal editor instance attached to this
    // TextBoxBase. All reflected calls below operate on that instance.
    private static readonly MethodInfo? GetTextEditorMethod = TextEditorType?.GetMethod(
        "_GetTextEditor",
        BindingFlags.Static | BindingFlags.NonPublic
    );

    // Native mouse-down caret placement. It accepts a point in render-scope coordinates, so we can
    // clamp a padding click to the nearest render-scope edge and let WPF choose the insertion
    // position exactly as it would for a real render-scope click.
    private static readonly MethodInfo? SetCaretPositionOnMouseEventMethod = TextEditorMouseType?.GetMethod(
        "SetCaretPositionOnMouseEvent",
        BindingFlags.Static | BindingFlags.NonPublic
    );

    // Native QueryCursor handler. We call it for points inside the render scope so WPF can still
    // choose Arrow over selected text, IBeam over editable text, resize cursors, etc.
    private static readonly MethodInfo? TextEditorMouseOnQueryCursorMethod = TextEditorMouseType?.GetMethod(
        "OnQueryCursor",
        BindingFlags.Static | BindingFlags.NonPublic
    );

    // TextEditorMouse sets this flag while it captures the mouse. CaptureMouse raises a synthetic
    // mouse move, and the flag prevents that move from being processed too early. We mirror that
    // small native guard when starting a selection from padding.
    private static readonly FieldInfo? MouseCapturingInProgressField = TextEditorType?.GetField(
        "_mouseCapturingInProgress",
        BindingFlags.Instance | BindingFlags.NonPublic
    );

    private readonly TextBoxBase _owner;

    // Cached template internals. They are reset whenever the template is reapplied because
    // TextBoxBase may create a new render scope and PART_ContentHost instance.
    private FrameworkElement? _renderScope;
    private ScrollViewer? _contentHost;

    public TextEditorMouseInputHelper(TextBoxBase owner)
    {
        _owner = owner;
    }

    public void OnApplyTemplate(ScrollViewer? contentHost)
    {
        _renderScope = null;
        _contentHost = contentHost;
    }

    public void OnQueryCursor(QueryCursorEventArgs e)
    {
        FrameworkElement? renderScope = GetRenderScope();
        if (
            renderScope is null
            || _contentHost is null
            || !IsPointWithinElementBounds(e.GetPosition(_contentHost), _contentHost)
        )
        {
            return;
        }

        // Inside the render scope, use WPF's normal QueryCursor path. This preserves subtle native
        // behavior such as Arrow over selected text and IBeam over editable text.
        if (IsPointWithinElementBounds(e.GetPosition(renderScope), renderScope))
        {
            ForwardQueryCursorToTextEditor(e);
            return;
        }

        // We are inside PART_ContentHost but outside the render scope, which means the mouse is
        // over the text editor padding area. Present it as editable text space.
        e.Cursor = Cursors.IBeam;
        e.Handled = true;
    }

    public bool TryForwardMouseDownFromContentHostPadding(MouseButtonEventArgs e)
    {
        FrameworkElement? renderScope = GetRenderScope();
        object? textEditor = GetTextEditor();

        // Only bridge the exact gap caused by TextBoxBase padding. If any internal member cannot
        // be reflected, or the mouse is not in the content-host padding band, fall back to normal
        // TextBoxBase behavior.
        if (
            renderScope is null
            || textEditor is null
            || _contentHost is null
            || SetCaretPositionOnMouseEventMethod is null
            || !IsPointWithinContentHostPadding(e, renderScope)
        )
        {
            return false;
        }

        // This bridge is called after TextBoxBase.OnMouseDown. The native editor has therefore
        // already moved keyboard focus, completed any IME composition and rejected the padding hit
        // as non-interactive. Doing those steps here from PreviewMouseDown used to raise focus
        // events before the normal mouse route and could let controls such as AutoSuggestBox open a
        // non-sticky Popup while this click was still being processed.
        if (!_owner.IsKeyboardFocused)
        {
            return false;
        }

        // Ensure the render scope has valid layout before asking it to interpret a point.
        renderScope.UpdateLayout();

        // Convert the owner-space click to render-scope space and clamp it to the scope's bounds.
        // WPF's SetCaretPositionOnMouseEvent expects render-scope coordinates.
        Point mouseDownPoint = GetClampedRenderScopePoint(e.GetPosition(_owner), renderScope);

        // Use the public change block API around the reflected internal operation. This mirrors
        // the native mouse-down path and keeps selection notifications grouped.
        _owner.BeginChange();
        try
        {
            SetCaretPositionOnMouseEventMethod.Invoke(
                null,
                [textEditor, mouseDownPoint, e.ChangedButton, e.ClickCount]
            );

            // Capture the mouse on the TextBoxBase itself, just like native TextEditorMouse does.
            // After this point we do not synthesize move/up events; WPF's normal MouseMove and
            // MouseUp handlers take over, including drag-selection and auto-scroll outside the
            // viewport.
            SetMouseCapturingInProgress(textEditor, true);
            try
            {
                _owner.CaptureMouse();
            }
            finally
            {
                SetMouseCapturingInProgress(textEditor, false);
            }
        }
        finally
        {
            _owner.EndChange();
        }

        return true;
    }

    private bool IsPointWithinContentHostPadding(MouseEventArgs e, FrameworkElement renderScope)
    {
        // First restrict the bridge to PART_ContentHost. This prevents clicks on template chrome,
        // icons, clear buttons or other adjacent elements from becoming text input.
        Point contentHostPoint = e.GetPosition(_contentHost);
        if (!IsPointWithinElementBounds(contentHostPoint, _contentHost!))
        {
            return false;
        }

        // Then require the point to be outside the render scope itself. True render-scope hits
        // should flow through native WPF input without interference.
        Point renderScopePoint = e.GetPosition(renderScope);

        return !IsPointWithinElementBounds(renderScopePoint, renderScope);
    }

    private Point GetClampedRenderScopePoint(Point ownerPoint, FrameworkElement renderScope)
    {
        // TextEditorMouse.SetCaretPositionOnMouseEvent consumes points in the render scope's
        // coordinate space. Transform from the public owner coordinate space into the internal
        // render-scope coordinate space.
        GeneralTransform transform = _owner.TransformToDescendant(renderScope);
        if (transform is null || !transform.TryTransform(ownerPoint, out Point renderScopePoint))
        {
            return default;
        }

        // Padding clicks are outside the render scope by definition. Clamp them to the nearest
        // edge so WPF snaps to the closest valid text insertion position.
        renderScopePoint.X = Math.Max(0, Math.Min(renderScopePoint.X, renderScope.ActualWidth));
        renderScopePoint.Y = Math.Max(0, Math.Min(renderScopePoint.Y, renderScope.ActualHeight));

        return renderScopePoint;
    }

    private FrameworkElement? GetRenderScope()
    {
        // The render scope is stable for a given template instance. Cache it to avoid paying
        // reflection cost on every mouse or cursor event.
        if (_renderScope is not null)
        {
            return _renderScope;
        }

        _renderScope = RenderScopePropertyInfo?.GetValue(_owner) as FrameworkElement;

        return _renderScope;
    }

    private object? GetTextEditor()
    {
        // Do not cache TextEditor separately: it is cheap to resolve and belongs to the current
        // TextBoxBase instance managed by WPF.
        return GetTextEditorMethod?.Invoke(null, [_owner]);
    }

    private void ForwardQueryCursorToTextEditor(QueryCursorEventArgs e)
    {
        // This is intentionally invoked even from a handled QueryCursor route. It lets WPF
        // recompute the correct cursor for real render-scope hits after a template element, such
        // as PART_ContentHost, has supplied a broader Cursor value.
        object? textEditor = GetTextEditor();
        if (textEditor is null || TextEditorMouseOnQueryCursorMethod is null)
        {
            return;
        }

        TextEditorMouseOnQueryCursorMethod.Invoke(null, [_owner, e]);
    }

    private static void SetMouseCapturingInProgress(object textEditor, bool value)
    {
        // Best-effort reflection: if the field is not available on a future WPF version, the
        // bridge still works, but a synthetic capture-time mouse move may not be suppressed.
        MouseCapturingInProgressField?.SetValue(textEditor, value);
    }

    private static bool IsPointWithinElementBounds(Point point, FrameworkElement element)
    {
        // WPF hit testing treats points on the edge as inside for this use case; matching that
        // behavior avoids one-pixel gaps around the render-scope border.
        return point.X >= 0
            && point.Y >= 0
            && point.X <= element.ActualWidth
            && point.Y <= element.ActualHeight;
    }
}
