// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui.Gallery.Models;

public record WindowCard
{
    public string Name { get; set; }

    public string Description { get; init; }

    public FluentSystemIcon Icon { get; init; }

    public string Value { get; set; }

    public WindowCard(string name, string description, SymbolRegular icon, string value)
    {
        Name = name;
        Description = description;
        Icon = new FluentSystemIcon(icon);
        Value = value;
    }
}
