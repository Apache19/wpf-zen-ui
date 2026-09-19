// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using Expression = System.Linq.Expressions.Expression;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

/// <summary>
/// Borrows the current thread's existing WPF TSF manager through a compiled property chain.
/// </summary>
internal static class WpfTextServicesAccessor
{
    private static readonly Lazy<Func<object?>?> Accessor =
        WpfReflectionBridge.Create("TextServices", CreateAccessor);

    /// <summary>
    /// Reads the calling thread's live manager, or null when WPF has not initialized TSF or the
    /// internal bridge is unavailable. Ownership and activation remain with WPF.
    /// </summary>
    internal static object? TryGetThreadManager()
    {
        try
        {
            return Accessor.Value?.Invoke();
        }
        catch (Exception exception) when (WpfReflectionBridge.IsUnavailable(exception))
        {
            return null;
        }
    }

    /// <summary>
    /// Resolves WPF's internal properties once, preserving its lazy thread-local store behavior.
    /// </summary>
    private static Func<object?>? CreateAccessor()
    {
        // These members are cached once because candidate updates can be frequent. Merely reading
        // the thread-local store does not instantiate TextServicesHost; a null host means WPF has
        // no active TSF text store and the bridge must retain native candidate presentation.
        var presentationFramework = typeof(System.Windows.Controls.TextBox).Assembly;
        var editorType = presentationFramework.GetType("System.Windows.Documents.TextEditor");

        var storeProperty = editorType?.GetProperty(
            "_ThreadLocalStore",
            BindingFlags.Static | BindingFlags.NonPublic
        );

        var storeType = presentationFramework.GetType("System.Windows.Documents.TextEditorThreadLocalStore");

        var hostProperty = storeType?.GetProperty(
            "TextServicesHost",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        var hostType = presentationFramework.GetType("System.Windows.Documents.TextServicesHost");

        var managerProperty = hostType?.GetProperty(
            "ThreadManager",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        return CreateAccessor(storeProperty, hostProperty, managerProperty);
    }

    /// <summary>
    /// Compiles null-safe access with one evaluation per getter. Only code is cached: each call
    /// reads the current thread's store, host, and manager again, including after host replacement.
    /// </summary>
    internal static Func<object?>? CreateAccessor(
        PropertyInfo? storeProperty,
        PropertyInfo? hostProperty,
        PropertyInfo? managerProperty)
    {
        var storeGetter = storeProperty?.GetGetMethod(nonPublic: true);
        var hostGetter = hostProperty?.GetGetMethod(nonPublic: true);
        var managerGetter = managerProperty?.GetGetMethod(nonPublic: true);

        if (storeGetter is null || !storeGetter.IsStatic || storeGetter.GetParameters().Length != 0
            || hostGetter is null || hostGetter.IsStatic || hostGetter.GetParameters().Length != 0
            || managerGetter is null || managerGetter.IsStatic
            || managerGetter.GetParameters().Length != 0
            || storeGetter.ReturnType.IsValueType || hostGetter.ReturnType.IsValueType
            || managerGetter.ReturnType.IsValueType
            || hostGetter.DeclaringType != storeGetter.ReturnType
            || managerGetter.DeclaringType != hostGetter.ReturnType)
        {
            return null;
        }

        var store = Expression.Variable(storeGetter.ReturnType, "store");
        var host = Expression.Variable(hostGetter.ReturnType, "host");
        var noManager = Expression.Constant(null, typeof(object));

        return Expression.Lambda<Func<object?>>(
            Expression.Block(
                [store, host],
                Expression.Assign(store, Expression.Call(storeGetter)),
                Expression.Condition(
                    Expression.ReferenceEqual(store, Expression.Constant(null, store.Type)),
                    noManager,
                    Expression.Block(
                        Expression.Assign(host, Expression.Call(store, hostGetter)),
                        Expression.Condition(
                            Expression.ReferenceEqual(host, Expression.Constant(null, host.Type)),
                            noManager,
                            Expression.Convert(Expression.Call(host, managerGetter), typeof(object))))))
        ).Compile();
    }
}
