// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>Presents one generated node inside a <see cref="NavigationMapView"/>.</summary>
/// <remarks>
/// The container derives from <see cref="Button"/> to retain keyboard, focus, automation, and
/// command behavior. Read-only hierarchy, editor-selection, and search states are assigned by the
/// owning view and panel so templates can present them without mutating navigation data.
/// </remarks>
public class NavigationMapItem : Button
{
    /// <summary>Identifies the <see cref="NodeKey"/> dependency property.</summary>
    public static readonly DependencyProperty NodeKeyProperty = DependencyProperty.Register(
        nameof(NodeKey),
        typeof(string),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        )
    );

    /// <summary>Gets or sets the stable placement key used by layout children.</summary>
    public string? NodeKey
    {
        get => (string?)GetValue(NodeKeyProperty);
        set => SetValue(NodeKeyProperty, value);
    }

    /// <summary>Identifies the <see cref="ParentKey"/> dependency property.</summary>
    public static readonly DependencyProperty ParentKeyProperty = DependencyProperty.Register(
        nameof(ParentKey),
        typeof(string),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(
            null,
            FrameworkPropertyMetadataOptions.AffectsParentMeasure
                | FrameworkPropertyMetadataOptions.AffectsParentArrange
        )
    );

    /// <summary>Gets or sets the placement key of this node's visual parent.</summary>
    public string? ParentKey
    {
        get => (string?)GetValue(ParentKeyProperty);
        set => SetValue(ParentKeyProperty, value);
    }

    /// <summary>Identifies the <see cref="Target"/> dependency property.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(NavigationTarget),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets the optional navigation destination represented by this item.</summary>
    public NavigationTarget? Target
    {
        get => (NavigationTarget?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Identifies the <see cref="Description"/> dependency property.</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Gets or sets localized help text explaining this node.</summary>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    private static readonly DependencyPropertyKey DepthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Depth),
            typeof(int),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(0)
        );

    /// <summary>Identifies the read-only <see cref="Depth"/> dependency property.</summary>
    public static readonly DependencyProperty DepthProperty = DepthPropertyKey.DependencyProperty;

    /// <summary>Gets the zero-based depth assigned by the resolved hierarchy.</summary>
    public int Depth => (int)GetValue(DepthProperty);

    private static readonly DependencyPropertyKey IsRootPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsRoot),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(true)
        );

    /// <summary>Identifies the read-only <see cref="IsRoot"/> dependency property.</summary>
    public static readonly DependencyProperty IsRootProperty = IsRootPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether this item is a resolved visual root.</summary>
    public bool IsRoot => (bool)GetValue(IsRootProperty);

    private static readonly DependencyPropertyKey PositionInSetPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(PositionInSet),
            typeof(int),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(1)
        );

    /// <summary>Identifies the read-only <see cref="PositionInSet"/> dependency property.</summary>
    public static readonly DependencyProperty PositionInSetProperty =
        PositionInSetPropertyKey.DependencyProperty;

    /// <summary>Gets the one-based position among resolved visual siblings.</summary>
    public int PositionInSet => (int)GetValue(PositionInSetProperty);

    private static readonly DependencyPropertyKey SizeOfSetPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SizeOfSet),
            typeof(int),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(1)
        );

    /// <summary>Identifies the read-only <see cref="SizeOfSet"/> dependency property.</summary>
    public static readonly DependencyProperty SizeOfSetProperty = SizeOfSetPropertyKey.DependencyProperty;

    /// <summary>Gets the number of items in this resolved visual sibling set.</summary>
    public int SizeOfSet => (int)GetValue(SizeOfSetProperty);

    private static readonly DependencyPropertyKey IsLayoutSelectedPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsLayoutSelected),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="IsLayoutSelected"/> dependency property.</summary>
    public static readonly DependencyProperty IsLayoutSelectedProperty =
        IsLayoutSelectedPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether this item belongs to the editor selection.</summary>
    public bool IsLayoutSelected => (bool)GetValue(IsLayoutSelectedProperty);

    private static readonly DependencyPropertyKey IsPrimaryLayoutSelectionPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsPrimaryLayoutSelection),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="IsPrimaryLayoutSelection"/> property.</summary>
    public static readonly DependencyProperty IsPrimaryLayoutSelectionProperty =
        IsPrimaryLayoutSelectionPropertyKey.DependencyProperty;

    /// <summary>
    /// Gets a value indicating whether this selected item is the focus, alignment, and connector
    /// highlight target.
    /// </summary>
    public bool IsPrimaryLayoutSelection => (bool)GetValue(IsPrimaryLayoutSelectionProperty);

    private static readonly DependencyPropertyKey IsSearchMatchPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsSearchMatch),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="IsSearchMatch"/> dependency property.</summary>
    public static readonly DependencyProperty IsSearchMatchProperty =
        IsSearchMatchPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether this node matches the current map search.</summary>
    public bool IsSearchMatch => (bool)GetValue(IsSearchMatchProperty);

    private static readonly DependencyPropertyKey IsCurrentSearchMatchPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsCurrentSearchMatch),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(false)
        );

    /// <summary>Identifies the read-only <see cref="IsCurrentSearchMatch"/> property.</summary>
    public static readonly DependencyProperty IsCurrentSearchMatchProperty =
        IsCurrentSearchMatchPropertyKey.DependencyProperty;

    /// <summary>Gets a value indicating whether this node is the active search result.</summary>
    public bool IsCurrentSearchMatch => (bool)GetValue(IsCurrentSearchMatchProperty);

    static NavigationMapItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(typeof(NavigationMapItem))
        );
    }

    internal void SetLayoutPosition(int depth, bool isRoot, int positionInSet, int sizeOfSet)
    {
        SetValue(DepthPropertyKey, depth);
        SetValue(IsRootPropertyKey, isRoot);
        SetValue(PositionInSetPropertyKey, positionInSet);
        SetValue(SizeOfSetPropertyKey, sizeOfSet);
    }

    internal void SetLayoutSelected(bool selected, bool primary)
    {
        SetValue(IsLayoutSelectedPropertyKey, selected);
        SetValue(IsPrimaryLayoutSelectionPropertyKey, selected && primary);
    }

    internal void SetSearchMatch(bool match, bool current)
    {
        SetValue(IsSearchMatchPropertyKey, match);
        SetValue(IsCurrentSearchMatchPropertyKey, match && current);
    }
}