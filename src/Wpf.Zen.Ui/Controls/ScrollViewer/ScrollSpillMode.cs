// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines how <see cref="PassiveScrollViewer"/> handles mouse wheel events that cannot scroll its own content.
/// </summary>
public enum ScrollSpillMode
{
    /// <summary>
    /// Mouse wheel events spill to ancestor controls when the content cannot scroll further.
    /// </summary>
    Enabled,

    /// <summary>
    /// Mouse wheel events are handled when the content cannot scroll further.
    /// </summary>
    Disabled,

    /// <summary>
    /// Mouse wheel events are handled by the native <see cref="System.Windows.Controls.ScrollViewer"/> behavior.
    /// </summary>
    Native,
}
