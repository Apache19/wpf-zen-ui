// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// 为 ActualWindowStateChanged 事件提供数据。
/// </summary>
public class WindowStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// 获取新的窗口状态。
    /// </summary>
    public WindowState NewState { get; }

    /// <summary>
    /// 初始化 <see cref="WindowStateChangedEventArgs"/> 类的新实例。
    /// </summary>
    /// <param name="newState">新的窗口状态。</param>
    public WindowStateChangedEventArgs(WindowState newState)
    {
        NewState = newState;
    }
}

