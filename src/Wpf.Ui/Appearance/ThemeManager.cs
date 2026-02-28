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
/// Manages application theme state, listens to system theme events, synchronizes system theme, and coordinates theme resource loading.
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

    private static Uri? _additionalLightThemeUri;
    private static Uri? _additionalDarkThemeUri;

    private static readonly List<WeakReference<FrameworkElement>>        _registeredElements        = [];
    private static readonly List<WeakReference<FrameworkContentElement>> _registeredContentElements = [];

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static UiThemeMode _themeMode = UiThemeMode.Auto;
    private static bool        _useSystemAccentColor;

    private static ThemeManager Instance { get; } = new();

    private ThemeManager() { }

    #region Public Properties

    /// <summary>
    /// Gets or sets the global theme mode for the Application.
    /// </summary>
    public static UiThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            EnsureInitialized();
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
            EnsureInitialized();
            if (_useSystemAccentColor != value)
            {
                _useSystemAccentColor = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the theme manager is initialized.
    /// </summary>
    public static bool IsInitialized { get; private set; }

    #endregion

    #region Attached Property: BaseTheme

    /// <summary>
    /// Identifies the BaseTheme attached property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This property is used to set the theme for the RootVisual element.
    /// It functions as a "pseudo-global" theme but is scoped strictly to the current visual tree.
    /// It is specifically designed to resolve theme desynchronization issues for isolated components
    /// such as ContextMenus and Popups when WPF content is hosted in non-WPF environments (e.g., WinForms ElementHost).
    /// </para>
    /// <para>
    /// In non-native WPF hosting environments (e.g., WinForms ElementHost), 
    /// this property should be declared on the root node of the top-level UserControl.
    /// This ensures that theme resources are injected into the hidden root container, 
    /// allowing ContextMenus and Popups to synchronize correctly without a global Application object.
    /// </para>
    /// </remarks>
    public static readonly DependencyProperty BaseThemeProperty =
        DependencyProperty.RegisterAttached(
            "BaseTheme",
            typeof(UiThemeMode),
            typeof(ThemeManager),
            new PropertyMetadata(UiThemeMode.Auto, OnBaseThemeChanged)
        );

    /// <summary>Helper for getting <see cref="BaseThemeProperty"/> from <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to read <see cref="BaseThemeProperty"/> from.</param>
    /// <returns>BaseTheme property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UiThemeMode GetBaseTheme(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (UiThemeMode)element.GetValue(BaseThemeProperty);
    }

    /// <summary>Helper for setting <see cref="BaseThemeProperty"/> on <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to set <see cref="BaseThemeProperty"/> on.</param>
    /// <param name="value">BaseTheme property value.</param>
    public static void SetBaseTheme(DependencyObject element, UiThemeMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(BaseThemeProperty, value);
    }

    private static void OnBaseThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (PresentationSource.FromDependencyObject(d) == null)
        {
            if (d is FrameworkElement fe)
            {
                // todo 不要直接订阅，应先检查是否已Loaded，未Loaded再订阅
                fe.Loaded += OnElementLoadedForBaseTheme;
            }

            return;
        }

        ApplyBaseTheme(d);
    }

    private static void OnElementLoadedForBaseTheme(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            fe.Loaded -= OnElementLoadedForBaseTheme;
            ApplyBaseTheme(fe);
        }
    }

    private static void ApplyBaseTheme(DependencyObject d)
    {
        var ps = PresentationSource.FromDependencyObject(d);
        if (ps?.RootVisual is FrameworkElement rootVisual)
        {
            EnsureInitialized();
            RegisterElement(rootVisual, isBaseTheme: true);
            ApplyThemeToElement(rootVisual, GetBaseTheme(d));
        }
    }

    #endregion

    #region Attached Property: RequestedTheme

    /// <summary>
    /// Identifies the RequestedTheme attached property.
    /// </summary>
    public static readonly DependencyProperty RequestedThemeProperty =
        DependencyProperty.RegisterAttached(
            "RequestedTheme",
            typeof(UiThemeMode),
            typeof(ThemeManager),
            new FrameworkPropertyMetadata(UiThemeMode.Auto, OnRequestedThemeChanged)
        );

    /// <summary>Helper for getting <see cref="RequestedThemeProperty"/> from <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to read <see cref="RequestedThemeProperty"/> from.</param>
    /// <returns>RequestedTheme property value.</returns>
    [AttachedPropertyBrowsableForType(typeof(DependencyObject))]
    public static UiThemeMode GetRequestedTheme(DependencyObject element)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        return (UiThemeMode)element.GetValue(RequestedThemeProperty);
    }

    /// <summary>Helper for setting <see cref="RequestedThemeProperty"/> on <paramref name="element"/>.</summary>
    /// <param name="element"><see cref="DependencyObject"/> to set <see cref="RequestedThemeProperty"/> on.</param>
    /// <param name="value">RequestedTheme property value.</param>
    public static void SetRequestedTheme(DependencyObject element, UiThemeMode value)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        element.SetValue(RequestedThemeProperty, value);
    }

    private static void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        EnsureInitialized();

        if (d is FrameworkElement fe)
        {
            RegisterElement(fe, isBaseTheme: false);
            ApplyThemeToElement(fe, (UiThemeMode)e.NewValue);
        }
        else if (d is FrameworkContentElement fce)
        {
            RegisterContentElement(fce);
            ApplyThemeToContentElement(fce, (UiThemeMode)e.NewValue);
        }
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the theme manager with optional additional theme resource URIs.
    /// </summary>
    /// <param name="additionalLightThemeUri">Additional Light mode resource URI.</param>
    /// <param name="additionalDarkThemeUri">Additional Dark mode resource URI.</param>
    public static void Initialize(Uri? additionalLightThemeUri = null, Uri? additionalDarkThemeUri = null)
    {
        lock (_lock)
        {
            if (IsInitialized)
            {
                return;
            }

            _additionalLightThemeUri = additionalLightThemeUri;
            _additionalDarkThemeUri  = additionalDarkThemeUri;

            if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
                SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
            }

            RebuildThemeDictionaries();

            IsInitialized = true;
        }
    }

    private static void EnsureInitialized()
    {
        if (!IsInitialized)
        {
            Initialize();
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
        EnsureInitialized();

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
            var effectiveMode = ResolveEffectiveMode(mode);
            var themeDict     = effectiveMode == UiThemeMode.Dark ? _darkDictionary : _lightDictionary;
            MergeThemeDictionary(resources, themeDict);
        }

        // 4. EndInit
        foreach (var (_, resources, _) in targets)
        {
            resources.EndInit();
        }

        // 5. Raise Changed event
        var finalMode = ResolveEffectiveMode(_themeMode);
        Instance.ChangedInternal?.Invoke(Instance, new ThemeChangedEventArgs(finalMode, source));
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

        // Registered FrameworkElements
        CleanupDeadReferences();

        foreach (var wr in _registeredElements)
        {
            if (wr.TryGetTarget(out var fe))
            {
                var mode = GetEffectiveThemeForElement(fe);
                targets.Add((fe, fe.Resources, mode));
            }
        }

        // Registered FrameworkContentElements
        foreach (var wr in _registeredContentElements)
        {
            if (wr.TryGetTarget(out var fce))
            {
                var mode = GetRequestedTheme(fce);
                targets.Add((fce, fce.Resources, mode));
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

        // Add new theme dictionary
        target.MergedDictionaries.Insert(0, themeDict);
    }

    #endregion

    #region Registration

    private static void RegisterElement(FrameworkElement element, bool isBaseTheme)
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

    private static void CleanupDeadReferences()
    {
        lock (_lock)
        {
            _registeredElements.RemoveAll(wr => !wr.TryGetTarget(out _));
            _registeredContentElements.RemoveAll(wr => !wr.TryGetTarget(out _));
        }
    }

    #endregion

    #region Helpers

    private static UiThemeMode GetEffectiveThemeForElement(FrameworkElement element)
    {
        var requested = GetRequestedTheme(element);
        if (requested != UiThemeMode.Auto)
        {
            return requested;
        }

        var baseTheme = GetBaseTheme(element);
        if (baseTheme != UiThemeMode.Auto)
        {
            return baseTheme;
        }

        return _themeMode;
    }

    private static UiThemeMode ResolveEffectiveMode(UiThemeMode mode)
    {
        if (mode == UiThemeMode.Auto)
        {
            return GetSystemThemeMode();
        }

        return mode;
    }

    private static void ApplyThemeToElement(FrameworkElement element, UiThemeMode mode)
    {
        var effectiveMode = ResolveEffectiveMode(
            mode == UiThemeMode.Auto
                ? GetEffectiveThemeForElement(element)
                : mode
        );

        var themeDict = effectiveMode == UiThemeMode.Dark
                ? _darkDictionary
                : _lightDictionary;

        element.Resources.BeginInit();
        MergeThemeDictionary(element.Resources, themeDict);
        element.Resources.EndInit();
    }

    private static void ApplyThemeToContentElement(FrameworkContentElement element, UiThemeMode mode)
    {
        var effectiveMode = ResolveEffectiveMode(mode);
        var themeDict     = effectiveMode == UiThemeMode.Dark ? _darkDictionary : _lightDictionary;

        element.Resources.BeginInit();
        MergeThemeDictionary(element.Resources, themeDict);
        element.Resources.EndInit();
    }

    private static UiThemeMode GetSystemThemeMode()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            -1
        );

        return value switch
        {
            1 => UiThemeMode.Light,
            0 => UiThemeMode.Dark,
            _ => UiThemeMode.Light
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

    private event EventHandler<ThemeChangedEventArgs>? ChangedInternal;

    #endregion
}