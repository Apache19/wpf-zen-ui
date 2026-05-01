// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

// todo: 修复无响应问题
// todo: 增加双击命令，让视图模型容易绑定

public class ListBox : System.Windows.Controls.ListBox
{
    static ListBox()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(ListBox),
            new FrameworkPropertyMetadata(typeof(ListBox))
        );
    }
}