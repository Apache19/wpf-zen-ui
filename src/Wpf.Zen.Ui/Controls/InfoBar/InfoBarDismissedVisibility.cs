// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Specifies how an <see cref="InfoBar"/> participates in layout after it is dismissed.
/// </summary>
/// <remarks>
/// This type deliberately excludes a visible state because it describes only the two valid
/// dismissal policies. The dedicated type also keeps XAML editors and property inspectors from
/// offering <see cref="System.Windows.Visibility.Visible"/> for
/// <see cref="InfoBar.DismissedVisibility"/>.
/// </remarks>
public enum InfoBarDismissedVisibility
{
    /// <summary>
    /// The control is not rendered and does not reserve space in layout.
    /// </summary>
    Collapsed,

    /// <summary>
    /// The control is not rendered, but its layout slot, including margin, is retained.
    /// </summary>
    Hidden,
}
