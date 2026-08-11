// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;

namespace Wpf.Zen.Ui;

/// <summary>Exposes read-only semantic state for exactly one navigation surface.</summary>
/// <remarks>
/// Breadcrumb controls can bind to <see cref="Breadcrumbs"/> without being owned by a
/// <c>NavigationView</c>. Navigation actions remain on <see cref="INavigationService"/>, preserving
/// a one-way dependency from presentation to scoped navigation state.
/// </remarks>
public interface INavigationState
{
    /// <summary>Gets the navigation map currently used to resolve default semantic paths.</summary>
    NavigationMap NavigationMap { get; }

    /// <summary>Gets the current resolved location, or <see langword="null"/> before navigation.</summary>
    NavigationLocation? CurrentLocation { get; }

    /// <summary>Gets the stable observable collection projected from <see cref="CurrentLocation"/>.</summary>
    ReadOnlyObservableCollection<BreadcrumbSegment> Breadcrumbs { get; }

    /// <summary>Occurs after the current location or its resolved path changes.</summary>
    event EventHandler? CurrentLocationChanged;
}
