// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Overrides individual standard button labels for one MessageBox.Show call.
/// </summary>
/// <remarks>
/// Null or empty values use built-in localized text. An underscore marks an access key;
/// use two underscores for a literal underscore. Labels do not change the returned WPF result.
/// </remarks>
public sealed class MessageBoxButtonText
{
    /// <summary>
    /// Gets or sets the OK button label. The default is localized automatically.
    /// </summary>
    public string? OK { get; set; }

    /// <summary>
    /// Gets or sets the Cancel button label. The default is localized automatically.
    /// </summary>
    public string? Cancel { get; set; }

    /// <summary>
    /// Gets or sets the Yes button label. The default is localized automatically.
    /// </summary>
    public string? Yes { get; set; }

    /// <summary>
    /// Gets or sets the No button label. The default is localized automatically.
    /// </summary>
    public string? No { get; set; }
}
