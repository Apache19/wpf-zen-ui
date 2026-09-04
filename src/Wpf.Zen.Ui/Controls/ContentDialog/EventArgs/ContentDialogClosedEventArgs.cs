// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>Reports the result after the dialog's presentation has been cleaned up.</summary>
public sealed class ContentDialogClosedEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="ContentDialogClosedEventArgs"/> class for a dialog notification.</summary>
    public ContentDialogClosedEventArgs(RoutedEvent routedEvent, object source)
        : base(routedEvent, source) { }

    /// <summary>Gets the selected action, or None for cancellation or dismissal without a choice.</summary>
    public required ContentDialogResult Result { get; init; }
}
