using System;
using System.Collections.Generic;
using System.Text;

namespace Wpf.Ui.Markup;

#pragma warning disable SA1516 // Elements should be separated by blank line

public static class ThemeStyleKeys
{
    public static ComponentResourceKey DefaultTextBoxScrollViewerStyle { get; } = Create(nameof(DefaultTextBoxScrollViewerStyle));

    public static ComponentResourceKey DefaultControlContextMenu      { get; } = Create(nameof(DefaultControlContextMenu));
    public static ComponentResourceKey DefaultControlFocusVisualStyle { get; } = Create(nameof(DefaultControlFocusVisualStyle));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}

#pragma warning disable SA1516 // Elements should be separated by blank line