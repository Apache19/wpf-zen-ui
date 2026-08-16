// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Media;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Contains the obstacle-aware orthogonal path finder used by
/// <see cref="NavigationMapPanel"/> connector routing.
/// </summary>
public partial class NavigationMapPanel
{
    private sealed class OrthogonalRouter
    {
        // A bend is much more visually expensive than a few extra device-independent pixels. The
        // former value (14) let the router enter a nearby side port with two turns instead of using
        // the facing top/bottom port with one turn, producing the hooked routes seen around rows of
        // cards. Seventy-two DIPs is approximately one normal card height: it remains a preference,
        // not a hard constraint, but makes a materially shorter or reusable path justify each turn.
        private const double BendPenalty = 72D;
        private const double ReusedLengthFactor = 0.22D;
        private const double ReservedLengthFactor = 4D;
        private const double ReservedEdgePenalty = 36D;
        private readonly IReadOnlyList<LayoutNode> _nodes;
        private readonly double _clearance;
        private readonly NavigationMapDirection? _constrainedDirection;
        private readonly Rect[] _obstacles;
        private readonly double[] _xCoordinates;
        private readonly double[] _yCoordinates;
        private readonly Dictionary<double, int> _xIndices;
        private readonly Dictionary<double, int> _yIndices;
        private readonly bool[] _validVertices;
        private readonly bool[] _rightEdges;
        private readonly bool[] _downEdges;
        private readonly HashSet<GridEdge> _networkEdges = [];
        private readonly HashSet<GridEdge> _reservedEdges = [];
        private readonly List<ConnectorSegment> _networkSegments = [];
        private readonly List<ConnectorSegment> _reservedSegments = [];
        private readonly MinimumQueue _queue = new();
        private readonly double[] _distances;
        private readonly int[] _previous;
        private readonly int[] _sourceOwners;
        private readonly int[] _stateGenerations;
        private int _searchGeneration;

        internal OrthogonalRouter(
            IReadOnlyList<LayoutNode> nodes,
            double clearance,
            NavigationMapDirection? constrainedDirection)
        {
            _nodes = nodes;
            _clearance = clearance;
            _constrainedDirection = constrainedDirection;
            _obstacles = nodes.Select(node => Inflate(node.ArrangedBounds, clearance)).ToArray();

            var xs = new SortedSet<double>();
            var ys = new SortedSet<double>();
            foreach (Rect obstacle in _obstacles)
            {
                xs.Add(obstacle.Left);
                xs.Add(obstacle.Left + (obstacle.Width / 2D));
                xs.Add(obstacle.Right);
                ys.Add(obstacle.Top);
                ys.Add(obstacle.Top + (obstacle.Height / 2D));
                ys.Add(obstacle.Bottom);
            }

            xs.Add(Math.Max(0D, nodes.Min(static node => node.ArrangedBounds.Left) - clearance));
            ys.Add(Math.Max(0D, nodes.Min(static node => node.ArrangedBounds.Top) - clearance));
            xs.Add(nodes.Max(static node => node.ArrangedBounds.Right) + clearance);
            ys.Add(nodes.Max(static node => node.ArrangedBounds.Bottom) + clearance);
            _xCoordinates = [.. xs];
            _yCoordinates = [.. ys];
            _xIndices = _xCoordinates
                .Select((value, index) => (value, index))
                .ToDictionary(static pair => pair.value, static pair => pair.index);
            _yIndices = _yCoordinates
                .Select((value, index) => (value, index))
                .ToDictionary(static pair => pair.value, static pair => pair.index);
            _validVertices = new bool[_xCoordinates.Length * _yCoordinates.Length];
            _rightEdges = new bool[_validVertices.Length];
            _downEdges = new bool[_validVertices.Length];
            _distances = new double[_validVertices.Length * 2];
            _previous = new int[_validVertices.Length * 2];
            _sourceOwners = new int[_validVertices.Length * 2];
            _stateGenerations = new int[_validVertices.Length * 2];
            int width = _xCoordinates.Length;
            int height = _yCoordinates.Length;
            int[] blockedPoints = new int[(width + 1) * (height + 1)];
            int[] blockedHorizontalEdges = new int[(width + 1) * (height + 1)];
            int[] blockedVerticalEdges = new int[(width + 1) * (height + 1)];
            foreach (Rect obstacle in _obstacles)
            {
                int left = _xIndices[obstacle.Left];
                int right = _xIndices[obstacle.Right];
                int top = _yIndices[obstacle.Top];
                int bottom = _yIndices[obstacle.Bottom];
                AddCoverageRectangle(blockedPoints, width, left + 1, top + 1, right - 1, bottom - 1);
                AddCoverageRectangle(blockedHorizontalEdges, width, left, top + 1, right - 1, bottom - 1);
                AddCoverageRectangle(blockedVerticalEdges, width, left + 1, top, right - 1, bottom - 1);
            }

            AccumulateCoverage(blockedPoints, width, height);
            AccumulateCoverage(blockedHorizontalEdges, width, height);
            AccumulateCoverage(blockedVerticalEdges, width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int vertex = GetVertex(x, y);
                    int coverageIndex = (y * (width + 1)) + x;
                    _validVertices[vertex] = blockedPoints[coverageIndex] == 0;
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int vertex = GetVertex(x, y);
                    int coverageIndex = (y * (width + 1)) + x;
                    _rightEdges[vertex] = x + 1 < width
                        && _validVertices[vertex]
                        && _validVertices[GetVertex(x + 1, y)]
                        && blockedHorizontalEdges[coverageIndex] == 0;
                    _downEdges[vertex] = y + 1 < height
                        && _validVertices[vertex]
                        && _validVertices[GetVertex(x, y + 1)]
                        && blockedVerticalEdges[coverageIndex] == 0;
                }
            }
        }

        /// <summary>
        /// Starts one parent's outgoing connector network. Lines committed by siblings remain cheap
        /// to reuse, while lines reserved by other parents receive an overlap penalty.
        /// </summary>
        internal void BeginNetwork()
        {
            _networkEdges.Clear();
            _networkSegments.Clear();
        }

        internal IReadOnlyList<Point> FindRoute(LayoutNode parent, LayoutNode child)
        {
            if (
                _constrainedDirection is null
                && TryCreateDirectFacingRoute(parent, child, out IReadOnlyList<Point> directRoute)
            )
            {
                return directRoute;
            }

            List<Port> sources = CreatePorts(parent, child, outgoing: true);
            List<Port> targets = CreatePorts(child, parent, outgoing: false);
            // First treat another semantic network's exact grid edges as unavailable. Only retry
            // with a large overlap penalty when those reservations disconnect the graph entirely.
            IReadOnlyList<Point>? path = FindShortestPath(sources, targets, avoidReservedEdges: true)
                ?? FindShortestPath(sources, targets, avoidReservedEdges: false);
            if (path is not null)
            {
                return path;
            }

            // Overlapping or very closely spaced imported nodes can invalidate every clearance
            // port. Preserve centered edge attachments and a deterministic orthogonal shape until
            // the user separates the cards. Manual routing derives the facing edges from the actual
            // coordinates; only the generated hierarchical layout retains a direction contract.
            if (_constrainedDirection is { } direction)
            {
                Point sourceAnchor = GetDirectionalAnchor(parent.ArrangedBounds, direction, outgoing: true);
                Point targetAnchor = GetDirectionalAnchor(child.ArrangedBounds, direction, outgoing: false);
                return CreateFallbackRoute(sourceAnchor, targetAnchor, direction == NavigationMapDirection.LeftToRight);
            }

            Point sourceCenter = Center(parent.ArrangedBounds);
            Point targetCenter = Center(child.ArrangedBounds);
            bool horizontal = Math.Abs(targetCenter.X - sourceCenter.X)
                >= Math.Abs(targetCenter.Y - sourceCenter.Y);
            Point manualSourceAnchor = horizontal
                ? new Point(
                    targetCenter.X >= sourceCenter.X ? parent.ArrangedBounds.Right : parent.ArrangedBounds.Left,
                    sourceCenter.Y)
                : new Point(
                    sourceCenter.X,
                    targetCenter.Y >= sourceCenter.Y ? parent.ArrangedBounds.Bottom : parent.ArrangedBounds.Top);
            Point manualTargetAnchor = horizontal
                ? new Point(
                    targetCenter.X >= sourceCenter.X ? child.ArrangedBounds.Left : child.ArrangedBounds.Right,
                    targetCenter.Y)
                : new Point(
                    targetCenter.X,
                    targetCenter.Y >= sourceCenter.Y ? child.ArrangedBounds.Top : child.ArrangedBounds.Bottom);
            return CreateFallbackRoute(manualSourceAnchor, manualTargetAnchor, horizontal);
        }

        internal void Commit(IReadOnlyList<Point> points)
        {
            for (int index = 1; index < points.Count; index++)
            {
                Point first = points[index - 1];
                Point second = points[index];
                if (first == second)
                {
                    continue;
                }

                ConnectorSegment segment = ConnectorSegment.Create(first, second);
                _networkSegments.Add(segment);
                _reservedSegments.Add(segment);

                if (
                    first.Y == second.Y
                    && _yIndices.TryGetValue(first.Y, out int row)
                )
                {
                    double minimum = Math.Min(first.X, second.X);
                    double maximum = Math.Max(first.X, second.X);
                    for (int x = 0; x + 1 < _xCoordinates.Length; x++)
                    {
                        if (_xCoordinates[x] < minimum || _xCoordinates[x + 1] > maximum)
                        {
                            continue;
                        }

                        GridEdge edge = GridEdge.Create(GetVertex(x, row), GetVertex(x + 1, row));
                        _networkEdges.Add(edge);
                        _reservedEdges.Add(edge);
                    }
                }
                else if (
                    first.X == second.X
                    && _xIndices.TryGetValue(first.X, out int column)
                )
                {
                    double minimum = Math.Min(first.Y, second.Y);
                    double maximum = Math.Max(first.Y, second.Y);
                    for (int verticalIndex = 0; verticalIndex + 1 < _yCoordinates.Length; verticalIndex++)
                    {
                        if (_yCoordinates[verticalIndex] < minimum || _yCoordinates[verticalIndex + 1] > maximum)
                        {
                            continue;
                        }

                        GridEdge edge = GridEdge.Create(
                            GetVertex(column, verticalIndex),
                            GetVertex(column, verticalIndex + 1)
                        );
                        _networkEdges.Add(edge);
                        _reservedEdges.Add(edge);
                    }
                }
            }
        }

        private List<Port> CreatePorts(LayoutNode owner, LayoutNode destination, bool outgoing)
        {
            if (_constrainedDirection is { } direction)
            {
                return CreateDirectionalPorts(owner, direction, outgoing);
            }

            Rect bounds = owner.ArrangedBounds;
            Point center = Center(bounds);
            Point destinationCenter = Center(destination.ArrangedBounds);
            var ports = new List<Port>
            {
                new(new Point(bounds.Left, center.Y), new Point(bounds.Left - _clearance, center.Y), Axis.Horizontal),
                new(new Point(bounds.Right, center.Y), new Point(bounds.Right + _clearance, center.Y), Axis.Horizontal),
                new(new Point(center.X, bounds.Top), new Point(center.X, bounds.Top - _clearance), Axis.Vertical),
                new(new Point(center.X, bounds.Bottom), new Point(center.X, bounds.Bottom + _clearance), Axis.Vertical),
            };

            // Every Manual card exposes four centered edge ports. Ordering provides deterministic
            // tie-breaking near the destination, while Dijkstra remains free to pick another side
            // when that produces a shorter path or avoids a card/semantic-network reservation.
            return ports
                .Where(port => IsPortAvailable(owner, port))
                .OrderBy(port => Math.Abs(port.Outside.X - destinationCenter.X)
                    + Math.Abs(port.Outside.Y - destinationCenter.Y))
                .ToList();
        }

        private List<Port> CreateDirectionalPorts(
            LayoutNode owner,
            NavigationMapDirection direction,
            bool outgoing)
        {
            Point anchor = GetDirectionalAnchor(owner.ArrangedBounds, direction, outgoing);
            Axis axis = direction == NavigationMapDirection.TopToBottom
                ? Axis.Vertical
                : Axis.Horizontal;
            Vector offset = direction switch
            {
                NavigationMapDirection.TopToBottom when outgoing => new Vector(0D, _clearance),
                NavigationMapDirection.TopToBottom => new Vector(0D, -_clearance),
                NavigationMapDirection.LeftToRight when outgoing => new Vector(_clearance, 0D),
                _ => new Vector(-_clearance, 0D),
            };
            var port = new Port(anchor, anchor + offset, axis);
            return IsPortAvailable(owner, port) ? [port] : [];
        }

        private bool IsPortAvailable(LayoutNode owner, Port port) =>
            port.Outside.X >= 0D
                && port.Outside.Y >= 0D
                && _obstacles
                    .Where((_, index) => !ReferenceEquals(_nodes[index], owner))
                    .All(obstacle => !IsInside(port.Outside, obstacle))
                && IsSegmentAvailableToNetwork(ConnectorSegment.Create(port.Anchor, port.Outside));

        private bool TryCreateDirectFacingRoute(
            LayoutNode source,
            LayoutNode target,
            out IReadOnlyList<Point> route)
        {
            const double epsilon = 0.0001D;
            Point sourceCenter = Center(source.ArrangedBounds);
            Point targetCenter = Center(target.ArrangedBounds);
            Point sourceAnchor;
            Point targetAnchor;
            if (Math.Abs(sourceCenter.X - targetCenter.X) < epsilon)
            {
                bool targetIsBelow = targetCenter.Y >= sourceCenter.Y;
                sourceAnchor = new Point(
                    sourceCenter.X,
                    targetIsBelow ? source.ArrangedBounds.Bottom : source.ArrangedBounds.Top);
                targetAnchor = new Point(
                    targetCenter.X,
                    targetIsBelow ? target.ArrangedBounds.Top : target.ArrangedBounds.Bottom);
                if ((targetIsBelow && sourceAnchor.Y > targetAnchor.Y)
                    || (!targetIsBelow && sourceAnchor.Y < targetAnchor.Y))
                {
                    route = [];
                    return false;
                }
            }
            else if (Math.Abs(sourceCenter.Y - targetCenter.Y) < epsilon)
            {
                bool targetIsRight = targetCenter.X >= sourceCenter.X;
                sourceAnchor = new Point(
                    targetIsRight ? source.ArrangedBounds.Right : source.ArrangedBounds.Left,
                    sourceCenter.Y);
                targetAnchor = new Point(
                    targetIsRight ? target.ArrangedBounds.Left : target.ArrangedBounds.Right,
                    targetCenter.Y);
                if ((targetIsRight && sourceAnchor.X > targetAnchor.X)
                    || (!targetIsRight && sourceAnchor.X < targetAnchor.X))
                {
                    route = [];
                    return false;
                }
            }
            else
            {
                route = [];
                return false;
            }

            // This narrow shortcut fixes the clearance dead zone between two aligned cards. It is
            // intentionally limited to one unobstructed center-to-center segment, so it cannot
            // reintroduce the arbitrary projected endpoints that previously made attachments look
            // off-center. Non-aligned relationships always go through the orthogonal router.
            ConnectorSegment segment = ConnectorSegment.Create(sourceAnchor, targetAnchor);
            bool unobstructed = _nodes
                .Select((node, index) => (node, index))
                .Where(pair => !ReferenceEquals(pair.node, source) && !ReferenceEquals(pair.node, target))
                .All(pair => !SegmentIntersectsRectangleInterior(
                    sourceAnchor,
                    targetAnchor,
                    _obstacles[pair.index]));
            if (!unobstructed || !IsSegmentAvailableToNetwork(segment))
            {
                route = [];
                return false;
            }

            route = [sourceAnchor, targetAnchor];
            return true;
        }

        private bool IsSegmentAvailableToNetwork(ConnectorSegment candidate) =>
            !_reservedSegments.Any(reserved =>
                OverlapsCollinearly(candidate, reserved)
                && !_networkSegments.Any(network => OverlapsCollinearly(candidate, network)));

        private static Point GetDirectionalAnchor(
            Rect bounds,
            NavigationMapDirection direction,
            bool outgoing)
        {
            Point center = Center(bounds);
            return direction switch
            {
                NavigationMapDirection.TopToBottom when outgoing => new Point(center.X, bounds.Bottom),
                NavigationMapDirection.TopToBottom => new Point(center.X, bounds.Top),
                NavigationMapDirection.LeftToRight when outgoing => new Point(bounds.Right, center.Y),
                _ => new Point(bounds.Left, center.Y),
            };
        }

        private static IReadOnlyList<Point> CreateFallbackRoute(
            Point sourceAnchor,
            Point targetAnchor,
            bool horizontal)
        {
            if (horizontal)
            {
                double lane = (sourceAnchor.X + targetAnchor.X) / 2D;
                return SimplifyPath(
                    [sourceAnchor, new Point(lane, sourceAnchor.Y), new Point(lane, targetAnchor.Y), targetAnchor]
                );
            }

            double verticalLane = (sourceAnchor.Y + targetAnchor.Y) / 2D;
            return SimplifyPath(
                [sourceAnchor, new Point(sourceAnchor.X, verticalLane), new Point(targetAnchor.X, verticalLane), targetAnchor]
            );
        }

        private IReadOnlyList<Point>? FindShortestPath(
            IReadOnlyList<Port> sources,
            IReadOnlyList<Port> targets,
            bool avoidReservedEdges)
        {
            if (sources.Count == 0 || targets.Count == 0)
            {
                return null;
            }

            BeginSearch();
            for (int index = 0; index < sources.Count; index++)
            {
                Port port = sources[index];
                if (!TryGetVertex(port.Outside, out int vertex))
                {
                    continue;
                }

                int state = GetState(vertex, port.Axis);
                SetSearchState(state, _clearance, -1, index);
                _queue.Enqueue(state, _clearance);
            }

            var targetsByVertex = new Dictionary<int, List<int>>();
            for (int index = 0; index < targets.Count; index++)
            {
                if (!TryGetVertex(targets[index].Outside, out int vertex))
                {
                    continue;
                }

                if (!targetsByVertex.TryGetValue(vertex, out List<int>? indices))
                {
                    indices = [];
                    targetsByVertex.Add(vertex, indices);
                }

                indices.Add(index);
            }

            double bestCost = double.PositiveInfinity;
            int bestState = -1;
            int bestTarget = -1;
            while (_queue.TryDequeue(out int state, out double queuedCost))
            {
                if (queuedCost != GetDistance(state) || queuedCost > bestCost)
                {
                    continue;
                }

                int vertex = state / 2;
                Axis incoming = (Axis)(state % 2);
                if (targetsByVertex.TryGetValue(vertex, out List<int>? targetIndices))
                {
                    foreach (int targetIndex in targetIndices)
                    {
                        double cost = queuedCost + _clearance;
                        if (incoming != targets[targetIndex].Axis)
                        {
                            cost += BendPenalty;
                        }

                        if (cost < bestCost)
                        {
                            bestCost = cost;
                            bestState = state;
                            bestTarget = targetIndex;
                        }
                    }
                }

                foreach (
                    (int neighbor, Axis axis, double length) in GetNeighbors(
                        vertex,
                        avoidReservedEdges
                    )
                )
                {
                    int nextState = GetState(neighbor, axis);
                    double cost = queuedCost + GetSegmentCost(vertex, neighbor, length);
                    if (incoming != axis)
                    {
                        cost += BendPenalty;
                    }

                    if (cost >= GetDistance(nextState))
                    {
                        continue;
                    }

                    SetSearchState(nextState, cost, state, _sourceOwners[state]);
                    _queue.Enqueue(nextState, cost);
                }
            }

            if (bestState < 0 || _sourceOwners[bestState] < 0)
            {
                return null;
            }

            var vertices = new List<Point>();
            for (int state = bestState; state >= 0; state = _previous[state])
            {
                vertices.Add(GetPoint(state / 2));
            }

            vertices.Reverse();
            Port source = sources[_sourceOwners[bestState]];
            Port target = targets[bestTarget];
            var result = new List<Point>(vertices.Count + 4) { source.Anchor, source.Outside };
            result.AddRange(vertices);
            result.Add(target.Outside);
            result.Add(target.Anchor);
            return SimplifyPath(result);
        }

        private void BeginSearch()
        {
            _queue.Clear();
            if (_searchGeneration == int.MaxValue)
            {
                Array.Clear(_stateGenerations, 0, _stateGenerations.Length);
                _searchGeneration = 1;
            }
            else
            {
                _searchGeneration++;
            }
        }

        private double GetDistance(int state) =>
            _stateGenerations[state] == _searchGeneration
                ? _distances[state]
                : double.PositiveInfinity;

        private void SetSearchState(int state, double distance, int previous, int sourceOwner)
        {
            _stateGenerations[state] = _searchGeneration;
            _distances[state] = distance;
            _previous[state] = previous;
            _sourceOwners[state] = sourceOwner;
        }

        private IEnumerable<(int Neighbor, Axis Axis, double Length)> GetNeighbors(
            int vertex,
            bool avoidReservedEdges)
        {
            int width = _xCoordinates.Length;
            int x = vertex % width;
            int y = vertex / width;
            if (x > 0)
            {
                int neighbor = GetVertex(x - 1, y);
                if (
                    _rightEdges[neighbor]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Horizontal, _xCoordinates[x] - _xCoordinates[x - 1]);
                }
            }

            if (x + 1 < width)
            {
                int neighbor = GetVertex(x + 1, y);
                if (
                    _rightEdges[vertex]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Horizontal, _xCoordinates[x + 1] - _xCoordinates[x]);
                }
            }

            if (y > 0)
            {
                int neighbor = GetVertex(x, y - 1);
                if (
                    _downEdges[neighbor]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Vertical, _yCoordinates[y] - _yCoordinates[y - 1]);
                }
            }

            if (y + 1 < _yCoordinates.Length)
            {
                int neighbor = GetVertex(x, y + 1);
                if (
                    _downEdges[vertex]
                    && CanTraverseReservedEdge(vertex, neighbor, avoidReservedEdges)
                )
                {
                    yield return (neighbor, Axis.Vertical, _yCoordinates[y + 1] - _yCoordinates[y]);
                }
            }
        }

        private bool CanTraverseReservedEdge(
            int firstVertex,
            int secondVertex,
            bool avoidReservedEdges)
        {
            if (!avoidReservedEdges)
            {
                return true;
            }

            GridEdge edge = GridEdge.Create(firstVertex, secondVertex);
            return _networkEdges.Contains(edge) || !_reservedEdges.Contains(edge);
        }

        private static void AddCoverageRectangle(
            int[] difference,
            int width,
            int left,
            int top,
            int right,
            int bottom)
        {
            if (left > right || top > bottom)
            {
                return;
            }

            int stride = width + 1;
            difference[(top * stride) + left]++;
            difference[(top * stride) + right + 1]--;
            difference[((bottom + 1) * stride) + left]--;
            difference[((bottom + 1) * stride) + right + 1]++;
        }

        private static void AccumulateCoverage(int[] difference, int width, int height)
        {
            int stride = width + 1;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = (y * stride) + x;
                    int left = x == 0 ? 0 : difference[index - 1];
                    int top = y == 0 ? 0 : difference[index - stride];
                    int diagonal = x == 0 || y == 0 ? 0 : difference[index - stride - 1];
                    difference[index] += left + top - diagonal;
                }
            }
        }

        private double GetSegmentCost(int firstVertex, int secondVertex, double length)
        {
            GridEdge edge = GridEdge.Create(firstVertex, secondVertex);
            if (_networkEdges.Contains(edge))
            {
                return length * ReusedLengthFactor;
            }

            return _reservedEdges.Contains(edge)
                ? (length * ReservedLengthFactor) + ReservedEdgePenalty
                : length;
        }

        private bool TryGetVertex(Point point, out int vertex)
        {
            if (_xIndices.TryGetValue(point.X, out int x) && _yIndices.TryGetValue(point.Y, out int y))
            {
                vertex = GetVertex(x, y);
                return _validVertices[vertex];
            }

            vertex = -1;
            return false;
        }

        private int GetVertex(int x, int y) => (y * _xCoordinates.Length) + x;

        private Point GetPoint(int vertex) =>
            new(_xCoordinates[vertex % _xCoordinates.Length], _yCoordinates[vertex / _xCoordinates.Length]);

        private static int GetState(int vertex, Axis axis) => (vertex * 2) + (int)axis;

        private static bool IsInside(Point point, Rect bounds)
        {
            const double epsilon = 0.0001D;
            return point.X > bounds.Left + epsilon
                && point.X < bounds.Right - epsilon
                && point.Y > bounds.Top + epsilon
                && point.Y < bounds.Bottom - epsilon;
        }

        private static Rect Inflate(Rect bounds, double amount)
        {
            bounds.Inflate(amount, amount);
            return bounds;
        }

        private readonly record struct Port(Point Anchor, Point Outside, Axis Axis);

        private readonly record struct GridEdge(int First, int Second)
        {
            internal static GridEdge Create(int first, int second) =>
                first <= second ? new GridEdge(first, second) : new GridEdge(second, first);
        }

        private enum Axis
        {
            Horizontal,
            Vertical,
        }
    }

    /// <summary>
    /// Binary heap used instead of <c>PriorityQueue&lt;T&gt;</c>, which is unavailable on the .NET
    /// Framework target. Duplicate states are intentional; stale entries are ignored on dequeue.
    /// </summary>
    private sealed class MinimumQueue
    {
        private readonly List<(int State, double Cost)> _items = [];

        internal void Clear() => _items.Clear();

        internal void Enqueue(int state, double cost)
        {
            _items.Add((state, cost));
            int index = _items.Count - 1;
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_items[parent].Cost <= cost)
                {
                    break;
                }

                _items[index] = _items[parent];
                index = parent;
            }

            _items[index] = (state, cost);
        }

        internal bool TryDequeue(out int state, out double cost)
        {
            if (_items.Count == 0)
            {
                state = -1;
                cost = 0D;
                return false;
            }

            (state, cost) = _items[0];
            (int State, double Cost) tail = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count == 0)
            {
                return true;
            }

            int index = 0;
            while (true)
            {
                int left = (index * 2) + 1;
                if (left >= _items.Count)
                {
                    break;
                }

                int right = left + 1;
                int child = right < _items.Count && _items[right].Cost < _items[left].Cost ? right : left;
                if (_items[child].Cost >= tail.Cost)
                {
                    break;
                }

                _items[index] = _items[child];
                index = child;
            }

            _items[index] = tail;
            return true;
        }
    }
}
