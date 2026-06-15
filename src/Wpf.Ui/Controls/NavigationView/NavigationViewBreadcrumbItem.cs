// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

// Based on Windows UI Library
// Copyright(c) Microsoft Corporation.All rights reserved.

// ReSharper disable once CheckNamespace
namespace Wpf.Ui.Controls;

internal class NavigationViewBreadcrumbItem : DependencyObject
{
    /// <summary>Identifies the <see cref="Content"/> dependency property.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content),
        typeof(object),
        typeof(NavigationViewBreadcrumbItem),
        new PropertyMetadata(null)
    );

    public NavigationViewBreadcrumbItem(INavigationViewItem item)
    {
        NavigationKey = new NavigationKey(
            item.TargetPageType
                ?? throw new InvalidOperationException("Breadcrumb items must define a target page type."),
            item.TargetPageVariant
        );
        SourceItem = item;
        Content = item.Content;
    }

    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public NavigationKey NavigationKey { get; }

    public INavigationViewItem SourceItem { get; }

    public void UpdateFromSource()
    {
        SetCurrentValue(ContentProperty, SourceItem.Content);
    }
}
