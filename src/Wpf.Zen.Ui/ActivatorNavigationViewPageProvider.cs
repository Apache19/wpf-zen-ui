// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Controls;

namespace Wpf.Zen.Ui;

/// <summary>
/// Creates pages for a container-free navigation scope without requiring dependency injection.
/// </summary>
/// <remarks>
/// The provider uses the same activator as <see cref="NavigationView"/>, including its design-mode fallback,
/// one-argument data-context convention, and validation that destinations derive from
/// <see cref="FrameworkElement"/>. DI scopes use their own scoped activator and do not register this provider.
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
