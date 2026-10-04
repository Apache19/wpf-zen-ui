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
/// The icon slot exposed by a control remains ordinary WPF content, so applications
/// may still supply any visual or data object. Deriving reusable icon descriptions from this type
/// gives icon families a common semantic boundary without forcing them into one rendering model.
/// </para>
/// <para>
/// A concrete source should be immutable, or otherwise safe to share. Font-based families derive
/// from <see cref="FontIconSource"/> and share the library's implicit FontIcon template. Other
/// rendering models supply an implicit <see cref="DataTemplate"/> that creates a visual per host;
/// those visuals may consume <see cref="IconPresenter.VariantProperty"/> when appropriate.
/// </para>
/// </remarks>
public abstract class IconSource
{
    /// <summary>
    /// Initializes a presentation-independent icon description.
    /// </summary>
    protected IconSource()
    {
    }
}
