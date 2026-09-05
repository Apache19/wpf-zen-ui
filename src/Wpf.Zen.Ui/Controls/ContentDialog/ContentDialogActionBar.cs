// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents a collection of application-defined dialog actions using the standard
/// <see cref="ContentDialog"/> footer layout.
/// </summary>
/// <remarks>
/// <para>
/// This control supplies layout only. Each item remains an ordinary WPF element or data item;
/// buttons therefore retain their own command, default/cancel, appearance, automation and result
/// semantics. <see cref="ItemsControl.ItemsSource"/>, <see cref="ItemsControl.ItemTemplate"/>,
/// <see cref="ItemsControl.ItemTemplateSelector"/>, <see cref="ItemsControl.ItemStringFormat"/>,
/// <see cref="ItemsControl.ItemContainerStyle"/> and the corresponding selector are available for
/// data-driven action collections.
/// </para>
/// <para>
/// UI elements placed directly in <see cref="ItemsControl.Items"/> are their own containers, as
/// defined by <see cref="ItemsControl"/>. Non-visual data items receive the framework's generated
/// <see cref="ContentPresenter"/> containers. The default non-virtualizing items panel is an
/// <see cref="AdaptiveUniformGrid"/>: up to <see cref="Columns"/> actions share a row, additional
/// actions continue on following rows, and an incomplete row shares all available width.
/// </para>
/// <para>
/// Place this control in <see cref="ContentDialog.Footer"/> when the footer consists of standard
/// actions. Use an arbitrary footer element, or replace the dialog template, when supplementary
/// content requires a different composition.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;ui:ContentDialogActionBar&gt;
///     &lt;ui:Button Content="Cancel"
///                Command="{x:Static ui:ContentDialog.DismissCommand}" /&gt;
///     &lt;ui:Button Content="Save"
///                Command="{x:Static ui:ContentDialog.CloseCommand}"
///                CommandParameter="{Binding Result}" /&gt;
/// &lt;/ui:ContentDialogActionBar&gt;
/// </code>
/// </example>
[ContentProperty(nameof(Items))]
[DefaultProperty(nameof(Items))]
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(FrameworkElement))]
public class ContentDialogActionBar : ItemsControl
{
    /// <summary>
    /// Identifies the <see cref="Columns"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnsProperty = UniformGrid.ColumnsProperty.AddOwner(
        typeof(ContentDialogActionBar),
        new FrameworkPropertyMetadata(3, FrameworkPropertyMetadataOptions.AffectsMeasure)
    );

    /// <summary>
    /// Gets or sets the maximum number of action slots in each row. The default is 3.
    /// </summary>
    /// <remarks>
    /// Zero enables the inherited <see cref="UniformGrid"/> automatic square-grid calculation.
    /// A positive value fixes the row capacity; extra items continue on subsequent rows. The value
    /// affects layout, does not inherit, and supports discrete animation. Negative values are
    /// rejected by the underlying <see cref="UniformGrid.ColumnsProperty"/> validation.
    /// </remarks>
    public int Columns
    {
        get => (int)GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="RowSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty RowSpacingProperty = AdaptiveUniformGrid.RowSpacingProperty.AddOwner(
        typeof(ContentDialogActionBar),
        new FrameworkPropertyMetadata(8D, FrameworkPropertyMetadataOptions.AffectsMeasure)
    );

    /// <summary>
    /// Gets or sets the distance, in device-independent units, between action rows. The default is 8.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. It affects layout, does not inherit, and can be
    /// animated. The default items panel consumes this value; a replacement
    /// <see cref="ItemsControl.ItemsPanel"/> may choose whether to honor it.
    /// </remarks>
    public double RowSpacing
    {
        get => (double)GetValue(RowSpacingProperty);
        set => SetValue(RowSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="ColumnSpacing"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ColumnSpacingProperty = AdaptiveUniformGrid.ColumnSpacingProperty.AddOwner(
        typeof(ContentDialogActionBar),
        new FrameworkPropertyMetadata(8D, FrameworkPropertyMetadataOptions.AffectsMeasure)
    );

    /// <summary>
    /// Gets or sets the distance, in device-independent units, between adjacent action slots.
    /// The default is 8.
    /// </summary>
    /// <remarks>
    /// The value must be finite and nonnegative. It affects layout, does not inherit, and can be
    /// animated. The default items panel consumes this value; a replacement
    /// <see cref="ItemsControl.ItemsPanel"/> may choose whether to honor it.
    /// </remarks>
    public double ColumnSpacing
    {
        get => (double)GetValue(ColumnSpacingProperty);
        set => SetValue(ColumnSpacingProperty, value);
    }

    /// <summary>
    /// Identifies the <see cref="IsRowStretchEnabled"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty IsRowStretchEnabledProperty =
        AdaptiveUniformGrid.IsRowStretchEnabledProperty.AddOwner(
            typeof(ContentDialogActionBar),
            new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsMeasure)
        );

    /// <summary>
    /// Gets or sets a value indicating whether each populated row divides all available width
    /// among its non-collapsed actions. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// When true, one action fills its row, two actions split it equally, and an incomplete final
    /// row does not retain empty trailing cells. When false, the default panel follows ordinary
    /// <see cref="UniformGrid"/> cell placement. The value affects layout, does not inherit, and
    /// supports discrete animation. A replacement <see cref="ItemsControl.ItemsPanel"/> may choose
    /// whether to honor it.
    /// </remarks>
    public bool IsRowStretchEnabled
    {
        get => (bool)GetValue(IsRowStretchEnabledProperty);
        set => SetValue(IsRowStretchEnabledProperty, value);
    }

    static ContentDialogActionBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ContentDialogActionBar),
            new FrameworkPropertyMetadata(typeof(ContentDialogActionBar))
        );

        // The collection is a layout surface, not an additional focus stop. Its child actions keep
        // their normal focus and tab behavior through ItemsControl's generated or direct containers.
        FocusableProperty.OverrideMetadata(
            typeof(ContentDialogActionBar),
            new FrameworkPropertyMetadata(false)
        );
        IsTabStopProperty.OverrideMetadata(
            typeof(ContentDialogActionBar),
            new FrameworkPropertyMetadata(false)
        );
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentDialogActionBar"/> class.
    /// </summary>
    public ContentDialogActionBar() { }
}
