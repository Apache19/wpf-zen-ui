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
/// Pages are activated through their public parameterless constructor and destinations must derive from
/// <see cref="FrameworkElement"/>. No design-time placeholder is substituted, so the designer renders the
/// real page or surfaces construction errors itself. <see cref="NavigationView"/> uses this provider as its
/// non-DI fallback; DI scopes use their own scoped activator and do not register this provider.
/// </remarks>
public sealed class ActivatorNavigationViewPageProvider : INavigationViewPageProvider
{
    /// <summary>Gets the shared stateless provider used by navigation scopes that do not specify one.</summary>
    public static ActivatorNavigationViewPageProvider Instance { get; } = new();

    private ActivatorNavigationViewPageProvider() { }

    /// <inheritdoc />
    public object GetPage(Type pageType)
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        if (!typeof(FrameworkElement).IsAssignableFrom(pageType))
        {
            throw new InvalidCastException(
                $"TargetPageType must be derived from {typeof(FrameworkElement)}. {pageType} is not."
            );
        }

        var emptyConstructor =
            pageType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"The {pageType} page does not have a public parameterless constructor. "
                    + "Attach a dependency-injection navigation scope to use constructor injection, "
                    + $"or supply an {typeof(INavigationViewPageProvider)} to {typeof(NavigationScope)}."
            );

        return (FrameworkElement)emptyConstructor.Invoke(null);
    }
}
