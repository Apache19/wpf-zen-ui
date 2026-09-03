// This Source Code Form is subject to the terms of the MIT License.
// If a copy of the MIT was not distributed with this file, You can obtain one at https://opensource.org/licenses/MIT.
// Copyright (C) Leszek Pomianowski and WPF UI Contributors.
// All Rights Reserved.

using Wpf.Zen.Ui.Appearance;

// ReSharper disable once CheckNamespace
namespace Wpf.Zen.Ui;

/// <summary>
/// A resource dictionary that configures the WPF Zen UI theme system declaratively in XAML.
/// Place this dictionary in <c>Application.Resources</c> to set the global theme preference,
/// accent color behavior, and additional theme resource URIs.
/// In non-WPF environments, such as WinForms projects, it can be merged into the first
/// XAML content like a UserControl.
/// </summary>
/// <remarks>
/// <para>
/// During XAML parsing, property changes are batched through <see cref="ISupportInitialize"/>
/// and applied once after the dictionary is fully initialized.
/// </para>
/// <para>
/// At runtime, changing any property after initialization triggers an immediate theme refresh.
/// </para>
/// <para>
/// Additional theme dictionaries configured by this type are palette extensions. They should contain only
/// non-structural values such as colors, brushes, numbers, and thicknesses. Styles, control or data templates, event
/// setters, and other resources that require WPF to replace a theme style or template are unsupported. Keeping that
/// graph stable is what allows a global theme switch to preserve control, popup, focus, and animation state.
/// </para>
/// <para>
/// In an applicationless host, process-wide overrides must use the assembly-scoped keys published by
/// <c>Wpf.Zen.Ui.Theming</c>. WPF does not route ordinary string keys through its system-theme dictionaries; keep such
/// application-specific resources in a window or element resource scope instead.
/// </para>
/// </remarks>
/// <example>
/// <code lang="xml">
/// &lt;Application.Resources&gt;
///     &lt;ResourceDictionary&gt;
///         &lt;ResourceDictionary.MergedDictionaries&gt;
///             &lt;ui:ThemeManagerOptions
///                 Preference="Dark"
///                 AccentSource="System"
///                 AdditionalLightThemeSource="pack://application:,,,/MyApp;component/Themes/Light.xaml"
///                 AdditionalDarkThemeSource="pack://application:,,,/MyApp;component/Themes/Dark.xaml" /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/Application.Resources&gt;
/// </code>
/// </example>
/// <example>
/// <code lang="xml">
/// &lt;UserControl.Resources&gt;
///     &lt;ResourceDictionary&gt;
///         &lt;ResourceDictionary.MergedDictionaries&gt;
///             &lt;ui:ThemeManagerOptions
///                 Preference="Dark"
///                 AccentSource="Custom"
///                 CustomAccentColor="#7A3FD0"
///                 AdditionalLightThemeSource="pack://application:,,,/MyApp;component/Themes/Light.xaml"
///                 AdditionalDarkThemeSource="pack://application:,,,/MyApp;component/Themes/Dark.xaml" /&gt;
///         &lt;/ResourceDictionary.MergedDictionaries&gt;
///     &lt;/ResourceDictionary&gt;
/// &lt;/UserControl.Resources&gt;
/// </code>
/// </example>
/// <example>
/// <code lang="csharp">
///
/// // Programmatic usage from C# - configure ThemeManager via ThemeManagerOptions
/// var options = new ThemeManagerOptions();
///
/// options.BeginInit(); // Request deferred configuration
///
/// options.Preference = ThemePreference.Dark;
/// options.AccentSource = AccentColorSource.Custom;
/// options.CustomAccentColor = Color.FromRgb(0x7A, 0x3F, 0xD0);
/// options.AdditionalLightThemeSource = new Uri("pack://application:,,,/MyApp;component/Themes/Light.xaml");
/// options.AdditionalDarkThemeSource = new Uri("pack://application:,,,/MyApp;component/Themes/Dark.xaml");
///
/// // To explicitly clear a previously set URI, set the property to null:
/// // options.AdditionalLightThemeSource = null;
///
/// options.EndInit(); // Apply theme configuration
///
/// </code>
/// </example>
public class ThemeManagerOptions : ResourceDictionary, ISupportInitialize
{
    private bool _isInitializing;
    private bool _pendingApply;

    private ThemePreference _preference = ThemePreference.Light;
    private AccentColorSource _accentSource = AccentColorSource.System;
    private Color _customAccentColor = Color.FromRgb(0x00, 0x78, 0xD4);
    private Uri? _additionalLightThemeSource;
    private Uri? _additionalDarkThemeSource;
    private Uri? _additionalHighContrastLightThemeSource;
    private Uri? _additionalHighContrastDarkThemeSource;

    // Flags to track whether a URI property was explicitly set (even to null) from XAML
    private bool _aLightChanged;
    private bool _aDarkChanged;
    private bool _aHcLightChanged;
    private bool _aHcDarkChanged;

    /// <summary>
    /// Gets or sets the application's theme preference. The default value is <see cref="ThemePreference.Light"/>,
    /// matching the palette included by Generic.xaml when no global theme configuration has been applied.
    /// </summary>
    public ThemePreference Preference
    {
        get => _preference;
        set
        {
            if (_preference != value)
            {
                _preference = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets the source from which the base accent color is obtained.
    /// The default value is <see cref="AccentColorSource.System"/>.
    /// </summary>
    public AccentColorSource AccentSource
    {
        get => _accentSource;
        set
        {
            if (_accentSource != value)
            {
                _accentSource = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets the custom base accent color used when <see cref="AccentSource"/> is
    /// <see cref="AccentColorSource.Custom"/>. The default value is <c>#0078D4</c>.
    /// </summary>
    /// <remarks>
    /// The alpha channel is ignored when the configuration is applied. Interaction opacity
    /// belongs to individual controls and is not encoded in the shared palette.
    /// </remarks>
    public Color CustomAccentColor
    {
        get => _customAccentColor;
        set
        {
            if (_customAccentColor != value)
            {
                _customAccentColor = value;
                RequestApply();
            }
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when the Light theme is active.
    /// </summary>
    /// <remarks>
    /// The dictionary should contain only non-structural palette values such as colors, brushes, numbers, and
    /// thicknesses. Do not define styles, control or data templates, event setters, or other resources that require
    /// replacement of the stable theme-style/template graph.
    /// In an applicationless host, use the assembly-scoped keys published by <c>Wpf.Zen.Ui.Theming</c> for process-wide
    /// overrides; ordinary string keys require a normal application, window, or element resource scope.
    /// </remarks>
    public Uri? AdditionalLightThemeSource
    {
        get => _additionalLightThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aLightChanged = true;
            _additionalLightThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when the Dark theme is active.
    /// </summary>
    /// <remarks>
    /// The dictionary should contain only non-structural palette values such as colors, brushes, numbers, and
    /// thicknesses. Do not define styles, control or data templates, event setters, or other resources that require
    /// replacement of the stable theme-style/template graph.
    /// In an applicationless host, use the assembly-scoped keys published by <c>Wpf.Zen.Ui.Theming</c> for process-wide
    /// overrides; ordinary string keys require a normal application, window, or element resource scope.
    /// </remarks>
    public Uri? AdditionalDarkThemeSource
    {
        get => _additionalDarkThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aDarkChanged = true;
            _additionalDarkThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when a light high contrast theme is active.
    /// </summary>
    /// <remarks>
    /// The dictionary should contain only non-structural palette values such as colors, brushes, numbers, and
    /// thicknesses. Do not define styles, control or data templates, event setters, or other resources that require
    /// replacement of the stable theme-style/template graph.
    /// In an applicationless host, use the assembly-scoped keys published by <c>Wpf.Zen.Ui.Theming</c> for process-wide
    /// overrides; ordinary string keys require a normal application, window, or element resource scope.
    /// </remarks>
    public Uri? AdditionalHighContrastLightThemeSource
    {
        get => _additionalHighContrastLightThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aHcLightChanged = true;
            _additionalHighContrastLightThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Gets or sets an additional resource dictionary URI to merge when a dark high contrast theme is active.
    /// </summary>
    /// <remarks>
    /// The dictionary should contain only non-structural palette values such as colors, brushes, numbers, and
    /// thicknesses. Do not define styles, control or data templates, event setters, or other resources that require
    /// replacement of the stable theme-style/template graph.
    /// In an applicationless host, use the assembly-scoped keys published by <c>Wpf.Zen.Ui.Theming</c> for process-wide
    /// overrides; ordinary string keys require a normal application, window, or element resource scope.
    /// </remarks>
    public Uri? AdditionalHighContrastDarkThemeSource
    {
        get => _additionalHighContrastDarkThemeSource;
        set
        {
            // Mark that the property was explicitly set (including explicit x:Null in XAML)
            _aHcDarkChanged = true;
            _additionalHighContrastDarkThemeSource = value;

            RequestApply();
        }
    }

    /// <summary>
    /// Begins a configuration transaction so multiple property changes are applied in one refresh.
    /// </summary>
    /// <remarks>
    /// This member intentionally hides <see cref="ResourceDictionary.BeginInit"/>. Calling the
    /// inherited method directly would start WPF resource initialization without enabling the
    /// configuration batching promised by this type.
    /// </remarks>
    public new void BeginInit()
    {
        base.BeginInit();
        _isInitializing = true;
    }

    /// <summary>
    /// Ends the configuration transaction and applies all pending changes once.
    /// </summary>
    public new void EndInit()
    {
        base.EndInit();
        _isInitializing = false;

        // The presence of ThemeManagerOptions is itself a complete declarative configuration request. Apply even
        // when every property equals its CLR default so a dictionary containing only this object remains meaningful.
        if (_pendingApply)
        {
            _pendingApply = false;
        }

        ApplyConfiguration();
    }

    /// <inheritdoc cref="ISupportInitialize.BeginInit"/>
    void ISupportInitialize.BeginInit()
    {
        BeginInit();
    }

    /// <inheritdoc cref="ISupportInitialize.EndInit"/>
    void ISupportInitialize.EndInit()
    {
        EndInit();
    }

    private void RequestApply()
    {
        if (_isInitializing)
        {
            _pendingApply = true;
        }
        else
        {
            ApplyConfiguration();
        }
    }

    private void ApplyConfiguration()
    {
        ThemeManager.Configure(
            _preference,
            _accentSource,
            _customAccentColor,
            _aLightChanged ? _additionalLightThemeSource : ThemeManager.AdditionalLightThemeUri,
            _aDarkChanged ? _additionalDarkThemeSource : ThemeManager.AdditionalDarkThemeUri,
            _aHcLightChanged ? _additionalHighContrastLightThemeSource : ThemeManager.AdditionalHighContrastLightThemeUri,
            _aHcDarkChanged ? _additionalHighContrastDarkThemeSource : ThemeManager.AdditionalHighContrastDarkThemeUri
        );

        // Reset explicit-set flags after applying so subsequent merges without that attribute
        // don't unintentionally clear values.
        _aLightChanged = _aDarkChanged = _aHcLightChanged = _aHcDarkChanged = false;
    }

    static ThemeManagerOptions()
    {
        LibraryInitializer.EnsureInitialized();
    }
}
