// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Represents a content control that exposes the orientation implied by its rendered size.
/// </summary>
public class OrientationAwareContentControl : ContentControl
{
    private static readonly DependencyPropertyKey ActualOrientationPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(ActualOrientation),
            typeof(Orientation),
            typeof(OrientationAwareContentControl),
            new PropertyMetadata(Orientation.Horizontal)
        );

    /// <summary>
    /// Identifies the <see cref="ActualOrientation"/> dependency property.
    /// </summary>
    public static readonly DependencyProperty ActualOrientationProperty =
        ActualOrientationPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets the orientation implied by this control's rendered size.
    /// </summary>
    /// <remarks>
    /// The value is <see cref="Orientation.Horizontal"/> when the rendered width is greater
    /// than the rendered height, and <see cref="Orientation.Vertical"/> when the rendered
    /// height is greater than the rendered width. Equal dimensions preserve the current value.
    /// </remarks>
    public Orientation ActualOrientation => (Orientation)GetValue(ActualOrientationProperty);

    /// <inheritdoc />
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);

        UpdateActualOrientation(sizeInfo.NewSize);
    }

    private void UpdateActualOrientation(Size size)
    {
        if (size.Width > size.Height)
        {
            SetActualOrientation(Orientation.Horizontal);
            return;
        }

        if (size.Height > size.Width)
        {
            SetActualOrientation(Orientation.Vertical);
        }
    }

    private void SetActualOrientation(Orientation orientation)
    {
        SetValue(ActualOrientationPropertyKey, orientation);
    }
}
