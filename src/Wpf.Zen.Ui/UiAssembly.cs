// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

namespace Wpf.Zen.Ui;

/// <summary>
/// Allows to get the WPF Zen UI assembly through <see cref="Assembly"/>.
/// </summary>
public static class UiAssembly
{
    /// <summary>
    /// Gets the WPF Zen UI assembly.
    /// </summary>
    public static Assembly Assembly => Assembly.GetExecutingAssembly();
}
