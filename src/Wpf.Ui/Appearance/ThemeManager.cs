// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using System.Reflection;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Theming;
using Wpf.Ui.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Ui;

// todo 实现自定义强调色功能

/// <summary>
/// Manages application theme state, listens to system theme events,
/// synchronizes system theme, and coordinates theme resource loading.
/// </summary>
public sealed class ThemeManager
{
    #region Private Constants, Fields

    // Rooted component URIs do not depend on PackUriHelper's process-wide URI parser being
    // registered before this type's static field initializers run. This matters in WinForms,
    // console, and test hosts that can access ThemeManager before constructing a WPF Application.
    private const string ResourceBaseUri = "/Wpf.Ui;component/Resources/";
    private const string ThemeBaseUri    = ResourceBaseUri + "Theme/";

    private static readonly Uri LightThemeUri          = new(ThemeBaseUri + "Light.xaml", UriKind.Relative);
    private static readonly Uri DarkThemeUri           = new(ThemeBaseUri + "Dark.xaml", UriKind.Relative);
    private static readonly Uri HighContrastThemeUri   = new(ThemeBaseUri + "HighContrast.xaml", UriKind.Relative);
    private static readonly Uri NativeControlStylesUri = new(ResourceBaseUri + "NativeControlStyles.xaml", UriKind.Relative);

    private static readonly object _lock = new();

    private static readonly ResourceDictionary _lightDictionary = [];
    private static readonly ResourceDictionary _darkDictionary  = [];

    // Registration follows object lifetime rather than Loaded/Unloaded lifetime: WPF elements can
    // be unloaded and later reattached without their attached-property callbacks running again.
    // Weak references preserve that behavior without retaining abandoned visual trees.
    private static readonly List<WeakReference<FrameworkElement>>        _registeredElements        = [];
    private static readonly List<WeakReference<FrameworkContentElement>> _registeredContentElements = [];

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static readonly PropertyInfo? _isInitializePendingProperty =
        typeof(ResourceDictionary).GetProperty(
            "IsInitializePending",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    private static Uri? _additionalLightThemeUri;
    private static Uri? _additionalDarkThemeUri;
    private static Uri? _additionalHighContrastLightThemeUri;
    private static Uri? _additionalHighContrastDarkThemeUri;

    private static UiThemeMode _themeMode = UiThemeMode.Default;

    private static bool _useSystemAccentColor       = true;
    private static bool _enableNativeControlStyling = true;

    private static ThemeManager Instance { get; } = new();

    #endregion

    #region Initialize

    /// <summary>
    /// Forces the static constructor to run, ensuring that theme resources
    /// are loaded and system event listeners are registered.
    /// </summary>
    public static void Initialize() { }

    static ThemeManager()
    {
        // Static constructor is a suitable place for library initialization
        LibraryInitializer.EnsureInitialized();

        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            // In non-native WPF hosting environments (such as WinForms with ElementHost),
            // an Application instance is not created automatically.
            //
            // WPF resource lookup walks up the logical tree and eventually falls back
            // to Application-level resources. If no Application exists, resource
            // resolution may fail when crossing logical tree boundaries
            // (e.g., ContextMenu, Popup, or Frame).
            //
            // Ensure an Application instance exists so global theme resources
            // have a stable fallback scope.
            EnsureApplication();

            SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }

        RebuildThemeDictionaries();
    }

    private static void EnsureApplication()
    {
        if (Application.Current == null)
        {
            // Create a lightweight Application instance to act purely as
            // a resource container. This provides an Application-level
            // resource scope for WPF resource lookup when hosted in
            // non-WPF environments.
            //
            // IMPORTANT:
            // Run() must NOT be called. Starting the Dispatcher loop would
            // take control of the thread lifetime, which is incorrect when
            // WPF is hosted inside another UI framework.
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }

    #endregion

    #region Configure

    /// <summary>
    /// Sets additional theme URIs in a single operation,
    /// triggering only one theme refresh.
    /// </summary>
    /// <param name="lightThemeUri">
    /// The URI of the additional light theme resource dictionary, or <c>null</c> to clear.
    /// </param>
    /// <param name="darkThemeUri">
    /// The URI of the additional dark theme resource dictionary, or <c>null</c> to clear.
    /// </param>
    /// <param name="highContrastLightThemeUri">
    /// The URI of the additional high contrast light theme resource dictionary, or <c>null</c> to clear.
    /// </param>
    /// <param name="highContrastDarkThemeUri">
    /// The URI of the additional high contrast dark theme resource dictionary, or <c>null</c> to clear.
    /// </param>
    public static void SetAdditionalThemeUris(
        Uri? lightThemeUri,
        Uri? darkThemeUri,
        Uri? highContrastLightThemeUri = null,
        Uri? highContrastDarkThemeUri = null)
    {
        var changed = false;

        if (_additionalLightThemeUri != lightThemeUri)
        {
            _additionalLightThemeUri = lightThemeUri;
            changed = true;
        }

        if (_additionalDarkThemeUri != darkThemeUri)
        {
            _additionalDarkThemeUri = darkThemeUri;
            changed = true;
        }

        if (_additionalHighContrastLightThemeUri != highContrastLightThemeUri)
        {
            _additionalHighContrastLightThemeUri = highContrastLightThemeUri;
            changed = true;
        }

        if (_additionalHighContrastDarkThemeUri != highContrastDarkThemeUri)
        {
            _additionalHighContrastDarkThemeUri = highContrastDarkThemeUri;
            changed = true;
        }

        if (changed)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    /// <summary>
    /// Configures the theme mode and the system accent color setting without modifying
    /// any existing additional theme resource URIs.
    /// </summary>
    /// <param name="themeMode">The theme mode to apply.</param>
    /// <param name="useSystemAccentColor">A value indicating whether the system accent color should be used.</param>
    /// <param name="enableNativeControlStyling">A value indicating whether to merge native WPF control styles.</param>
    public static void Configure(UiThemeMode themeMode, bool useSystemAccentColor = true, bool enableNativeControlStyling = true)
    {
        ValidateThemeMode(themeMode);

        var changed = false;

        if (_themeMode != themeMode)
        {
            _themeMode = themeMode;
            changed = true;
        }

        if (_useSystemAccentColor != useSystemAccentColor)
        {
            _useSystemAccentColor = useSystemAccentColor;
            changed = true;
        }

        if (_enableNativeControlStyling != enableNativeControlStyling)
        {
            _enableNativeControlStyling = enableNativeControlStyling;
            changed = true;
        }

        if (changed)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    /// <summary>
    /// Configures multiple theme settings in a single operation, triggering only one theme refresh.
    /// </summary>
    /// <param name="themeMode">The theme mode to apply.</param>
    /// <param name="useSystemAccentColor">Whether to use the system accent color.</param>
    /// <param name="enableNativeControlStyling">Whether to merge native WPF control styles.</param>
    /// <param name="additionalLightThemeUri">The URI of the additional light theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalDarkThemeUri">The URI of the additional dark theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalHighContrastLightThemeUri">The URI of the additional high contrast light theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalHighContrastDarkThemeUri">The URI of the additional high contrast dark theme resource dictionary, or <c>null</c> to clear.</param>
    /// <example>
    /// <code lang="csharp">
    /// ThemeManager.Configure(
    ///     themeMode: UiThemeMode.Dark,
    ///     useSystemAccentColor: true,
    ///     enableNativeControlStyling: true,
    ///     additionalLightThemeUri: new Uri("pack://application:,,,/MyApp;component/Themes/Light.xaml"),
    ///     additionalDarkThemeUri: new Uri("pack://application:,,,/MyApp;component/Themes/Dark.xaml")
    /// );
    /// </code>
    /// </example>
    public static void Configure(
        UiThemeMode themeMode,
        bool useSystemAccentColor,
        bool enableNativeControlStyling,
        Uri? additionalLightThemeUri,
        Uri? additionalDarkThemeUri,
        Uri? additionalHighContrastLightThemeUri,
        Uri? additionalHighContrastDarkThemeUri)
    {
        ValidateThemeMode(themeMode);

        var changed = false;

        if (_themeMode != themeMode)
        {
            _themeMode = themeMode;
            changed = true;
        }

        if (_useSystemAccentColor != useSystemAccentColor)
        {
            _useSystemAccentColor = useSystemAccentColor;
            changed = true;
        }

        if (_enableNativeControlStyling != enableNativeControlStyling)
        {
            _enableNativeControlStyling = enableNativeControlStyling;
            changed = true;
        }

        if (_additionalLightThemeUri != additionalLightThemeUri)
        {
            _additionalLightThemeUri = additionalLightThemeUri;
            changed = true;
        }

        if (_additionalDarkThemeUri != additionalDarkThemeUri)
        {
            _additionalDarkThemeUri = additionalDarkThemeUri;
            changed = true;
        }

        if (_additionalHighContrastLightThemeUri != additionalHighContrastLightThemeUri)
        {
            _additionalHighContrastLightThemeUri = additionalHighContrastLightThemeUri;
            changed = true;
        }

        if (_additionalHighContrastDarkThemeUri != additionalHighContrastDarkThemeUri)
        {
            _additionalHighContrastDarkThemeUri = additionalHighContrastDarkThemeUri;
            changed = true;
        }

        if (changed)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the currently active theme applied to the application.
    /// </summary>
    /// <remarks>
    /// Returns <see cref="ApplicationTheme.Default"/> while <see cref="ThemeMode"/> is
    /// <see cref="UiThemeMode.Default"/>; otherwise it returns the resolved Light or Dark theme.
    /// Per-element requests made through <see cref="Theme.RequestedProperty"/> are intentionally
    /// scoped to those elements and are not reflected by this global property.
    /// </remarks>
    public static ApplicationTheme CurrentTheme { get; private set; } = ApplicationTheme.Default;

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
    /// Gets or sets an additional resource dictionary URI to be merged
    /// when a light high contrast theme is active.
    /// </summary>
    /// <remarks>
    /// High contrast light vs. dark is determined by comparing the relative
    /// luminance of <see cref="SystemColors.WindowColor"/> and
    /// <see cref="SystemColors.WindowTextColor"/>.
    /// </remarks>
    public static Uri? AdditionalHighContrastLightThemeUri
    {
        get => _additionalHighContrastLightThemeUri;
        set
        {
            if (_additionalHighContrastLightThemeUri != value)
            {
                _additionalHighContrastLightThemeUri = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to be merged
    /// when a dark high contrast theme is active.
    /// </summary>
    /// <remarks>
    /// High contrast light vs. dark is determined by comparing the relative
    /// luminance of <see cref="SystemColors.WindowColor"/> and
    /// <see cref="SystemColors.WindowTextColor"/>.
    /// </remarks>
    public static Uri? AdditionalHighContrastDarkThemeUri
    {
        get => _additionalHighContrastDarkThemeUri;
        set
        {
            if (_additionalHighContrastDarkThemeUri != value)
            {
                _additionalHighContrastDarkThemeUri = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets the global theme mode for the application.
    /// </summary>
    /// <remarks>
    /// This property controls the overall theme mode of the application. The available values are:
    /// <list>
    ///     <item><see cref="UiThemeMode.Default"/> - Uses the Light-compatible palette provided
    ///     by Generic.xaml while reporting <see cref="ApplicationTheme.Default"/>.</item>
    ///     <item><see cref="UiThemeMode.Light"/> - The application will use the Light theme.</item>
    ///     <item><see cref="UiThemeMode.Dark"/> - The application will use the Dark theme.</item>
    ///     <item><see cref="UiThemeMode.System"/> - The application will automatically follow the system theme settings.</item>
    /// </list>
    ///
    /// When set to <see cref="UiThemeMode.System"/>, the application resolves the effective theme
    /// based on the current system setting. The <see cref="CurrentTheme"/> property will reflect
    /// the resolved theme (either Light or Dark), and changes to the system theme will
    /// automatically trigger updates.
    ///
    /// When set to <see cref="UiThemeMode.Default"/>, the manager keeps a Light-compatible global
    /// resource layer so system accent, native-control, and additional Light resources continue
    /// to work. This matches the Light dictionary included by Generic.xaml but preserves Default
    /// as the public mode rather than treating it as an explicit Light selection.
    ///
    /// Changing the value of this property triggers a theme refresh. If the mode is set to
    /// <see cref="UiThemeMode.System"/>, the effective theme will be synchronized immediately
    /// with the system setting.
    /// </remarks>
    public static UiThemeMode ThemeMode
    {
        get => _themeMode;
        set
        {
            ValidateThemeMode(value);

            if (_themeMode != value)
            {
                _themeMode = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the application should use the system accent color.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, the application will use the current system accent color
    /// as part of its theme resources. Changes to the system accent color will be
    /// reflected in the application after a theme refresh.
    ///
    /// When set to <c>false</c>, the application will ignore the system accent color
    /// and fall back to the built-in default accent color (blue).
    ///
    /// The default value is <c>true</c>.
    /// Changing this property triggers a theme refresh to ensure that
    /// the updated accent color is applied immediately.
    /// </remarks>
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

    /// <summary>
    /// Gets or sets a value indicating whether the library should merge implicit styles
    /// for native WPF controls (e.g., <c>Button</c>) so that they also adopt
    /// the current Fluent UI theme.
    /// </summary>
    /// <remarks>
    /// When set to <c>true</c>, the resource dictionary defined in <c>NativeControlStyles.xaml</c>
    /// is merged into each theme dictionary, providing implicit styles that re-template
    /// or re-style built-in WPF controls to match the Fluent UI appearance.
    ///
    /// The default value is <c>true</c>.
    /// Changing this property triggers an immediate theme refresh.
    /// </remarks>
    public static bool EnableNativeControlStyling
    {
        get => _enableNativeControlStyling;
        set
        {
            if (_enableNativeControlStyling != value)
            {
                _enableNativeControlStyling = value;
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    #endregion

    #region Attached Property: ApplyToRootVisual (Internal)

    /// <summary>
    /// Represents an internal attached property used to mark and track root visual elements.
    /// </summary>
    /// <remarks>
    /// This property is an internal implementation detail of <see cref="ThemeManager"/>.
    /// It is used to identify and manage the application's root visual elements.
    ///
    /// When an element is marked as a root visual, the theme manager registers it in a
    /// global theme tracking list and automatically applies the appropriate theme
    /// resources when the system theme or application theme changes.
    ///
    /// A value of <c>true</c> indicates that the element is a root visual and should
    /// receive theme updates. A value of <c>false</c> indicates that the element is
    /// not considered a root visual.
    /// </remarks>
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
        else
        {
            if (d is FrameworkElement fe)
            {
                fe.Loaded -= OnElementLoadedForRootVisual;
                SetIsRootVisual(fe, false);
                UnregisterElement(fe);
            }
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
            RegisterElement(rootVisual);
            ApplyThemeToElement(rootVisual, _themeMode);
        }
    }

    #endregion

    #region Attached Property: RequestedTheme (Internal)

    internal static void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newMode = (UiThemeMode)e.NewValue;

        if (d is FrameworkElement fe)
        {
            if (newMode == UiThemeMode.Default)
            {
                RemoveThemeDictionary(fe.Resources);
                UnregisterElement(fe);
            }
            else
            {
                RegisterElement(fe);
                ApplyThemeToElement(fe, newMode);
            }
        }
        else if (d is FrameworkContentElement fce)
        {
            if (newMode == UiThemeMode.Default)
            {
                RemoveThemeDictionary(fce.Resources);
                UnregisterContentElement(fce);
            }
            else
            {
                RegisterContentElement(fce);
                ApplyThemeToContentElement(fce, newMode);
            }
        }
    }

    #endregion

    #region System Events

    private static void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // SystemParameters publishes changes for many unrelated settings, including input,
        // fonts, and animation. Only high contrast and the Windows colorization palette can
        // change resources owned by this manager; the remaining theme/accent notifications
        // arrive through SystemEvents.UserPreferenceChanged.
        if (e.PropertyName is nameof(SystemParameters.HighContrast) or nameof(SystemParameters.WindowGlassColor))
        {
            ScheduleThemeChangedDebounced();
        }
    }

    private static void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
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
        _themeChangedDebouncer.Debounce(
            TimeSpan.FromMilliseconds(250),
            () =>
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                _ = dispatcher.BeginInvoke(() => RefreshTheme(ThemeChangeSource.System));
            }
        );
    }

    #endregion

    #region Theme Refresh

    private static void RefreshTheme(ThemeChangeSource source)
    {
        // Build every Source-backed dictionary before beginning transactions on application or
        // element resources. A malformed custom URI therefore leaves both the visible theme and
        // the targets' WPF initialization state intact.
        RebuildThemeDictionaries();

        // Per-element Default targets are already unregistered in OnRequestedThemeChanged, so
        // the only Default targets here are Application and root visuals. For those, Default
        // resolves to Light (matching Generic.xaml) so global accent, native-control, and
        // additional resources remain available.
        foreach (var (_, resources, mode) in CollectTargets())
        {
            ApplyThemeToResources(resources, mode);
        }

        var appliedTheme = _themeMode == UiThemeMode.Default
            ? ApplicationTheme.Default
            : ResolveEffectiveTheme(_themeMode);

        CurrentTheme = appliedTheme;

        // Event handlers may immediately query DynamicResource values, so publish the change
        // only after WPF has completed every resource transaction and invalidated its owners.
        Instance.ChangedInternal?.Invoke(Instance, new ThemeChangedEventArgs(appliedTheme, source));
    }

    private static List<(object Target, ResourceDictionary Resources, UiThemeMode Mode)> CollectTargets()
    {
        var targets = new List<(object, ResourceDictionary, UiThemeMode)>();

        // Application
        var app = Application.Current;
        if (app?.Dispatcher is { HasShutdownStarted: false })
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
        var lightDictionaries = new List<ResourceDictionary>();
        var darkDictionaries  = new List<ResourceDictionary>();

        if (SystemParameters.HighContrast)
        {
            lightDictionaries.Add(
                new ResourceDictionary { Source = HighContrastThemeUri }
            );

            darkDictionaries.Add(
                new ResourceDictionary { Source = HighContrastThemeUri }
            );

            if (_enableNativeControlStyling)
            {
                lightDictionaries.Add(new ResourceDictionary { Source = NativeControlStylesUri });
                darkDictionaries.Add(new ResourceDictionary { Source = NativeControlStylesUri });
            }

            if (_additionalHighContrastLightThemeUri != null)
            {
                lightDictionaries.Add(
                    LoadAdditionalResourceDictionary(_additionalHighContrastLightThemeUri)
                );
            }

            if (_additionalHighContrastDarkThemeUri != null)
            {
                darkDictionaries.Add(
                    LoadAdditionalResourceDictionary(_additionalHighContrastDarkThemeUri)
                );
            }
        }
        else
        {
            lightDictionaries.Add(
                new ResourceDictionary { Source = LightThemeUri }
            );

            darkDictionaries.Add(
                new ResourceDictionary { Source = DarkThemeUri }
            );

            if (_enableNativeControlStyling)
            {
                lightDictionaries.Add(new ResourceDictionary { Source = NativeControlStylesUri });
                darkDictionaries.Add(new ResourceDictionary { Source = NativeControlStylesUri });
            }

            if (_additionalLightThemeUri != null)
            {
                lightDictionaries.Add(
                    LoadAdditionalResourceDictionary(_additionalLightThemeUri)
                );
            }

            if (_additionalDarkThemeUri != null)
            {
                darkDictionaries.Add(
                    LoadAdditionalResourceDictionary(_additionalDarkThemeUri)
                );
            }

            if (_useSystemAccentColor)
            {
                var palette = SystemAccentPaletteReader.Read();
                if (palette != null)
                {
                    lightDictionaries.Add(
                        BuildAccentDictionary(palette, UiThemeMode.Light)
                    );

                    darkDictionaries.Add(
                        BuildAccentDictionary(palette, UiThemeMode.Dark)
                    );
                }
            }
        }

        ReplaceMergedDictionaries(_lightDictionary, lightDictionaries);
        ReplaceMergedDictionaries(_darkDictionary, darkDictionaries);
    }

    private static void ReplaceMergedDictionaries(
        ResourceDictionary target,
        IReadOnlyList<ResourceDictionary> replacements)
    {
        target.BeginInit();

        try
        {
            target.MergedDictionaries.Clear();

            foreach (var replacement in replacements)
            {
                target.MergedDictionaries.Add(replacement);
            }
        }
        finally
        {
            // These dictionaries can own resources used by many visual roots. Ending their
            // transaction once avoids a separate WPF invalidation for every merged child.
            target.EndInit();
        }
    }

    private static ResourceDictionary BuildAccentDictionary(SystemAccentPalette p, UiThemeMode mode)
    {
        var dic = new ResourceDictionary
        {
            [ColorKeys.SystemAccentColor]      = p.Accent,
            [BrushKeys.SystemAccentColorBrush] = FreezeBrush(p.Accent),
        };

        if (mode == UiThemeMode.Light)
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Dark1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Dark2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Dark3;

            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = FreezeBrush(p.Dark1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = FreezeBrush(p.Dark2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = FreezeBrush(p.Dark3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = FreezeBrush(p.Dark1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = FreezeBrush(p.Dark2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = FreezeBrush(p.Dark3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = FreezeBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = FreezeBrush(p.Dark1);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = FreezeBrush(p.Dark1, 0.9);
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = FreezeBrush(p.Dark1, 0.8);
            dic[BrushKeys.SystemFillColorAttentionBrush]              = FreezeBrush(p.Accent);
        }
        else
        {
            dic[ColorKeys.SystemAccentColorPrimary]   = p.Light1;
            dic[ColorKeys.SystemAccentColorSecondary] = p.Light2;
            dic[ColorKeys.SystemAccentColorTertiary]  = p.Light3;

            dic[BrushKeys.SystemAccentColorPrimaryBrush]              = FreezeBrush(p.Light1);
            dic[BrushKeys.SystemAccentColorSecondaryBrush]            = FreezeBrush(p.Light2);
            dic[BrushKeys.SystemAccentColorTertiaryBrush]             = FreezeBrush(p.Light3);
            dic[BrushKeys.AccentTextFillColorPrimaryBrush]            = FreezeBrush(p.Light1);
            dic[BrushKeys.AccentTextFillColorSecondaryBrush]          = FreezeBrush(p.Light2);
            dic[BrushKeys.AccentTextFillColorTertiaryBrush]           = FreezeBrush(p.Light3);
            dic[BrushKeys.AccentFillColorSelectedTextBackgroundBrush] = FreezeBrush(p.Accent);
            dic[BrushKeys.AccentFillColorDefaultBrush]                = FreezeBrush(p.Light2);
            dic[BrushKeys.AccentFillColorSecondaryBrush]              = FreezeBrush(p.Light2, 0.9);
            dic[BrushKeys.AccentFillColorTertiaryBrush]               = FreezeBrush(p.Light2, 0.8);
            dic[BrushKeys.SystemFillColorAttentionBrush]              = FreezeBrush(p.Accent);
        }

        return dic;
    }

    private static void MergeThemeDictionary(ResourceDictionary target, ResourceDictionary themeDict)
    {
        if (IsThemeDictionaryCurrent(target, themeDict))
        {
            return;
        }

        RemoveThemeDictionary(target);

        // Insert at beginning so user-defined resources can override theme resources
        target.MergedDictionaries.Insert(0, themeDict);
    }

    private static bool IsThemeDictionaryCurrent(ResourceDictionary target, ResourceDictionary expected)
    {
        return target.MergedDictionaries.Count > 0
            && ReferenceEquals(target.MergedDictionaries[0], expected)
            && !ContainsAdditionalThemeDictionary(target);
    }

    private static bool ContainsAdditionalThemeDictionary(ResourceDictionary target)
    {
        for (int i = 1; i < target.MergedDictionaries.Count; i++)
        {
            var dictionary = target.MergedDictionaries[i];
            if (ReferenceEquals(dictionary, _lightDictionary) || ReferenceEquals(dictionary, _darkDictionary))
            {
                return true;
            }
        }

        return false;
    }

    private static void RemoveThemeDictionary(ResourceDictionary target)
    {
        for (int i = target.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            var dict = target.MergedDictionaries[i];

            if (ReferenceEquals(dict, _lightDictionary) || ReferenceEquals(dict, _darkDictionary))
            {
                target.MergedDictionaries.RemoveAt(i);
            }
        }
    }

    #endregion

    #region Registration

    private static void RegisterElement(FrameworkElement element)
    {
        lock (_lock)
        {
            if (IsElementRegistered(element))
            {
                return;
            }

            _registeredElements.Add(
                new WeakReference<FrameworkElement>(element)
            );
        }
    }

    private static void UnregisterElement(FrameworkElement element)
    {
        lock (_lock)
        {
            _registeredElements.RemoveAll(
                wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, element)
            );
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

            _registeredContentElements.Add(
                new WeakReference<FrameworkContentElement>(element)
            );
        }
    }

    private static void UnregisterContentElement(FrameworkContentElement element)
    {
        lock (_lock)
        {
            _registeredContentElements.RemoveAll(
                wr => !wr.TryGetTarget(out var target) || ReferenceEquals(target, element)
            );
        }
    }

    private static bool IsElementRegistered(FrameworkElement element)
    {
        return _registeredElements.Any(
            wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, element)
        );
    }

    private static bool IsContentElementRegistered(FrameworkContentElement element)
    {
        return _registeredContentElements.Any(
            wr => wr.TryGetTarget(out var target) && ReferenceEquals(target, element)
        );
    }

    #endregion

    #region Helpers

    private static bool IsInitializePending(ResourceDictionary dictionary)
    {
        try
        {
            return _isInitializePendingProperty?.GetValue(dictionary) is true;
        }
        catch (Exception exception) when (exception is MethodAccessException or TargetInvocationException)
        {
            // Reflection is an optimization that avoids deliberately provoking WPF's nested
            // BeginInit exception while XAML is loading. If internals change or reflection is
            // restricted, TryBeginInit below provides the behavior-preserving fallback.
            return false;
        }
    }

    private static bool TryBeginInit(ResourceDictionary dictionary)
    {
        if (IsInitializePending(dictionary))
        {
            return false;
        }

        try
        {
            dictionary.BeginInit();
            return true;
        }
        catch (InvalidOperationException)
        {
            // WPF's ResourceDictionary.BeginInit has a single failure path: it rejects a nested
            // initialization transaction. This fallback keeps XAML loading functional if the
            // private IsInitializePending property is renamed in a future framework version.
            return false;
        }
    }

    private static void ValidateThemeMode(UiThemeMode mode)
    {
        if (mode is < UiThemeMode.Default or > UiThemeMode.System)
        {
            throw new ArgumentOutOfRangeException(nameof(mode), mode, "The theme mode is not defined.");
        }
    }

    private static UiThemeMode GetEffectiveThemeForElement(FrameworkElement element)
    {
        if (GetIsRootVisual(element))
        {
            return _themeMode;
        }

        return Theme.GetRequested(element);
    }

    private static ApplicationTheme ResolveEffectiveTheme(UiThemeMode mode)
    {
        if (mode == UiThemeMode.Light) return ApplicationTheme.Light;
        if (mode == UiThemeMode.Dark) return ApplicationTheme.Dark;

        if (SystemParameters.HighContrast)
        {
            return IsHighContrastDark() ? ApplicationTheme.Dark : ApplicationTheme.Light;
        }

        return mode == UiThemeMode.System ? GetSystemThemeMode() : ApplicationTheme.Light;
    }

    private static void ApplyThemeToElement(FrameworkElement element, UiThemeMode mode)
    {
        ApplyThemeToResources(element.Resources, mode);
    }

    private static void ApplyThemeToContentElement(FrameworkContentElement element, UiThemeMode mode)
    {
        ApplyThemeToResources(element.Resources, mode);
    }

    private static void ApplyThemeToResources(ResourceDictionary resources, UiThemeMode mode)
    {
        var effectiveTheme = ResolveEffectiveTheme(mode);

        var themeDict = effectiveTheme == ApplicationTheme.Dark
            ? _darkDictionary
            : _lightDictionary;

        if (IsThemeDictionaryCurrent(resources, themeDict))
        {
            return;
        }

        var initiated = TryBeginInit(resources);

        try
        {
            MergeThemeDictionary(resources, themeDict);
        }
        finally
        {
            // End only transactions started here. During XAML parsing the owning dictionary is
            // already inside BeginInit and its loader remains responsible for the matching EndInit.
            if (initiated)
            {
                resources.EndInit();
            }
        }
    }

    private static SolidColorBrush FreezeBrush(Color color, double opacity = 1.0)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };

        brush.Freeze();

        return brush;
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

    /// <summary>
    /// Determines whether the current high contrast theme is a dark variant
    /// by comparing the relative luminance of <see cref="SystemColors.WindowColor"/>
    /// and <see cref="SystemColors.WindowTextColor"/>.
    /// </summary>
    internal static bool IsHighContrastDark()
    {
        var bg = SystemColors.WindowColor;
        var fg = SystemColors.WindowTextColor;

        var bgLuma = (0.299 * bg.R) + (0.587 * bg.G) + (0.114 * bg.B);
        var fgLuma = (0.299 * fg.R) + (0.587 * fg.G) + (0.114 * fg.B);

        return bgLuma < fgLuma;
    }

    private static ResourceDictionary LoadAdditionalResourceDictionary(Uri uri)
    {
        try
        {
            // Let WPF resolve every supported ResourceDictionary URI form directly. The old
            // GetResourceStream preflight both loaded pack resources twice and incorrectly
            // rejected valid content/site-of-origin dictionaries.
            return new ResourceDictionary { Source = uri };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Unable to load the additional theme resource dictionary '{uri}'.",
                ex
            );
        }
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
               .AddHandler(Instance, nameof(ChangedInternal), value);
        }
        remove
        {
            WeakEventManager<ThemeManager, ThemeChangedEventArgs>
               .RemoveHandler(Instance, nameof(ChangedInternal), value);
        }
    }

    public event EventHandler<ThemeChangedEventArgs>? ChangedInternal;

    #endregion

    private ThemeManager() { }
}