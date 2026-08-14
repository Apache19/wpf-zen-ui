// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Specifies how selected navigation-map nodes are aligned to the primary selection.</summary>
public enum NavigationMapAlignment
{
    /// <summary>Aligns the left edges of every selected node.</summary>
    Left,

    /// <summary>Aligns the horizontal centers of every selected node.</summary>
    HorizontalCenter,

    /// <summary>Aligns the right edges of every selected node.</summary>
    Right,

    /// <summary>Aligns the top edges of every selected node.</summary>
    Top,

    /// <summary>Aligns the vertical centers of every selected node.</summary>
    VerticalCenter,

    /// <summary>Aligns the bottom edges of every selected node.</summary>
    Bottom,
}