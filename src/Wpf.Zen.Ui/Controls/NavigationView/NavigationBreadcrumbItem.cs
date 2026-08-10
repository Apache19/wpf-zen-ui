// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.ComponentModel;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Represents one non-visual segment in a <see cref="NavigationView"/> navigation path.
/// </summary>
/// <remarks>
/// <para>
/// A navigation menu can contain either application data or a directly declared
/// <see cref="NavigationViewItem"/> control. Exposing either object directly to a breadcrumb
/// surface gives the collection two different meanings and, for a control, violates WPF's rule
/// that a <see cref="System.Windows.Media.Visual"/> can have only one visual parent.
/// </para>
/// <para>
/// This type is therefore the stable data boundary between navigation and presentation.
/// <see cref="SourceItem"/> preserves application identity, while <see cref="Content"/> supplies
/// a value that a breadcrumb can present without adopting a live menu visual. Applications can
/// target this type from a <see cref="DataTemplate"/> and bind through <see cref="SourceItem"/>
/// when richer source-specific presentation is required.
/// </para>
/// </remarks>
public sealed class NavigationBreadcrumbItem : INotifyPropertyChanged
{
    private readonly NavigationEntry _entry;

    internal NavigationBreadcrumbItem(NavigationEntry entry)
    {
        _entry = entry;
    }

    /// <summary>
    /// Gets the original application item represented by this path segment.
    /// </summary>
    /// <remarks>
    /// This value preserves reference identity and can be a directly declared
    /// <see cref="NavigationViewItem"/>. It is intended for identity checks and template bindings;
    /// it should not be inserted directly into another visual tree when it is a WPF element.
    /// </remarks>
    public object SourceItem => _entry.Item;

    /// <summary>
    /// Gets presentation content that can safely be hosted by a breadcrumb item.
    /// </summary>
    /// <remarks>
    /// Non-visual content is preserved. If the navigation header itself is a WPF element, the
    /// textual search representation is returned instead, because reusing that element would
    /// transfer or duplicate its visual parent. The property can change when the source navigation
    /// item raises <see cref="INotifyPropertyChanged.PropertyChanged"/>.
    /// </remarks>
    public object Content
    {
        get
        {
            object? content = _entry.Content;
            return content is not UIElement and not null ? content : _entry.SearchText;
        }
    }

    /// <summary>
    /// Gets the text used by navigation search and as the fallback breadcrumb representation.
    /// </summary>
    public string Text => _entry.SearchText;

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    internal NavigationEntry NavigationEntry => _entry;

    /// <summary>
    /// Notifies breadcrumb bindings after the underlying navigation source changes.
    /// </summary>
    /// <remarks>
    /// NavigationEntry deliberately remains the authoritative semantic model. Keeping notification
    /// forwarding here avoids making presentation state part of that internal navigation object.
    /// </remarks>
    internal void NotifyPresentationChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Content)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
    }

    /// <inheritdoc />
    public override string ToString() => Content.ToString() ?? string.Empty;
}
