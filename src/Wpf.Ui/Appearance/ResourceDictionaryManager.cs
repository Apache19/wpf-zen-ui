// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace Wpf.Ui.Appearance;

/// <summary>
/// Allows managing application dictionaries.
/// </summary>
internal class ResourceDictionaryManager
{
    internal const string LibraryNamespace = "wpf.ui;";

    internal const string ThemesDictionaryPath = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    /// <summary>
    /// Gets the namespace, e.g. the library the resource is being searched for.
    /// </summary>
    private string SearchNamespace { get; }

    public ResourceDictionaryManager(string searchNamespace)
    {
        SearchNamespace = searchNamespace;
    }

    /// <summary>
    /// Shows whether the application contains the <see cref="ResourceDictionary"/>.
    /// </summary>
    /// <param name="resourceLookup">Any part of the resource name.</param>
    /// <returns><see langword="false"/> if it doesn't exist.</returns>
    public bool HasDictionary(string resourceLookup)
    {
        return GetDictionary(resourceLookup) != null;
    }

    /// <summary>
    /// Gets the <see cref="ResourceDictionary"/> if exists.
    /// </summary>
    /// <param name="resourceLookup">Any part of the resource name.</param>
    /// <returns><see cref="ResourceDictionary"/>, <see langword="null"/> if it doesn't exist.</returns>
    public ResourceDictionary? GetDictionary(string resourceLookup)
    {
        Collection<ResourceDictionary> applicationDictionaries = GetApplicationMergedDictionaries();

        if (applicationDictionaries.Count == 0)
        {
            return null;
        }

        foreach (ResourceDictionary t in applicationDictionaries)
        {
            string resourceDictionaryUri;

            if (t?.Source != null)
            {
                resourceDictionaryUri = t.Source.ToString();

                if (
                    resourceDictionaryUri.Contains(SearchNamespace, StringComparison.OrdinalIgnoreCase)
                 && resourceDictionaryUri.Contains(resourceLookup,  StringComparison.OrdinalIgnoreCase)
                )
                {
                    return t;
                }
            }

            foreach (ResourceDictionary? t1 in t!.MergedDictionaries)
            {
                if (t1?.Source == null)
                {
                    continue;
                }

                resourceDictionaryUri = t1.Source.ToString();

                if (
                    !resourceDictionaryUri.Contains(SearchNamespace, StringComparison.OrdinalIgnoreCase)
                 || !resourceDictionaryUri.Contains(resourceLookup,  StringComparison.OrdinalIgnoreCase)
                )
                {
                    continue;
                }

                return t1;
            }
        }

        return null;
    }

    public bool UpdateDictionary(string resourceLookup, Uri? newResourceUri)
    {
        Collection<ResourceDictionary> applicationDictionaries = UiApplication
           .Current
           .Resources
           .MergedDictionaries;

        if (applicationDictionaries.Count == 0 || newResourceUri is null)
        {
            return false;
        }

        for (var i = 0; i < applicationDictionaries.Count; i++)
        {
            string sourceUri;

            if (applicationDictionaries[i]?.Source != null)
            {
                sourceUri = applicationDictionaries[i].Source.ToString();

                if (sourceUri.Contains(SearchNamespace, StringComparison.OrdinalIgnoreCase)
                 && sourceUri.Contains(resourceLookup,  StringComparison.OrdinalIgnoreCase))
                {
                    applicationDictionaries[i] = new() { Source = newResourceUri };

                    return true;
                }
            }

            for (var j = 0; j < applicationDictionaries[i].MergedDictionaries.Count; j++)
            {
                if (applicationDictionaries[i].MergedDictionaries[j]?.Source == null)
                {
                    continue;
                }

                sourceUri = applicationDictionaries[i].MergedDictionaries[j].Source.ToString();

                if (!sourceUri.Contains(SearchNamespace, StringComparison.OrdinalIgnoreCase)
                 || !sourceUri.Contains(resourceLookup,  StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                applicationDictionaries[i].MergedDictionaries[j] = new() { Source = newResourceUri };

                return true;
            }
        }

        return false;
    }

    private Collection<ResourceDictionary> GetApplicationMergedDictionaries()
    {
        return EnsureApplication().Resources.MergedDictionaries;
    }

    private static Application EnsureApplication()
    {
        return Application.Current ?? new Application();
    }
}