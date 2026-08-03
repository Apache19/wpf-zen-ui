// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// This Source Code is partially based on the source code provided by the .NET Foundation.

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Defines values that specify the input validation behavior of a <see cref="NumberBox"/> when invalid input is entered.
/// </summary>
public enum NumberBoxValidationMode
{
    /// <summary>
    /// Invalid input is overwritten by a valid value (e.g. formatted current Value) when validation runs.
    /// </summary>
    InvalidInputOverwritten,

    /// <summary>
    /// Input validation is disabled; invalid text is left as-is (no automatic correction).
    /// </summary>
    Disabled,
}
