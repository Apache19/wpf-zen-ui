// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Declares a sequential row whose keys share the layout's logical column grid.</summary>
[ContentProperty(nameof(Keys))]
public sealed class SoftKeyboardRowDefinition
{
    /// <summary>Gets or sets the leading empty columns. Defaults to zero; negative values are rejected when built.</summary>
    public int Offset { get; set; }

    /// <summary>Gets key declarations in reading order. Spans determine each following key's position.</summary>
    public Collection<SoftKeyboardKeyDefinition> Keys { get; } = [];
}
