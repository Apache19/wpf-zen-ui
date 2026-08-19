// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides a presentation-independent description of an icon.
/// </summary>
/// <remarks>
/// <para>
/// Icon slots defined by <see cref="IconAssist.IconProperty"/> remain ordinary WPF content, so applications
/// may still supply any visual or data object. Deriving reusable icon descriptions from this type
/// gives icon families a common semantic boundary without forcing them into one rendering model.
/// </para>
/// <para>
/// A concrete source should be immutable, or otherwise safe to share, and should provide an
/// implicit <see cref="DataTemplate"/> that creates a new visual for every presentation. If the
/// family supports presentation variants, that visual can consume
/// <see cref="IconPresenter.VariantProperty"/>.
/// </para>
/// </remarks>
public abstract class IconSource
{
    /// <summary>Initializes a new instance of the <see cref="IconSource"/> class.</summary>
    protected IconSource() { }
}
