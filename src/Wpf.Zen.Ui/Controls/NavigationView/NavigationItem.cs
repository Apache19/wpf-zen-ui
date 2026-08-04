// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Runtime.CompilerServices;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Provides the default data model for a presentation-independent
/// <see cref="NavigationView"/> item.
/// </summary>
/// <remarks>
/// Applications may use this type directly or implement <see cref="INavigationItem"/> on their
/// own view-model type. This class intentionally derives from neither <see cref="DependencyObject"/>
/// nor a WPF control: it can safely live in a view model, be unit-tested without a dispatcher, and
/// be presented simultaneously by a <see cref="NavigationView"/> and a <see cref="BreadcrumbBar"/>.
/// </remarks>
public class NavigationItem : INavigationItem, INotifyPropertyChanged
{
    private string              _id;
    private object?             _content;
    private object?             _icon;
    private string?             _targetPageTag;
    private Type?               _targetPageType;
    private NavigationCacheMode _navigationCacheMode;
    private IEnumerable?        _children;
    private string?             _searchText;

    /// <summary>Initializes a new instance of the <see cref="NavigationItem"/> class.</summary>
    public NavigationItem()
    {
        _id = Guid.NewGuid().ToString("n");
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationItem"/> class.</summary>
    /// <param name="content">Content used by the default presentation.</param>
    /// <param name="targetPageType">Page type activated by this item.</param>
    public NavigationItem(object? content, Type? targetPageType = null)
        : this()
    {
        Content        = content;
        TargetPageType = targetPageType;
    }

    /// <summary>Initializes a new instance of the <see cref="NavigationItem"/> class.</summary>
    /// <param name="content">Content used by the default presentation.</param>
    /// <param name="icon">The Fluent symbol wrapped as reusable icon data.</param>
    /// <param name="targetPageType">Page type activated by this item.</param>
    /// <param name="children">Optional semantic child collection.</param>
    public NavigationItem(
        object?       content,
        SymbolRegular icon,
        Type?         targetPageType = null,
        IEnumerable?  children       = null)
        : this(content, targetPageType)
    {
        Icon = new FluentSystemIcon(icon);
        Children = children;
    }

    /// <inheritdoc />
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <inheritdoc />
    public object? Content
    {
        get => _content;
        set => SetProperty(ref _content, value);
    }

    /// <inheritdoc />
    public object? Icon
    {
        get => _icon;
        set => SetProperty(ref _icon, value);
    }

    /// <inheritdoc />
    public string? TargetPageTag
    {
        get => _targetPageTag;
        set => SetProperty(ref _targetPageTag, value);
    }

    /// <inheritdoc />
    public Type? TargetPageType
    {
        get => _targetPageType;
        set => SetProperty(ref _targetPageType, value);
    }

    /// <inheritdoc />
    public NavigationCacheMode NavigationCacheMode
    {
        get => _navigationCacheMode;
        set => SetProperty(ref _navigationCacheMode, value);
    }

    /// <inheritdoc />
    public IEnumerable? Children
    {
        get => _children;
        set => SetProperty(ref _children, value);
    }

    /// <inheritdoc />
    public string? SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    /// <summary>Occurs when a mutable navigation value changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Returns the textual form used when no explicit data template is supplied.</summary>
    public override string ToString() => Content?.ToString() ?? string.Empty;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
