// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace

namespace Wpf.Ui.Controls;

public enum WindowBackdropType
{
    /// <summary>
    /// No backdrop effect.
    /// </summary>
    None,

    /// <summary>
    /// 自动设置，
    /// </summary>
    Auto,

    /// <summary>
    /// Windows Acrylic effect.
    /// </summary>
    Acrylic,

    /// <summary>
    /// Windows 11 云母效果，Windows 10 没有效果
    /// </summary>
    Mica,

    /// <summary>
    /// Windows 11 增强的云母效果，Windows 10 没有效果
    /// </summary>
    Mica_Alt,
}