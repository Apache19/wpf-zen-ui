// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>
/// Defers immutable host binding until a runtime navigation scope has selected its host control.
/// </summary>
internal sealed class ScopedNavigationService : NavigationService
{
    /// <summary>Binds this scoped service exactly once before pages can resolve it.</summary>
    internal void Attach(INavigationHost host) => AttachHost(host);
}
