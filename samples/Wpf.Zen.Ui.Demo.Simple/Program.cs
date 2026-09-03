// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System;
using System.Runtime.CompilerServices;

namespace Wpf.Zen.Ui.Demo.Simple;

/// <summary>
/// Configures WPF compatibility before the application loads its theme resources and startup window.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the example with selection backgrounds rendered beneath text.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        WpfCompatibility.EnableModernTextSelectionRendering();
        RunApplication();
    }

    // Keep application initialization after compatibility configuration even when the JIT inlines
    // startup code. App.xaml loads theme resources before WPF raises the Startup event.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApplication()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
