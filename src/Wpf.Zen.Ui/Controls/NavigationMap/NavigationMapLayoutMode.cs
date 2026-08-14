// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Specifies how a <see cref="NavigationMapView"/> places its nodes.</summary>
public enum NavigationMapLayoutMode
{
    /// <summary>
    /// Places every hierarchy depth on one logical level and reserves a continuous span for each
    /// subtree. This produces the clearest organization chart for narrow, deep maps.
    /// </summary>
    Hierarchical,

    /// <summary>
    /// Uses positions from <see cref="NavigationMapLayout"/> when available and the standard
    /// hierarchical organization chart for nodes that do not yet have a saved position. Set
    /// <see cref="NavigationMapView.IsEditing"/> to let users update those positions by dragging
    /// nodes. This fallback deliberately preserves ancestry while an empty or partial layout is
    /// being designed.
    /// </summary>
    Manual,
}