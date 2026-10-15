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
/// Pages use their public parameterless constructor and must derive from
/// <see cref="FrameworkElement"/>. The designer renders the real page or surfaces construction
/// errors. <see cref="NavigationView"/> uses this provider for container-free scopes;
/// DI scopes use their own scoped activator.
/// </remarks>
internal sealed class ActivatorNavigationViewPageProvider : INavigationViewPageProvider
{
    /// <summary>
    /// Gets the shared stateless provider used by navigation scopes that do not specify one.
    /// </summary>
    public static ActivatorNavigationViewPageProvider Instance { get; } = new();

    private ActivatorNavigationViewPageProvider() { }

    /// <inheritdoc />
    public NavigationPageLease GetPage(Type pageType)
    {
        if (pageType is null)
        {
            throw new ArgumentNullException(nameof(pageType));
        }

        // Activation is hand-rolled via reflection instead of Activator.CreateInstance so that a
        // page without a public parameterless constructor produces a domain-specific error pointing
        // at the supported alternatives (DI scope or custom provider) rather than a raw
        // MissingMethodException. The lookup runs once per navigation, not on a hot path, so the
        // metadata needs no caching; on any failure the exception propagates and navigation aborts
        // loudly instead of degrading silently.
        var emptyConstructor =
            pageType.GetConstructor(Type.EmptyTypes)
            ?? throw new InvalidOperationException(
                $"The {pageType} page does not have a public parameterless constructor. "
                    + "Attach a dependency-injection navigation scope to use constructor injection, "
                    + $"or supply an {typeof(INavigationViewPageProvider)} to {typeof(NavigationScope)}."
            );

        var page = (FrameworkElement)emptyConstructor.Invoke(null);
        return NavigationPageLease.Owned(page);
    }
}
