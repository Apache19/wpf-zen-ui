// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Theming;

/// <summary>
/// Provides assembly-scoped keys for reusable styles published by WPF Zen UI.
/// </summary>
/// <remarks>
/// XAML consumers should normally use <c>DynamicStyle</c> or <c>StaticStyle</c>. Each property is initialized
/// explicitly so adding a key does not depend on reflection or a writable static property.
/// </remarks>
public static class StyleKeys
{
    /// <summary>
    /// Gets the reusable style for the presenter hosted by the global keyboard-focus visual.
    /// </summary>
    /// <remarks>
    /// Use this key as <see cref="Style.BasedOn"/> for a <see cref="Wpf.Zen.Ui.Controls.FocusVisual.PresenterStyleProperty"/> value when
    /// the default rounded-rectangle template is suitable and only
    /// <see cref="Wpf.Zen.Ui.Controls.FocusVisualPresenter"/> properties such as margin need to vary. Replace
    /// <see cref="System.Windows.Controls.Control.Template"/> in the derived style when the focus outline must use a
    /// different shape.
    /// </remarks>
    public static ComponentResourceKey DefaultFocusVisualPresenterStyle { get; } = Create(
        nameof(DefaultFocusVisualPresenterStyle)
    );

    public static ComponentResourceKey UiButtonStyle                   { get; } = Create(nameof(UiButtonStyle));
    public static ComponentResourceKey UiComboBoxStyle                 { get; } = Create(nameof(UiComboBoxStyle));
    public static ComponentResourceKey DefaultUiTextBoxStyle           { get; } = Create(nameof(DefaultUiTextBoxStyle));
    public static ComponentResourceKey UiRichTextBoxStyle              { get; } = Create(nameof(UiRichTextBoxStyle));
    public static ComponentResourceKey UiDynamicScrollViewerStyle      { get; } = Create(nameof(UiDynamicScrollViewerStyle));
    public static ComponentResourceKey IconInfoBadgeStyle              { get; } = Create(nameof(IconInfoBadgeStyle));
    public static ComponentResourceKey ValueInfoBadgeStyle             { get; } = Create(nameof(ValueInfoBadgeStyle));
    public static ComponentResourceKey DotInfoBadgeStyle               { get; } = Create(nameof(DotInfoBadgeStyle));
    public static ComponentResourceKey UiSplitButtonStyle              { get; } = Create(nameof(UiSplitButtonStyle));
    public static ComponentResourceKey UiAutoSuggestBoxStyle           { get; } = Create(nameof(UiAutoSuggestBoxStyle));
    public static ComponentResourceKey UiBadgeStyle                    { get; } = Create(nameof(UiBadgeStyle));
    public static ComponentResourceKey UiBreadcrumbBarStyle            { get; } = Create(nameof(UiBreadcrumbBarStyle));
    public static ComponentResourceKey UiBreadcrumbBarItemStyle        { get; } = Create(nameof(UiBreadcrumbBarItemStyle));
    public static ComponentResourceKey UiSeparatorStyle                { get; } = Create(nameof(UiSeparatorStyle));

    /// <summary>Gets the reusable Fluent style for the embedded <see cref="Controls.SoftKeyboard"/>.</summary>
    public static ComponentResourceKey UiSoftKeyboardStyle             { get; } = Create(nameof(UiSoftKeyboardStyle));

    /// <summary>
    /// Gets the theme-routing key for the ListView style used while a WPF Zen UI GridView is active.
    /// </summary>
    public static ComponentResourceKey GridViewListViewStyle           { get; } = Create(nameof(GridViewListViewStyle));

    /// <summary>
    /// Gets the theme-routing key for item containers generated while a WPF Zen UI GridView is active.
    /// </summary>
    public static ComponentResourceKey GridViewItemContainerStyle      { get; } = Create(nameof(GridViewItemContainerStyle));

    /// <summary>
    /// Gets the reusable Fluent column-header style key for a WPF Zen UI GridView.
    /// </summary>
    /// <remarks>
    /// Use <c>StaticStyle GridViewColumnHeader</c> as a <see cref="Style.BasedOn"/> value when a
    /// custom header should preserve the default template, resize gripper, and role-specific behavior.
    /// </remarks>
    public static ComponentResourceKey GridViewColumnHeaderStyle       { get; } = Create(nameof(GridViewColumnHeaderStyle));

    /// <summary>
    /// Gets the reusable, self-contained style key for the inner <see cref="Controls.UiScrollViewer"/>
    /// inside a WPF Zen UI GridView template. The style bundles <c>GridViewScrollViewerTemplate</c>, intrinsic
    /// constants (<c>Focusable</c>, <c>HorizontalScrollBarVisibilityThreshold</c>, selection-indicator
    /// gutter), and templated-parent routing (<c>Padding</c>, scroll-bar visibility, <c>CanContentScroll</c>,
    /// <c>ScrollSpillMode</c>, column definitions) so that a custom <c>GridViewTemplate</c> only needs to
    /// set this one <c>Style</c> without any local property duplication.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Reference with <c>{m:StaticStyle GridViewScrollViewer}</c>. Apply on a
    /// <see cref="Controls.UiScrollViewer"/> placed inside a <c>GridViewTemplate</c>. The style is
    /// self-contained: no additional attributes are required on the control instance.
    /// </para>
    /// <para>
    /// <b>This style is scoped exclusively to the GridView template's inner scroll viewer.</b>
    /// Its <c>TemplatedParent</c> bindings resolve against the hosting <see cref="System.Windows.Controls.ListView"/>;
    /// using it on a stand-alone <see cref="Controls.UiScrollViewer"/> or inside any other template
    /// will produce silent binding failures and is not supported. Prefer <c>DefaultScrollBarStyle</c> or a
    /// local style for general-purpose scroll-viewer customization.
    /// </para>
    /// </remarks>
    public static ComponentResourceKey GridViewScrollViewerStyle       { get; } = Create(nameof(GridViewScrollViewerStyle));

    private static ComponentResourceKey Create(object resourceId)
    {
        return new ComponentResourceKey(typeof(ThemeResourceAnchor), resourceId);
    }
}
