// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Windows.Data;

namespace Wpf.Zen.Ui.Infrastructure;

/// <summary>
/// Observes effective dependency-property values on the source dispatcher
/// through a private binding.
/// </summary>
/// <remarks>
/// The owner must retain this observer while notifications are needed. WPF holds dependency links
/// weakly, so an abandoned owner/observer/source graph remains collectible. Initial transfer and
/// detachment do not notify the handler. The source's own value, binding and metadata
/// are untouched.
/// </remarks>
internal sealed class DependencyPropertyObserver : DependencyObject
{
    private static readonly DependencyProperty ValueProperty
        = DependencyProperty.Register(
            "Value", typeof(object), typeof(DependencyPropertyObserver),
            new PropertyMetadata(null, OnValueChanged)
        );

    private DependencyObject? _source;
    private EventHandler?     _handler;

    /// <summary>
    /// Establishes observation without invoking a partially initialized owner.
    /// </summary>
    internal DependencyPropertyObserver(
        DependencyObject   source,
        DependencyProperty property,
        EventHandler       handler)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (property is null)
        {
            throw new ArgumentNullException(nameof(property));
        }

        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        source.VerifyAccess();

        Property = property;

        var binding = new Binding
        {
            Source = source,
            Path   = new PropertyPath(property),
            Mode   = BindingMode.OneWay,
        };

        BindingOperations.SetBinding(this, ValueProperty, binding);

        _source  = source;
        _handler = handler;
    }

    /// <summary>
    /// Identifies the observed property for subscription matching.
    /// </summary>
    internal DependencyProperty Property { get; }

    /// <summary>
    /// Tests source identity, property identity and delegate equality
    /// for an active subscription.
    /// </summary>
    internal bool Matches(DependencyObject source, DependencyProperty property, EventHandler handler)
    {
        return ReferenceEquals(_source, source) && Property == property && _handler == handler;
    }

    /// <summary>
    /// Stops notifications and releases the source and handler. Repeated calls are harmless.
    /// </summary>
    internal void Detach()
    {
        VerifyAccess();
        // ClearBinding changes Value to its default. Disable delivery before that change can run.
        _handler = null;
        _source  = null;
        BindingOperations.ClearBinding(this, ValueProperty);
    }

    /// <summary>
    /// Delivers subsequent effective-value changes with the observed source as sender.
    /// </summary>
    private static void OnValueChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var observer = (DependencyPropertyObserver)sender;
        observer._handler?.Invoke(observer._source, EventArgs.Empty);
    }
}