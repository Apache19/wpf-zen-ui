// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Resources;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui.Controls;

/// <summary>
/// Resolves the localized UI text used by <see cref="NavigationMapCommands"/>.
/// </summary>
/// <remarks>
/// Routed command text is framework UI, rather than theme data, so it is stored in embedded
/// <c>.resx</c> resources. This follows the same satellite-assembly model used by WPF's standard
/// command libraries and lets <see cref="ResourceManager"/> apply the normal culture-parent and
/// neutral-resource fallback rules. The supplied satellites cover the same culture set as the
/// current open-source WPF assemblies: cs, de, es, fr, it, ja, ko, pl, pt-BR, ru, tr, zh-Hans,
/// and zh-Hant. Region-specific cultures such as zh-CN and zh-TW resolve through their standard
/// script parents; cultures outside that set fall back to neutral English.
/// </remarks>
internal static class NavigationMapCommandResources
{
    private const string ResourceBaseName =
        "Wpf.Zen.Ui.Controls.NavigationMap.NavigationMapCommandStrings";

    private static readonly ResourceManager _resourceManager = new(
        ResourceBaseName,
        typeof(NavigationMapCommandResources).Assembly
    );

    /// <summary>
    /// Gets a command label for the current UI culture.
    /// </summary>
    /// <param name="name">The resource name, which is also the corresponding command name.</param>
    /// <returns>
    /// The localized label, or <paramref name="name"/> if a malformed deployment omits both the
    /// requested satellite resource and the neutral resource.
    /// </returns>
    internal static string GetString(string name)
    {
        return GetString(name, CultureInfo.CurrentUICulture);
    }

    /// <summary>
    /// Gets a command label for an explicit UI culture.
    /// </summary>
    /// <remarks>
    /// The overload keeps resource fallback independently testable without changing the
    /// process-wide culture or forcing the static command objects to be reconstructed.
    /// </remarks>
    internal static string GetString(string name, CultureInfo culture)
    {
        return _resourceManager.GetString(name, culture) ?? name;
    }
}
