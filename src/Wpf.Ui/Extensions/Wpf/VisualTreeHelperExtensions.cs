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
                var parent = current.GetParent();

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

        public DependencyObject? GetParent()
        {
            try
            {
                // VisualTreeHelper.GetParent() only works with Visual or Visual3D types
                if (child is Visual or Visual3D)
                {
                    var p = VisualTreeHelper.GetParent(child);
                    if (p != null)
                    {
                        return p;
                    }
                }

                // LogicalTreeHelper works with all DependencyObject types
                if (child is not (Visual or Visual3D or null))
                {
                    var lp = LogicalTreeHelper.GetParent(child);
                    if (lp != null)
                    {
                        return lp;
                    }
                }
            }
            catch
            {
                // defensive: ignore traversal errors
            }

            return child switch
            {
                // Last resort:
                // check FrameworkElement and FrameworkContentElement (including TemplatedParent)
                FrameworkElement fe         => fe.Parent ?? fe.TemplatedParent,
                FrameworkContentElement fce => fce.Parent ?? fce.TemplatedParent,
                _                           => null
            };
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