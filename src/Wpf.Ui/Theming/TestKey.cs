// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Collections.Generic;
using System.Text;

namespace Wpf.Ui.Theming;

[TypeConverter(typeof(TestKeyConverter))]
public class TestKey : ComponentResourceKey
{
    private TestKey(string resourceId)
        : base(typeof(TestKey), resourceId)
    {
        ResourceId = resourceId;
    }

    // 预定义静态实例（实验用）
    public static TestKey Alpha { get; } = new TestKey(nameof(Alpha));
    public static TestKey Beta  { get; } = new TestKey(nameof(Beta));
    public static TestKey Gamma { get; } = new TestKey(nameof(Gamma));

    public override string ToString()
    {
        return ResourceId?.ToString() ?? "null";
    }
}