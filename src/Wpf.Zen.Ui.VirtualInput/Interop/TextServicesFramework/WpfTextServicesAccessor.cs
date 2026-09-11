// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;

namespace Wpf.Zen.Ui.VirtualInput.Interop.TextServicesFramework;

internal static class WpfTextServicesAccessor
{
    private static readonly Lazy<Func<object?>?> Accessor = new(CreateAccessor);

    internal static object? TryGetThreadManager()
    {
        try
        {
            return Accessor.Value?.Invoke();
        }
        catch (Exception exception) when (
            exception is TargetInvocationException
                or TypeLoadException
                or MemberAccessException
                or InvalidOperationException)
        {
            return null;
        }
    }

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

        if (storeProperty is null || hostProperty is null || managerProperty is null)
        {
            return null;
        }

        return () =>
        {
            var store = storeProperty.GetValue(null, null);
            var host = store is null ? null : hostProperty.GetValue(store, null);

            return host is null ? null : managerProperty.GetValue(host, null);
        };
    }
}