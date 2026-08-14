// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Markup;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Stores presentation-only positions for the Manual layout of a <see cref="NavigationMapView"/>.
/// </summary>
/// <remarks>
/// Layout is intentionally separate from <see cref="NavigationMapNode"/>. Navigation nodes describe
/// route identity and ancestry and may be projected into several views, while coordinates belong to
/// one particular presentation, viewport, or saved user workspace. Missing placements are valid and
/// receive deterministic hierarchical positions until the user moves them.
/// </remarks>
[ContentProperty(nameof(Placements))]
public sealed class NavigationMapLayout : INotifyPropertyChanged
{
    private readonly ObservableCollection<NavigationMapPlacement> _placements = [];
    private readonly HashSet<NavigationMapPlacement> _subscribedPlacements = [];

    /// <summary>Initializes a new instance of the <see cref="NavigationMapLayout"/> class.</summary>
    public NavigationMapLayout()
    {
        _placements.CollectionChanged += OnPlacementsChanged;
    }

    /// <summary>Gets the mutable collection of saved node placements.</summary>
    /// <remarks>
    /// When duplicate keys are supplied, lookup deterministically uses the first placement. Prefer
    /// <see cref="SetPosition"/>, which updates an existing entry and never creates a duplicate.
    /// Collection and item changes immediately invalidate every panel using this layout.
    /// </remarks>
    public ObservableCollection<NavigationMapPlacement> Placements => _placements;

    /// <summary>Finds the first placement associated with <paramref name="nodeKey"/>.</summary>
    /// <param name="nodeKey">The stable navigation-map node key.</param>
    /// <param name="placement">Receives the placement when it exists.</param>
    /// <returns><see langword="true"/> when a matching placement was found.</returns>
    public bool TryGetPlacement(string nodeKey, out NavigationMapPlacement? placement)
    {
        NavigationMapPlacement? match = FindPlacement(nodeKey);
        placement = match;
        return match is not null;
    }

    private NavigationMapPlacement? FindPlacement(string nodeKey) =>
        _placements.FirstOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.NodeKey, nodeKey)
        );

    /// <summary>Creates or updates a pinned position for one navigation-map node.</summary>
    /// <param name="nodeKey">The stable navigation-map node key.</param>
    /// <param name="position">The desired node center in map coordinates.</param>
    /// <returns>The created or updated placement.</returns>
    public NavigationMapPlacement SetPosition(string nodeKey, Point position)
    {
        NavigationMapPlacement? placement = FindPlacement(nodeKey);
        if (placement is not null)
        {
            placement.Position = position;
            placement.IsPinned = true;
            return placement;
        }

        placement = new NavigationMapPlacement(nodeKey, position);
        _placements.Add(placement);
        return placement;
    }

    /// <summary>Removes the first saved placement for one node.</summary>
    /// <param name="nodeKey">The stable navigation-map node key.</param>
    /// <returns><see langword="true"/> when a placement was removed.</returns>
    public bool Remove(string nodeKey)
    {
        NavigationMapPlacement? placement = FindPlacement(nodeKey);
        if (placement is null)
        {
            return false;
        }

        return _placements.Remove(placement);
    }

    /// <summary>
    /// Removes all saved positions so Manual mode returns to the automatic hierarchical layout.
    /// </summary>
    public void Clear() => _placements.Clear();

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPlacementsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Reset does not expose removed items. Resynchronizing the small placement collection keeps
        // subscriptions correct for Clear and for custom ObservableCollection implementations.
        foreach (NavigationMapPlacement placement in _subscribedPlacements.ToArray())
        {
            if (_placements.Contains(placement))
            {
                continue;
            }

            placement.PropertyChanged -= OnPlacementPropertyChanged;
            _subscribedPlacements.Remove(placement);
        }

        foreach (NavigationMapPlacement placement in _placements)
        {
            if (_subscribedPlacements.Add(placement))
            {
                placement.PropertyChanged += OnPlacementPropertyChanged;
            }
        }

        RaisePlacementsChanged();
    }

    private void OnPlacementPropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        RaisePlacementsChanged();

    private void RaisePlacementsChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Placements)));
}