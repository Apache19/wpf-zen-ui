// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Wpf.Zen.Ui;

/// <summary>Represents one mutable semantic route placement in a <see cref="NavigationMap"/>.</summary>
/// <remarks>
/// A node is a graph record rather than a menu item. Its parent, label, and destination can change
/// at runtime, and the owning map will notify active navigation locations to resolve again. Two
/// nodes may target the same page type under different keys, which models aliases and distinct
/// placements without pretending that a CLR page type has exactly one parent.
/// </remarks>
public sealed class NavigationMapNode : INotifyPropertyChanged
{
    private string _key = string.Empty;
    private string? _parentKey;
    private object? _content;
    private NavigationTarget? _target;
    private Func<NavigationLocationContext, object?>? _contentResolver;
    private NavigationMap? _owner;

    /// <summary>Initializes a new instance of the <see cref="NavigationMapNode"/> class for XAML construction.</summary>
    /// <remarks>
    /// XAML assigns <see cref="Key"/> before inserting the node into a map. Application code should
    /// normally use the validating constructor or one of <see cref="NavigationMap.Add(string, object?, NavigationTarget?, string?)"/>
    /// overloads instead. A node with an empty key cannot be added to a map.
    /// </remarks>
    public NavigationMapNode() { }

    /// <summary>Initializes a new instance of the <see cref="NavigationMapNode"/> class.</summary>
    /// <param name="key">The stable route key used by children and explicit targets.</param>
    /// <param name="content">The default breadcrumb content.</param>
    /// <param name="target">The optional page destination; omit it for a structural node.</param>
    /// <param name="parentKey">The optional key of the semantic parent node.</param>
    /// <exception cref="ArgumentException"><paramref name="key"/> is empty.</exception>
    public NavigationMapNode(
        string key,
        object? content,
        NavigationTarget? target = null,
        string? parentKey = null)
    {
        Key = key;
        _content = content;
        _target = target;
        ParentKey = parentKey;
    }

    /// <summary>Gets or sets the stable identity of this placement.</summary>
    /// <remarks>The key cannot be changed while the node belongs to a map. Remove and re-add the node to rename it.</remarks>
    /// <exception cref="ArgumentException">The assigned key is empty.</exception>
    /// <exception cref="InvalidOperationException">The key is changed while this node belongs to a map.</exception>
    public string Key
    {
        get => _key;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A navigation-map key cannot be empty.", nameof(value));
            }

            if (_owner is not null && !StringComparer.Ordinal.Equals(_key, value))
            {
                throw new InvalidOperationException(
                    "A navigation-map key cannot change while the node belongs to a map."
                );
            }

            SetProperty(ref _key, value);
        }
    }

    /// <summary>Gets or sets the key of the semantic parent node.</summary>
    /// <remarks>
    /// A missing parent is valid. Resolution stops at that boundary and retains the resolvable
    /// suffix, so a broken chain such as an unavailable A above B still presents B → C → D.
    /// </remarks>
    public string? ParentKey
    {
        get => _parentKey;
        set
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("A parent route key cannot be empty.", nameof(value));
            }

            SetProperty(ref _parentKey, value);
        }
    }

    /// <summary>Gets or sets the default breadcrumb content for this placement.</summary>
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <summary>Gets or sets the optional destination activated by this placement.</summary>
    public NavigationTarget? Target
    {
        get => _target;
        set
        {
            if (EqualityComparer<NavigationTarget?>.Default.Equals(_target, value))
            {
                return;
            }

            _target = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Target)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageType)));
        }
    }

    /// <summary>Gets or sets the page type activated by this placement.</summary>
    /// <remarks>
    /// This is the XAML-friendly shorthand for assigning <see cref="Target"/>. The map uses
    /// <see cref="Key"/> as the route identity, so a typical XAML node only needs <c>Key</c>,
    /// <c>Content</c>, <c>PageType</c>, and optionally <c>ParentKey</c>. Omitting it on a newly
    /// constructed XAML node creates a structural, non-navigable node. When a custom
    /// <see cref="Target"/> already carries a route key, assigning <see langword="null"/> removes
    /// only its page type and preserves that route-only destination; clear <see cref="Target"/>
    /// explicitly when converting such a programmatic node to a structural node.
    /// </remarks>
    public Type? PageType
    {
        get => Target?.PageType;
        set
        {
            NavigationTarget? current = Target;
            if (value is null)
            {
                Target = current?.RouteKey is { } routeKey
                    ? new NavigationTarget(null, routeKey, current.Parameter)
                    : null;
                return;
            }

            Target = new NavigationTarget(value, current?.RouteKey, current?.Parameter);
        }
    }

    /// <summary>
    /// Gets or sets an optional resolver that computes breadcrumb content for each occurrence.
    /// </summary>
    /// <remarks>
    /// Use this for parameterized destinations such as article pages. The resolver runs only while
    /// the map builds the active path; mutate the node or raise a page location-source notification
    /// when external data used by the resolver changes.
    /// </remarks>
    public Func<NavigationLocationContext, object?>? ContentResolver
    {
        get => _contentResolver;
        set => SetProperty(ref _contentResolver, value);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Prevents a node from being shared between maps and freezes its identity while registered.
    /// Mutable placement data remains intentionally supported.
    /// </summary>
    internal void AttachTo(NavigationMap owner)
    {
        if (_owner is not null && !ReferenceEquals(_owner, owner))
        {
            throw new InvalidOperationException(
                "A navigation-map node cannot belong to more than one map."
            );
        }

        _owner = owner;
    }

    internal void DetachFrom(NavigationMap owner)
    {
        if (ReferenceEquals(_owner, owner))
        {
            _owner = null;
        }
    }

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
