// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Media.Media3D;

namespace Wpf.Ui.Extensions.Wpf;

public static class VisualTreeHelperExtensions
{
    extension(DependencyObject? child)
    {
        public T? FindAncestor<T>(int maxDepth = int.MaxValue)
            where T : DependencyObject
        {
            if (child is null || maxDepth <= 0)
            {
                return null;
            }

            var depth   = 0;
            var current = child;

            while (current is not null && depth < maxDepth)
            {
                var parent = current is Visual or Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);

                if (parent is T t)
                {
                    return t;
                }

                current = parent;
                depth++;
            }

            return null;
        }

        public T? FindDescendant<T>(Predicate<T>? predicate = null, int maxDepth = int.MaxValue)
            where T : DependencyObject
        {
            return FindDescendantCore(child, predicate, maxDepth, 0);
        }
    }

    private static T? FindDescendantCore<T>(DependencyObject? root, Predicate<T>? predicate, int maxDepth, int currentDepth)
        where T : DependencyObject
    {
        if (root is null || currentDepth >= maxDepth)
        {
            return null;
        }

        try
        {
            // If the root is a Visual or Visual3D, traverse the visual tree
            if (root is Visual or Visual3D)
            {
                var childrenCount = VisualTreeHelper.GetChildrenCount(root);

                for (var i = 0; i < childrenCount; i++)
                {
                    var c = VisualTreeHelper.GetChild(root, i);

                    if (c is T t && (predicate is null || predicate(t)))
                    {
                        return t;
                    }

                    T? found = FindDescendantCore(c, predicate, maxDepth, currentDepth + 1);
                    if (found is not null)
                    {
                        return found;
                    }
                }

                return null;
            }

            // For non-visual elements, fall back to logical tree traversal
            foreach (var logicalChild in LogicalTreeHelper.GetChildren(root))
            {
                if (logicalChild is not DependencyObject c)
                {
                    continue;
                }

                if (c is T t && (predicate is null || predicate(t)))
                {
                    return t;
                }

                T? found = FindDescendantCore(c, predicate, maxDepth, currentDepth + 1);
                if (found is not null)
                {
                    return found;
                }
            }
        }
        catch
        {
            // defensive: ignore traversal errors and return null
        }

        return null;
    }
}