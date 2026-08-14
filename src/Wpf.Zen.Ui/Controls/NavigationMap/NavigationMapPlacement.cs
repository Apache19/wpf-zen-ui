// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Stores one presentation-only position in a <see cref="NavigationMapLayout"/>.</summary>
/// <remarks>
/// <para>
/// The position is the center of the generated node, expressed in device-independent coordinates
/// relative to the map canvas. A center anchor keeps a saved layout visually stable when a template,
/// localization, or font change alters the size of a node.
/// </para>
/// <para>
/// Placements deliberately refer to nodes by key instead of retaining data or container instances.
/// The same layout can therefore survive item-container regeneration and can be serialized without
/// coupling it to WPF's visual tree.
/// </para>
/// </remarks>
public sealed class NavigationMapPlacement : INotifyPropertyChanged
{
    private string _nodeKey = string.Empty;
    private Point _position;
    private bool _isPinned = true;

    /// <summary>Initializes a new instance of the <see cref="NavigationMapPlacement"/> class.</summary>
    public NavigationMapPlacement() { }

    /// <summary>Initializes a new instance of the <see cref="NavigationMapPlacement"/> class.</summary>
    /// <param name="nodeKey">The stable key of the corresponding navigation-map node.</param>
    /// <param name="position">The desired center of the generated node.</param>
    public NavigationMapPlacement(string nodeKey, Point position)
    {
        NodeKey = nodeKey;
        Position = position;
    }

    /// <summary>Gets or sets the stable key of the corresponding navigation-map node.</summary>
    /// <exception cref="ArgumentException">The assigned value is empty.</exception>
    public string NodeKey
    {
        get => _nodeKey;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A navigation-map placement key cannot be empty.", nameof(value));
            }

            SetProperty(ref _nodeKey, value);
        }
    }

    /// <summary>Gets or sets the center of the generated node in map coordinates.</summary>
    /// <remarks>
    /// Both coordinates must be finite. Negative coordinates are accepted by the data model so a
    /// serializer can faithfully round-trip external layouts; the panel normalizes them into its
    /// non-negative scrolling extent when arranging the map.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Either coordinate is not finite.</exception>
    public Point Position
    {
        get => _position;
        set
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "A layout position must be finite.");
            }

            SetProperty(ref _position, value);
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether <see cref="Position"/> overrides automatic
    /// hierarchical placement.
    /// </summary>
    /// <remarks>
    /// The default is <see langword="true"/>. Clearing this value retains the last position for a
    /// future edit but allows Manual mode to place the node automatically with the rest of the map.
    /// </remarks>
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}