// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;

namespace Wpf.Ui.UnitTests;

internal static class WpfTestEnvironment
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        // WPF's font cache constructs the Windows Fonts URI directly from the process-level
        // windir variable. Some isolated test hosts omit it even though SystemRoot is present.
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("windir")))
        {
            return;
        }

        var windowsDirectory = Environment.GetEnvironmentVariable("SystemRoot");
        if (string.IsNullOrWhiteSpace(windowsDirectory))
        {
            windowsDirectory = Environment.GetEnvironmentVariable(
                "windir",
                EnvironmentVariableTarget.Machine
            );
        }

        if (!string.IsNullOrWhiteSpace(windowsDirectory))
        {
            Environment.SetEnvironmentVariable("windir", windowsDirectory);
        }
    }
}
