// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// 为 ActualWindowStateChanged 事件提供数据。
/// </summary>
public class WindowStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the new window state.
    /// </summary>
    public WindowState NewState { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="WindowStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="newState">The new window state.</param>
    public WindowStateChangedEventArgs(WindowState newState)
    {
        NewState = newState;
    }
}

