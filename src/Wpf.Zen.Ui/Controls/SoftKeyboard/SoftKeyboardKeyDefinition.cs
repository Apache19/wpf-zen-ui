// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Markup;

namespace Wpf.Zen.Ui.Controls;

/// <summary>Declares a key action, presentation and long-press variants without constructing a WPF visual.</summary>
/// <remarks>Use presentation data or an icon source as Content, so templates can create independent visuals.</remarks>
[ContentProperty(nameof(Alternates))]
public sealed class SoftKeyboardKeyDefinition
{
    /// <summary>Gets or sets the semantic action. Defaults to a Unicode text key.</summary>
    public SoftKeyboardKeyKind Kind { get; set; } = SoftKeyboardKeyKind.Text;

    /// <summary>Gets or sets the cap content; null uses Text or the virtual-key name.</summary>
    public object? Content { get; set; }

    /// <summary>Gets or sets the accessible action name; null uses Text or the virtual-key name.</summary>
    public string? AutomationName { get; set; }

    /// <summary>Gets or sets the virtual key sent by a VirtualKey action. Defaults to None.</summary>
    public Key Key { get; set; }

    /// <summary>Gets or sets Unicode text for a Text action. Defaults to null.</summary>
    public string? Text { get; set; }

    /// <summary>Gets or sets the destination for ChangeDisplayMode. Defaults to Letters.</summary>
    public SoftKeyboardDisplayMode DisplayMode { get; set; } = SoftKeyboardDisplayMode.Letters;

    /// <summary>Gets or sets the positive column span. Defaults to two, one ordinary key.</summary>
    public int Span { get; set; } = 2;

    /// <summary>Gets Unicode alternatives displayed above the cap on long press.</summary>
    public Collection<string> Alternates { get; } = [];
}
