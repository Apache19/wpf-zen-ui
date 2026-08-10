// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Creates navigation pages through their parameterless constructors without requiring a dependency-injection
/// container.
/// </summary>
/// <remarks>
/// The provider uses the same activator as <see cref="NavigationView"/>, including its design-mode fallback and
/// validation that destinations derive from <see cref="FrameworkElement"/>. Applications that need constructor
/// injection or an existing-object registry can pass their own <see cref="INavigationViewPageProvider"/> to
/// <see cref="NavigationScope.Attach(INavigationHost,INavigationViewPageProvider?)"/>.
/// </remarks>
public sealed class ActivatorNavigationViewPageProvider : INavigationViewPageProvider
{
    /// <summary>Gets the shared stateless provider used by navigation scopes that do not specify one.</summary>
    public static ActivatorNavigationViewPageProvider Instance { get; } = new();

    private ActivatorNavigationViewPageProvider() { }

    /// <inheritdoc />
    public object? GetPage(Type pageType)
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        return NavigationViewActivator.CreateInstance(pageType);
    }
}
