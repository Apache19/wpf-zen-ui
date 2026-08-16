// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides routed commands for operations that are specific to <see cref="NavigationMapView"/>.
/// </summary>
/// <remarks>
/// General editor and viewport operations intentionally use WPF's standard command libraries:
/// <see cref="ApplicationCommands.Undo"/>, <see cref="ApplicationCommands.Redo"/>,
/// <see cref="ApplicationCommands.SelectAll"/>, <see cref="NavigationCommands.IncreaseZoom"/>,
/// <see cref="NavigationCommands.DecreaseZoom"/>, <see cref="NavigationCommands.Zoom"/>, and the
/// directional <see cref="ComponentCommands"/> commands. This type contains only map-specific
/// semantics so menus and toolbars can compose naturally with other WPF editors.
/// </remarks>
/// <remarks>
/// When used with a navigation map, <see cref="NavigationCommands.Zoom"/> accepts an invariant
/// numeric percentage or percentage string as its command parameter; for example, <c>125</c> and
/// <c>"125%"</c> both set <see cref="NavigationMapView.ZoomFactor"/> to <c>1.25</c>.
/// </remarks>
/// <remarks>
/// Command labels are resolved from the Wpf.Zen.Ui satellite resources by using
/// <see cref="CultureInfo.CurrentUICulture"/> when each command property is
/// first read. WPF can defer its own command label slightly longer, until the first read of
/// <see cref="RoutedUICommand.Text"/>, through an internal constructor that is unavailable to
/// custom command libraries. Both models cache the resulting text in the command instance, so a
/// later UI-culture change does not automatically relocalize an existing command.
/// </remarks>
public static class NavigationMapCommands
{
    // RoutedUICommand's public constructors require the final text. Lazy<T> therefore gives a
    // custom command library the closest public-API equivalent to WPF's internal lazy command
    // factory: merely initializing this type does not lock all labels to the culture of the first
    // unrelated command. Lazy<T>'s default execution-and-publication mode also preserves WPF's
    // thread-safe singleton behavior if command properties are queried concurrently.
    private static readonly Lazy<RoutedUICommand> _selectNode = new(() =>
        CreateCommand(nameof(SelectNode))
    );

    private static readonly Lazy<RoutedUICommand> _clearSelection = new(() =>
        CreateCommand(
            nameof(ClearSelection),
            new InputGestureCollection { new KeyGesture(Key.Escape) }
        )
    );

    private static readonly Lazy<RoutedUICommand> _findNext = new(() =>
        CreateCommand(
            nameof(FindNext),
            new InputGestureCollection { new KeyGesture(Key.F3) }
        )
    );

    private static readonly Lazy<RoutedUICommand> _findPrevious = new(() =>
        CreateCommand(
            nameof(FindPrevious),
            new InputGestureCollection { new KeyGesture(Key.F3, ModifierKeys.Shift) }
        )
    );

    private static readonly Lazy<RoutedUICommand> _alignToGrid = new(() =>
        CreateCommand(nameof(AlignToGrid))
    );

    private static readonly Lazy<RoutedUICommand> _alignSelection = new(() =>
        CreateCommand(nameof(AlignSelection))
    );

    private static readonly Lazy<RoutedUICommand> _resetLayout = new(() =>
        CreateCommand(nameof(ResetLayout))
    );

    private static readonly Lazy<RoutedUICommand> _resetZoom = new(() =>
        CreateCommand(
            nameof(ResetZoom),
            new InputGestureCollection { new KeyGesture(Key.D0, ModifierKeys.Control) }
        )
    );

    private static readonly Lazy<RoutedUICommand> _zoomToFit = new(() =>
        CreateCommand(nameof(ZoomToFit))
    );

    /// <summary>
    /// Gets the command that selects and reveals the node whose key is supplied as a
    /// <see cref="string"/> command parameter.
    /// </summary>
    public static RoutedUICommand SelectNode => _selectNode.Value;

    /// <summary>Gets the command that clears the current node selection.</summary>
    /// <remarks>The default input gesture is <see cref="Key.Escape"/>.</remarks>
    public static RoutedUICommand ClearSelection => _clearSelection.Value;

    /// <summary>Gets the command that activates the next node matching the current search text.</summary>
    /// <remarks>The default input gesture is <see cref="Key.F3"/>.</remarks>
    public static RoutedUICommand FindNext => _findNext.Value;

    /// <summary>Gets the command that activates the previous node matching the current search text.</summary>
    /// <remarks>The default input gesture is Shift+F3.</remarks>
    public static RoutedUICommand FindPrevious => _findPrevious.Value;

    /// <summary>
    /// Gets the command that aligns selected nodes to the current grid, or aligns every node when
    /// the selection is empty.
    /// </summary>
    public static RoutedUICommand AlignToGrid => _alignToGrid.Value;

    /// <summary>
    /// Gets the command that aligns selected nodes to the primary node. The command parameter must
    /// be a <see cref="NavigationMapAlignment"/> value.
    /// </summary>
    public static RoutedUICommand AlignSelection => _alignSelection.Value;

    /// <summary>
    /// Gets the command that removes saved placements and restores the grid-aligned hierarchical
    /// starting arrangement.
    /// </summary>
    public static RoutedUICommand ResetLayout => _resetLayout.Value;

    /// <summary>Gets the command that restores the map's one-to-one zoom scale.</summary>
    /// <remarks>The default input gesture is Ctrl+0.</remarks>
    public static RoutedUICommand ResetZoom => _resetZoom.Value;

    /// <summary>Gets the command that scales the complete map to fit the scrolling viewport.</summary>
    public static RoutedUICommand ZoomToFit => _zoomToFit.Value;

    /// <summary>
    /// Creates a command with text resolved for the UI culture active at first access.
    /// </summary>
    /// <param name="name">The stable command and resource name.</param>
    /// <param name="inputGestures">Optional default gestures associated with the command.</param>
    /// <returns>The initialized routed UI command.</returns>
    private static RoutedUICommand CreateCommand(
        string name,
        InputGestureCollection? inputGestures = null
    )
    {
        return new RoutedUICommand(
            NavigationMapCommandResources.GetString(name),
            name,
            typeof(NavigationMapCommands),
            inputGestures
        );
    }
}
