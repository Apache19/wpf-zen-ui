// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls.Primitives;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

/// <summary>
/// Provides the expand/collapse interaction used by the left navigation item template.
/// </summary>
public sealed class NavigationViewItemExpander : ToggleButton
{
    /// <inheritdoc />
    protected override void OnClick()
    {
        base.OnClick();
        NavigationViewItemTemplateBehavior.OnItemExpanderClick(this);
    }
}
