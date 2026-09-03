// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Runtime.CompilerServices;

namespace Wpf.Zen.Ui.Gallery;

/// <summary>
/// Configures WPF compatibility before the gallery creates its application and loads theme resources.
/// </summary>
internal static class Program
{
    /// <summary>
    /// Starts the gallery with selection backgrounds rendered beneath text.
    /// </summary>
    [STAThread]
    public static void Main()
    {
        WpfCompatibility.EnableModernTextSelectionRendering();
        RunApplication();
    }

    // Keep App references behind a non-inlined call: its static host initialization must not be
    // pulled ahead of compatibility configuration when the entry point is compiled or executed.
    // App.xaml loads theme resources, so the Startup event is too late for this configuration.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RunApplication()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
