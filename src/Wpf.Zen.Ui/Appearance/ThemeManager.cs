// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Microsoft.Win32;
using System.Collections;
using System.Reflection;
using System.Windows.Threading;
using Wpf.Zen.Ui.Appearance;
using Wpf.Zen.Ui.Theming;
using Wpf.Zen.Ui.Threading;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui;

/// <summary>
/// Manages application theme state, listens to system theme events,
/// synchronizes system theme, and coordinates theme resource loading.
/// </summary>
/// <remarks>
/// Referencing Wpf.Zen.Ui or calling <see cref="Initialize"/> does not install a global theme or replace native WPF
/// control styles. Global theme management begins only after an explicit configuration API or property setter is
/// used, or after a <see cref="ThemeManagerOptions"/> dictionary finishes initialization.
/// </remarks>
public sealed class ThemeManager
{
    #region Private Constants, Fields

    // Rooted component URIs do not depend on PackUriHelper's process-wide URI parser being
    // registered before this type's static field initializers run. This matters in WinForms,
    // console, and test hosts that can access ThemeManager before constructing a WPF Application.
    private const string ResourceBaseUri = "/Wpf.Zen.Ui;component/Resources/";
    private const string ThemeBaseUri    = ResourceBaseUri + "Theme/";

    // This is the documented Windows blue used by the built-in theme. It is also the honest
    // fallback on Windows 7/8, where UISettings.GetColorValue(Accent) is unavailable.
    private static readonly Color BuiltInAccentColor = Color.FromRgb(0x00, 0x78, 0xD4);

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

    // Automatically discovered roots are deliberately separate from _registeredElements. The latter opt into a
    // local ResourceDictionary through Theme.ApplyToRootVisual/Requested; these roots only provide lifetime-safe
    // dispatcher discovery for the process-wide SystemResources backend and must not mutate element resources.
    private static readonly List<WeakReference<FrameworkElement>> _systemThemeRoots = [];

    // A root can raise Loaded repeatedly as it is reparented. Keep a short strong reference only while its targeted
    // replay is queued; the persistent registry above remains weak so closed ElementHost trees can be collected.
    private static readonly HashSet<FrameworkElement> _pendingSystemThemeInvalidationRoots = [];

    private static readonly DebounceDispatcher _themeChangedDebouncer = new();

    private static readonly PropertyInfo? _isInitializePendingProperty =
        typeof(ResourceDictionary).GetProperty(
            "IsInitializePending",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

    // System ThemeStyle has stricter validation than an Application implicit style (notably it rejects
    // OverridesDefaultStyle). Keep independently loaded dictionaries so compatibility normalization never changes the
    // public dictionaries merged into Application or element resources.
    private static ResourceDictionary _lightSystemDictionary = [];
    private static ResourceDictionary _darkSystemDictionary  = [];

    // The theme preference is deliberately absent from this key. Light and Dark dictionaries are built as one
    // immutable pair, so changing only the selection policy must not reload XAML. Settings that affect dictionary
    // contents do invalidate both the normal and reflected-backend snapshots.
    private static ThemeResourceConfiguration? _normalThemeResourceConfiguration;
    private static ThemeResourceConfiguration? _systemThemeResourceConfiguration;

    private static Uri? _additionalLightThemeUri;
    private static Uri? _additionalDarkThemeUri;
    private static Uri? _additionalHighContrastLightThemeUri;
    private static Uri? _additionalHighContrastDarkThemeUri;

    private static ThemePreference   _themePreference   = ThemePreference.Default;
    private static AccentColorSource _accentSource      = AccentColorSource.System;
    private static Color             _customAccentColor = BuiltInAccentColor;

    // Native WPF controls keep their platform theme unless the consumer explicitly opts into the library's
    // implicit Type-keyed styles. This is intentionally independent from activating theme management: palette
    // resources are additive, while native styles replace the visual contract of every matching WPF control.
    private static bool _enableNativeControlStyling;

    // Merely referencing Wpf.Zen.Ui must not replace the implicit styles of native WPF controls. Theme resources are
    // built eagerly for Generic.xaml-compatible local requests, but the global Application/SystemResources layer is
    // installed only after a consumer explicitly configures ThemeManager.
    private static int _themeManagementActivationState;

    // Applicationless hosts first use WPF's real system-theme lookup path. Keep the Application path as a permanent
    // compatibility fallback because the reflected contract is intentionally allowed to fail on future WPF versions.
    private static SystemThemeResourceHost? _systemThemeResourceHost;
    private static bool                     _systemThemeResourceHostUnavailable;

    private static ThemeManager Instance { get; } = new();

    #endregion

    #region Initialize

    /// <summary>
    /// Forces the static constructor to run, preparing theme resources and system event listeners without applying a
    /// global theme or changing native WPF control styles.
    /// </summary>
    public static void Initialize() { }

    static ThemeManager()
    {
        // Static constructor is a suitable place for library initialization
        LibraryInitializer.EnsureInitialized();

        _ = RebuildThemeDictionaries();

        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
        {
            // Loaded is the first public, framework-owned notification at which an arbitrary WPF visual island is
            // guaranteed to have a PresentationSource. A class handler removes the need for ElementHost consumers to
            // opt into Theme.ApplyToRootVisual merely so detached ThemeStyle values can be refreshed.
            EventManager.RegisterClassHandler(
                typeof(FrameworkElement),
                FrameworkElement.LoadedEvent,
                new RoutedEventHandler(OnFrameworkElementLoadedForSystemTheme),
                handledEventsToo: true
            );

            SystemEvents.UserPreferenceChanged     += SystemEvents_UserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += SystemParameters_StaticPropertyChanged;
        }
    }

    /// <summary>
    /// Selects the process-wide resource host used by the global theme.
    /// </summary>
    /// <remarks>
    /// Normal WPF applications keep using <see cref="Application.Resources"/>, preserving the public and familiar
    /// resource override surface. A WinForms <c>ElementHost</c> has no Application, so it first attempts to install
    /// the active dictionary in WPF's internal system-theme table. That path reaches Frame navigation content,
    /// Popup roots, and other presentation islands through WPF's own ThemeStyle lookup and invalidation machinery.
    /// If reflection is unavailable, the method falls back to a resource-only Application so theming remains
    /// functional rather than failing application startup.
    /// </remarks>
    private static void EnsureGlobalResourceHost(bool nativeControlStylesChanged)
    {
        ResourceDictionary activeTheme = GetSystemThemeDictionary();

        if (Application.Current is not null)
        {
            // ThemeManager can be initialized unusually early, before a normal WPF executable constructs App.
            // Once that App exists, return SystemResources to Windows ownership and use Application.Resources.
            _systemThemeResourceHost?.Restore();
            _systemThemeResourceHost = null;
            return;
        }

        if (!_systemThemeResourceHostUnavailable)
        {
            if (_systemThemeResourceHost is null
                && !SystemThemeResourceHost.TryCreate(out _systemThemeResourceHost))
            {
                _systemThemeResourceHostUnavailable = true;
            }

            if (_systemThemeResourceHost?.TryApply(activeTheme, nativeControlStylesChanged) is true)
            {
                return;
            }

            // A later installation can fail after an earlier one succeeded (for example, after a WPF update changes
            // an internal state transition). Remove the old overlay before switching resource-host strategies.
            _systemThemeResourceHost?.Restore();
            _systemThemeResourceHost = null;
            _systemThemeResourceHostUnavailable = true;
        }

        EnsureApplicationResourceHost();
    }

    /// <summary>
    /// Creates the compatibility Application used only when the system-theme backend is unavailable.
    /// </summary>
    /// <remarks>
    /// The synthetic instance is deliberately not passed to <see cref="Application.Run()"/>. The outer UI framework
    /// continues to own the Win32 message loop; this object only supplies WPF's process-wide resource scope. This is
    /// a safe fallback for framework versions whose private SystemResources layout is not recognized.
    /// </remarks>
    private static void EnsureApplicationResourceHost()
    {
        if (Application.Current is null)
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        }
    }

    /// <summary>
    /// Detects newly connected presentation roots and schedules one SystemResources replay for their dispatcher.
    /// </summary>
    /// <remarks>
    /// WPF's own theme-change invalidation enumerates only the PresentationSources that exist at the instant the
    /// change occurs. Controls commonly get their initial ThemeStyle while XAML is being constructed, before an
    /// ElementHost assigns its child to an HwndSource. Without this replay those controls retain Generic.xaml's
    /// original Light style even though later resource lookups already observe the Dark system overlay.
    /// </remarks>
    private static void OnFrameworkElementLoadedForSystemTheme(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element)
        {
            return;
        }

        PresentationSource? source = PresentationSource.FromDependencyObject(element);
        if (source is null || !ReferenceEquals(source.RootVisual, element))
        {
            return;
        }

        RegisterSystemThemeRoot(element);

        if (_systemThemeResourceHost is null)
        {
            return;
        }

        Dispatcher dispatcher = source.Dispatcher;
        lock (_lock)
        {
            if (!_pendingSystemThemeInvalidationRoots.Add(element))
            {
                return;
            }
        }

        _ = dispatcher.BeginInvoke(
            DispatcherPriority.Loaded,
            new Action(() =>
            {
                lock (_lock)
                {
                    _pendingSystemThemeInvalidationRoots.Remove(element);
                }

                PresentationSource? currentSource = PresentationSource.FromDependencyObject(element);
                if (currentSource is not null && ReferenceEquals(currentSource.RootVisual, element))
                {
                    TryInitializeSystemThemePresentationSource(element);
                }
            })
        );
    }

    /// <summary>
    /// Retains an automatically discovered presentation root without opting it into element-local resources.
    /// </summary>
    private static void RegisterSystemThemeRoot(FrameworkElement root)
    {
        lock (_lock)
        {
            _systemThemeRoots.RemoveAll(reference => !reference.TryGetTarget(out _));
            if (_systemThemeRoots.Any(
                    reference => reference.TryGetTarget(out FrameworkElement? target)
                        && ReferenceEquals(target, root)
                ))
            {
                return;
            }

            _systemThemeRoots.Add(new WeakReference<FrameworkElement>(root));
        }
    }

    /// <summary>
    /// Initializes a newly connected root without re-evaluating ThemeStyle in existing presentation sources.
    /// </summary>
    private static void TryInitializeSystemThemePresentationSource(FrameworkElement root)
    {
        if (_systemThemeResourceHost is not { } systemHost
            || systemHost.TryInitializePresentationSource(root))
        {
            return;
        }

        FallBackFromSystemThemeHost(systemHost);
    }

    /// <summary>
    /// Replays palette-resource invalidation, or switches atomically to the Application fallback on failure.
    /// </summary>
    private static void TryInvalidateSystemThemePresentationSources()
    {
        if (_systemThemeResourceHost is not { } systemHost
            || systemHost.TryInvalidatePresentationSources())
        {
            return;
        }

        FallBackFromSystemThemeHost(systemHost);
    }

    private static void FallBackFromSystemThemeHost(SystemThemeResourceHost systemHost)
    {
        // Do not keep a partially functional global host. The compatibility Application restores the same
        // process-wide resource reach while leaving the surrounding WinForms message loop in control.
        systemHost.Restore();
        _systemThemeResourceHost = null;
        _systemThemeResourceHostUnavailable = true;
        EnsureApplicationResourceHost();

        if (Application.Current is { } application)
        {
            ApplyThemeToResources(application.Resources, _themePreference);
        }
    }

    #endregion

    #region Configure

    /// <summary>
    /// Sets additional theme URIs in a single operation,
    /// triggering only one theme refresh.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These dictionaries are palette extensions. They should contain only non-structural resources that can be
    /// refreshed in place, such as <see cref="Color"/>, <see cref="Brush"/>, numbers, thicknesses, and other values
    /// consumed through dynamic resource references.
    /// </para>
    /// <para>
    /// Do not place styles, control or data templates, event setters, or other resources that require WPF to replace
    /// a control's theme style or template in these dictionaries. Global theme changes deliberately preserve the
    /// existing style and template graph so control state, popup state, focus, and visual-state animations survive.
    /// </para>
    /// <para>
    /// In an applicationless host, WPF's system-theme lookup accepts only assembly-scoped type or resource keys.
    /// Use the keys published by <c>Wpf.Zen.Ui.Theming</c> when overriding Wpf.Zen.Ui palette entries; ordinary string keys
    /// must remain in an Application, window, or element resource scope.
    /// </para>
    /// </remarks>
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

        bool activated = ActivateThemeManagement();
        if (changed || activated)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    /// <summary>
    /// Configures the theme preference and accent color source without modifying native WPF control styling or any
    /// existing additional theme resource URIs.
    /// </summary>
    /// <param name="preference">The theme selection policy to apply.</param>
    /// <param name="accentSource">The source from which the base accent color is obtained.</param>
    /// <param name="customAccentColor">
    /// The custom base color to remember. It is used only when <paramref name="accentSource"/>
    /// is <see cref="AccentColorSource.Custom"/>. A <c>null</c> value preserves the current color.
    /// </param>
    public static void Configure(
        ThemePreference preference,
        AccentColorSource accentSource = AccentColorSource.System,
        Color? customAccentColor = null)
    {
        ConfigureCore(preference, accentSource, customAccentColor, enableNativeControlStyling: null);
    }

    /// <summary>
    /// Configures the theme preference, accent color source, and native WPF control styling while preserving the remembered
    /// custom accent color and all additional theme resource URIs.
    /// </summary>
    /// <param name="preference">The theme selection policy to apply.</param>
    /// <param name="accentSource">The source from which the base accent color is obtained.</param>
    /// <param name="enableNativeControlStyling">A value indicating whether to merge native WPF control styles.</param>
    public static void Configure(
        ThemePreference preference,
        AccentColorSource accentSource,
        bool enableNativeControlStyling)
    {
        ConfigureCore(preference, accentSource, customAccentColor: null, enableNativeControlStyling);
    }

    /// <summary>
    /// Configures the theme preference, accent color source, and native WPF control styling without modifying any existing
    /// additional theme resource URIs.
    /// </summary>
    /// <remarks>
    /// Native control styling is intentionally a required argument in this overload because enabling it installs
    /// implicit <see cref="Style"/> resources keyed by WPF control types and therefore replaces the platform visual
    /// contract for those controls. Use the overload without <paramref name="enableNativeControlStyling"/> when a
    /// theme update should preserve the current opt-in state.
    /// </remarks>
    /// <param name="preference">The theme selection policy to apply.</param>
    /// <param name="accentSource">The source from which the base accent color is obtained.</param>
    /// <param name="customAccentColor">
    /// The custom base color to remember. It is used only when <paramref name="accentSource"/>
    /// is <see cref="AccentColorSource.Custom"/>. A <c>null</c> value preserves the current color.
    /// </param>
    /// <param name="enableNativeControlStyling">A value indicating whether to merge native WPF control styles.</param>
    public static void Configure(
        ThemePreference preference,
        AccentColorSource accentSource,
        Color? customAccentColor,
        bool enableNativeControlStyling)
    {
        ConfigureCore(preference, accentSource, customAccentColor, enableNativeControlStyling);
    }

    /// <summary>
    /// Applies the short-form configuration and optionally changes the independently opt-in native style layer.
    /// </summary>
    /// <param name="preference">The theme selection policy to apply.</param>
    /// <param name="accentSource">The source from which the base accent color is obtained.</param>
    /// <param name="customAccentColor">The custom base color to remember, or <c>null</c> to preserve it.</param>
    /// <param name="enableNativeControlStyling">
    /// The requested native style state, or <c>null</c> to preserve the current state.
    /// </param>
    private static void ConfigureCore(
        ThemePreference preference,
        AccentColorSource accentSource,
        Color? customAccentColor,
        bool? enableNativeControlStyling)
    {
        ValidateThemePreference(preference);
        ValidateAccentSource(accentSource);

        var changed = false;

        if (_themePreference != preference)
        {
            _themePreference = preference;
            changed = true;
        }

        if (_accentSource != accentSource)
        {
            _accentSource = accentSource;
            changed = true;
        }

        if (customAccentColor.HasValue)
        {
            var opaqueCustomAccentColor = MakeOpaque(customAccentColor.Value);
            if (_customAccentColor != opaqueCustomAccentColor)
            {
                _customAccentColor = opaqueCustomAccentColor;
                changed = true;
            }
        }

        if (enableNativeControlStyling.HasValue
            && _enableNativeControlStyling != enableNativeControlStyling.Value)
        {
            _enableNativeControlStyling = enableNativeControlStyling.Value;
            changed = true;
        }

        bool activated = ActivateThemeManagement();
        if (changed || activated)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    /// <summary>
    /// Configures multiple theme settings in a single operation, triggering only one theme refresh.
    /// </summary>
    /// <remarks>
    /// The four additional theme dictionaries are palette extensions and should contain only non-structural values
    /// such as colors, brushes, numbers, and thicknesses. They must not define styles, control or data templates,
    /// event setters, or other resources that require replacement of the stable theme-style/template graph.
    /// In an applicationless host, process-wide overrides must use the assembly-scoped keys published by
    /// <c>Wpf.Zen.Ui.Theming</c>; WPF does not route ordinary string keys through its system-theme dictionaries.
    /// </remarks>
    /// <param name="preference">The theme selection policy to apply.</param>
    /// <param name="accentSource">The source from which the base accent color is obtained.</param>
    /// <param name="customAccentColor">
    /// The custom base color to remember. It is used only when <paramref name="accentSource"/>
    /// is <see cref="AccentColorSource.Custom"/>.
    /// </param>
    /// <param name="enableNativeControlStyling">Whether to merge native WPF control styles.</param>
    /// <param name="additionalLightThemeUri">The URI of the additional light theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalDarkThemeUri">The URI of the additional dark theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalHighContrastLightThemeUri">The URI of the additional high contrast light theme resource dictionary, or <c>null</c> to clear.</param>
    /// <param name="additionalHighContrastDarkThemeUri">The URI of the additional high contrast dark theme resource dictionary, or <c>null</c> to clear.</param>
    /// <example>
    /// <code lang="csharp">
    /// ThemeManager.Configure(
    ///     preference: ThemePreference.Dark,
    ///     accentSource: AccentColorSource.Custom,
    ///     customAccentColor: Color.FromRgb(0x7A, 0x3F, 0xD0),
    ///     enableNativeControlStyling: true,
    ///     additionalLightThemeUri: new Uri("pack://application:,,,/MyApp;component/Themes/Light.xaml"),
    ///     additionalDarkThemeUri: new Uri("pack://application:,,,/MyApp;component/Themes/Dark.xaml")
    /// );
    /// </code>
    /// </example>
    public static void Configure(
        ThemePreference preference,
        AccentColorSource accentSource,
        Color customAccentColor,
        bool enableNativeControlStyling,
        Uri? additionalLightThemeUri,
        Uri? additionalDarkThemeUri,
        Uri? additionalHighContrastLightThemeUri,
        Uri? additionalHighContrastDarkThemeUri)
    {
        ValidateThemePreference(preference);
        ValidateAccentSource(accentSource);

        var changed = false;

        if (_themePreference != preference)
        {
            _themePreference = preference;
            changed = true;
        }

        if (_accentSource != accentSource)
        {
            _accentSource = accentSource;
            changed = true;
        }

        customAccentColor = MakeOpaque(customAccentColor);
        if (_customAccentColor != customAccentColor)
        {
            _customAccentColor = customAccentColor;
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

        bool activated = ActivateThemeManagement();
        if (changed || activated)
        {
            RefreshTheme(ThemeChangeSource.Manual);
        }
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// Gets the concrete Light or Dark theme currently effective for the application.
    /// </summary>
    /// <remarks>
    /// This value is the resolved rendering result, not the configured <see cref="Preference"/>. It is
    /// therefore always <see cref="ThemeVariant.Light"/> or <see cref="ThemeVariant.Dark"/>.
    /// Outside high contrast, <see cref="ThemePreference.Default"/> resolves to Light while
    /// <see cref="ThemePreference.System"/> follows the operating-system setting. High contrast can override
    /// either automatic preference with the variant matching the active high-contrast palette.
    /// Per-element requests made through <see cref="Theme.RequestedProperty"/> are intentionally
    /// scoped to those elements and are not reflected by this global property.
    /// </remarks>
    public static ThemeVariant EffectiveTheme { get; private set; } = ThemeVariant.Light;

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to be merged
    /// when the Light theme is active.
    /// </summary>
    /// <remarks>
    /// This property allows consumers to provide extra theme resources
    /// that extend or override non-structural values in the built-in Light theme dictionary.
    ///
    /// When set, the specified resource dictionary will be merged
    /// after the default Light theme resources, giving it higher
    /// lookup priority in the resource resolution chain.
    ///
    /// Changing this property triggers an immediate theme refresh,
    /// causing the new dictionary to be loaded and applied dynamically.
    /// The dictionary should contain palette values such as colors, brushes, numbers, and thicknesses. It must not
    /// contain styles, control or data templates, event setters, or other structural resources: theme switching
    /// intentionally keeps the existing style and template graph to preserve live control state.
    /// Applicationless process-wide overrides must use the assembly-scoped keys published by
    /// <c>Wpf.Zen.Ui.Theming</c>; ordinary string keys are not routed through WPF's system-theme dictionaries.
    /// </remarks>
    public static Uri? AdditionalLightThemeUri
    {
        get => _additionalLightThemeUri;
        set
        {
            bool changed = _additionalLightThemeUri != value;
            if (changed)
            {
                _additionalLightThemeUri = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
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
    /// that extend or override non-structural values in the built-in Dark theme dictionary.
    ///
    /// When set, the specified resource dictionary will be merged
    /// after the default Dark theme resources, giving it higher
    /// lookup priority in the resource resolution chain.
    ///
    /// Changing this property triggers an immediate theme refresh,
    /// causing the new dictionary to be loaded and applied dynamically.
    /// The dictionary should contain palette values such as colors, brushes, numbers, and thicknesses. It must not
    /// contain styles, control or data templates, event setters, or other structural resources: theme switching
    /// intentionally keeps the existing style and template graph to preserve live control state.
    /// Applicationless process-wide overrides must use the assembly-scoped keys published by
    /// <c>Wpf.Zen.Ui.Theming</c>; ordinary string keys are not routed through WPF's system-theme dictionaries.
    /// </remarks>
    public static Uri? AdditionalDarkThemeUri
    {
        get => _additionalDarkThemeUri;
        set
        {
            bool changed = _additionalDarkThemeUri != value;
            if (changed)
            {
                _additionalDarkThemeUri = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
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
    /// The dictionary is a palette extension and should contain only non-structural values such as colors, brushes,
    /// numbers, and thicknesses. Styles, control or data templates, event setters, and other structural resources
    /// are unsupported because theme switching preserves the existing theme-style/template graph.
    /// Applicationless process-wide overrides must use the assembly-scoped keys published by
    /// <c>Wpf.Zen.Ui.Theming</c>; ordinary string keys are not routed through WPF's system-theme dictionaries.
    /// </remarks>
    public static Uri? AdditionalHighContrastLightThemeUri
    {
        get => _additionalHighContrastLightThemeUri;
        set
        {
            bool changed = _additionalHighContrastLightThemeUri != value;
            if (changed)
            {
                _additionalHighContrastLightThemeUri = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
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
    /// The dictionary is a palette extension and should contain only non-structural values such as colors, brushes,
    /// numbers, and thicknesses. Styles, control or data templates, event setters, and other structural resources
    /// are unsupported because theme switching preserves the existing theme-style/template graph.
    /// Applicationless process-wide overrides must use the assembly-scoped keys published by
    /// <c>Wpf.Zen.Ui.Theming</c>; ordinary string keys are not routed through WPF's system-theme dictionaries.
    /// </remarks>
    public static Uri? AdditionalHighContrastDarkThemeUri
    {
        get => _additionalHighContrastDarkThemeUri;
        set
        {
            bool changed = _additionalHighContrastDarkThemeUri != value;
            if (changed)
            {
                _additionalHighContrastDarkThemeUri = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets the application's theme preference.
    /// </summary>
    /// <remarks>
    /// This property controls how the effective theme is selected. The available values are:
    /// <list>
    ///     <item><see cref="ThemePreference.Default"/> - Uses the library default palette, which is Light-compatible outside high contrast.</item>
    ///     <item><see cref="ThemePreference.Light"/> - The application will use the Light theme.</item>
    ///     <item><see cref="ThemePreference.Dark"/> - The application will use the Dark theme.</item>
    ///     <item><see cref="ThemePreference.System"/> - The application will automatically follow the system theme settings.</item>
    /// </list>
    ///
    /// When set to <see cref="ThemePreference.System"/>, the application resolves the effective theme
    /// based on the current system setting. The <see cref="EffectiveTheme"/> property will reflect
    /// the resolved theme (either Light or Dark), and changes to the system theme will
    /// automatically trigger updates.
    ///
    /// When set to <see cref="ThemePreference.Default"/>, the manager keeps the global resource layer needed for
    /// accent, native-control, additional, and high-contrast resources. Outside high contrast this matches the
    /// Light dictionary included by Generic.xaml, but Default remains the public preference rather than becoming an
    /// explicit Light selection.
    ///
    /// Changing the value of this property triggers a theme refresh. If the preference is set to
    /// <see cref="ThemePreference.System"/>, the effective theme will be synchronized immediately
    /// with the system setting.
    /// </remarks>
    public static ThemePreference Preference
    {
        get => _themePreference;
        set
        {
            ValidateThemePreference(value);

            bool changed = _themePreference != value;
            if (changed)
            {
                _themePreference = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets the source of the base accent color used to generate accent resources.
    /// </summary>
    /// <remarks>
    /// <see cref="AccentColorSource.System"/> uses the documented Windows UISettings API and
    /// falls back to <see cref="AccentColorSource.Default"/> behavior when that API is absent,
    /// including on Windows 7. <see cref="AccentColorSource.Custom"/> uses
    /// <see cref="CustomAccentColor"/>. Changing this property refreshes both light and dark
    /// semantic accent palettes immediately.
    /// </remarks>
    public static AccentColorSource AccentSource
    {
        get => _accentSource;
        set
        {
            ValidateAccentSource(value);

            bool changed = _accentSource != value;
            if (changed)
            {
                _accentSource = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
                RefreshTheme(ThemeChangeSource.Manual);
            }
        }
    }

    /// <summary>
    /// Gets or sets the custom base accent color used when <see cref="AccentSource"/> is
    /// <see cref="AccentColorSource.Custom"/>.
    /// </summary>
    /// <remarks>
    /// The default value is <c>#0078D4</c>. The alpha channel is normalized to opaque because
    /// transparency belongs to the consuming control, not to the shared accent palette. Changing
    /// this value refreshes resources only while the custom source is active.
    /// </remarks>
    public static Color CustomAccentColor
    {
        get => _customAccentColor;
        set
        {
            value = MakeOpaque(value);

            bool changed = _customAccentColor != value;
            if (changed)
            {
                _customAccentColor = value;
            }

            bool activated = ActivateThemeManagement();
            if ((changed && _accentSource == AccentColorSource.Custom) || activated)
            {
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
    /// The default value is <c>false</c>. Native control styling is a separate, explicit opt-in from theme
    /// management because these implicit type-keyed styles replace the platform appearance of matching controls.
    /// Setting this property explicitly activates global theme management; subsequent changes trigger an immediate
    /// theme refresh. Merely referencing Wpf.Zen.Ui does not merge the native-control dictionary.
    /// </remarks>
    public static bool EnableNativeControlStyling
    {
        get => _enableNativeControlStyling;
        set
        {
            bool changed = _enableNativeControlStyling != value;
            if (changed)
            {
                _enableNativeControlStyling = value;
            }

            bool activated = ActivateThemeManagement();
            if (changed || activated)
            {
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
    /// This marker distinguishes roots explicitly opted into an element-local theme dictionary through
    /// <see cref="Theme.ApplyToRootVisualProperty"/>. It is not used by the applicationless global SystemResources
    /// backend, which discovers presentation roots automatically and does not modify their Resources collections.
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
            ApplyThemeToElement(rootVisual, _themePreference);

            TryInvalidateSystemThemePresentationSources();
        }
    }

    #endregion

    #region Attached Property: RequestedTheme (Internal)

    internal static void OnRequestedThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var newPreference = (ThemePreference)e.NewValue;

        if (d is FrameworkElement fe)
        {
            if (newPreference == ThemePreference.Default)
            {
                RemoveThemeDictionary(fe.Resources);
                UnregisterElement(fe);
            }
            else
            {
                RegisterElement(fe);
                ApplyThemeToElement(fe, newPreference);
            }
        }
        else if (d is FrameworkContentElement fce)
        {
            if (newPreference == ThemePreference.Default)
            {
                RemoveThemeDictionary(fce.Resources);
                UnregisterContentElement(fce);
            }
            else
            {
                RegisterContentElement(fce);
                ApplyThemeToContentElement(fce, newPreference);
            }
        }
    }

    #endregion

    #region System Events

    private static void SystemParameters_StaticPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // SystemParameters publishes changes for many unrelated settings, including input,
        // fonts, and animation. High contrast changes resource semantics, while WindowGlassColor
        // is a useful WPF notification that Windows personalization changed. The actual accent is
        // still re-read from UISettings; the glass color itself is never treated as the accent.
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
        // System notifications are passive until the consumer has opted into global theme management. Otherwise a
        // personalization change could make a referenced-but-unconfigured library start restyling native controls.
        if (!IsThemeManagementActive)
        {
            return;
        }

        _themeChangedDebouncer.Debounce(
            TimeSpan.FromMilliseconds(250),
            () =>
            {
                Dispatcher? dispatcher = GetThemeDispatcher();
                if (dispatcher is null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                {
                    return;
                }

                _ = dispatcher.BeginInvoke(() => RefreshTheme(ThemeChangeSource.System));
            }
        );
    }

    private static Dispatcher? GetThemeDispatcher()
    {
        Dispatcher? applicationDispatcher = Application.Current?.Dispatcher;
        if (applicationDispatcher is { HasShutdownStarted: false, HasShutdownFinished: false })
        {
            return applicationDispatcher;
        }

        lock (_lock)
        {
            // Explicitly registered roots remain valid dispatcher owners even when they are temporarily unloaded.
            foreach (WeakReference<FrameworkElement> reference in _registeredElements)
            {
                if (reference.TryGetTarget(out FrameworkElement? element))
                {
                    Dispatcher dispatcher = element.Dispatcher;
                    if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    {
                        return dispatcher;
                    }
                }
            }

            foreach (WeakReference<FrameworkContentElement> reference in _registeredContentElements)
            {
                if (reference.TryGetTarget(out FrameworkContentElement? element))
                {
                    Dispatcher dispatcher = element.Dispatcher;
                    if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    {
                        return dispatcher;
                    }
                }
            }

            // ElementHost and raw HwndSource users do not need Theme.ApplyToRootVisual merely to make system-driven
            // refreshes possible. Their presentation roots are discovered from Loaded and retained weakly here.
            _systemThemeRoots.RemoveAll(reference => !reference.TryGetTarget(out _));
            foreach (WeakReference<FrameworkElement> reference in _systemThemeRoots)
            {
                if (reference.TryGetTarget(out FrameworkElement? root))
                {
                    Dispatcher dispatcher = root.Dispatcher;
                    if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    {
                        return dispatcher;
                    }
                }
            }
        }

        return null;
    }

    #endregion

    #region Theme Refresh

    private static bool IsThemeManagementActive =>
        Volatile.Read(ref _themeManagementActivationState) != 0;

    /// <summary>
    /// Records the consumer's first explicit ThemeManager configuration.
    /// </summary>
    /// <returns><see langword="true"/> only for the call that activates global theme management.</returns>
    private static bool ActivateThemeManagement() =>
        Interlocked.Exchange(ref _themeManagementActivationState, 1) == 0;

    private static void RefreshTheme(ThemeChangeSource source)
    {
        // Build every Source-backed dictionary before beginning transactions on application or
        // element resources. A malformed custom URI therefore leaves both the visible theme and
        // the targets' WPF initialization state intact.
        bool nativeControlStylesChanged = RebuildThemeDictionaries();

        // Install the process-wide layer before touching individual roots. DynamicResource expressions and default
        // styles re-evaluated by those root transactions must already observe the new global theme.
        EnsureGlobalResourceHost(nativeControlStylesChanged);

        // Per-element Default targets are already unregistered in OnRequestedThemeChanged, so
        // the only Default targets here are Application and root visuals. For those, Default
        // resolves to Light (matching Generic.xaml) so global accent, native-control, and
        // additional resources remain available.
        foreach (var (_, resources, preference) in CollectTargets())
        {
            ApplyThemeToResources(resources, preference);
        }

        ThemeVariant effectiveTheme = ResolveEffectiveTheme(_themePreference);
        EffectiveTheme = effectiveTheme;

        // Event handlers may immediately query DynamicResource values, so publish the change
        // only after WPF has completed every resource transaction and invalidated its owners.
        Instance.ChangedInternal?.Invoke(Instance, new ThemeChangedEventArgs(effectiveTheme, source));
    }

    private static List<(object Target, ResourceDictionary Resources, ThemePreference Preference)> CollectTargets()
    {
        var targets = new List<(object, ResourceDictionary, ThemePreference)>();

        // Application
        var app = Application.Current;
        if (app?.Dispatcher is { HasShutdownStarted: false })
        {
            targets.Add((app, app.Resources, _themePreference));
        }

        lock (_lock)
        {
            // Cleanup and collect registered FrameworkElements
            _registeredElements.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _registeredElements)
            {
                if (wr.TryGetTarget(out var fe))
                {
                    ThemePreference preference = GetThemePreferenceForElement(fe);
                    targets.Add((fe, fe.Resources, preference));
                }
            }

            // Cleanup and collect registered FrameworkContentElements
            _registeredContentElements.RemoveAll(wr => !wr.TryGetTarget(out _));

            foreach (var wr in _registeredContentElements)
            {
                if (wr.TryGetTarget(out var fce))
                {
                    ThemePreference preference = Theme.GetRequested(fce);
                    targets.Add((fce, fce.Resources, preference));
                }
            }
        }

        return targets;
    }

    private static bool RebuildThemeDictionaries()
    {
        bool highContrast = SystemParameters.HighContrast;
        Color accentColor = highContrast ? default : ResolveAccentColor();
        var configuration = new ThemeResourceConfiguration(
            highContrast,
            accentColor,
            _enableNativeControlStyling,
            _additionalLightThemeUri,
            _additionalDarkThemeUri,
            _additionalHighContrastLightThemeUri,
            _additionalHighContrastDarkThemeUri
        );

        ThemeResourceConfiguration? previousSystemConfiguration = _systemThemeResourceConfiguration;
        bool rebuildNormalDictionaries = _normalThemeResourceConfiguration != configuration;
        bool rebuildSystemDictionaries = Application.Current is null
            && !_systemThemeResourceHostUnavailable
            && _systemThemeResourceConfiguration != configuration;
        bool nativeControlStylesChanged = rebuildSystemDictionaries
            && previousSystemConfiguration is { } previous
            && previous.EnableNativeControlStyling != configuration.EnableNativeControlStyling;

        List<ResourceDictionary>? lightDictionaries = null;
        List<ResourceDictionary>? darkDictionaries = null;
        ResourceDictionary? lightSystemDictionary = null;
        ResourceDictionary? darkSystemDictionary = null;

        // Load every Source-backed dictionary before publishing any of them. Besides avoiding repeated parsing on a
        // preference-only switch, this preserves the all-or-nothing behavior when a custom URI is malformed.
        if (rebuildNormalDictionaries)
        {
            lightDictionaries = [];
            darkDictionaries = [];

            // Native control styles are structural resources, not theme colors. Both palettes must reference the
            // same Style objects so replacing Light with Dark cannot invalidate a control template or its state.
            ResourceDictionary? nativeControlStyles = _enableNativeControlStyling
                ? new ResourceDictionary { Source = NativeControlStylesUri }
                : null;
            PopulateThemeDictionaries(
                lightDictionaries,
                darkDictionaries,
                highContrast,
                accentColor,
                nativeControlStyles
            );
        }

        if (rebuildSystemDictionaries)
        {
            var lightSystemDictionaries = new List<ResourceDictionary>();
            var darkSystemDictionaries = new List<ResourceDictionary>();

            // SystemResources treats an implicit native-control style as a ThemeStyle and applies stricter
            // validation than Application.Resources. Load one independent graph for this backend, normalize it
            // once, then share it between Light and Dark to preserve ThemeStyle identity across palette changes.
            ResourceDictionary? systemNativeControlStyles = _enableNativeControlStyling
                ? new ResourceDictionary { Source = NativeControlStylesUri }
                : null;
            if (systemNativeControlStyles is not null)
            {
                RemoveOverridesDefaultStyleSetters(systemNativeControlStyles);
            }

            PopulateThemeDictionaries(
                lightSystemDictionaries,
                darkSystemDictionaries,
                highContrast,
                accentColor,
                systemNativeControlStyles
            );

            lightSystemDictionary = new ResourceDictionary();
            darkSystemDictionary = new ResourceDictionary();
            ReplaceMergedDictionaries(lightSystemDictionary, lightSystemDictionaries);
            ReplaceMergedDictionaries(darkSystemDictionary, darkSystemDictionaries);
        }

        if (rebuildNormalDictionaries)
        {
            ReplaceMergedDictionaries(_lightDictionary, lightDictionaries!);
            ReplaceMergedDictionaries(_darkDictionary, darkDictionaries!);
            _normalThemeResourceConfiguration = configuration;
        }

        if (rebuildSystemDictionaries)
        {
            _lightSystemDictionary = lightSystemDictionary!;
            _darkSystemDictionary = darkSystemDictionary!;
            _systemThemeResourceConfiguration = configuration;
        }

        // A pure Light/Dark or palette update must keep the state-preserving resource invalidation path. Adding or
        // removing implicit native-control type keys is different: existing controls must discard their old
        // ThemeStyle once so the switch actually affects controls that are already connected to a PresentationSource.
        return nativeControlStylesChanged;
    }

    internal static void PopulateThemeDictionaries(
        List<ResourceDictionary> lightDictionaries,
        List<ResourceDictionary> darkDictionaries,
        bool highContrast,
        Color accentColor,
        ResourceDictionary? nativeControlStyles)
    {
        if (highContrast)
        {
            lightDictionaries.Add(
                new ResourceDictionary { Source = HighContrastThemeUri }
            );

            darkDictionaries.Add(
                new ResourceDictionary { Source = HighContrastThemeUri }
            );

            if (nativeControlStyles is not null)
            {
                lightDictionaries.Add(nativeControlStyles);
                darkDictionaries.Add(nativeControlStyles);
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

            if (nativeControlStyles is not null)
            {
                lightDictionaries.Add(nativeControlStyles);
                darkDictionaries.Add(nativeControlStyles);
            }

            // UISettings exposes one personalization color, not a documented reusable ramp.
            // Generate WPF Zen UI's own light and dark role palettes from that single stable input.
            // The generated dictionary precedes additional user dictionaries so applications can
            // still override individual semantic roles without replacing the entire accent source.
            lightDictionaries.Add(
                BuildAccentDictionary(AccentPalette.Create(accentColor, ThemeVariant.Light))
            );
            darkDictionaries.Add(
                BuildAccentDictionary(AccentPalette.Create(accentColor, ThemeVariant.Dark))
            );

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
        }
    }

    private static void RemoveOverridesDefaultStyleSetters(ResourceDictionary dictionary)
    {
        foreach (ResourceDictionary mergedDictionary in dictionary.MergedDictionaries)
        {
            RemoveOverridesDefaultStyleSetters(mergedDictionary);
        }

        List<object>? incompatibleThemeStyleKeys = null;
        foreach (DictionaryEntry entry in dictionary)
        {
            if (entry.Value is not Style style)
            {
                continue;
            }

            if (entry.Key is Type && ContainsEventSetter(style))
            {
                // WPF also rejects EventSetter in ThemeStyle. Unlike OverridesDefaultStyle, silently removing an
                // event handler would change behavior, so omit only this implicit system style and let WPF fall back
                // to its original style for that control type. Named explicit styles remain available unchanged.
                incompatibleThemeStyleKeys ??= [];
                incompatibleThemeStyleKeys.Add(entry.Key);
                continue;
            }

            if (style.IsSealed)
            {
                // Source-backed dictionaries loaded exclusively for this backend have no owner and therefore should
                // not be sealed yet. Treat a sealed style as an unsupported custom dictionary rather than attempting
                // to mutate WPF's compiled style tables through still more reflection.
                if (style.Setters.OfType<Setter>().Any(
                        setter => setter.Property == FrameworkElement.OverridesDefaultStyleProperty
                    ))
                {
                    throw new InvalidOperationException(
                        "A system-theme resource style was sealed before OverridesDefaultStyle could be normalized."
                    );
                }

                continue;
            }

            for (int index = style.Setters.Count - 1; index >= 0; index--)
            {
                if (style.Setters[index] is Setter setter
                    && setter.Property == FrameworkElement.OverridesDefaultStyleProperty)
                {
                    style.Setters.RemoveAt(index);
                }
            }
        }

        if (incompatibleThemeStyleKeys is not null)
        {
            foreach (object key in incompatibleThemeStyleKeys)
            {
                dictionary.Remove(key);
            }
        }
    }

    private static bool ContainsEventSetter(Style style)
    {
        for (Style? current = style; current is not null; current = current.BasedOn)
        {
            if (current.Setters.OfType<EventSetter>().Any())
            {
                return true;
            }
        }

        return false;
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

    private static ResourceDictionary BuildAccentDictionary(AccentPalette palette)
    {
        return new ResourceDictionary
        {
            [ColorKeys.AccentFillPrimary]        = palette.AccentFillPrimary,
            [ColorKeys.AccentFillSecondary]      = palette.AccentFillSecondary,
            [ColorKeys.AccentFillTertiary]       = palette.AccentFillTertiary,
            [ColorKeys.AccentTextPrimary]        = palette.AccentTextPrimary,
            [ColorKeys.AccentTextSecondary]      = palette.AccentTextSecondary,
            [ColorKeys.TextOnAccentPrimary]      = palette.TextOnAccentPrimary,
            [ColorKeys.AccentStroke]             = palette.AccentStroke,
            [ColorKeys.AccentSelection]          = palette.AccentSelection,
            [BrushKeys.AccentFillPrimaryBrush]   = FreezeBrush(palette.AccentFillPrimary),
            [BrushKeys.AccentFillSecondaryBrush] = FreezeBrush(palette.AccentFillSecondary),
            [BrushKeys.AccentFillTertiaryBrush]  = FreezeBrush(palette.AccentFillTertiary),
            [BrushKeys.AccentTextPrimaryBrush]   = FreezeBrush(palette.AccentTextPrimary),
            [BrushKeys.AccentTextSecondaryBrush] = FreezeBrush(palette.AccentTextSecondary),
            [BrushKeys.TextOnAccentPrimaryBrush] = FreezeBrush(palette.TextOnAccentPrimary),
            [BrushKeys.AccentStrokeBrush]        = FreezeBrush(palette.AccentStroke),
            [BrushKeys.AccentSelectionBrush]     = FreezeBrush(palette.AccentSelection),

            // Attention is an existing higher-level token whose default meaning is the current
            // accent. Override both representations so ColorResource and BrushResource agree.
            [ColorKeys.SystemFillColorAttention]      = palette.AccentTextPrimary,
            [BrushKeys.SystemFillColorAttentionBrush] = FreezeBrush(palette.AccentTextPrimary),
        };
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

    private static void ValidateThemePreference(ThemePreference preference)
    {
        if (preference is < ThemePreference.Default or > ThemePreference.System)
        {
            throw new ArgumentOutOfRangeException(
                nameof(preference),
                preference,
                "The theme preference is not defined."
            );
        }
    }

    private static void ValidateAccentSource(AccentColorSource source)
    {
        if (source is < AccentColorSource.System or > AccentColorSource.Default)
        {
            throw new ArgumentOutOfRangeException(nameof(source), source, "The accent color source is not defined.");
        }
    }

    private static Color ResolveAccentColor()
    {
        if (_accentSource == AccentColorSource.Custom)
        {
            return _customAccentColor;
        }

        if (_accentSource == AccentColorSource.System
            && SystemAccentColorProvider.TryGetAccentColor(out var systemAccentColor))
        {
            return systemAccentColor;
        }

        return BuiltInAccentColor;
    }

    private static Color MakeOpaque(Color color)
    {
        return Color.FromRgb(color.R, color.G, color.B);
    }

    private static ThemePreference GetThemePreferenceForElement(FrameworkElement element)
    {
        if (GetIsRootVisual(element))
        {
            return _themePreference;
        }

        return Theme.GetRequested(element);
    }

    private static ThemeVariant ResolveEffectiveTheme(ThemePreference preference)
    {
        if (preference == ThemePreference.Light) return ThemeVariant.Light;
        if (preference == ThemePreference.Dark) return ThemeVariant.Dark;

        if (SystemParameters.HighContrast)
        {
            return IsHighContrastDark() ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        return preference == ThemePreference.System ? GetSystemThemeVariant() : ThemeVariant.Light;
    }

    private static ResourceDictionary GetSystemThemeDictionary() =>
        ResolveEffectiveTheme(_themePreference) == ThemeVariant.Dark
            ? _darkSystemDictionary
            : _lightSystemDictionary;

    private static void ApplyThemeToElement(FrameworkElement element, ThemePreference preference)
    {
        ApplyThemeToResources(element.Resources, preference);
    }

    private static void ApplyThemeToContentElement(FrameworkContentElement element, ThemePreference preference)
    {
        ApplyThemeToResources(element.Resources, preference);
    }

    private static void ApplyThemeToResources(ResourceDictionary resources, ThemePreference preference)
    {
        ThemeVariant effectiveTheme = ResolveEffectiveTheme(preference);

        var themeDict = effectiveTheme == ThemeVariant.Dark
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

    private static SolidColorBrush FreezeBrush(Color color)
    {
        var brush = new SolidColorBrush(color);

        brush.Freeze();

        return brush;
    }

    private static ThemeVariant GetSystemThemeVariant()
    {
        var value = Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "AppsUseLightTheme",
            -1
        );

        return value switch
        {
            1 => ThemeVariant.Light,
            0 => ThemeVariant.Dark,
            _ => ThemeVariant.Light
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

    private readonly record struct ThemeResourceConfiguration(
        bool HighContrast,
        Color AccentColor,
        bool EnableNativeControlStyling,
        Uri? AdditionalLightThemeUri,
        Uri? AdditionalDarkThemeUri,
        Uri? AdditionalHighContrastLightThemeUri,
        Uri? AdditionalHighContrastDarkThemeUri
    );

    private ThemeManager() { }
}
