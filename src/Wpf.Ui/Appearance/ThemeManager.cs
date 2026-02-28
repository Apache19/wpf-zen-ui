// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Diagnostics;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Theming;
using Wpf.Ui.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui;

/// <summary>
/// Manages application theme state, listens to system theme events,
/// synchronizes system theme, and coordinates theme resource loading.
/// </summary>
public sealed class ThemeManager
{
    internal const string ThemeBaseUri = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/";

    private static readonly Uri LightThemeUri        = new(ThemeBaseUri + "Light.xaml", UriKind.Absolute);
    private static readonly Uri DarkThemeUri         = new(ThemeBaseUri + "Dark.xaml", UriKind.Absolute);
    private static readonly Uri HighContrastThemeUri = new(ThemeBaseUri + "HighContrast.xaml", UriKind.Absolute);

    private static readonly object _lock = new();

    private static readonly ResourceDictionary _lightDictionary = new();
    private static readonly ResourceDictionary _darkDictionary  = new();

    private static readonly List<WeakReference<FrameworkElement>>        _registeredElements        = [];
    private static readonly List<WeakReference<FrameworkContentElement>> _registeredContentElements = [];

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static Uri? _additionalLightThemeUri;
    private static Uri? _additionalDarkThemeUri;

    private static UiThemeMode _themeMode = UiThemeMode.Light; // Default theme merged with Generic.xaml
    private static bool        _useSystemAccentColor;

    private static ThemeManager Instance { get; } = new();

    static ThemeManager()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        RebuildThemeDictionaries();
    }

    private ThemeManager() { }

    #region Public Properties

    public static ApplicationTheme CurrentTheme { get; private set; } = ResolveEffectiveTheme(_themeMode);

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to be merged
    /// when the Light theme is active.
    /// </summary>
    /// <remarks>
    /// This property allows consumers to provide extra theme resources
    /// that extend or override the built-in Light theme dictionary.
    ///
    /// When set, the specified resource dictionary will be merged
    /// after the default Light theme resources, giving it higher
    /// lookup priority in the resource resolution chain.
    ///
    /// Changing this property triggers an immediate theme refresh,
    /// causing the new dictionary to be loaded and applied dynamically.
    /// </remarks>
    public static Uri? AdditionalLightThemeUri
    {
        get => _additionalLightThemeUri;
        set
        {
            if (_additionalLightThemeUri != value)
            {
                _additionalLightThemeUri = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to be merged
    /// when the Dark theme is active.
    /// </summary>
    /// <remarks>
    /// This property allows consumers to provide extra theme resources
    /// that extend or override the built-in Dark theme dictionary.
    ///
    /// When set, the specified resource dictionary will be merged
    /// after the default Dark theme resources, giving it higher
    /// lookup priority in the resource resolution chain.
    ///
    /// Changing this property triggers an immediate theme refresh,
    /// causing the new dictionary to be loaded and applied dynamically.
    /// </remarks>
    public static Uri? AdditionalDarkThemeUri
    {
        get => _additionalDarkThemeUri;
        set
        {
            if (_additionalDarkThemeUri != value)
            {
                _additionalDarkThemeUri = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets the global theme mode for the Application.
    /// </summary>
    public static UiThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            if (_themeMode != value)
            {
                _themeMode = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether gets or sets whether to use the system accent color.
    /// </summary>
    public static bool UseSystemAccentColor
    {
        get => _useSystemAccentColor;
        set
        {
            if (_useSystemAccentColor != value)
            {
                _useSystemAccentColor = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    #endregion

    public static void Initialize() { }

    #region Attached Property: ApplyToRootVisual (Internal)

    private static readonly DependencyProperty IsRootVisualProperty =
        DependencyProperty.RegisterAttached(
            "IsRootVisual",
            typeof(bool),
            typeof(ThemeManager),
            new PropertyMetadata(false)
        );

    private static void SetIsRootVisual(DependencyObject element, bool value)
    {
        element.SetValue(IsRootVisualProperty, value);
    }

    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    private static bool GetIsRootVisual(DependencyObject element)
    {
        return (bool)element.GetValue(IsRootVisualProperty);
    }

    internal static void OnApplyToRootVisualChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if ((bool)e.NewValue)
        {
            if (PresentationSource.FromDependencyObject(d) == null)
            {
                if (d is FrameworkElement fe)
                {
                    if (fe.IsLoaded)
                    {
                        RegisterRootVisual(fe);
                    }
                    else
                    {
                        fe.Loaded += OnElementLoadedForRootVisual;
                    }
                }

                return;
            }

            RegisterRootVisual(d);
        }
    }

    private static void OnElementLoadedForRootVisual(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.Loaded -= OnElementLoadedForRootVisual;
            RegisterRootVisual(fe);
        }
    }

    private static void RegisterRootVisual(DependencyObject d)
    {
        var ps = PresentationSource.FromDependencyObject(d);

        // RootVisual must be a Visual.
        // FrameworkElement derives from Visual, but FrameworkContentElement does not.
        // Therefore RootVisual can only be a FrameworkElement.
        // It is safe to cast without checking for FrameworkContentElement.
        if (ps?.RootVisual is FrameworkElement rootVisual)
        {
            SetIsRootVisual(rootVisual, true);
            RegisterElement(rootVisual, isRootVisual: true);
            ApplyThemeToElement(rootVisual, _themeMode);
        }
    }

    #endregion

    #region Attached Property: RequestedTheme (Internal)

    internal static void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement fe)
        {
            RegisterElement(fe, isRootVisual: false);
            ApplyThemeToElement(fe, (UiThemeMode)e.NewValue);
        }
        else if (d is FrameworkContentElement fce)
        {
            RegisterContentElement(fce);
            ApplyThemeToContentElement(fce, (UiThemeMode)e.NewValue);
        }
    }

    #endregion

    #region System Events

    private static void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        Debug.WriteLine($"ThemeManager: SystemParameters_StaticPropertyChanged - {e.PropertyName}");
        ScheduleThemeChangedDebounced();
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Debug.WriteLine($"ThemeManager: SystemEvents_UserPreferenceChanged: {e.Category}");

        if (e.Category is
            UserPreferenceCategory.General or
            UserPreferenceCategory.VisualStyle or
            UserPreferenceCategory.Color)
        {
            ScheduleThemeChangedDebounced();
        }
    }

    private static void ScheduleThemeChangedDebounced()
    {
        _ = _themeChangedDebouncer.DebounceAsync(
            TimeSpan.FromMilliseconds(250),
            () =>
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null)
                {
                    dispatcher.BeginInvoke(() => RefreshTheme(ThemeChangeSource.System));
                }
                else
                {
                    RefreshTheme(ThemeChangeSource.System);
                }
            }
        );
    }

    #endregion

    #region Theme Refresh

    private static void RefreshTheme(ThemeChangeSource source)
    {
        var targets = CollectTargets();

        // 1. BeginInit
        foreach (var (_, resources, _) in targets)
        {
            resources.BeginInit();
        }

        // 2. Rebuild theme dictionaries
        RebuildThemeDictionaries();

        // 3. Merge dictionaries to each target
        foreach (var (_, resources, mode) in targets)
        {
            var effectiveTheme = ResolveEffectiveTheme(mode);

            var themeDict = effectiveTheme == ApplicationTheme.Dark
                ? _darkDictionary
                : _lightDictionary;

            MergeThemeDictionary(resources, themeDict);
        }

        // 4. EndInit
        foreach (var (_, resources, _) in targets)
        {
            resources.EndInit();
        }

        // 5. Raise Changed event
        var finalTheme = ResolveEffectiveTheme(_themeMode);
        CurrentTheme = finalTheme;
        Instance.ChangedInternal?.Invoke(Instance, new ThemeChangedEventArgs(finalTheme, source));
    }

    private static List<(object Target, ResourceDictionary Resources, UiThemeMode Mode)> CollectTargets()
    {
        var targets = new List<(object, ResourceDictionary, UiThemeMode)>();

        // Application
        var app = Application.Current;
        if (app?.Dispatcher != null && !app.Dispatcher.HasShutdownStarted)
        {
            targets.Add((app, app.Resources, _themeMode));
        }

        lock (_lock)
        {
            // Cleanup and collect registered FrameworkElements
            _registeredElements.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _registeredElements)
            {
                if (wr.TryGetTarget(out var fe))
                {
                    var mode = GetEffectiveThemeForElement(fe);
                    targets.Add((fe, fe.Resources, mode));
                }
            }

            // Cleanup and collect registered FrameworkContentElements
            _registeredContentElements.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _registeredContentElements)
            {
                if (wr.TryGetTarget(out var fce))
                {
                    var mode = Theme.GetRequested(fce);
                    targets.Add((fce, fce.Resources, mode));
                }
            }
        }

        return targets;
    }

    private static void RebuildThemeDictionaries()
    {
        var isHighContrast = SystemParameters.HighContrast;

        _lightDictionary.MergedDictionaries.Clear();
        _darkDictionary.MergedDictionaries.Clear();

        if (isHighContrast)
        {
            // High contrast mode: both dictionaries load high contrast theme
            _lightDictionary.MergedDictionaries.Add(new ResourceDictionary { Source = HighContrastThemeUri });
            _darkDictionary.MergedDictionaries.Add(new ResourceDictionary { Source  = HighContrastThemeUri });
        }
        else
        {
            // Load library themes
            _lightDictionary.MergedDictionaries.Add(new ResourceDictionary { Source = LightThemeUri });
            _darkDictionary.MergedDictionaries.Add(new ResourceDictionary { Source  = DarkThemeUri });

            // Load additional theme resources
            if (_additionalLightThemeUri != null)
            {
                _lightDictionary.MergedDictionaries.Add(new ResourceDictionary { Source = _additionalLightThemeUri });
            }

            if (_additionalDarkThemeUri != null)
            {
                _darkDictionary.MergedDictionaries.Add(new ResourceDictionary { Source = _additionalDarkThemeUri });
            }

            // Apply system accent colors
            if (_useSystemAccentColor)
            {
                var palette = SystemAccentPaletteReader.Read();
                if (palette != null)
                {
                    _lightDictionary.MergedDictionaries.Add(BuildAccentDictionary(palette, UiThemeMode.Light));
                    _darkDictionary.MergedDictionaries.Add(BuildAccentDictionary(palette,  UiThemeMode.Dark));
                }
            }
        }
    }

    private static ResourceDictionary BuildAccentDictionary(SystemAccentPalette p, UiThemeMode mode)
    {
        var dic = new ResourceDictionary { [ColorKeys.SystemAccentColor] = p.Accent };

        if (mode == UiThemeMode.Light)
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Dark1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Dark2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Dark3;

            dic[BrushKeys.SystemAccentColorBrush]                     = new SolidColorBrush(p.Accent);
            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = new SolidColorBrush(p.Dark2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = new SolidColorBrush(p.Dark3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = new SolidColorBrush(p.Dark2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = new SolidColorBrush(p.Dark3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = new SolidColorBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = new SolidColorBrush(p.Dark1);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = new SolidColorBrush(p.Dark1) { Opacity = 0.9 };
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = new SolidColorBrush(p.Dark1) { Opacity = 0.8 };
            dic[BrushKeys.SystemFillColorAttentionBrush]              = new SolidColorBrush(p.Accent);
        }
        else
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Light1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Light2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Light3;

            dic[BrushKeys.SystemAccentColorBrush]                     = new SolidColorBrush(p.Accent);
            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = new SolidColorBrush(p.Light1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = new SolidColorBrush(p.Light2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = new SolidColorBrush(p.Light3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = new SolidColorBrush(p.Light1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = new SolidColorBrush(p.Light2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = new SolidColorBrush(p.Light3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = new SolidColorBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = new SolidColorBrush(p.Light2);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = new SolidColorBrush(p.Light2) { Opacity = 0.9 };
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = new SolidColorBrush(p.Light2) { Opacity = 0.8 };
            dic[BrushKeys.SystemFillColorAttentionBrush]              = new SolidColorBrush(p.Accent);
        }

        return dic;
    }

    private static void MergeThemeDictionary(ResourceDictionary target, ResourceDictionary themeDict)
    {
        // Remove old theme dictionary
        for (int i = target.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var dict = target.MergedDictionaries[i];
            if (ReferenceEquals(dict, _lightDictionary) || ReferenceEquals(dict, _darkDictionary))
            {
                target.MergedDictionaries.RemoveAt(i);
            }
        }

        // Insert at beginning so user-defined resources can override theme resources
        target.MergedDictionaries.Insert(0, themeDict);
    }

    #endregion

    #region Registration

    private static void RegisterElement(FrameworkElement element, bool isRootVisual)
    {
        lock (_lock)
        {
            if (IsElementRegistered(element))
            {
                return;
            }

            _registeredElements.Add(new WeakReference<FrameworkElement>(element));
            element.Unloaded += OnElementUnloaded;
        }
    }

    private static void RegisterContentElement(FrameworkContentElement element)
    {
        lock (_lock)
        {
            if (IsContentElementRegistered(element))
            {
                return;
            }

            _registeredContentElements.Add(new WeakReference<FrameworkContentElement>(element));
            element.Unloaded += OnContentElementUnloaded;
        }
    }

    private static bool IsElementRegistered(FrameworkElement element)
    {
        return _registeredElements.Any(wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, element));
    }

    private static bool IsContentElementRegistered(FrameworkContentElement element)
    {
        return _registeredContentElements.Any(wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, element));
    }

    private static void OnElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            lock (_lock)
            {
                _registeredElements.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, fe));
                fe.Unloaded -= OnElementUnloaded;
            }
        }
    }

    private static void OnContentElementUnloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkContentElement fce)
        {
            lock (_lock)
            {
                _registeredContentElements.RemoveAll(wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, fce));
                fce.Unloaded -= OnContentElementUnloaded;
            }
        }
    }

    #endregion

    #region Helpers

    private static UiThemeMode GetEffectiveThemeForElement(FrameworkElement element)
    {
        // RootVisual always follows global theme mode
        if (GetIsRootVisual(element))
        {
            return _themeMode;
        }

        var requested = Theme.GetRequested(element);
        if (requested != UiThemeMode.Auto)
        {
            return requested;
        }

        return _themeMode;
    }

    private static ApplicationTheme ResolveEffectiveTheme(UiThemeMode mode)
    {
        if (mode == UiThemeMode.Auto)
        {
            return GetSystemThemeMode();
        }

        return mode == UiThemeMode.Dark ? ApplicationTheme.Dark : ApplicationTheme.Light;
    }

    private static void ApplyThemeToElement(FrameworkElement element, UiThemeMode mode)
    {
        var effectiveTheme = ResolveEffectiveTheme(
            mode == UiThemeMode.Auto
                ? GetEffectiveThemeForElement(element)
                : mode
        );

        var themeDict = effectiveTheme == ApplicationTheme.Dark
            ? _darkDictionary
            : _lightDictionary;

        element.Resources.BeginInit();
        MergeThemeDictionary(element.Resources, themeDict);
        element.Resources.EndInit();
    }

    private static void ApplyThemeToContentElement(FrameworkContentElement element, UiThemeMode mode)
    {
        var effectiveTheme = ResolveEffectiveTheme(mode);
        var themeDict = effectiveTheme == ApplicationTheme.Dark
            ? _darkDictionary
            : _lightDictionary;

        element.Resources.BeginInit();
        MergeThemeDictionary(element.Resources, themeDict);
        element.Resources.EndInit();
    }

    private static ApplicationTheme GetSystemThemeMode()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            -1
        );

        return value switch
        {
            1 => ApplicationTheme.Light,
            0 => ApplicationTheme.Dark,
            _ => ApplicationTheme.Light
        };
    }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when the theme has changed.
    /// </summary>
    public static event EventHandler<ThemeChangedEventArgs> Changed
    {
        add
        {
            WeakEventManager<ThemeManager, ThemeChangedEventArgs>
               .AddHandler(
                    Instance,
                    nameof(ChangedInternal),
                    value
                );
        }
        remove
        {
            WeakEventManager<ThemeManager, ThemeChangedEventArgs>
               .RemoveHandler(
                    Instance,
                    nameof(ChangedInternal),
                    value
                );
        }
    }

    public event EventHandler<ThemeChangedEventArgs>? ChangedInternal;

    #endregion
}