// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

namespace Wpf.Ui;

/// <summary>
/// Extends <see cref="WeakEventManager"/> to support weak subscriptions for
/// arbitrary CLR events using generic event source and event argument types.
/// </summary>
/// <typeparam name="TSource">
/// The type of the object that raises the event.
/// </typeparam>
/// <typeparam name="TEventArgs">
/// The type of the event data associated with the event.
/// </typeparam>
/// <remarks>
/// This class provides a reusable alternative to creating a dedicated
/// <see cref="WeakEventManager"/> implementation for every event.
///
/// It enables application-wide or long-lived services to expose events that
/// can be safely consumed by short-lived objects such as windows, controls,
/// and view models without creating strong references that could lead to
/// memory leaks.
///
/// The manager automatically starts and stops listening to the event based on
/// whether listeners are present, preserving the standard weak-event pattern
/// behavior while removing the need for per-event manager classes.
/// </remarks>
public sealed class GenericWeakEventManager<TSource, TEventArgs> : WeakEventManager
    where TSource : class
    where TEventArgs : EventArgs
{
    private readonly Dictionary<string, EventInfo> _events = [];

    private GenericWeakEventManager() { }

    /// <summary>
    /// Adds a weak event handler to the specified event.
    /// </summary>
    /// <param name="source">The event source.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="handler">The event handler.</param>
    public static void AddHandler(TSource source, string eventName, EventHandler<TEventArgs> handler)
    {
        CurrentManager.ProtectedAddHandler(new EventKey(source, eventName), handler);
    }

    /// <summary>
    /// Removes a weak event handler from the specified event.
    /// </summary>
    /// <param name="source">The event source.</param>
    /// <param name="eventName">The name of the event.</param>
    /// <param name="handler">The event handler.</param>
    public static void RemoveHandler(TSource source, string eventName, EventHandler<TEventArgs> handler)
    {
        CurrentManager.ProtectedRemoveHandler(new EventKey(source, eventName), handler);
    }

    private static GenericWeakEventManager<TSource, TEventArgs> CurrentManager
    {
        get
        {
            var manager = (GenericWeakEventManager<TSource, TEventArgs>)GetCurrentManager(
                typeof(GenericWeakEventManager<TSource, TEventArgs>)
            );

            if (manager == null)
            {
                manager = new GenericWeakEventManager<TSource, TEventArgs>();
                SetCurrentManager(typeof(GenericWeakEventManager<TSource, TEventArgs>), manager);
            }

            return manager;
        }
    }

    protected override void StartListening(object source)
    {
        var key = (EventKey)source;

        if (!_events.TryGetValue(key.EventName, out var eventInfo))
        {
            eventInfo = typeof(TSource).GetEvent(key.EventName)!;
            _events[key.EventName] = eventInfo;
        }

        eventInfo.AddEventHandler(key.Source, OnEvent);
    }

    protected override void StopListening(object source)
    {
        var key = (EventKey)source;
        _events[key.EventName].RemoveEventHandler(key.Source, OnEvent);
    }

    private void OnEvent(object? sender, TEventArgs e)
    {
        DeliverEvent(sender, e);
    }

    private sealed record EventKey(object Source, string EventName);
}