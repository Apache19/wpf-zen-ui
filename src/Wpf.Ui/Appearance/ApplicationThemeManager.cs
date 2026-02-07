// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Ui.Controls;

namespace Wpf.Ui.Appearance;

/// <summary>
/// Allows to manage the application theme by swapping resource dictionaries containing dynamic resources with color information.
/// </summary>
/// <example>
/// <code lang="csharp">
/// ApplicationThemeManager.Apply(
///     ApplicationTheme.Light
/// );
/// </code>
/// <code lang="csharp">
/// if (ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark)
/// {
///     ApplicationThemeManager.Apply(
///         ApplicationTheme.Light
///     );
/// }
/// </code>
/// <code>
/// ApplicationThemeManager.Changed += (theme, accent) =>
/// {
///     Debug.WriteLine($"Application theme changed to {theme.ToString()}");
/// };
/// </code>
/// </example>
public static class ApplicationThemeManager
{
    private static ApplicationTheme _cachedApplicationTheme = ApplicationTheme.Unknown;

    internal const string LibraryNamespace = "wpf.ui;";

    internal const string ThemesDictionaryPath = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    /// <summary>
    /// Event triggered when the application's theme is changed.
    /// </summary>
    public static event ThemeChangedEvent? Changed;

    /// <summary>
    /// Gets a value that indicates whether the application is currently using the high contrast theme.
    /// </summary>
    /// <returns><see langword="true"/> if application uses high contrast theme.</returns>
    public static bool IsHighContrast() => _cachedApplicationTheme == ApplicationTheme.HighContrast;

    /// <summary>
    /// Gets a value that indicates whether the Windows is currently using the high contrast theme.
    /// </summary>
    /// <returns><see langword="true"/> if system uses high contrast theme.</returns>
    public static bool IsSystemHighContrast() => SystemThemeManager.HighContrast;

    /// <summary>
    /// Changes the current application theme.
    /// </summary>
    /// <param name="applicationTheme">Theme to set.</param>
    /// <param name="updateAccent">Whether the color accents should be changed.</param>
    public static void Apply(ApplicationTheme applicationTheme, bool updateAccent = true)
    {
        if (updateAccent)
        {
            ApplicationAccentColorManager.Apply(
                ApplicationAccentColorManager.GetColorizationColor(),
                applicationTheme,
                false
            );
        }

        if (applicationTheme == ApplicationTheme.Unknown)
        {
            return;
        }

        ResourceDictionaryManager appDictionaries = new(LibraryNamespace);

        string themeDictionaryName = applicationTheme switch
        {
            ApplicationTheme.Dark => "Dark",
            ApplicationTheme.HighContrast => GetSystemTheme() switch
            {
                SystemTheme.HC1     => "HC1",
                SystemTheme.HC2     => "HC2",
                SystemTheme.HCBlack => "HCBlack",
                _                   => "HCWhite",
            },
            _ => "Light"
        };

        bool isUpdated = appDictionaries.UpdateDictionary(
            "theme",
            new Uri(ThemesDictionaryPath + themeDictionaryName + ".xaml", UriKind.Absolute)
        );

        if (!isUpdated)
        {
            return;
        }

        SystemThemeManager.UpdateSystemThemeCache();

        _cachedApplicationTheme = applicationTheme;

        Changed?.Invoke(applicationTheme, ApplicationAccentColorManager.SystemAccentColor);

        // todo 禁用，可能需要遍历每个窗口，根据暗黑模式、背景，单独处理
        // if (UiApplication.Current.MainWindow is Window mainWindow)
        // {
        //      WindowBackgroundManager.UpdateBackground(mainWindow, applicationTheme, backgroundEffect);
        // }
    }

    /// <summary>
    /// Applies Resources in the <paramref name="frameworkElement"/>.
    /// </summary>
    public static void Apply(FrameworkElement frameworkElement)
    {
        if (frameworkElement is null)
        {
            return;
        }

        ResourceDictionary[] resourcesRemove = frameworkElement
            .Resources.MergedDictionaries.Where(e => e.Source is not null)
            .Where(e => e.Source.ToString().Contains(LibraryNamespace, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        foreach (ResourceDictionary? resource in UiApplication.Current.Resources.MergedDictionaries)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Add {resource.Source}",
                "Wpf.Ui.Appearance"
            );
            frameworkElement.Resources.MergedDictionaries.Add(resource);
        }

        foreach (ResourceDictionary resource in resourcesRemove)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Remove {resource.Source}",
                "Wpf.Ui.Appearance"
            );

            _ = frameworkElement.Resources.MergedDictionaries.Remove(resource);
        }

        foreach (System.Collections.DictionaryEntry resource in UiApplication.Current.Resources)
        {
            System.Diagnostics.Debug.WriteLine(
                $"INFO | {typeof(ApplicationThemeManager)} Copy Resource {resource.Key} - {resource.Value}",
                "Wpf.Ui.Appearance"
            );
            frameworkElement.Resources[resource.Key] = resource.Value;
        }
    }

    public static void ApplySystemTheme()
    {
        ApplySystemTheme(true);
    }

    public static void ApplySystemTheme(bool updateAccent)
    {
        SystemThemeManager.UpdateSystemThemeCache();

        var systemTheme = GetSystemTheme();

        ApplicationTheme themeToSet = systemTheme switch
        {
            SystemTheme.Dark           => ApplicationTheme.Dark,
            SystemTheme.CapturedMotion => ApplicationTheme.Dark,
            SystemTheme.Glow           => ApplicationTheme.Dark,
            SystemTheme.HC1            => ApplicationTheme.HighContrast,
            SystemTheme.HC2            => ApplicationTheme.HighContrast,
            SystemTheme.HCBlack        => ApplicationTheme.HighContrast,
            SystemTheme.HCWhite        => ApplicationTheme.HighContrast,
            _                          => ApplicationTheme.Light
        };

        Apply(themeToSet, updateAccent: updateAccent);
    }

    /// <summary>
    /// Gets currently set application theme.
    /// </summary>
    /// <returns><see cref="ApplicationTheme.Unknown"/> if something goes wrong.</returns>
    public static ApplicationTheme GetAppTheme()
    {
        if (_cachedApplicationTheme == ApplicationTheme.Unknown)
        {
            FetchApplicationTheme();
        }

        return _cachedApplicationTheme;
    }

    /// <summary>
    /// Gets currently set system theme.
    /// </summary>
    /// <returns><see cref="SystemTheme.Unknown"/> if something goes wrong.</returns>
    public static SystemTheme GetSystemTheme()
    {
        return SystemThemeManager.GetCachedSystemTheme();
    }

    /// <summary>
    /// Gets a value that indicates whether the application is matching the system theme.
    /// </summary>
    /// <returns><see langword="true"/> if the application has the same theme as the system.</returns>
    public static bool IsAppMatchesSystem()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme      sysTheme            = GetSystemTheme();

        return appApplicationTheme switch
        {
            ApplicationTheme.Dark => sysTheme
                is SystemTheme.Dark
                or SystemTheme.CapturedMotion
                or SystemTheme.Glow,
            ApplicationTheme.Light => sysTheme
                is SystemTheme.Light
                or SystemTheme.Flow
                or SystemTheme.Sunrise,
            _ => appApplicationTheme == ApplicationTheme.HighContrast && SystemThemeManager.HighContrast,
        };
    }

    /// <summary>
    /// Checks if the application and the operating system are currently working in a dark theme.
    /// </summary>
    public static bool IsMatchedDark()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme      sysTheme            = GetSystemTheme();

        if (appApplicationTheme != ApplicationTheme.Dark)
        {
            return false;
        }

        return sysTheme is SystemTheme.Dark or SystemTheme.CapturedMotion or SystemTheme.Glow;
    }

    /// <summary>
    /// Checks if the application and the operating system are currently working in a light theme.
    /// </summary>
    public static bool IsMatchedLight()
    {
        ApplicationTheme appApplicationTheme = GetAppTheme();
        SystemTheme      sysTheme            = GetSystemTheme();

        if (appApplicationTheme != ApplicationTheme.Light)
        {
            return false;
        }

        return sysTheme is SystemTheme.Light or SystemTheme.Flow or SystemTheme.Sunrise;
    }

    /// <summary>
    /// Tries to guess the currently set application theme.
    /// </summary>
    private static void FetchApplicationTheme()
    {
        ResourceDictionaryManager appDictionaries = new(LibraryNamespace);
        ResourceDictionary?       themeDictionary = appDictionaries.GetDictionary("theme");

        if (themeDictionary == null)
        {
            return;
        }

        string themeUri = themeDictionary.Source.ToString();

        if (themeUri.Contains("light", StringComparison.OrdinalIgnoreCase))
        {
            _cachedApplicationTheme = ApplicationTheme.Light;
        }

        if (themeUri.Contains("dark", StringComparison.OrdinalIgnoreCase))
        {
            _cachedApplicationTheme = ApplicationTheme.Dark;
        }

        if (themeUri.Contains("highcontrast", StringComparison.OrdinalIgnoreCase))
        {
            _cachedApplicationTheme = ApplicationTheme.HighContrast;
        }
    }
}