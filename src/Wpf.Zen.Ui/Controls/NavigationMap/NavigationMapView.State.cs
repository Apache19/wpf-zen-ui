// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.Specialized;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains selection, search, generated-container lookup, and layout-history state management
/// for <see cref="NavigationMapView"/>.
/// </summary>
public partial class NavigationMapView
{
    private void ApplySelection(HashSet<string> newSelection, string? primaryKey)
    {
        var removed = _selectedNodeKeys.Except(newSelection, StringComparer.Ordinal).ToArray();
        var added   = newSelection.Except(_selectedNodeKeys, StringComparer.Ordinal).ToArray();

        _selectedNodeKeys.Clear();
        _selectedNodeKeys.UnionWith(newSelection);

        if (primaryKey is null || !_selectedNodeKeys.Contains(primaryKey))
        {
            primaryKey = _selectedNodeKeys.FirstOrDefault();
        }

        SetValue(PrimarySelectedNodeKeyPropertyKey, primaryKey);

        foreach (var container in GetGeneratedContainers())
        {
            var key      = container.NodeKey;
            var selected = key is not null && _selectedNodeKeys.Contains(key);

            container.SetLayoutSelected(
                selected,
                selected && StringComparer.Ordinal.Equals(key, primaryKey)
            );
        }

        if (removed.Length > 0 || added.Length > 0)
        {
            RaiseEvent(new SelectionChangedEventArgs(SelectionChangedEvent, removed, added));
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void RefreshGeneratedState()
    {
        var existingKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var container in GetGeneratedContainers())
        {
            var item = ItemContainerGenerator.ItemFromContainer(container);
            if (container.NodeKey is { Length: > 0 } key)
            {
                existingKeys.Add(key);
            }

            UpdateContainerStates(container, item);
        }

        _selectedNodeKeys.IntersectWith(existingKeys);

        if (PrimarySelectedNodeKey is { } primary && !_selectedNodeKeys.Contains(primary))
        {
            SetValue(PrimarySelectedNodeKeyPropertyKey, _selectedNodeKeys.FirstOrDefault());
        }

        RefreshSearchMatches();
    }

    private void UpdateContainerStates(NavigationMapItem container, object item)
    {
        var selected = container.NodeKey is { } key && _selectedNodeKeys.Contains(key);

        container.SetLayoutSelected(
            selected,
            selected && StringComparer.Ordinal.Equals(container.NodeKey, PrimarySelectedNodeKey)
        );

        container.SetSearchMatch(IsSearchMatch(item, SearchText), false);
    }

    private void RefreshSearchMatches()
    {
        List<NavigationMapItem> matches = [];

        foreach (var container in GetGeneratedContainers())
        {
            var item  = ItemContainerGenerator.ItemFromContainer(container);
            var match = IsSearchMatch(item, SearchText);

            container.SetSearchMatch(match, false);

            if (match)
            {
                matches.Add(container);
            }
        }

        SetValue(SearchResultCountPropertyKey, matches.Count);
        SetValue(CurrentSearchResultIndexPropertyKey, -1);
        CommandManager.InvalidateRequerySuggested();
    }

    private bool MoveCurrentSearchResult(int direction)
    {
        var matches = GetGeneratedContainers().Where(static item => item.IsSearchMatch).ToList();
        if (matches.Count == 0)
        {
            return false;
        }

        var current = CurrentSearchResultIndex;
        var next = current < 0
            ? direction > 0 ? 0 : matches.Count - 1
            : (current + direction + matches.Count) % matches.Count;

        for (var index = 0; index < matches.Count; index++)
        {
            matches[index].SetSearchMatch(true, index == next);
        }

        SetValue(CurrentSearchResultIndexPropertyKey, next);

        var container = matches[next];
        if (container.NodeKey is { Length: > 0 } key)
        {
            ApplySelection(new HashSet<string>(StringComparer.Ordinal) { key }, key);
        }

        _ = CanEditSelection() ? Focus() : container.Focus();

        container.BringIntoView();

        return true;
    }

    private static bool IsSearchMatch(object item, string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return false;
        }

        var query = searchText.Trim();

        return item switch
        {
            NavigationMapNode node =>
                Contains(node.Key,                 query)
             || Contains(node.Content?.ToString(), query)
             || Contains(node.Description,         query),

            NavigationMapItem container =>
                Contains(container.NodeKey,             query)
             || Contains(container.Content?.ToString(), query)
             || Contains(container.Description,         query),

            _ => Contains(item?.ToString(), query),
        };
    }

    private static bool Contains(string? value, string query)
    {
        return value?.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private Dictionary<string, PlacementState> CapturePlacementStates(IEnumerable<string> keys)
    {
        var states = new Dictionary<string, PlacementState>(StringComparer.Ordinal);

        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            if (
                LayoutDefinition is { } layout
                && layout.TryGetPlacement(key, out var placement)
                && placement is not null
            )
            {
                states[key] = new PlacementState(true, placement.Position, placement.IsPinned);
            }
            else
            {
                states[key] = PlacementState.Missing;
            }
        }

        return states;
    }

    private void ApplyPlacementStates(IReadOnlyDictionary<string, PlacementState> states)
    {
        var layout = EnsureLayout();
        foreach (var pair in states)
        {
            if (!pair.Value.Exists)
            {
                _ = layout.Remove(pair.Key);
                continue;
            }

            var placement = layout.SetPosition(pair.Key, pair.Value.Position);
            placement.IsPinned = pair.Value.IsPinned;
        }
    }

    private void RecordLayoutEdit(
        Dictionary<string, PlacementState> before,
        Dictionary<string, PlacementState> after)
    {
        if (
            before.Count != after.Count
            || before.Any(pair => !after.TryGetValue(pair.Key, out var state) || state != pair.Value)
        )
        {
            const int maximumHistoryLength = 100;

            if (_undoHistory.Count == maximumHistoryLength)
            {
                _undoHistory.RemoveAt(0);
            }

            _undoHistory.Add(new LayoutEdit(before, after));
            _redoHistory.Clear();

            UpdateHistoryState();
        }
    }

    private void UpdateHistoryState()
    {
        SetValue(CanUndoPropertyKey, _undoHistory.Count > 0);
        SetValue(CanRedoPropertyKey, _redoHistory.Count > 0);
        CommandManager.InvalidateRequerySuggested();
    }

    private NavigationMapLayout EnsureLayout()
    {
        if (LayoutDefinition is { } layout)
        {
            return layout;
        }

        layout = new NavigationMapLayout();
        SetCurrentValue(LayoutDefinitionProperty, layout);
        return layout;
    }

    private bool CanEditSelection()
    {
        return IsEditing && LayoutMode == NavigationMapLayoutMode.Manual;
    }

    private double Snap(double coordinate)
    {
        return Math.Round(coordinate / GridSize) * GridSize;
    }

    private static Point ClampCenter(NavigationMapItem container, Point center)
    {
        return new Point(
            Math.Max(container.RenderSize.Width / 2D, center.X),
            Math.Max(container.RenderSize.Height / 2D, center.Y)
        );
    }

    private static Rect GetContainerBounds(NavigationMapItem container)
    {
        var offset = VisualTreeHelper.GetOffset(container);
        return new Rect(offset.X, offset.Y, container.RenderSize.Width, container.RenderSize.Height);
    }

    private static Point Center(Rect bounds)
    {
        return new Point(bounds.Left + (bounds.Width / 2D), bounds.Top + (bounds.Height / 2D));
    }

    private List<NavigationMapItem> GetSelectedContainers()
    {
        return GetGeneratedContainers()
            .Where(container => container.NodeKey is { } key && _selectedNodeKeys.Contains(key))
            .ToList();
    }

    private IEnumerable<NavigationMapItem> GetGeneratedContainers()
    {
        for (var index = 0; index < Items.Count; index++)
        {
            if (ItemContainerGenerator.ContainerFromIndex(index) is NavigationMapItem container)
            {
                yield return container;
            }
        }
    }

    private NavigationMapItem? FindContainer(string? nodeKey)
    {
        return nodeKey is null
            ? null
            : GetGeneratedContainers().FirstOrDefault(container =>
                StringComparer.Ordinal.Equals(container.NodeKey, nodeKey)
            );
    }

    private static string? GetNodeKey(object item)
    {
        return item switch
        {
            NavigationMapNode node      => node.Key,
            NavigationMapItem container => container.NodeKey,
            _                           => null,
        };
    }

    private static void BindContainer(
        DependencyObject target,
        DependencyProperty property,
        object source,
        string path)
    {
        BindingOperations.SetBinding(
            target,
            property,
            new Binding(path) { Source = source, Mode = BindingMode.OneWay }
        );
    }

    private static T? FindVisualAncestor<T>(DependencyObject? origin)
        where T : DependencyObject
    {
        var current = origin;

        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = current switch
            {
                Visual visual              => VisualTreeHelper.GetParent(visual),
                FrameworkContentElement ce => ce.Parent,
                ContentElement content     => ContentOperations.GetParent(content),
                _                          => LogicalTreeHelper.GetParent(current),
            };
        }

        return null;
    }

    private static T? FindVisualDescendant<T>(DependencyObject root)
        where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(root);

        for (var index = 0; index < count; index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);

            if (child is T match)
            {
                return match;
            }

            if (FindVisualDescendant<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private readonly record struct PlacementState(
        bool Exists,
        Point Position,
        bool IsPinned)
    {
        internal static PlacementState Missing { get; } = new(false, default, false);
    }

    private sealed class LayoutEdit(
        Dictionary<string, PlacementState> before,
        Dictionary<string, PlacementState> after)
    {
        internal Dictionary<string, PlacementState> Before { get; } = before;

        internal Dictionary<string, PlacementState> After { get; } = after;
    }
}
