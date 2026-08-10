// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui;

namespace Wpf.Zen.Ui.DependencyInjection;

/// <summary>
/// Resolves pages from the dependency-injection scope owned by one navigation host.
/// </summary>
/// <remarks>
/// The provider is registered as scoped by <see cref="ServiceCollectionExtensions.AddNavigation"/>.
/// It must never capture the application's root provider because that would let UIElement and
/// scoped view-model instances cross window or ElementHost boundaries.
/// </remarks>
public class DependencyInjectionNavigationViewPageProvider(IServiceProvider serviceProvider)
    : INavigationViewPageProvider
{
    /// <inheritdoc />
    public object? GetPage(Type pageType)
    {
        return serviceProvider.GetService(pageType);
    }
}
