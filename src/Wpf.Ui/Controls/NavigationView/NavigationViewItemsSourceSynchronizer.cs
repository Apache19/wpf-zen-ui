// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Collections.Specialized;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal static class NavigationViewItemsSourceSynchronizer
{
    public static void SyncCollectionFromSource(
        IList collection,
        object? source,
        NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
            {
                if (e.NewItems is not null)
                {
                    foreach (var item in e.NewItems)
                    {
                        _ = collection.Add(item);
                    }
                }

                break;
            }

            // #1661
            case NotifyCollectionChangedAction.Remove:
            {
                if (e.OldItems is not null)
                {
                    foreach (var item in e.OldItems)
                    {
                        collection.Remove(item);
                    }
                }

                break;
            }

            case NotifyCollectionChangedAction.Move:
            {
                var moveItem = collection[e.OldStartingIndex];
                collection.RemoveAt(e.OldStartingIndex);
                collection.Insert(e.NewStartingIndex, moveItem);
                break;
            }

            case NotifyCollectionChangedAction.Replace:
            {
                if (e.OldItems is not null)
                {
                    for (int i = 0; i < e.OldItems.Count; i++)
                    {
                        collection.RemoveAt(e.OldStartingIndex);
                    }
                }

                if (e.NewItems is not null)
                {
                    for (int i = 0; i < e.NewItems.Count; i++)
                    {
                        collection.Insert(e.NewStartingIndex + i, e.NewItems[i]);
                    }
                }

                break;
            }

            case NotifyCollectionChangedAction.Reset:
            {
                collection.Clear();
                if (source is IEnumerable sourceItems and not string)
                {
                    foreach (var item in sourceItems)
                    {
                        _ = collection.Add(item);
                    }
                }

                break;
            }
        }
    }
}
