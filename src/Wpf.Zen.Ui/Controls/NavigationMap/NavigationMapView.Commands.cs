// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Input;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains routed-command registration and state evaluation for <see cref="NavigationMapView"/>.
/// </summary>
public partial class NavigationMapView
{
    private static void RegisterCommandBindings()
    {
        Type ownerType = typeof(NavigationMapView);

        // Class bindings make commands route to the map that owns keyboard focus while still
        // allowing an external toolbar or menu to name that map explicitly as CommandTarget. This
        // is the same ownership model used by WPF's TextBoxBase, DataGrid, and document viewers.
        RegisterCommand(ownerType, ApplicationCommands.Undo);
        RegisterCommand(ownerType, ApplicationCommands.Redo);
        RegisterCommand(ownerType, ApplicationCommands.SelectAll);

        RegisterCommand(ownerType, ComponentCommands.MoveLeft);
        RegisterCommand(ownerType, ComponentCommands.MoveRight);
        RegisterCommand(ownerType, ComponentCommands.MoveUp);
        RegisterCommand(ownerType, ComponentCommands.MoveDown);

        RegisterCommand(ownerType, NavigationCommands.IncreaseZoom);
        RegisterCommand(ownerType, NavigationCommands.DecreaseZoom);
        RegisterCommand(ownerType, NavigationCommands.Zoom);

        RegisterCommand(ownerType, NavigationMapCommands.SelectNode);
        RegisterCommand(ownerType, NavigationMapCommands.ClearSelection);
        RegisterCommand(ownerType, NavigationMapCommands.FindNext);
        RegisterCommand(ownerType, NavigationMapCommands.FindPrevious);
        RegisterCommand(ownerType, NavigationMapCommands.AlignToGrid);
        RegisterCommand(ownerType, NavigationMapCommands.AlignSelection);
        RegisterCommand(ownerType, NavigationMapCommands.ResetLayout);
        RegisterCommand(ownerType, NavigationMapCommands.ResetZoom);
        RegisterCommand(ownerType, NavigationMapCommands.ZoomToFit);

        // NavigationCommands deliberately define no platform zoom gestures. Register the familiar
        // editor shortcuts at the control class so applications can replace them with instance
        // InputBindings without changing the commands used by their toolbar.
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(NavigationCommands.IncreaseZoom, Key.OemPlus, ModifierKeys.Control)
        );
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(
                NavigationCommands.IncreaseZoom,
                Key.OemPlus,
                ModifierKeys.Control | ModifierKeys.Shift
            )
        );
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(NavigationCommands.IncreaseZoom, Key.Add, ModifierKeys.Control)
        );
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(NavigationCommands.DecreaseZoom, Key.OemMinus, ModifierKeys.Control)
        );
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(NavigationCommands.DecreaseZoom, Key.Subtract, ModifierKeys.Control)
        );

        // ApplicationCommands.Redo supplies Ctrl+Y. Diagram editors also conventionally accept
        // Ctrl+Shift+Z, which was supported by the map before command routing was introduced.
        CommandManager.RegisterClassInputBinding(
            ownerType,
            new KeyBinding(ApplicationCommands.Redo, Key.Z, ModifierKeys.Control | ModifierKeys.Shift)
        );
    }

    private static void RegisterCommand(Type ownerType, ICommand command)
    {
        CommandManager.RegisterClassCommandBinding(
            ownerType,
            new CommandBinding(command, OnCommandExecuted, OnCommandCanExecute)
        );
    }

    private static void OnCommandCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        var view = (NavigationMapView)sender;

        // Mark every registered command query as handled, including disabled editor operations.
        // Otherwise a Window-level Undo or movement binding could unexpectedly execute while the
        // focused map is read-only or has no applicable selection.
        e.Handled = true;
        e.CanExecute = view.IsEnabled && view.CanExecuteCommand(e.Command, e.Parameter);
    }

    private static void OnCommandExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        var view = (NavigationMapView)sender;

        if (e.Command == ApplicationCommands.Undo)
        {
            _ = view.Undo();
        }
        else if (e.Command == ApplicationCommands.Redo)
        {
            _ = view.Redo();
        }
        else if (e.Command == ApplicationCommands.SelectAll)
        {
            view.SelectAll();
        }
        else if (e.Command == ComponentCommands.MoveLeft)
        {
            view.MoveSelection(view.GetKeyboardMoveDelta(-1D, 0D));
        }
        else if (e.Command == ComponentCommands.MoveRight)
        {
            view.MoveSelection(view.GetKeyboardMoveDelta(1D, 0D));
        }
        else if (e.Command == ComponentCommands.MoveUp)
        {
            view.MoveSelection(view.GetKeyboardMoveDelta(0D, -1D));
        }
        else if (e.Command == ComponentCommands.MoveDown)
        {
            view.MoveSelection(view.GetKeyboardMoveDelta(0D, 1D));
        }
        else if (e.Command == NavigationCommands.IncreaseZoom)
        {
            view.ZoomIn();
        }
        else if (e.Command == NavigationCommands.DecreaseZoom)
        {
            view.ZoomOut();
        }
        else if (e.Command == NavigationCommands.Zoom)
        {
            _ = TryGetZoomFactor(e.Parameter, out double zoomFactor);
            view.SetCurrentValue(ZoomFactorProperty, zoomFactor);
        }
        else if (e.Command == NavigationMapCommands.SelectNode)
        {
            _ = view.SelectNode((string)e.Parameter);
        }
        else if (e.Command == NavigationMapCommands.ClearSelection)
        {
            view.ClearSelection();
        }
        else if (e.Command == NavigationMapCommands.FindNext)
        {
            _ = view.FindNext();
        }
        else if (e.Command == NavigationMapCommands.FindPrevious)
        {
            _ = view.FindPrevious();
        }
        else if (e.Command == NavigationMapCommands.AlignToGrid)
        {
            _ = view.AlignToGrid();
        }
        else if (e.Command == NavigationMapCommands.AlignSelection)
        {
            _ = view.AlignSelection((NavigationMapAlignment)e.Parameter);
        }
        else if (e.Command == NavigationMapCommands.ResetLayout)
        {
            view.ResetLayout();
        }
        else if (e.Command == NavigationMapCommands.ResetZoom)
        {
            view.ResetZoom();
        }
        else if (e.Command == NavigationMapCommands.ZoomToFit)
        {
            _ = view.ZoomToFit();
        }

        e.Handled = true;
    }

    private bool CanExecuteCommand(ICommand command, object? parameter)
    {
        if (command == ApplicationCommands.Undo)
        {
            return CanEditSelection() && CanUndo;
        }

        if (command == ApplicationCommands.Redo)
        {
            return CanEditSelection() && CanRedo;
        }

        if (command == ApplicationCommands.SelectAll)
        {
            return CanEditSelection() && GetGeneratedContainers().Any(static item => item.NodeKey is { Length: > 0 });
        }

        if (
            command == ComponentCommands.MoveLeft
            || command == ComponentCommands.MoveRight
            || command == ComponentCommands.MoveUp
            || command == ComponentCommands.MoveDown
        )
        {
            return CanEditSelection() && _selectedNodeKeys.Count > 0;
        }

        if (command == NavigationCommands.IncreaseZoom)
        {
            return ZoomFactor < MaximumZoomFactor;
        }

        if (command == NavigationCommands.DecreaseZoom)
        {
            return ZoomFactor > MinimumZoomFactor;
        }

        if (command == NavigationCommands.Zoom)
        {
            return TryGetZoomFactor(parameter, out _);
        }

        if (command == NavigationMapCommands.SelectNode)
        {
            return parameter is string { Length: > 0 } nodeKey && FindContainer(nodeKey) is not null;
        }

        if (command == NavigationMapCommands.ClearSelection)
        {
            return _selectedNodeKeys.Count > 0;
        }

        if (command == NavigationMapCommands.FindNext || command == NavigationMapCommands.FindPrevious)
        {
            return SearchResultCount > 0;
        }

        if (command == NavigationMapCommands.AlignToGrid)
        {
            return CanEditSelection() && GetGeneratedContainers().Any();
        }

        if (command == NavigationMapCommands.AlignSelection)
        {
            return parameter is NavigationMapAlignment alignment
                && Enum.IsDefined(typeof(NavigationMapAlignment), alignment)
                && CanAlignSelection();
        }

        if (command == NavigationMapCommands.ResetLayout)
        {
            return CanEditSelection();
        }

        if (command == NavigationMapCommands.ResetZoom)
        {
            return ZoomFactor != GetResetZoomFactor();
        }

        if (command == NavigationMapCommands.ZoomToFit)
        {
            return CanZoomToFit();
        }

        return false;
    }

    private bool CanAlignSelection()
    {
        return CanEditSelection()
            && FindContainer(PrimarySelectedNodeKey) is not null
            && GetSelectedContainers().Count >= 2;
    }

    private bool CanZoomToFit()
    {
        var panel = FindVisualDescendant<NavigationMapPanel>(this);
        return _scrollViewer is not null
            && panel is not null
            && panel.RenderSize.Width > 0D
            && panel.RenderSize.Height > 0D;
    }

    private double GetResetZoomFactor()
    {
        return Math.Max(MinimumZoomFactor, Math.Min(MaximumZoomFactor, 1D));
    }

    private Vector GetKeyboardMoveDelta(double horizontalDirection, double verticalDirection)
    {
        double step = SnapToGrid
            ? GridSize
            : (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? 10D : 1D;

        return new Vector(horizontalDirection * step, verticalDirection * step);
    }

    private static bool TryGetZoomFactor(object? parameter, out double zoomFactor)
    {
        zoomFactor = default;
        double percentage;

        if (parameter is string text)
        {
            text = text.Trim();
            if (text.EndsWith("%", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1).Trim();
            }

            if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out percentage))
            {
                return false;
            }
        }
        else if (
            parameter is byte or sbyte or short or ushort or int or uint or long or ulong
                or float or double or decimal
        )
        {
            try
            {
                percentage = Convert.ToDouble(parameter, CultureInfo.InvariantCulture);
            }
            catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
            {
                return false;
            }
        }
        else
        {
            return false;
        }

        zoomFactor = percentage / 100D;
        return zoomFactor > 0D && !double.IsNaN(zoomFactor) && !double.IsInfinity(zoomFactor);
    }
}
