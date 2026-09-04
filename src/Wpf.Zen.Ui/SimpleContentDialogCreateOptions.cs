// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>Defines the content and optional semantic actions of a simple dialog.</summary>
public class SimpleContentDialogCreateOptions
{
    /// <summary>Gets or sets the dialog heading.</summary>
    public required string Title { get; set; }

    /// <summary>Gets or sets the message or content model.</summary>
    public required object Content { get; set; }

    /// <summary>Gets or sets the primary action; null omits it.</summary>
    public ContentDialogAction? PrimaryAction { get; set; }

    /// <summary>Gets or sets the secondary action; null omits it.</summary>
    public ContentDialogAction? SecondaryAction { get; set; }

    /// <summary>Gets or sets the third action; null omits it.</summary>
    public ContentDialogAction? TertiaryAction { get; set; }

    /// <summary>Gets or sets the explicit Enter action. Default None avoids accidental acceptance.</summary>
    public ContentDialogButton DefaultButton { get; set; } = ContentDialogButton.None;
}
