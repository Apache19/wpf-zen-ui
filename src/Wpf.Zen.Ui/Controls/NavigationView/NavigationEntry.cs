// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Joins a source item to navigation runtime state without making the generated visual container
/// the item's identity.
/// </summary>
/// <remarks>
/// Data entries read semantic values from <see cref="INavigationItem"/>. A directly supplied
/// <see cref="NavigationViewItem"/> is also accepted as a declarative container item, but generated
/// containers are attached only as transient presentation state. This distinction lets navigation
/// indexes exist before a template has generated any controls.
/// </remarks>
internal sealed class NavigationEntry
{
    private readonly string _fallbackId = Guid.NewGuid().ToString("n");

    internal NavigationEntry(object item, bool isMenuElement)
    {
        Item                         = item;
        IsMenuElement                = isMenuElement;
        // A direct control can be activated before registration. Adopt that initial state once;
        // subsequent container associations project the state owned by this entry.
        IsActive                     = item is NavigationViewItem { IsActive: true };
        LastKnownTargetPageType      = TargetPageType;
        LastKnownNavigationCacheMode = NavigationCacheMode;
    }

    internal object Item { get; }

    internal NavigationViewItem? Container { get; set; }

    /// <summary>
    /// Gets or sets the source-tree parent, including branches without generated containers.
    /// </summary>
    internal NavigationEntry? Parent { get; set; }

    /// <summary>
    /// Gets a value indicating whether this entry is active independently of container realization.
    /// </summary>
    internal bool IsActive { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether an active descendant exists, even without a
    /// container.
    /// </summary>
    internal bool IsDescendantActive { get; set; }

    internal bool IsMenuElement { get; set; }

    /// <summary>
    /// Gets or sets the page type observed when cache state was last synchronized.
    /// </summary>
    /// <remarks>
    /// <see cref="INotifyPropertyChanged"/> reports only the new value. Retaining the previous
    /// semantic value on the entry gives NavigationView enough information to remove a cache entry
    /// for an old page type instead of leaving an unreachable Required instance behind.
    /// </remarks>
    internal Type? LastKnownTargetPageType { get; set; }

    /// <summary>
    /// Gets or sets the cache policy observed when cache state was last synchronized.
    /// </summary>
    internal NavigationCacheMode LastKnownNavigationCacheMode { get; set; }

    internal string Id => Item switch
    {
        INavigationItem dataItem when !string.IsNullOrWhiteSpace(dataItem.Id) => dataItem.Id,
        NavigationViewItem container                                          => container.Id,
        _ when Container is not null                                          => Container.Id,
        _                                                                     => _fallbackId,
    };

    internal object? Content
    {
        get => Item switch
        {
            INavigationItem dataItem     => dataItem.Content,
            NavigationViewItem container => container.Header,
            _                            => Item,
        };
        set
        {
            switch (Item)
            {
                case INavigationItem dataItem:
                    dataItem.Content = value;
                    break;
                case NavigationViewItem container:
                    container.SetValue(System.Windows.Controls.HeaderedItemsControl.HeaderProperty, value);
                    break;
            }
        }
    }

    internal Type? TargetPageType => Item switch
    {
        INavigationItem dataItem     => dataItem.TargetPageType,
        NavigationViewItem container => container.TargetPageType,
        _                            => Container?.TargetPageType,
    };

    internal string? TargetPageTag => Item switch
    {
        INavigationItem dataItem     => dataItem.TargetPageTag,
        NavigationViewItem container => container.TargetPageTag,
        _                            => Container?.TargetPageTag,
    };

    internal NavigationCacheMode NavigationCacheMode => Item switch
    {
        INavigationItem dataItem     => dataItem.NavigationCacheMode,
        NavigationViewItem container => container.NavigationCacheMode,
        _                            => Container?.NavigationCacheMode ?? NavigationCacheMode.Disabled,
    };

    internal IEnumerable? Children => Item switch
    {
        INavigationItem dataItem                               => dataItem.Children,
        NavigationViewItem { ItemsSource: not null } container => container.ItemsSource,
        NavigationViewItem container                           => container.Items,
        _ when Container is { ItemsSource: not null }          => Container.ItemsSource,
        _                                                      => Container?.Items,
    };

    internal string SearchText
    {
        get
        {
            string? explicitText = Item switch
            {
                INavigationItem dataItem     => dataItem.SearchText,
                NavigationViewItem container => container.SearchText,
                _                            => Container?.SearchText,
            };

            return explicitText ?? Content?.ToString() ?? string.Empty;
        }
    }

    internal void Activate(NavigationView navigationView)
    {
        SetActiveState(navigationView, true);
    }

    internal void Deactivate(NavigationView navigationView)
    {
        SetActiveState(navigationView, false);
    }

    /// <summary>
    /// Commits activation before projecting it into containers and their extension hooks.
    /// </summary>
    /// <remarks>
    /// Both navigation and public container activation use this entry as the state owner. Container
    /// release only clears the visual projection, so unrealized items retain their semantic state.
    /// </remarks>
    private void SetActiveState(NavigationView navigationView, bool isActive)
    {
        object? session = navigationView.NavigationSessionIdentity;

        IsActive = isActive;

        navigationView.SynchronizeActiveNavigationBranches();

        if (IsActive == isActive && ReferenceEquals(session, navigationView.NavigationSessionIdentity))
        {
            Container?.ApplyActiveState(navigationView, isActive);
        }
    }
}
