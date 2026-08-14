// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Controls;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Presents one generated node inside a <see cref="NavigationMapView"/>.
/// </summary>
/// <remarks>
/// <para>
/// The container derives from <see cref="Button"/> so every navigable map node receives WPF's
/// standard keyboard, focus, access-key, automation, and command behavior. The owning map view
/// translates its click into <see cref="NavigationMapView.NodeInvoked"/> without coupling the
/// visual control to a particular navigation host.
/// </para>
/// <para>
/// Applications normally provide <see cref="NavigationMapNode"/> data and let WPF generate this
/// container. A container may also be inserted directly; in that case <see cref="NodeKey"/>,
/// <see cref="ParentKey"/>, <see cref="Target"/>, and <see cref="Description"/> describe its map
/// placement and contextual help.
/// </para>
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

    /// <summary>Identifies the <see cref="Target"/> dependency property.</summary>
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(NavigationTarget),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="Description"/> dependency property.</summary>
    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description),
        typeof(string),
        typeof(NavigationMapItem),
        new FrameworkPropertyMetadata(null)
    );

    private static readonly DependencyPropertyKey DepthPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(Depth),
            typeof(int),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(0)
        );

    /// <summary>Identifies the read-only <see cref="Depth"/> dependency property.</summary>
    public static readonly DependencyProperty DepthProperty = DepthPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey IsRootPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(IsRoot),
            typeof(bool),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(true)
        );

    /// <summary>Identifies the read-only <see cref="IsRoot"/> dependency property.</summary>
    public static readonly DependencyProperty IsRootProperty = IsRootPropertyKey.DependencyProperty;

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

    private static readonly DependencyPropertyKey SizeOfSetPropertyKey =
        DependencyProperty.RegisterReadOnly(
            nameof(SizeOfSet),
            typeof(int),
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(1)
        );

    /// <summary>Identifies the read-only <see cref="SizeOfSet"/> dependency property.</summary>
    public static readonly DependencyProperty SizeOfSetProperty =
        SizeOfSetPropertyKey.DependencyProperty;

    static NavigationMapItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationMapItem),
            new FrameworkPropertyMetadata(typeof(NavigationMapItem))
        );
    }

    /// <summary>Gets or sets the stable placement key used by layout children.</summary>
    /// <remarks>
    /// Generated containers bind this property to <see cref="NavigationMapNode.Key"/>. It affects
    /// parent layout because changing an identity can change multiple connector relationships.
    /// </remarks>
    public string? NodeKey
    {
        get => (string?)GetValue(NodeKeyProperty);
        set => SetValue(NodeKeyProperty, value);
    }

    /// <summary>Gets or sets the placement key of this node's visual parent.</summary>
    /// <remarks>
    /// A missing or unresolved key makes the item a root. Cycles are also promoted to roots by the
    /// layout panel so malformed or transient graphs remain visible and never recurse indefinitely.
    /// </remarks>
    public string? ParentKey
    {
        get => (string?)GetValue(ParentKeyProperty);
        set => SetValue(ParentKeyProperty, value);
    }

    /// <summary>Gets or sets the optional navigation destination represented by this item.</summary>
    /// <remarks>
    /// The default template uses this value only to distinguish navigable and structural nodes.
    /// Invocation remains available for structural nodes because applications may use the event
    /// for expansion, inspection, or custom routing.
    /// </remarks>
    public NavigationTarget? Target
    {
        get => (NavigationTarget?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    /// <summary>Gets or sets localized help text explaining the purpose of this visual node.</summary>
    /// <remarks>
    /// Generated containers bind this property to <see cref="NavigationMapNode.Description"/>. The
    /// default style projects it to both the WPF tooltip and accessibility help text. Directly
    /// inserted containers may assign it themselves, and applications can still replace either
    /// presentation through ordinary dependency-property precedence.
    /// </remarks>
    public string? Description
    {
        get => (string?)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>Gets the zero-based depth assigned by the current map layout.</summary>
    public int Depth => (int)GetValue(DepthProperty);

    /// <summary>Gets a value indicating whether this item is currently a visual root.</summary>
    public bool IsRoot => (bool)GetValue(IsRootProperty);

    /// <summary>Gets the one-based position of this item among its visual siblings.</summary>
    /// <remarks>
    /// The layout panel updates this value after resolving missing parents and cycles. It therefore
    /// describes the rendered forest rather than merely the source collection index. Custom
    /// templates may use it for ordinal badges, alternating sibling treatment, or accessibility
    /// metadata available on the application's target WPF version.
    /// </remarks>
    public int PositionInSet => (int)GetValue(PositionInSetProperty);

    /// <summary>Gets the number of items in this item's visual sibling set.</summary>
    /// <remarks>
    /// Roots form one sibling set. Every parent's resolved children form another. The default is
    /// one for a directly created or currently collapsed container. Together with
    /// <see cref="PositionInSet"/>, it exposes resolved hierarchy metadata without requiring a
    /// template to reconstruct the flat parent-key graph.
    /// </remarks>
    public int SizeOfSet => (int)GetValue(SizeOfSetProperty);

    internal void SetLayoutPosition(int depth, bool isRoot, int positionInSet, int sizeOfSet)
    {
        SetValue(DepthPropertyKey, depth);
        SetValue(IsRootPropertyKey, isRoot);
        SetValue(PositionInSetPropertyKey, positionInSet);
        SetValue(SizeOfSetPropertyKey, sizeOfSet);
    }
}
