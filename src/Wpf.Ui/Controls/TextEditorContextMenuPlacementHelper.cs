// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace Wpf.Ui.Controls;

/// <summary>
/// Provides WPF text-editor context menu placement behavior for controls that assign a custom
/// <see cref="ContextMenu"/>.
/// </summary>
/// <remarks>
/// WPF's internal <c>TextEditorContextMenu.OnContextMenuOpening</c> positions the default editor menu
/// at the selection start when the menu is invoked from the keyboard. If a custom menu is present,
/// WPF returns before running that placement logic, and <see cref="ContextMenuService"/> later centers
/// the menu on the control. This helper restores the keyboard placement path for Wpf.Ui editor menus
/// while leaving mouse invocation to the platform.
/// </remarks>
internal static class TextEditorContextMenuPlacementHelper
{
    // ContextMenuEventArgs uses -1 for CursorLeft/CursorTop when PopupControlService raises
    // ContextMenuOpening from the keyboard (Shift+F10 or the Menu key).
    private const double KeyboardInvokedContextMenuSentinel = -1.0;

    // TextBoxBase templates expose the render viewport through PART_ContentHost. Clipping to it
    // mirrors WPF's first clipping pass against TextView.RenderScope.
    private const string ContentHostTemplatePartName = "PART_ContentHost";

    /// <summary>
    /// Opens a custom editor context menu at the caret/selection position when the opening event
    /// was invoked from the keyboard.
    /// </summary>
    /// <param name="owner">
    /// The text editing control that owns the <see cref="ContextMenu"/> and supplies the coordinate space
    /// for placement offsets.
    /// </param>
    /// <param name="e">The <see cref="ContextMenuEventArgs"/> raised by WPF.</param>
    /// <param name="getPositionRect">
    /// Returns the character rectangle to place the menu under. The rectangle must be in
    /// <paramref name="owner"/>'s coordinate space, matching WPF's default editor menu offsets.
    /// Return <see cref="Rect.Empty"/> when layout is unavailable.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the helper opened and handled the menu; otherwise <see langword="false"/>.
    /// </returns>
    public static bool TryOpenKeyboardInvokedMenu(Control owner, ContextMenuEventArgs e, Func<Rect> getPositionRect)
    {
        // --------------------------------------------------------------------------------------------
        // Mouse-opened menus already get MousePoint placement from ContextMenuService. Only intercept
        // keyboard-opened menus because ContextMenuService would otherwise force PlacementMode.Center.
        // --------------------------------------------------------------------------------------------

        // ReSharper disable once CompareOfFloatsByEqualityOperator
        if (e.Handled || e.CursorLeft != KeyboardInvokedContextMenuSentinel || owner.ContextMenu is null)
        {
            return false;
        }

        var contextMenu = owner.ContextMenu;

        // -----------------------------------------------------------------------------------------
        // Open the menu ourselves so ContextMenuService cannot replace our placement with Center.
        // Offsets are calculated relative to the editor control, just like WPF's EditorContextMenu.
        // -----------------------------------------------------------------------------------------

        contextMenu.SetCurrentValue(ContextMenu.PlacementProperty,       PlacementMode.RelativePoint);
        contextMenu.SetCurrentValue(ContextMenu.PlacementTargetProperty, owner);

        GetKeyboardInvokedContextMenuOffsets(
            owner,
            getPositionRect,
            out var horizontalOffset,
            out var verticalOffset
        );

        contextMenu.SetCurrentValue(ContextMenu.HorizontalOffsetProperty, horizontalOffset);
        contextMenu.SetCurrentValue(ContextMenu.VerticalOffsetProperty,   verticalOffset);
        contextMenu.SetCurrentValue(ContextMenu.IsOpenProperty,           true);

        e.Handled = true;

        return true;
    }

    /// <summary>
    /// Calculates x/y offsets for the context menu from a text position rectangle and clips the result
    /// against the same classes of containing viewports as WPF's editor context menu code.
    /// </summary>
    private static void GetKeyboardInvokedContextMenuOffsets(
        Control owner,
        Func<Rect> getPositionRect,
        out double horizontalOffset,
        out double verticalOffset)
    {
        var positionRect = getPositionRect();

        if (positionRect == Rect.Empty)
        {
            var fallbackPoint = Mouse.GetPosition(owner);
            horizontalOffset = fallbackPoint.X;
            verticalOffset   = fallbackPoint.Y;
            return;
        }

        // Place the top-left corner of the menu at the bottom-left edge of the caret/character rect.
        horizontalOffset = positionRect.X;
        verticalOffset   = positionRect.Y + positionRect.Height;

        // WPF clips first to the text render scope, then to parent visuals, then to the window client
        // area. Keep the same order so scrolled, clipped, or mirrored layouts behave predictably.
        ClipToContentHost(owner, ref horizontalOffset, ref verticalOffset);
        ClipToParentVisuals(owner, ref horizontalOffset, ref verticalOffset);
        ClipToWindowClientArea(owner, ref horizontalOffset, ref verticalOffset);
    }

    /// <summary>
    /// Clips the candidate menu position to the text content host viewport.
    /// </summary>
    private static void ClipToContentHost(Control owner, ref double horizontalOffset, ref double verticalOffset)
    {
        if (owner.Template?.FindName(ContentHostTemplatePartName, owner) is not FrameworkElement contentHost)
        {
            return;
        }

        var transform = TryTransformToAncestor(contentHost, owner);

        if (transform is not null)
        {
            ClipToElement(contentHost, transform, ref horizontalOffset, ref verticalOffset);
        }
    }

    /// <summary>
    /// Clips the candidate menu position to each visual ancestor that can constrain the editor viewport.
    /// </summary>
    private static void ClipToParentVisuals(Visual start, ref double horizontalOffset, ref double verticalOffset)
    {
        // This is intentionally broader than just ScrollViewer: any ancestor with a clip, finite
        // ActualWidth/ActualHeight, or mirrored transform can constrain the visible text position.
        for (var visual = start; visual is not null; visual = VisualTreeHelper.GetParent(visual) as Visual)
        {
            if (visual is not FrameworkElement element)
            {
                continue;
            }

            var transform = TryTransformToDescendant(visual, start);

            if (transform is not null)
            {
                ClipToElement(element, transform, ref horizontalOffset, ref verticalOffset);
            }
        }
    }

    /// <summary>
    /// Clips the candidate menu position to the owning window's client visual area.
    /// </summary>
    private static void ClipToWindowClientArea(Control owner, ref double horizontalOffset, ref double verticalOffset)
    {
        var source     = PresentationSource.FromVisual(owner);
        var rootVisual = source?.CompositionTarget?.RootVisual;

        if (rootVisual is not FrameworkElement rootElement)
        {
            return;
        }

        var transform = TryTransformToDescendant(rootVisual, owner);

        if (transform is not null)
        {
            ClipToElement(rootElement, transform, ref horizontalOffset, ref verticalOffset);
        }
    }

    /// <summary>
    /// Returns a transform from <paramref name="visual"/> to <paramref name="ancestor"/>, or
    /// <see langword="null"/> when the visuals are not currently connected.
    /// </summary>
    private static GeneralTransform? TryTransformToAncestor(Visual visual, Visual ancestor)
    {
        try
        {
            return visual.TransformToAncestor(ancestor);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns a transform from <paramref name="visual"/> to <paramref name="descendant"/>, or
    /// <see langword="null"/> when the visuals are not currently connected.
    /// </summary>
    private static GeneralTransform? TryTransformToDescendant(Visual visual, Visual descendant)
    {
        try
        {
            return visual.TransformToDescendant(descendant);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Clips an x/y offset to a containing element after transforming that element's bounds into
    /// the editor control coordinate space.
    /// </summary>
    private static void ClipToElement(
        FrameworkElement element,
        GeneralTransform transform,
        ref double horizontalOffset,
        ref double verticalOffset)
    {
        Point minPoint;
        Point maxPoint;

        var clip = VisualTreeHelper.GetClip(element);

        if (clip is not null)
        {
            // Prefer an explicit clip geometry when present; this matches WPF's handling of clipped
            // viewports and avoids treating the whole layout slot as visible.
            var bounds = clip.Bounds;
            minPoint = new Point(bounds.X,                bounds.Y);
            maxPoint = new Point(bounds.X + bounds.Width, bounds.Y + bounds.Height);
        }
        else
        {
            if (element.ActualWidth == 0 && element.ActualHeight == 0)
            {
                // Some layout containers legitimately report a zero render size. They do not provide
                // a useful clipping rectangle, so skip them rather than collapsing the menu position.
                return;
            }

            minPoint = new Point(0,                   0);
            maxPoint = new Point(element.ActualWidth, element.ActualHeight);
        }

        _ = transform.TryTransform(minPoint, out minPoint);
        _ = transform.TryTransform(maxPoint, out maxPoint);

        horizontalOffset = ClipValueToBounds(minPoint.X, horizontalOffset, maxPoint.X);
        verticalOffset   = ClipValueToBounds(minPoint.Y, verticalOffset,   maxPoint.Y);
    }

    /// <summary>
    /// Clips <paramref name="value"/> to the range <paramref name="min"/> through
    /// <paramref name="max"/> - 1.
    /// </summary>
    private static double ClipValueToBounds(double min, double value, double max)
    {
        // Mirrored transforms, most commonly RTL nested in LTR or vice versa, can invert an axis.
        if (min > max)
        {
            (min, max) = (max, min);
        }

        if (value < min)
        {
            value = min;
        }
        else if (value >= max)
        {
            value = max - 1;
        }

        return value;
    }
}