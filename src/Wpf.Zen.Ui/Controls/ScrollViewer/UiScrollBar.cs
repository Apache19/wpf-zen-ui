// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// A styled <see cref="ScrollBar"/> that guarantees its own default style via metadata override,
/// ensuring consistent appearance even when the theme manager is not active.
/// </summary>
/// <remarks>
/// This control does not implement any custom functionality or logic.
/// Its sole purpose is to provide a <see cref="ScrollBar"/> derivative that explicitly overrides
/// <see cref="FrameworkElement.DefaultStyleKeyProperty"/> to point to its own type.
/// This ensures that the control always uses the default style defined for
/// <see cref="UiScrollBar"/> in the resource dictionary, rather than falling back to the
/// operating system's native <see cref="ScrollBar"/> style.
/// This is particularly important when used within <see cref="DynamicScrollViewer"/> to maintain
/// a consistent visual style, regardless of whether the theme manager has been initialized.
/// </remarks>
public class UiScrollBar : ScrollBar
{
    static UiScrollBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(UiScrollBar),
            new FrameworkPropertyMetadata(typeof(UiScrollBar))
        );
    }
}