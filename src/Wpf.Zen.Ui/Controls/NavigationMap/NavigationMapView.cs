// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Displays a flat semantic navigation graph as an organization-chart-style map.
/// </summary>
/// <remarks>
/// <para>
/// This control preserves WPF's ordinary <see cref="ItemsControl"/> contract: nodes can be added
/// directly to <see cref="ItemsControl.Items"/> or supplied through
/// <see cref="ItemsControl.ItemsSource"/>, and WPF remains responsible for container generation,
/// data templates, styles, and collection-view updates. A <see cref="NavigationMap"/> can be bound
/// directly because it implements the observable collection contract.
/// </para>
/// <para>
/// The source stays flat. <see cref="NavigationMapNode.ParentKey"/> is resolved only by the layout
/// panel, avoiding a second mutable child collection that could drift from navigation and
/// breadcrumb semantics. Missing parents form additional roots; cyclic nodes are promoted to
/// roots so partial runtime updates remain renderable.
/// </para>
/// <para>
/// The default node presentation displays <see cref="NavigationMapNode.Content"/> as its localized
/// name and optionally shows <see cref="NavigationMapNode.Key"/> as diagnostic route text. Set
/// <see cref="ItemsControl.ItemTemplate"/>, <see cref="ItemsControl.ItemTemplateSelector"/>, or
/// <see cref="ItemsControl.ItemContainerStyle"/> for application-specific cards.
/// </para>
/// </remarks>
/// <example>
/// Nodes may be declared inline:
/// <code lang="xml">
/// &lt;ui:NavigationMapView&gt;
///     &lt;ui:NavigationMapNode Key="home" Content="Home" /&gt;
///     &lt;ui:NavigationMapNode Key="reports" ParentKey="home" Content="Reports" /&gt;
/// &lt;/ui:NavigationMapView&gt;
/// </code>
/// Or an application's semantic map may be used as the live source:
/// <code lang="xml">
/// &lt;ui:NavigationMapView
///     Direction="LeftToRight"
///     ItemsSource="{Binding NavigationMap}" /&gt;
/// </code>
/// </example>
[StyleTypedProperty(Property = nameof(ItemContainerStyle), StyleTargetType = typeof(NavigationMapItem))]
public class NavigationMapView : ItemsControl
{
    /// <summary>Identifies the <see cref="Direction"/> dependency property.</summary>
    public static readonly DependencyProperty DirectionProperty = DependencyProperty.Register(
        nameof(Direction),
        typeof(NavigationMapDirection),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(
            NavigationMapDirection.TopToBottom,
            FrameworkPropertyMetadataOptions.AffectsMeasure
                | FrameworkPropertyMetadataOptions.AffectsArrange
                | FrameworkPropertyMetadataOptions.AffectsRender
        ),
        ValidateDirection
    );

    /// <summary>Identifies the <see cref="NodeWidth"/> dependency property.</summary>
    public static readonly DependencyProperty NodeWidthProperty = DependencyProperty.Register(
        nameof(NodeWidth),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(184D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        ValidateNodeWidth
    );

    /// <summary>Identifies the <see cref="HorizontalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty HorizontalSpacingProperty = DependencyProperty.Register(
        nameof(HorizontalSpacing),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(32D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        ValidateHorizontalSpacing
    );

    /// <summary>Identifies the <see cref="VerticalSpacing"/> dependency property.</summary>
    public static readonly DependencyProperty VerticalSpacingProperty = DependencyProperty.Register(
        nameof(VerticalSpacing),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(44D, FrameworkPropertyMetadataOptions.AffectsMeasure),
        ValidateVerticalSpacing
    );

    /// <summary>Identifies the <see cref="ConnectorBrush"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorBrushProperty = DependencyProperty.Register(
        nameof(ConnectorBrush),
        typeof(Brush),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender)
    );

    /// <summary>Identifies the <see cref="ConnectorThickness"/> dependency property.</summary>
    public static readonly DependencyProperty ConnectorThicknessProperty = DependencyProperty.Register(
        nameof(ConnectorThickness),
        typeof(double),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(1D, FrameworkPropertyMetadataOptions.AffectsRender),
        ValidateConnectorThickness
    );

    /// <summary>Identifies the <see cref="ShowRouteKey"/> dependency property.</summary>
    public static readonly DependencyProperty ShowRouteKeyProperty = DependencyProperty.Register(
        nameof(ShowRouteKey),
        typeof(bool),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(false)
    );

    /// <summary>Identifies the <see cref="Command"/> dependency property.</summary>
    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command),
        typeof(ICommand),
        typeof(NavigationMapView),
        new FrameworkPropertyMetadata(null)
    );

    /// <summary>Identifies the <see cref="NodeInvoked"/> routed event.</summary>
    public static readonly RoutedEvent NodeInvokedEvent = EventManager.RegisterRoutedEvent(
        nameof(NodeInvoked),
        RoutingStrategy.Bubble,
        typeof(TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs>),
        typeof(NavigationMapView)
    );

    static NavigationMapView()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(NavigationMapView),
            new FrameworkPropertyMetadata(typeof(NavigationMapView))
        );
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationMapView"/> class.</summary>
    public NavigationMapView()
    {
        // Container clicks bubble through the items host. Handling them once at the generator root
        // keeps item templates free of commands and does not retain generated containers.
        AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnItemContainerClick));
    }

    /// <summary>Gets or sets the direction in which hierarchy levels advance.</summary>
    /// <remarks>
    /// The default is <see cref="NavigationMapDirection.TopToBottom"/>. Changing this property
    /// invalidates layout and connector rendering. Nodes and their content are not transformed;
    /// only their organization-chart positions and connector endpoints change.
    /// </remarks>
    public NavigationMapDirection Direction
    {
        get => (NavigationMapDirection)GetValue(DirectionProperty);
        set => SetValue(DirectionProperty, value);
    }

    /// <summary>Occurs when a generated or directly supplied map item is activated.</summary>
    public event TypedEventHandler<NavigationMapView, NavigationMapNodeInvokedEventArgs> NodeInvoked
    {
        add => AddHandler(NodeInvokedEvent, value);
        remove => RemoveHandler(NodeInvokedEvent, value);
    }

    /// <summary>Gets or sets the width assigned to each default node container.</summary>
    /// <remarks>
    /// The default is 184 device-independent pixels. The value affects measurement and may be
    /// overridden for individual containers through <see cref="ItemsControl.ItemContainerStyle"/>.
    /// </remarks>
    public double NodeWidth
    {
        get => (double)GetValue(NodeWidthProperty);
        set => SetValue(NodeWidthProperty, value);
    }

    /// <summary>Gets or sets the horizontal gap used by the map layout.</summary>
    /// <remarks>
    /// The default is 32 device-independent pixels and the value must be non-negative. It separates
    /// sibling branches in <see cref="NavigationMapDirection.TopToBottom"/> mode and hierarchy
    /// levels in <see cref="NavigationMapDirection.LeftToRight"/> mode.
    /// </remarks>
    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    /// <summary>Gets or sets the vertical gap used by the map layout.</summary>
    /// <remarks>
    /// The default is 44 device-independent pixels and the value must be non-negative. It separates
    /// hierarchy levels in <see cref="NavigationMapDirection.TopToBottom"/> mode and sibling
    /// branches in <see cref="NavigationMapDirection.LeftToRight"/> mode.
    /// </remarks>
    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    /// <summary>Gets or sets the brush used to draw parent-child connector lines.</summary>
    public Brush? ConnectorBrush
    {
        get => (Brush?)GetValue(ConnectorBrushProperty);
        set => SetValue(ConnectorBrushProperty, value);
    }

    /// <summary>Gets or sets the connector-line thickness.</summary>
    /// <remarks>The default is one device-independent pixel and the value must be positive.</remarks>
    public double ConnectorThickness
    {
        get => (double)GetValue(ConnectorThicknessProperty);
        set => SetValue(ConnectorThicknessProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the default node template shows its map key.</summary>
    /// <remarks>
    /// The default is <see langword="false"/> because keys are routing identity, not user-facing
    /// content. This option is useful for diagnostics and administrative site maps.
    /// </remarks>
    public bool ShowRouteKey
    {
        get => (bool)GetValue(ShowRouteKeyProperty);
        set => SetValue(ShowRouteKeyProperty, value);
    }

    /// <summary>Gets or sets the command executed after <see cref="NodeInvoked"/> is raised.</summary>
    /// <remarks>The original source item is passed as the command parameter.</remarks>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>Raises the invocation event and then executes <see cref="Command"/>.</summary>
    /// <param name="item">The item owned by the WPF item generator.</param>
    /// <param name="container">The activated visual container.</param>
    protected virtual void OnNodeInvoked(object item, NavigationMapItem container)
    {
        var args = new NavigationMapNodeInvokedEventArgs(NodeInvokedEvent, this, item, container);
        RaiseEvent(args);

        if (!args.Handled && Command?.CanExecute(item) == true)
        {
            Command.Execute(item);
        }
    }

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is NavigationMapItem;

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new NavigationMapItem();

    /// <inheritdoc />
    protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
    {
        base.PrepareContainerForItemOverride(element, item);

        if (element is not NavigationMapItem container || item is not NavigationMapNode node)
        {
            return;
        }

        // Bind generator metadata instead of copying it. Runtime reparenting then invalidates the
        // panel through dependency-property metadata while Content and custom templates keep the
        // ordinary ItemsControl data-item contract established by the base implementation.
        BindingOperations.SetBinding(
            container,
            NavigationMapItem.NodeKeyProperty,
            new Binding(nameof(NavigationMapNode.Key)) { Source = node, Mode = BindingMode.OneWay }
        );
        BindingOperations.SetBinding(
            container,
            NavigationMapItem.ParentKeyProperty,
            new Binding(nameof(NavigationMapNode.ParentKey)) { Source = node, Mode = BindingMode.OneWay }
        );
        BindingOperations.SetBinding(
            container,
            NavigationMapItem.TargetProperty,
            new Binding(nameof(NavigationMapNode.Target)) { Source = node, Mode = BindingMode.OneWay }
        );
        BindingOperations.SetBinding(
            container,
            AutomationProperties.NameProperty,
            new Binding(nameof(NavigationMapNode.Content)) { Source = node, Mode = BindingMode.OneWay }
        );
        BindingOperations.SetBinding(
            container,
            NavigationMapItem.DescriptionProperty,
            new Binding(nameof(NavigationMapNode.Description)) { Source = node, Mode = BindingMode.OneWay }
        );
    }

    /// <inheritdoc />
    protected override void ClearContainerForItemOverride(DependencyObject element, object item)
    {
        if (element is NavigationMapItem container && !ReferenceEquals(element, item))
        {
            BindingOperations.ClearBinding(container, NavigationMapItem.NodeKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.ParentKeyProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.TargetProperty);
            BindingOperations.ClearBinding(container, NavigationMapItem.DescriptionProperty);
            BindingOperations.ClearBinding(container, AutomationProperties.NameProperty);
            container.SetLayoutPosition(0, true, 1, 1);
        }

        base.ClearContainerForItemOverride(element, item);
    }

    private void OnItemContainerClick(object sender, RoutedEventArgs e)
    {
        // Ignore buttons placed inside an application ItemTemplate. Only a click whose routed-event
        // source is the generated map container represents node activation.
        if (e.OriginalSource is not NavigationMapItem container)
        {
            return;
        }

        object item = ItemContainerGenerator.ItemFromContainer(container);
        if (ReferenceEquals(item, DependencyProperty.UnsetValue))
        {
            return;
        }

        OnNodeInvoked(item, container);
    }

    private static bool ValidateNodeWidth(object? value) => IsFinitePositiveDouble(value);

    private static bool ValidateDirection(object? value) =>
        value is NavigationMapDirection.TopToBottom or NavigationMapDirection.LeftToRight;

    private static bool ValidateHorizontalSpacing(object? value) => IsFiniteNonNegativeDouble(value);

    private static bool ValidateVerticalSpacing(object? value) => IsFiniteNonNegativeDouble(value);

    private static bool ValidateConnectorThickness(object? value) => IsFinitePositiveDouble(value);

    private static bool IsFinitePositiveDouble(object? value) =>
        value is double number && number > 0D && !double.IsInfinity(number) && !double.IsNaN(number);

    private static bool IsFiniteNonNegativeDouble(object? value) =>
        value is double number && number >= 0D && !double.IsInfinity(number) && !double.IsNaN(number);
}
