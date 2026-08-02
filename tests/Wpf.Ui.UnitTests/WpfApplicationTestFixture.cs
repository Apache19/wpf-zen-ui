// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

[assembly: Xunit.AssemblyFixture(typeof(Wpf.Ui.UnitTests.WpfApplicationTestFixture))]

namespace Wpf.Ui.UnitTests;

/// <summary>
/// Keeps the process-wide WPF <see cref="Application"/> alive until every test in this assembly has finished.
/// </summary>
/// <remarks>
/// WPF permits only one <see cref="Application"/> per AppDomain. Shutting it down from a collection fixture
/// permanently invalidates pack-resource state for whichever collections happen to run afterward. Assembly
/// lifetime is therefore the smallest safe ownership boundary. The separate <c>Wpf.Ui.WinFormsHostTests</c>
/// processes exercise ThemeManager's applicationless SystemResources backend with real ElementHost presentation
/// sources; keeping those incompatible global-state scenarios in separate processes is part of the test contract.
/// </remarks>
public sealed class WpfApplicationTestFixture : IDisposable
{
    /// <summary>
    /// Creates the application before any test class can claim its dispatcher from an arbitrary worker thread.
    /// </summary>
    public WpfApplicationTestFixture()
    {
        WpfApplicationTestHost.EnsureApplication();
    }

    /// <summary>
    /// Releases WPF process resources only after the complete test assembly has finished.
    /// </summary>
    public void Dispose()
    {
        WpfApplicationTestHost.Shutdown();
    }
}
