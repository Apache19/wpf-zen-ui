// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

namespace Wpf.Zen.Ui.Infrastructure;

/// <summary>
/// Owns a set of property observers for one controller, used only on its owning dispatcher.
/// </summary>
/// <remarks>
/// Retain this collection for as long as notifications are needed. All sources and operations
/// must belong to the same dispatcher. Initial binding transfer and removal do not invoke handlers.
/// Handlers receive the observed source as sender.
/// Source values, bindings and metadata are preserved.
/// Call <see cref="Clear"/> when the owner detaches; the collection can subsequently be reused.
/// </remarks>
public sealed class DependencyPropertySubscriptions
{
    private readonly List<DependencyPropertyObserver> _observers = [];

    /// <summary>
    /// Creates an empty subscription collection for an owner's property notifications.
    /// </summary>
    public DependencyPropertySubscriptions()
    {
    }

    /// <summary>
    /// Observes a source/property/handler combination once; repeated additions are idempotent.
    /// </summary>
    /// <param name="source">The dependency object whose effective value is observed.</param>
    /// <param name="property">The dependency property to observe.</param>
    /// <param name="handler">Receives subsequent effective-value changes.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    public void Add(DependencyObject source, DependencyProperty property, EventHandler handler)
    {
        foreach (var observer in _observers)
        {
            if (observer.Matches(source, property, handler))
            {
                return;
            }
        }

        var subscription = new DependencyPropertyObserver(source, property, handler);
        _observers.Add(subscription);
    }

    /// <summary>
    /// Removes a matching subscription without notifying its handler; missing entries are harmless.
    /// </summary>
    /// <param name="source">The source supplied when the subscription was added.</param>
    /// <param name="property">The property supplied when the subscription was added.</param>
    /// <param name="handler">The handler to remove, matched by delegate equality.</param>
    public void Remove(DependencyObject source, DependencyProperty property, EventHandler handler)
    {
        for (var index = _observers.Count - 1; index >= 0; index--)
        {
            var observer = _observers[index];
            if (observer.Matches(source, property, handler))
            {
                observer.Detach();
                _observers.RemoveAt(index);
                return;
            }
        }
    }

    /// <summary>
    /// Detaches all observers without callbacks. The collection can then be reused on reattachment.
    /// </summary>
    public void Clear()
    {
        foreach (var observer in _observers)
        {
            observer.Detach();
        }

        _observers.Clear();
    }
}