// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Decorates the footer region in the default <see cref="ContentDialog"/> template.
/// </summary>
/// <remarks>
/// <para>
/// The default theme style supplies a background, a one-DIP top border and Padding of 24 DIPs
/// horizontally and 20 DIPs vertically. Application styles can override individual properties
/// while retaining the remaining theme defaults, without specifying Style.BasedOn.
/// Place an implicit style in ContentDialog.Resources to customize a single dialog, or in
/// application resources to customize all dialog footers.
/// </para>
/// <para>
/// Initialization supplies a dynamic Style resource reference keyed by the runtime type when
/// no explicit style is present. Lookup reaches resources across the template boundary and
/// follows resource replacements. Explicit styles, including an explicit null, are retained.
/// Background, border and padding values are supplied by styles.
/// </para>
/// <para>
/// This decorator hosts the footer ContentPresenter. ContentDialog retains logical ownership
/// of the Footer content and controls whether the region is visible. The footer has square
/// corners by default; the dialog shell clips the region to its outer shape.
/// </para>
/// </remarks>
public class ContentDialogFooterContainer : Border
{
    static ContentDialogFooterContainer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialogFooterContainer),
            new FrameworkPropertyMetadata(typeof(ContentDialogFooterContainer))
        );
    }

    /// <summary>
    /// Enables type-keyed style lookup across template boundaries when the container has no
    /// explicit style. Custom templates receive the same resource lookup as the default template.
    /// </summary>
    /// <param name="e">The initialization event arguments.</param>
    protected override void OnInitialized(EventArgs e)
    {
        // Border is not a Control, so WPF's implicit style lookup stops at TemplatedParent.
        // A resource reference uses ordinary resource lookup and tracks resource changes.
        // Inspect the effective source to preserve both local and parent-template styles.
        var source = DependencyPropertyHelper.GetValueSource(this, StyleProperty).BaseValueSource;
        if (source == BaseValueSource.Default || source == BaseValueSource.ImplicitStyleReference)
        {
            SetResourceReference(StyleProperty, GetType());
        }

        base.OnInitialized(e);
    }
}
