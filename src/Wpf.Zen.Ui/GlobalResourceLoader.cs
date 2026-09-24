// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Diagnostics;

namespace Wpf.Zen.Ui;

/// <summary>
/// Installs palette-independent library defaults into WPF's system theme resource scope.
/// </summary>
/// <remarks>
/// The focus key belongs to PresentationFramework, so Generic.xaml alone cannot override it.
/// Global entries use assembly-scoped ResourceKey or Type keys, as required by system lookup.
/// WPF reloads the owning dictionary after WM_THEMECHANGED; its internal load notification lets
/// the defaults be installed before the lookup continues, without dispatcher timing dependencies.
/// </remarks>
internal static class GlobalResourceLoader
{
    private const BindingFlags StaticMembers = BindingFlags.Static | BindingFlags.NonPublic;

    private static readonly Type SystemResources = typeof(FrameworkElement).Assembly.GetType(
        "System.Windows.SystemResources",
        throwOnError: true
    )!;

    private static readonly PropertyInfo ThemeLock = SystemResources.GetProperty(
        "ThemeDictionaryLock",
        StaticMembers
    )!;

    private static readonly FieldInfo ResourceCache = SystemResources.GetField(
        "_resourceCache",
        StaticMembers
    )!;

    private static readonly ResourceDictionary Resources = new();

    /// <summary>
    /// Loads the defaults once, subscribes to system dictionary reloads, and installs immediately.
    /// </summary>
    /// <remarks>
    /// Called only by LibraryInitializer. Reflection is required because the public diagnostics
    /// event forwards subscriptions only when WPF's visual diagnostics are enabled. A missing
    /// framework contract fails initialization rather than silently losing the library defaults.
    /// </remarks>
    internal static void Initialize()
    {
        var themeDicLoaded = SystemResources.GetEvent("ThemedDictionaryLoaded", StaticMembers)!;
        var subscribe      = themeDicLoaded.GetAddMethod(nonPublic: true)!;

        var genericDicLoaded = SystemResources.GetEvent("GenericDictionaryLoaded", StaticMembers)!;
        var subscribeGeneric = genericDicLoaded.GetAddMethod(nonPublic: true)!;

        var genericDictionaries = SystemResources.GetProperty(
            "GenericResourceDictionaries", StaticMembers
        )!;

        var find = SystemResources.GetMethod(
            "FindResourceInternal", StaticMembers, null, [typeof(object)], null
        )!;

        var dictionaries = SystemResources.GetProperty(
            "ThemedResourceDictionaries", StaticMembers
        )!;

        var isThemeDictionary = typeof(ResourceDictionary).GetProperty(
            "IsThemeDictionary", BindingFlags.Instance | BindingFlags.NonPublic
        )!;

        // Application registers WPF's pack resource container in its static constructor.
        // Run it without constructing an Application, including in WinForms and console hosts.
        RuntimeHelpers.RunClassConstructor(typeof(Application).TypeHandle);

        Resources.Source = new Uri(
            "/Wpf.Zen.Ui;component/Resources/GlobalResources.xaml", UriKind.Relative
        );

        // Theme values must be sealed and detached from their construction dispatcher before
        // publication because WPF shares its system resource cache across UI threads.
        isThemeDictionary.SetValue(Resources, true);

        lock (ThemeLock.GetValue(null)!)
        {
            object[] handler = [new EventHandler<ResourceDictionaryLoadedEventArgs>(OnDictionaryLoaded)];

            subscribe.Invoke(null, handler);
            subscribeGeneric.Invoke(null, handler);

            // Include a framework dictionary loaded before this assembly, as well as cold startup.
            foreach (ResourceDictionaryInfo info in (IEnumerable)dictionaries.GetValue(null)!)
            {
                Install(info);
            }

            // Library component keys are routed to this assembly's Generic.xaml by WPF. Inject
            // into that dictionary when loaded, sharing the already sealed global values.
            foreach (ResourceDictionaryInfo info in (IEnumerable)genericDictionaries.GetValue(null)!)
            {
                Install(info);
            }

            var cache = (IDictionary)ResourceCache.GetValue(null)!;
            foreach (DictionaryEntry entry in Resources)
            {
                cache.Remove(entry.Key);
            }

            find.Invoke(null, [SystemParameters.FocusVisualStyleKey]);
        }
    }

    /// <summary>
    /// Reinstalls defaults synchronously while WPF holds its theme dictionary lock.
    /// </summary>
    private static void OnDictionaryLoaded(object? sender, ResourceDictionaryLoadedEventArgs e)
    {
        Install(e.ResourceDictionaryInfo);
    }

    /// <summary>
    /// Replaces system-owned defaults in the owning assembly's dictionary before WPF caches them.
    /// </summary>
    private static void Install(ResourceDictionaryInfo info)
    {
        if (info.Assembly != typeof(FrameworkElement).Assembly
            && info.Assembly != typeof(GlobalResourceLoader).Assembly)
        {
            return;
        }

        // Direct entries take precedence over merged dictionaries. Merging GlobalResources into
        // the Windows dictionary would leave its existing FocusVisualStyle entry in control.
        foreach (DictionaryEntry entry in Resources)
        {
            var owner = entry.Key switch
            {
                ResourceKey resKey => resKey.Assembly,
                Type typeKey       => typeKey.Assembly,
                _                  => null
            };

            if (owner == info.Assembly)
            {
                info.ResourceDictionary[entry.Key] = entry.Value;
            }
        }

        Debug.WriteLine(
            $"[GlobalResources] System theme DEFAULTS installed; assembly={info.Assembly.GetName().Name}."
        );
    }
}