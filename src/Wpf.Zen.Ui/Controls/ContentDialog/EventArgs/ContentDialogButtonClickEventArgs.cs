// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Describes a footer action before its command executes.</summary>
public sealed class ContentDialogButtonClickEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ContentDialogButtonClickEventArgs"/> class for a dialog notification.</summary>
    public ContentDialogButtonClickEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>Gets the semantic action being invoked.</summary>
    public required ContentDialogButton Button { get; init; }

    /// <summary>Gets or sets a value indicating whether this action is rejected. Default false; unrelated closure is unaffected.</summary>
    public bool Cancel { get; set; }
}
