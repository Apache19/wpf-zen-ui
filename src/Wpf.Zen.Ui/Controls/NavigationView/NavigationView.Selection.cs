// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <content>Projects active menu branches and reveals their currently visible headers.</content>
public partial class NavigationView
{
    private DispatcherOperation? _bringActiveBranchIntoViewOperation;

    private long _activeBranchesRevision;

    /// <summary>
    /// Projects selection through the indexed source hierarchy, including unrealized descendants.
    /// </summary>
    /// <remarks>
    /// Menu ancestry describes shortcut presentation, not breadcrumb ancestry. Keeping this state
    /// on entries allows a collapsed parent to indicate a selected data item without generating its
    /// children or expanding it. Rebuilding the index repeats this projection so moving or removing
    /// shortcuts cannot leave an old branch highlighted.
    /// </remarks>
    internal void SynchronizeActiveNavigationBranches()
    {
        var revision  = ++_activeBranchesRevision;
        var entries   = _entriesByItem.Values.ToArray();
        var ancestors = new HashSet<NavigationEntry>();

        foreach (var activeEntry in entries)
        {
            if (!activeEntry.IsActive)
            {
                continue;
            }

            // Public item activation does not navigate or deactivate other entries. Summarize all
            // active entries so deactivating one item cannot erase another item's branch state.
            var parent = activeEntry.Parent;
            while (parent is not null && ancestors.Add(parent))
            {
                parent = parent.Parent;
            }
        }

        // Publish all semantic summaries first: updating a container can synchronously propagate
        // to its parent, which must already observe the new source-tree state.
        foreach (var entry in entries)
        {
            entry.IsDescendantActive = ancestors.Contains(entry);
        }

        foreach (var entry in entries)
        {
            if (revision != _activeBranchesRevision)
            {
                break;
            }

            entry.Container?.UpdateDescendantActiveState();
        }
    }

    /// <summary>
    /// Coalesces navigation and container-generation requests until layout has created the visible
    /// header surfaces. The callback reads current selection so rapid navigation cannot reveal an
    /// obsolete destination. Unloaded hosts defer revealing until their next Loaded event.
    /// </summary>
    private void QueueActiveNavigationBranchIntoView()
    {
        if (!IsLoaded || _bringActiveBranchIntoViewOperation is { Status: DispatcherOperationStatus.Pending })
        {
            return;
        }

        _bringActiveBranchIntoViewOperation = Dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(
                () =>
                {
                    _bringActiveBranchIntoViewOperation = null;

                    if (!IsLoaded || _currentEntry is not { IsActive: true } activeEntry
                        || !_entriesByItem.Values.Contains(activeEntry))
                    {
                        return;
                    }

                    // IsVisible excludes collapsed inline descendants and source containers retained
                    // behind a detached menu. Walking toward the root therefore reveals the deepest
                    // currently displayed representative without changing expansion or keyboard focus.
                    for (var entry = activeEntry; entry is not null; entry = entry.Parent)
                    {
                        if (entry.Container?.TryBringHeaderIntoView() == true)
                        {
                            break;
                        }
                    }
                }
            )
        );
    }
}